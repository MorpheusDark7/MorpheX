using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Wpf.Ui.Controls;

namespace MorpheX;

public partial class MainWindow : FluentWindow
{
    private readonly Dictionary<Type, Page> _pageCache = new();
    private bool _allowClose;

    public Page? CurrentPage => ContentFrame.Content as Page;

    public MainWindow()
    {
        InitializeComponent();

        NavigationView.SelectionChanged += NavigationView_SelectionChanged;

        NavLibrary.Click += (_, _) => NavigateTo(typeof(LibraryPage));
        NavFavorites.Click += (_, _) => NavigateTo(typeof(FavoritesPage));
        NavDisplays.Click += (_, _) => NavigateTo(typeof(DisplaysPage));
        NavWidgets.Click += (_, _) => NavigateTo(typeof(WidgetsPage));
        NavProfiles.Click += (_, _) => NavigateTo(typeof(ProfilesPage));
        NavEffects.Click += (_, _) => NavigateTo(typeof(EffectsPage));
        NavKeybinds.Click += (_, _) => NavigateTo(typeof(KeybindsPage));
        NavSettings.Click += (_, _) => NavigateTo(typeof(SettingsPage));

        Loaded += MainWindow_Loaded;

        StateChanged += MainWindow_StateChanged;

        NavigateTo(typeof(LibraryPage));
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        var app = (App)System.Windows.Application.Current;
        if (WindowState == WindowState.Minimized)
        {
            if (!app.SettingsService.Settings.Widgets.SystemMonitorEnabled)
            {
                app.SystemMetricsService.Stop();
            }
            Hide();
            MorpheX.Core.Utilities.MemoryOptimizer.TrimWorkingSet(force: true);
        }
        else if (WindowState == WindowState.Normal || WindowState == WindowState.Maximized)
        {
            if (app.SettingsService.Settings.Widgets.SystemMonitorEnabled)
            {
                app.SystemMetricsService.Start();
            }
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ClampToWorkArea();
        UpdateThemeToggleIcon();
    }

    private async void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)System.Windows.Application.Current;
        var effective = Wpf.Ui.Appearance.ApplicationThemeManager.GetAppTheme();
        string newMode = (effective == Wpf.Ui.Appearance.ApplicationTheme.Light) ? "Dark" : "Light";

        app.SettingsService.Settings.Appearance.Theme = newMode;
        await app.SettingsService.SaveAsync();

        Services.ThemeService.ApplyTheme(newMode);
        UpdateThemeToggleIcon(newMode);
    }

    public void UpdateThemeToggleIcon(string? themeMode = null)
    {
        var effective = Wpf.Ui.Appearance.ApplicationThemeManager.GetAppTheme();
        if (effective == Wpf.Ui.Appearance.ApplicationTheme.Light)
        {
            ThemeToggleIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.WeatherMoon24;
            ThemeToggleButton.ToolTip = "Switch to Dark Theme";
        }
        else
        {
            ThemeToggleIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.WeatherSunny24;
            ThemeToggleButton.ToolTip = "Switch to Light Theme";
        }
    }

    private void ClampToWorkArea()
    {
        var hwndSource = PresentationSource.FromVisual(this);
        if (hwndSource == null) return;

        var dpiScaleX = hwndSource.CompositionTarget.TransformToDevice.M11;
        var dpiScaleY = hwndSource.CompositionTarget.TransformToDevice.M22;

        var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle);
        var workArea = screen.WorkingArea;

        double availableWidth = workArea.Width / dpiScaleX;
        double availableHeight = workArea.Height / dpiScaleY;

        if (Height > availableHeight)
        {
            Height = availableHeight;
        }
        if (Width > availableWidth)
        {
            Width = availableWidth;
        }

        double workLeft = workArea.Left / dpiScaleX;
        double workTop = workArea.Top / dpiScaleY;

        if (Top < workTop) Top = workTop;
        if (Left < workLeft) Left = workLeft;
        if (Top + Height > workTop + availableHeight)
        {
            Top = workTop + availableHeight - Height;
        }
        if (Left + Width > workLeft + availableWidth)
        {
            Left = workLeft + availableWidth - Width;
        }
    }

    public void AllowClose()
    {
        _allowClose = true;
    }

    public void NavigateTo(Type pageType)
    {
        try
        {
            if (ContentFrame.Content?.GetType() == pageType)
            {
                if (ContentFrame.Content is LibraryPage lib) lib.RefreshWallpaperList();
                else if (ContentFrame.Content is FavoritesPage fav) fav.RefreshFavorites();
                else if (ContentFrame.Content is DisplaysPage disp) disp.RefreshDisplays();
                else if (ContentFrame.Content is ProfilesPage prof) prof.RenderProfiles();
                SelectNavigationItem(pageType);
                return;
            }

            if (!_pageCache.TryGetValue(pageType, out var page))
            {
                page = (Page)Activator.CreateInstance(pageType)!;
                _pageCache[pageType] = page;
            }

            if (page is LibraryPage libPage)
            {
                libPage.RefreshWallpaperList();
            }
            else if (page is FavoritesPage favPage)
            {
                favPage.RefreshFavorites();
            }
            else if (page is DisplaysPage dispPage)
            {
                dispPage.RefreshDisplays();
            }
            else if (page is ProfilesPage profPage)
            {
                profPage.RenderProfiles();
            }

            ContentFrame.Navigate(page);

            SelectNavigationItem(pageType);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to navigate to {PageType}", pageType.Name);
            MorpheX.Views.ModernDialogWindow.Error(
                title:   "Navigation Error",
                heading: $"Could not load {pageType.Name}",
                message: ex.Message);
        }
    }

    private void SelectNavigationItem(Type pageType)
    {
        NavLibrary.IsActive = (pageType == typeof(LibraryPage));
        NavFavorites.IsActive = (pageType == typeof(FavoritesPage));
        NavDisplays.IsActive = (pageType == typeof(DisplaysPage));
        NavWidgets.IsActive = (pageType == typeof(WidgetsPage));
        NavProfiles.IsActive = (pageType == typeof(ProfilesPage));
        NavEffects.IsActive = (pageType == typeof(EffectsPage));
        NavKeybinds.IsActive = (pageType == typeof(KeybindsPage));
        NavSettings.IsActive = (pageType == typeof(SettingsPage));
    }

    private void NavigationView_SelectionChanged(NavigationView sender,
        System.Windows.RoutedEventArgs args)
    {
        if (sender.SelectedItem is NavigationViewItem item && item.TargetPageType != null)
        {
            NavigateTo(item.TargetPageType);
        }
    }

    // ── Win32 helpers for reliable tray-icon focus ──────────────────────────
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    private const int SW_RESTORE = 9;
    private const int SW_SHOW    = 5;

    public void BringToFront()
    {
        Show();

        var hwnd = new WindowInteropHelper(this).Handle;

        // Restore from minimised / hidden state
        if (WindowState == WindowState.Minimized)
            ShowWindow(hwnd, SW_RESTORE);
        else
            ShowWindow(hwnd, SW_SHOW);

        // Attach to the foreground thread so Windows lets us steal focus
        IntPtr fgHwnd = GetForegroundWindow();
        uint fgThread = GetWindowThreadProcessId(fgHwnd, IntPtr.Zero);
        uint myThread = GetCurrentThreadId();

        if (fgThread != myThread)
            AttachThreadInput(myThread, fgThread, true);

        SetForegroundWindow(hwnd);

        if (fgThread != myThread)
            AttachThreadInput(myThread, fgThread, false);

        Activate();
        Focus();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized)
        {
            MorpheX.Core.Utilities.MemoryOptimizer.TrimWorkingSet(force: true);
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            var app = (App)System.Windows.Application.Current;
            if (!app.SettingsService.Settings.Widgets.SystemMonitorEnabled)
            {
                app.SystemMetricsService.Stop();
            }
            Hide();
            MorpheX.Core.Utilities.MemoryOptimizer.TrimWorkingSet(force: true);
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WndProc);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320;
        const int WM_SETTINGCHANGE = 0x001A;
        if (msg is WM_DWMCOLORIZATIONCOLORCHANGED or WM_SETTINGCHANGE)
        {
            Dispatcher.InvokeAsync(() =>
            {
                Services.ThemeService.ApplyTheme();
                UpdateThemeToggleIcon();
            });
        }
        return IntPtr.Zero;
    }
}
