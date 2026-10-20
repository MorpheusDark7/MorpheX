using System.Drawing;
using MorpheX.Core.Configuration;
using MorpheX.Core.Detection;
using MorpheX.Core.Models;
using Serilog;

namespace MorpheX.Core.Services;

public interface IPlaybackService : IDisposable
{
    void Start();
    void Stop();

    IReadOnlyDictionary<string, bool> MonitorPauseStates { get; }

    bool IsManuallyPaused { get; }

    void Pause();

    void Resume();

    void PollNow();
}

public sealed class PlaybackService : IPlaybackService
{
    private readonly IWallpaperService _wallpaperService;
    private readonly IMonitorService _monitorService;
    private readonly ISettingsService _settingsService;
    private readonly FullscreenDetector _fullscreenDetector = new();
    private readonly BatteryDetector _batteryDetector = new();

    private Timer? _pollTimer;
    private bool _disposed;
    private bool _isManuallyPaused;
    private int _pollInProgress;

    private readonly Dictionary<string, bool> _monitorPaused = new();

    private readonly Dictionary<string, DateTime> _pausedSince = new();

    public IReadOnlyDictionary<string, bool> MonitorPauseStates => _monitorPaused;
    public bool IsManuallyPaused => _isManuallyPaused;

    public PlaybackService(IWallpaperService wallpaperService,
                            IMonitorService monitorService,
                            ISettingsService settingsService)
    {
        _wallpaperService = wallpaperService;
        _monitorService = monitorService;
        _settingsService = settingsService;
    }

    public void Start()
    {
        _pollTimer = new Timer(PollState, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        Log.Debug("PlaybackService started — per-monitor polling active");
    }

    public void Stop()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    public void Pause()
    {
        _isManuallyPaused = true;
        _wallpaperService.PauseAll();
        foreach (var monitor in _monitorService.Monitors)
        {
            _monitorPaused[monitor.DeviceId] = true;
            _pausedSince[monitor.DeviceId] = DateTime.UtcNow;
        }
        Log.Information("PlaybackService: all wallpapers manually paused");
    }

    public void Resume()
    {
        _isManuallyPaused = false;
        _wallpaperService.ResumeAll();
        foreach (var monitor in _monitorService.Monitors)
        {
            _monitorPaused[monitor.DeviceId] = false;
            _pausedSince.Remove(monitor.DeviceId);
        }
        Log.Information("PlaybackService: all wallpapers resumed");
    }

    public void PollNow()
    {
        PollState(null);
    }

    private void PollState(object? state)
    {
        // Timer callbacks can overlap when desktop/window enumeration is slow. A
        // second pass racing the first can otherwise pause and resume a monitor out
        // of order, particularly while displays are being reconfigured.
        if (Interlocked.Exchange(ref _pollInProgress, 1) != 0)
        {
            return;
        }

        try
        {
            if (_isManuallyPaused)
            {
                return;
            }
            if (!_wallpaperService.IsAnyPlaying && !_monitorPaused.Values.Any(v => v))
            {
                return;
            }

            var settings = _settingsService.Settings.Performance;
            bool globalBatteryPause = settings.PauseOnBattery && _batteryDetector.IsOnBattery();
            bool isSpanned = string.Equals(_settingsService.Settings.Display.PlacementMode, "Span", StringComparison.OrdinalIgnoreCase);

            // 1. Evaluate pause condition for each monitor
            var monitorTriggers = new Dictionary<string, bool>();
            bool anyMonitorTriggered = false;

            foreach (var monitor in _monitorService.Monitors)
            {
                bool triggered = false;

                if (globalBatteryPause)
                {
                    triggered = true;
                }
                else
                {
                    var rule = GetEffectivePauseRule(monitor.DeviceId, settings);

                    if (rule != PauseRule.Never)
                    {
                        _fullscreenDetector.Check(monitor.Bounds, monitor.WorkArea, monitor.Handle,
                            out bool isFullscreen, out bool isMaximized);

                        triggered = rule switch
                        {
                            PauseRule.OnFullscreen => isFullscreen,
                            PauseRule.OnMaximized => isFullscreen || isMaximized,
                            PauseRule.OnAnyFocused => isFullscreen || isMaximized || IsAnyWindowFocusedOnMonitor(monitor),
                            PauseRule.OnCompletelyCovered => isFullscreen || isMaximized,
                            _ => false
                        };
                    }
                }

                monitorTriggers[monitor.DeviceId] = triggered;
                if (triggered) anyMonitorTriggered = true;
            }

            // 2. Decide per-monitor pause based on MultiMonitorPauseMode:
            // When AllDisplays (recommended, matches Wallpaper Engine default) or on battery,
            // any active trigger pauses ALL monitors.
            bool pauseAllMonitors = globalBatteryPause || isSpanned ||
                                    (settings.MultiMonitorPauseMode == MultiMonitorPauseMode.AllDisplays && anyMonitorTriggered);

            foreach (var monitor in _monitorService.Monitors)
            {
                bool shouldPause = pauseAllMonitors
                    ? (anyMonitorTriggered || globalBatteryPause)
                    : monitorTriggers.GetValueOrDefault(monitor.DeviceId);

                bool wasPaused = _monitorPaused.GetValueOrDefault(monitor.DeviceId);

                if (shouldPause && !wasPaused)
                {
                    _wallpaperService.PauseMonitor(monitor.DeviceId);
                    _monitorPaused[monitor.DeviceId] = true;
                    _pausedSince[monitor.DeviceId] = DateTime.UtcNow;
                    Log.Information("PlaybackService: Monitor '{Name}' paused (Rule: {Rule}, Battery: {Battery}, AllDisplays: {AllDisplays})",
                        monitor.DisplayName, settings.PauseRule, globalBatteryPause, settings.MultiMonitorPauseMode == MultiMonitorPauseMode.AllDisplays);
                }
                else if (!shouldPause && wasPaused)
                {
                    _wallpaperService.ResumeMonitor(monitor.DeviceId);
                    _monitorPaused[monitor.DeviceId] = false;
                    _pausedSince.Remove(monitor.DeviceId);
                    Log.Information("PlaybackService: Monitor '{Name}' resumed", monitor.DisplayName);
                }

                if (shouldPause && _pausedSince.TryGetValue(monitor.DeviceId, out var pausedAt))
                {
                    var unloadDelay = GetUnloadDelay(settings.UnloadPausedAfter);
                    if (unloadDelay.HasValue)
                    {
                        var elapsed = DateTime.UtcNow - pausedAt;
                        if (elapsed >= unloadDelay.Value)
                        {
                            var wallpaper = _wallpaperService.GetActiveWallpaper(monitor.DeviceId);
                            if (wallpaper != null)
                            {
                                _wallpaperService.UnloadMonitorResources(monitor.DeviceId);
                                Log.Information("Auto-unloaded wallpaper on {Monitor} after {Minutes}m idle",
                                    monitor.DisplayName, (int)elapsed.TotalMinutes);
                            }
                            _pausedSince.Remove(monitor.DeviceId);
                        }
                    }
                }
            }

            // 3. Handle spanned host wallpaper if active
            if (isSpanned)
            {
                bool spanShouldPause = anyMonitorTriggered || globalBatteryPause;
                bool spanWasPaused = _monitorPaused.GetValueOrDefault(WallpaperService.SpanDeviceId);

                if (spanShouldPause && !spanWasPaused)
                {
                    _wallpaperService.PauseMonitor(WallpaperService.SpanDeviceId);
                    _monitorPaused[WallpaperService.SpanDeviceId] = true;
                    Log.Information("PlaybackService: Spanned wallpaper paused");
                }
                else if (!spanShouldPause && spanWasPaused)
                {
                    _wallpaperService.ResumeMonitor(WallpaperService.SpanDeviceId);
                    _monitorPaused[WallpaperService.SpanDeviceId] = false;
                    Log.Information("PlaybackService: Spanned wallpaper resumed");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "PlaybackService poll error (non-fatal)");
        }
        finally
        {
            Volatile.Write(ref _pollInProgress, 0);
        }
    }

    private PauseRule GetEffectivePauseRule(string monitorDeviceId, PerformanceSettings settings)
    {
        if (_settingsService.Settings.MonitorAssignments.TryGetValue(monitorDeviceId, out var assignment)
            && assignment.PauseRule.HasValue)
        {
            return assignment.PauseRule.Value;
        }
        return settings.PauseRule;
    }

    private bool IsAnyWindowFocusedOnMonitor(Models.MonitorInfo monitor)
    {
        var fgWindow = Desktop.NativeMethods.GetForegroundWindow();
        if (fgWindow == IntPtr.Zero) return false;

        if (ShellWindowHelper.IsShellOrDesktopWindow(fgWindow))
            return false;

        if (!Desktop.NativeMethods.IsWindowVisible(fgWindow) || Desktop.NativeMethods.IsIconic(fgWindow))
            return false;

        if (!Desktop.NativeMethods.GetWindowRect(fgWindow, out var wr)) return false;
        var winRect = wr.ToRectangle();
        if (winRect.Width < 100 || winRect.Height < 100) return false;

        if (!monitor.Bounds.IntersectsWith(winRect)) return false;

        var windowMonitor = Desktop.NativeMethods.MonitorFromWindow(fgWindow,
            Desktop.NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (windowMonitor != IntPtr.Zero && monitor.Handle != IntPtr.Zero && windowMonitor == monitor.Handle)
        {
            return true;
        }

        var intersect = Rectangle.Intersect(monitor.Bounds, winRect);
        return intersect.Width >= 100 && intersect.Height >= 100;
    }

    private static TimeSpan? GetUnloadDelay(UnloadDelay delay) => delay switch
    {
        UnloadDelay.Never => null,
        UnloadDelay.FiveMinutes => TimeSpan.FromMinutes(5),
        UnloadDelay.FifteenMinutes => TimeSpan.FromMinutes(15),
        UnloadDelay.ThirtyMinutes => TimeSpan.FromMinutes(30),
        UnloadDelay.OneHour => TimeSpan.FromHours(1),
        _ => null
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
