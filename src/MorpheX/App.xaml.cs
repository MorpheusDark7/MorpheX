using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using System.IO;
using Application = System.Windows.Application;
using MorpheX.Core.Detection;
using MorpheX.Core.Logging;
using MorpheX.Core.Models;
using MorpheX.Core.Providers;
using MorpheX.Core.Services;
using MorpheX.Services;
using Serilog;

namespace MorpheX;

public partial class App : Application
{
    private const string AppMutexName = @"Global\MorpheX_Live_SingleInstance_Mutex";
    private const string PipeName = "MorpheX_Live_SingleInstance_Pipe";

    private static Mutex? _instanceMutex;
    private CancellationTokenSource? _pipeCts;

    public SettingsService SettingsService { get; } = new();
    public LibraryService LibraryService { get; } = new();
    public MonitorService MonitorService { get; } = new();
    public StartupService StartupService { get; } = new();
    public WallpaperService WallpaperService { get; private set; } = null!;
    public PlaybackService PlaybackService { get; private set; } = null!;
    public PlaylistService PlaylistService { get; private set; } = null!;
    public HotkeyService HotkeyService { get; private set; } = null!;
    public SystemMetricsService SystemMetricsService { get; } = new();
    public UpdateService UpdateService { get; } = new();
    public WidgetService WidgetService { get; private set; } = null!;
    public ProfileService ProfileService { get; private set; } = null!;

    public UpdateInfo? AvailableUpdate { get; private set; }
    public event EventHandler<UpdateInfo>? UpdateAvailable;

    private WallpaperProviderFactory _providerFactory = null!;
    private ExplorerWatcher _explorerWatcher = null!;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private bool _startMinimized;
    private SplashWindow? _splash;
    private CancellationTokenSource? _monitorChangeDebounceCts;
    private readonly SemaphoreSlim _monitorChangeLock = new(1, 1);
    private MorpheX.Core.Services.AmbientDimService? _ambientDimService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            Log.Fatal(ex, "Fatal AppDomain unhandled exception (IsTerminating={IsTerminating})", args.IsTerminating);
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            Log.Error(args.Exception, "Unobserved background task exception intercepted");
            args.SetObserved();
        };

        bool createdNew;
        try
        {
            _instanceMutex = new Mutex(true, AppMutexName, out createdNew);
        }
        catch (AbandonedMutexException)
        {
            Log.Warning("Previous instance exited abnormally (AbandonedMutex). Taking ownership.");
            createdNew = true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to create single-instance mutex, proceeding as new instance");
            createdNew = true;
        }

        if (!createdNew)
        {
            SignalExistingInstance();
            Shutdown();
            return;
        }

        StartNamedPipeServer();

        _startMinimized = e.Args.Contains("--minimized");

        DisableProcessWindowsGhosting();

        LogManager.Initialize();
        Log.Information("═══════════════════════════════════════");
        Log.Information("MorpheX Live starting up");
        Log.Information("═══════════════════════════════════════");

        ThemeService.Initialize();

        if (!_startMinimized)
        {
            _splash = new SplashWindow();
            _splash.Show();
        }

        _splash?.SetStatus("Loading settings...");
        _splash?.SetProgress(0.10);
        await SettingsService.LoadAsync();
        ProfileService = new ProfileService(SettingsService);

        _splash?.SetStatus("Loading wallpaper library...");
        _splash?.SetProgress(0.30);
        await LibraryService.LoadAsync();

        _splash?.SetStatus("Initializing wallpaper engine...");
        _splash?.SetProgress(0.50);
        _providerFactory = new WallpaperProviderFactory();
        _providerFactory.Register(WallpaperType.Image,
            () => new ImageWallpaperProvider(),
            new[] { ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tiff", ".tif" });
        _providerFactory.Register(WallpaperType.Video,
            () => new VideoWallpaperProvider(),
            new[] { ".mp4", ".webm", ".mkv", ".mov", ".avi" });

        WallpaperService = new WallpaperService(MonitorService, SettingsService, _providerFactory, LibraryService);

        // Run WorkerW interop init on a background thread to keep the UI thread
        // (and the splash screen) responsive during SendMessageTimeout + Thread.Sleep calls.
        var initOk = await Task.Run(() => WallpaperService.Initialize());
        if (!initOk)
        {
            Log.Error("Failed to initialize desktop wallpaper integration");
        }

        PlaybackService = new PlaybackService(WallpaperService, MonitorService, SettingsService);
        PlaybackService.Start();

        _explorerWatcher = new ExplorerWatcher();
        _explorerWatcher.ExplorerRestarted += async (_, _) =>
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                await WallpaperService.RecoverFromExplorerRestartAsync();
            });
        };
        _explorerWatcher.Start();

        MonitorService.MonitorsChanged += (_, _) =>
        {
            _monitorChangeDebounceCts?.Cancel();
            _monitorChangeDebounceCts?.Dispose();
            _monitorChangeDebounceCts = new CancellationTokenSource();
            var token = _monitorChangeDebounceCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    // Debounce rapid display setting changes (e.g. extending monitors triggers bursts of WM_DISPLAYCHANGE)
                    await Task.Delay(800, token);
                    if (token.IsCancellationRequested) return;

                    await _monitorChangeLock.WaitAsync(token);
                    try
                    {
                        await Dispatcher.InvokeAsync(async () =>
                        {
                            Log.Information("Processing debounced monitor change event...");
                            await WallpaperService.HandleMonitorChangeAsync();
                            await RestoreWallpapersAsync(onlyMissing: true);

                            if (_mainWindow?.CurrentPage is DisplaysPage displaysPage)
                            {
                                displaysPage.RefreshDisplays();
                            }
                        });
                    }
                    finally
                    {
                        _monitorChangeLock.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Newer display settings event superseded this one
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error handling monitor change");
                }
            });
        };

        Microsoft.Win32.SystemEvents.PowerModeChanged += async (_, args) =>
        {
            if (args.Mode == Microsoft.Win32.PowerModes.StatusChange)
            {
                PlaybackService?.PollNow();
                return;
            }

            if (args.Mode == Microsoft.Win32.PowerModes.Resume)
            {
                Log.Information("System resumed from sleep — refreshing display and recovering wallpapers");
                await Task.Delay(1500);
                await _monitorChangeLock.WaitAsync();
                try
                {
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        MonitorService.Refresh();
                        await WallpaperService.RecoverFromExplorerRestartAsync();
                        await RestoreWallpapersAsync();
                        WallpaperService.ResumeAll();
                    });
                }
                finally
                {
                    _monitorChangeLock.Release();
                }
            }
        };

        _splash?.SetStatus("Preparing desktop...");
        _splash?.SetProgress(0.70);
        await RestoreWallpapersAsync();

        _splash?.SetStatus("Starting services...");
        _splash?.SetProgress(0.88);
        PlaylistService = new PlaylistService(WallpaperService, LibraryService, MonitorService, SettingsService, PlaybackService);
        PlaylistService.Start();

        HotkeyService = new HotkeyService(SettingsService, PlaybackService, WallpaperService, PlaylistService);
        HotkeyService.Start();

        _ambientDimService = new MorpheX.Core.Services.AmbientDimService(WallpaperService, SettingsService);
        _ambientDimService.Start();

        SetupTrayIcon();

        _splash?.SetStatus("Ready");
        _splash?.SetProgress(1.0);

        Log.Information("MorpheX startup complete. Monitors: {Count}, Wallpapers in library: {LibCount}",
            MonitorService.Monitors.Count, LibraryService.Wallpapers.Count);

        // Initialise widgets (after all services are ready, before opening window)
        try
        {
            WidgetService = new WidgetService(SettingsService.Settings.Widgets);
            WidgetService.ApplySettings();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize WidgetService");
        }

        if (!_startMinimized)
        {
            if (_splash != null)
            {
                await Task.Delay(320);
                await _splash.CloseWithFadeAsync();
                _splash.Close();
                _splash = null;
            }
            try
            {
                ShowMainWindow();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to show main window on startup");
            }
        }
        else
        {
            MorpheX.Core.Utilities.MemoryOptimizer.TrimWorkingSet(force: true);
        }

        if (SettingsService.Settings.General.CheckForUpdates)
        {
            _ = CheckForUpdatesInBackgroundAsync();
        }
    }

    private async Task CheckForUpdatesInBackgroundAsync()
    {
        try
        {
            await Task.Delay(5000);
            var update = await UpdateService.CheckForUpdatesAsync();
            if (update != null)
            {
                AvailableUpdate = update;
                Dispatcher.Invoke(() =>
                {
                    UpdateAvailable?.Invoke(this, update);
                    if (_trayIcon != null)
                    {
                        _trayIcon.BalloonTipClicked += OnUpdateBalloonClicked;
                        _trayIcon.ShowBalloonTip(
                            5000,
                            "MorpheX Update Available",
                            $"Version {update.TagName} is available. Click here to view and install.",
                            System.Windows.Forms.ToolTipIcon.Info);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Background update check failed (non-critical)");
        }
    }

    private void OnUpdateBalloonClicked(object? sender, EventArgs e)
    {
        if (_trayIcon != null)
        {
            _trayIcon.BalloonTipClicked -= OnUpdateBalloonClicked;
        }
        Dispatcher.InvokeAsync(ShowSettings);
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var client = new System.IO.Pipes.NamedPipeClientStream(".", PipeName, System.IO.Pipes.PipeDirection.Out);
            client.Connect(1500);
            using var writer = new System.IO.StreamWriter(client);
            writer.WriteLine("SHOW");
            writer.Flush();
        }
        catch
        {
        }
    }

    private void StartNamedPipeServer()
    {
        _pipeCts = new CancellationTokenSource();
        var token = _pipeCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new System.IO.Pipes.NamedPipeServerStream(
                        PipeName,
                        System.IO.Pipes.PipeDirection.In,
                        1,
                        System.IO.Pipes.PipeTransmissionMode.Byte,
                        System.IO.Pipes.PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token);
                    using var reader = new System.IO.StreamReader(server);
                    var msg = await reader.ReadLineAsync(token);

                    if (msg == "SHOW")
                    {
                        await Dispatcher.InvokeAsync(ShowMainWindow);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Debug("Named pipe listener error: {Message}", ex.Message);
                    await Task.Delay(500, token);
                }
            }
        }, token);
    }

    private async Task RestoreWallpapersAsync(bool onlyMissing = false)
    {
        if (string.Equals(SettingsService.Settings.Display.PlacementMode, "Span", StringComparison.OrdinalIgnoreCase))
        {
            if (onlyMissing && WallpaperService.GetActiveWallpaper(WallpaperService.SpanDeviceId) != null)
                return;

            string? wallpaperId = null;
            if (SettingsService.Settings.MonitorAssignments.TryGetValue(WallpaperService.SpanDeviceId, out var spanAssign)
                && !string.IsNullOrEmpty(spanAssign.WallpaperId))
            {
                wallpaperId = spanAssign.WallpaperId;
            }
            else
            {
                var first = SettingsService.Settings.MonitorAssignments.Values.FirstOrDefault(a => !string.IsNullOrEmpty(a.WallpaperId));
                wallpaperId = first?.WallpaperId;
            }

            if (!string.IsNullOrEmpty(wallpaperId))
            {
                var wallpaper = LibraryService.GetById(wallpaperId);
                if (wallpaper != null)
                {
                    try
                    {
                        await WallpaperService.SetSpannedWallpaperAsync(wallpaper, skipSave: true);
                        await SettingsService.SaveAsync();
                        return;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to restore spanned wallpaper '{Name}'", wallpaper.Name);
                    }
                }
            }
        }

        // Restore monitors concurrently. When onlyMissing is true, skip monitors that already have an active wallpaper.
        var tasks = SettingsService.Settings.MonitorAssignments
            .Where(kv => kv.Key != WallpaperService.SpanDeviceId && !string.IsNullOrEmpty(kv.Value.WallpaperId))
            .Where(kv => !onlyMissing || WallpaperService.GetActiveWallpaper(kv.Key) == null)
            .Select(async kv =>
            {
                var (monitorId, assignment) = (kv.Key, kv.Value);
                var wallpaper = LibraryService.GetById(assignment.WallpaperId!);
                if (wallpaper == null)
                {
                    Log.Warning("Previously assigned wallpaper {Id} not found in library", assignment.WallpaperId);
                    return;
                }

                try
                {
                    await WallpaperService.SetWallpaperAsync(monitorId, wallpaper, skipSave: true);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to restore wallpaper '{Name}' on monitor {Monitor}",
                        wallpaper.Name, monitorId);
                }
            });

        await Task.WhenAll(tasks);

        // Single settings save after all monitors are restored.
        await SettingsService.SaveAsync();
    }

    private void SetupTrayIcon()
    {
        System.Drawing.Icon? icon = null;

        try
        {
            // Tray icon: use dedicated small circular icon (restored from original design)
            var iconUri = new Uri("pack://application:,,,/MorpheX;component/Assets/Branding/tray-icon.ico", UriKind.Absolute);
            var stream = Application.GetResourceStream(iconUri)?.Stream;
            if (stream != null)
            {
                icon = new System.Drawing.Icon(stream);
            }
        }
        catch { }

        if (icon == null)
        {
            try
            {
                var iconUri = new Uri("pack://application:,,,/MorpheX;component/Assets/Branding/icon.ico", UriKind.Absolute);
                var stream = Application.GetResourceStream(iconUri)?.Stream;
                if (stream != null)
                {
                    icon = new System.Drawing.Icon(stream);
                }
            }
            catch { }
        }

        if (icon == null)
        {
            try
            {
                var iconUri = new Uri("pack://application:,,,/MorpheX;component/Assets/Branding/morphex-icon.ico", UriKind.Absolute);
                var stream = Application.GetResourceStream(iconUri)?.Stream;
                if (stream != null)
                {
                    icon = new System.Drawing.Icon(stream);
                }
            }
            catch { }
        }

        if (icon == null)
        {
            var diskCandidates = new[]
            {
                System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Branding", "icon.ico"),
                System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Branding", "morphex-icon.ico")
            };
            foreach (var path in diskCandidates)
            {
                if (System.IO.File.Exists(path))
                {
                    try
                    {
                        icon = new System.Drawing.Icon(path);
                        break;
                    }
                    catch { }
                }
            }
        }

        if (icon == null)
        {
            try
            {
                var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exe))
                {
                    icon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
                }
            }
            catch { }
        }

        icon ??= System.Drawing.SystemIcons.Application;

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = icon,
            Text = "MorpheX Live",
            Visible = true,
            ContextMenuStrip = CreateTrayContextMenu()
        };

        // Keep tooltip fresh: update on every right-click open via menu.Opening
        // (already handled inside CreateTrayContextMenu)

        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                Dispatcher.InvokeAsync(ShowMainWindow);
            }
        };
    }

    private System.Windows.Forms.ContextMenuStrip CreateTrayContextMenu()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip
        {
            Renderer = new DarkContextMenuRenderer(),
            ShowImageMargin = false,
            BackColor = System.Drawing.Color.FromArgb(32, 32, 32),
            ForeColor = System.Drawing.Color.FromArgb(240, 240, 240),
            Padding = new System.Windows.Forms.Padding(4, 8, 4, 8),
        };

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        string verStr = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v0.1.64";
        var header = new System.Windows.Forms.ToolStripMenuItem($"MorpheX Live  •  {verStr}")
        {
            Enabled = false,
            Font = new System.Drawing.Font(menu.Font.FontFamily, 9.25f, System.Drawing.FontStyle.Bold)
        };
        menu.Items.Add(header);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var pauseResumeItem = new System.Windows.Forms.ToolStripMenuItem(
            PlaybackService.IsManuallyPaused ? "Resume Wallpaper" : "Pause Wallpaper");
        pauseResumeItem.Click += (_, _) =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (PlaybackService.IsManuallyPaused)
                {
                    Log.Information("Tray: Resume Wallpaper clicked");
                    PlaybackService.Resume();
                    pauseResumeItem.Text = "Pause Wallpaper";
                }
                else
                {
                    Log.Information("Tray: Pause Wallpaper clicked");
                    PlaybackService.Pause();
                    pauseResumeItem.Text = "Resume Wallpaper";
                }
            });
        };
        menu.Items.Add(pauseResumeItem);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        bool isMuted = !SettingsService.Settings.Audio.Enabled;
        var muteItem = new System.Windows.Forms.ToolStripMenuItem(isMuted ? "Unmute Audio" : "Mute Audio");
        muteItem.Click += (_, _) =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                var audio = SettingsService.Settings.Audio;
                audio.Enabled = !audio.Enabled;
                if (!audio.Enabled)
                {
                    WallpaperService.SetVolume(-1);
                    muteItem.Text = "Unmute Audio";
                    Log.Information("Tray: Audio muted");
                }
                else
                {
                    WallpaperService.SetVolume(audio.Volume / 100f);
                    muteItem.Text = "Mute Audio";
                    Log.Information("Tray: Audio unmuted ({Volume}%)", audio.Volume);
                }
                _ = SettingsService.SaveAsync();
            });
        };
        menu.Items.Add(muteItem);

        var profilesSubMenu = new System.Windows.Forms.ToolStripMenuItem("Desktop Profiles");

        menu.Opening += (_, _) =>
        {
            pauseResumeItem.Text = PlaybackService.IsManuallyPaused ? "Resume Wallpaper" : "Pause Wallpaper";
            muteItem.Text = SettingsService.Settings.Audio.Enabled ? "Mute Audio" : "Unmute Audio";

            var activeWallpapers = WallpaperService.GetAllActiveWallpapers();

            // Also update tray icon tooltip (max 63 chars for WinForms)
            string tooltip = activeWallpapers.Count > 0
                ? "MorpheX - " + activeWallpapers[0].Name
                : "MorpheX Live";
            if (_trayIcon != null)
                _trayIcon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;

            // Populate profiles submenu
            profilesSubMenu.DropDownItems.Clear();
            if (ProfileService?.Profiles != null)
            {
                foreach (var p in ProfileService.Profiles)
                {
                    var profileItem = new System.Windows.Forms.ToolStripMenuItem(p.Name);
                    profileItem.Checked = (p.Id == ProfileService.ActiveProfileId);
                    profileItem.Click += (_, _) =>
                    {
                        Dispatcher.InvokeAsync(async () =>
                        {
                            await ProfileService.ApplyProfileAsync(p.Id, WallpaperService, LibraryService, WidgetService);
                        });
                    };
                    profilesSubMenu.DropDownItems.Add(profileItem);
                }
            }
        };

        var nextItem = new System.Windows.Forms.ToolStripMenuItem("Next Wallpaper");
        nextItem.Click += (_, _) =>
        {
            Dispatcher.InvokeAsync(async () =>
            {
                Log.Information("Tray: Next Wallpaper clicked");
                await PlaylistService.TriggerNextWallpaperAsync();
            });
        };
        menu.Items.Add(nextItem);

        menu.Items.Add(profilesSubMenu);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var openItem = new System.Windows.Forms.ToolStripMenuItem("Open MorpheX")
        {
            Font = new System.Drawing.Font(menu.Font, System.Drawing.FontStyle.Bold)
        };
        openItem.Click += (_, _) =>
        {
            Dispatcher.InvokeAsync(ShowMainWindow);
        };
        menu.Items.Add(openItem);

        var settingsItem = new System.Windows.Forms.ToolStripMenuItem("Settings");
        settingsItem.Click += (_, _) =>
        {
            Dispatcher.InvokeAsync(ShowSettings);
        };
        menu.Items.Add(settingsItem);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            Dispatcher.InvokeAsync(ExitApplication);
        };
        menu.Items.Add(exitItem);

        return menu;
    }

    public void ShowMainWindow()
    {
        try
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow();
            }

            _mainWindow.BringToFront();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open or focus MainWindow");
            _mainWindow = null;
        }
    }

    public void ToggleMainWindow()
    {
        try
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow();
                _mainWindow.BringToFront();
                return;
            }

            if (_mainWindow.IsVisible && _mainWindow.WindowState != WindowState.Minimized && _mainWindow.IsActive)
            {
                _mainWindow.Hide();
            }
            else
            {
                _mainWindow.BringToFront();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to toggle MainWindow");
        }
    }

    public void HideMainWindow()
    {
        try
        {
            if (_mainWindow != null && _mainWindow.IsVisible)
            {
                _mainWindow.Hide();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to hide MainWindow");
        }
    }

    public void OpenLibraryPage()
    {
        try
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow();
            }

            _mainWindow.NavigateTo(typeof(LibraryPage));
            _mainWindow.BringToFront();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to navigate to LibraryPage");
        }
    }

    public void OpenFavoritesPage()
    {
        try
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow();
            }

            _mainWindow.NavigateTo(typeof(FavoritesPage));
            _mainWindow.BringToFront();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to navigate to FavoritesPage");
        }
    }

    public void OpenEffectsPage()
    {
        try
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow();
            }

            _mainWindow.NavigateTo(typeof(EffectsPage));
            _mainWindow.BringToFront();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to navigate to EffectsPage");
        }
    }

    public void ShowSettings()
    {
        try
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow();
            }

            _mainWindow.NavigateTo(typeof(SettingsPage));
            _mainWindow.BringToFront();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open SettingsPage");
            _mainWindow = null;
        }
    }

    private void ExitApplication()
    {
        Log.Information("MorpheX exiting via Tray menu");

        _pipeCts?.Cancel();
        _pipeCts?.Dispose();
        _monitorChangeDebounceCts?.Cancel();
        _monitorChangeDebounceCts?.Dispose();
        _monitorChangeLock.Dispose();

        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        _explorerWatcher?.Dispose();
        HotkeyService?.Dispose();
        PlaylistService?.Dispose();
        SystemMetricsService?.Dispose();
        PlaybackService?.Dispose();
        _ambientDimService?.Dispose();
        WidgetService?.Dispose();
        WallpaperService?.Dispose();
        MonitorService?.Dispose();

        if (_mainWindow != null)
        {
            _mainWindow.AllowClose();
            _mainWindow.Close();
        }

        if (_instanceMutex != null)
        {
            try { _instanceMutex.ReleaseMutex(); } catch { }
            _instanceMutex.Dispose();
            _instanceMutex = null;
        }

        LogManager.Shutdown();

        Shutdown();
        Environment.Exit(0);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled exception");
        e.Handled = true;
    }

    [DllImport("user32.dll")]
    private static extern void DisableProcessWindowsGhosting();
}
