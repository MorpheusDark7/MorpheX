using MorpheX.Core.Desktop;
using MorpheX.Core.Models;
using MorpheX.Core.Providers;
using Serilog;

namespace MorpheX.Core.Services;

public interface IWallpaperService : IDisposable
{
    bool Initialize();

    Task SetWallpaperAsync(string monitorDeviceId, WallpaperInfo wallpaper, CancellationToken ct = default);

    Task SetWallpaperOnAllMonitorsAsync(WallpaperInfo wallpaper, CancellationToken ct = default);

    Task RemoveWallpaperAsync(string monitorDeviceId);

    void PauseAll();

    void ResumeAll();

    void PauseMonitor(string monitorDeviceId);

    void ResumeMonitor(string monitorDeviceId);

    void UnloadMonitorResources(string monitorDeviceId);

    void SetVolume(float volume);

    void SetMonitorVolume(string monitorDeviceId, int volume);

    void SetMonitorMuted(string monitorDeviceId, bool muted);

    int GetMonitorVolume(string monitorDeviceId);

    bool IsMonitorMuted(string monitorDeviceId);

    bool IsAnyPlaying { get; }

    bool IsWallpaperActive(string wallpaperId);

    IReadOnlyList<WallpaperInfo> GetAllActiveWallpapers();

    WallpaperInfo? GetActiveWallpaper(string monitorDeviceId);

    Task HandleMonitorChangeAsync();

    Task RecoverFromExplorerRestartAsync();

    Task ReloadActiveWallpapersAsync();
}

public sealed class WallpaperService : IWallpaperService
{
    private readonly WorkerWInterop _workerW = new();
    private readonly IMonitorService _monitorService;
    private readonly ISettingsService _settingsService;
    private readonly WallpaperProviderFactory _providerFactory;

    private readonly Dictionary<string, MonitorWallpaperState> _monitorStates = new();

    private bool _disposed;

    public bool IsAnyPlaying => _monitorStates.Values.Any(s => s.Provider?.State == WallpaperState.Playing);

    public WallpaperService(IMonitorService monitorService, ISettingsService settingsService,
                             WallpaperProviderFactory providerFactory)
    {
        _monitorService = monitorService;
        _settingsService = settingsService;
        _providerFactory = providerFactory;
    }

    public bool Initialize()
    {
        if (!_workerW.Initialize())
        {
            Log.Error("Failed to initialize WorkerW desktop integration");
            return false;
        }

        Log.Information("WallpaperService initialized successfully");
        return true;
    }

    public async Task SetWallpaperAsync(string monitorDeviceId, WallpaperInfo wallpaper,
                                         CancellationToken ct = default)
    {
        var monitor = _monitorService.Monitors.FirstOrDefault(m => m.DeviceId == monitorDeviceId);
        if (monitor == null)
        {
            Log.Warning("Cannot set wallpaper: monitor {DeviceId} not found", monitorDeviceId);
            return;
        }        
        var filePath = wallpaper.EffectivePath;
        if (!File.Exists(filePath))
        {
            var msg = $"Wallpaper file not found on disk:\n{filePath}\n\nIt may have been moved, renamed, or deleted.";
            Log.Error("Wallpaper file not found: {Path}", filePath);
            throw new FileNotFoundException(msg, filePath);
        }

        await RemoveWallpaperAsync(monitorDeviceId);

        if (!_workerW.IsAttached)
        {
            Log.Information("WorkerW not attached, attempting to initialize...");
            _workerW.Initialize();
        }

        var host = new WallpaperHostWindow();

        if (!host.Create(_workerW.WorkerWHandle,
            monitor.Bounds.X, monitor.Bounds.Y,
            monitor.Bounds.Width, monitor.Bounds.Height))
        {
            Log.Error("Failed to create wallpaper host window on monitor {DeviceId}", monitorDeviceId);
            host.Dispose();
            return;
        }

        IWallpaperProvider provider;
        try
        {
            provider = _providerFactory.Create(wallpaper.Type);
        }
        catch (NotSupportedException ex)
        {
            Log.Error(ex, "No provider for wallpaper type {Type}", wallpaper.Type);
            host.Dispose();
            return;
        }

        var scaling = _settingsService.Settings.Display.DefaultScalingMode;
        var assignment = _settingsService.Settings.MonitorAssignments
            .GetValueOrDefault(monitorDeviceId);
        if (assignment?.ScalingMode.HasValue == true)
        {
            scaling = assignment.ScalingMode.Value;
        }

        bool audioEnabled = _settingsService.Settings.Audio.Enabled && !IsMonitorMuted(monitorDeviceId);
        if (provider is VideoWallpaperProvider vwp)
        {
            vwp.AudioEnabled = audioEnabled;
            vwp.HardwareAccelerationEnabled = _settingsService.Settings.Playback.HardwareAcceleration;
        }

        try
        {
            await provider.LoadAsync(wallpaper, host.Handle,
                monitor.Bounds.Width, monitor.Bounds.Height,
                scaling, ct);

            ApplyAudioToMonitor(monitorDeviceId, provider);

            Log.Information("Wallpaper '{Name}' ({Type}) set on monitor {DeviceId}",
                wallpaper.Name, wallpaper.Type, monitorDeviceId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load wallpaper '{Name}' on monitor {DeviceId}",
                wallpaper.Name, monitorDeviceId);
            provider.Dispose();
            host.Dispose();
            return;
        }

        _monitorStates[monitorDeviceId] = new MonitorWallpaperState
        {
            MonitorDeviceId = monitorDeviceId,
            Wallpaper = wallpaper,
            Host = host,
            Provider = provider
        };

        if (!_settingsService.Settings.MonitorAssignments.TryGetValue(monitorDeviceId, out var savedAssignment))
        {
            savedAssignment = new Configuration.MonitorAssignment();
            _settingsService.Settings.MonitorAssignments[monitorDeviceId] = savedAssignment;
        }
        savedAssignment.WallpaperId = wallpaper.Id;
        savedAssignment.ScalingMode = scaling;
        await _settingsService.SaveAsync(ct);

        Utilities.MemoryOptimizer.TrimWorkingSet();
    }

    public async Task RemoveWallpaperAsync(string monitorDeviceId)
    {
        if (_monitorStates.TryGetValue(monitorDeviceId, out var state))
        {
            try
            {
                if (state.Provider != null)
                {
                    await state.Provider.UnloadAsync();
                    state.Provider.Dispose();
                }
                state.Host?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error during wallpaper cleanup on monitor {DeviceId}", monitorDeviceId);
            }

            _monitorStates.Remove(monitorDeviceId);
            Log.Information("Removed wallpaper from monitor {DeviceId}", monitorDeviceId);
            Utilities.MemoryOptimizer.TrimWorkingSet();
        }
    }

    public void PauseAll()
    {
        foreach (var state in _monitorStates.Values)
        {
            if (state.Provider != null && _settingsService.Settings.Playback.ResumeFromPreviousPosition)
            {
                SaveProviderPosition(state.MonitorDeviceId, state.Provider);
            }
            state.Provider?.Pause();
        }
        Log.Debug("Paused all wallpapers");
    }

    public void ResumeAll()
    {
        bool resumeFromPos = _settingsService.Settings.Playback.ResumeFromPreviousPosition;
        foreach (var (deviceId, state) in _monitorStates)
        {
            if (state.Provider == null || state.Provider.State == WallpaperState.Unloaded)
            {
                if (state.Wallpaper != null)
                {
                    Log.Information("Reloading auto-unloaded wallpaper '{Name}' on monitor {DeviceId}",
                        state.Wallpaper.Name, deviceId);
                    _ = SetWallpaperAsync(deviceId, state.Wallpaper);
                }
                continue;
            }

            if (!resumeFromPos)
            {
                state.Provider.SetResumePosition(TimeSpan.Zero);
            }
            state.Provider.Resume();
        }
        Log.Debug("Resumed all wallpapers");
    }

    public void PauseMonitor(string monitorDeviceId)
    {
        if (_monitorStates.TryGetValue(monitorDeviceId, out var state))
        {
            if (state.Provider != null && _settingsService.Settings.Playback.ResumeFromPreviousPosition)
            {
                SaveProviderPosition(monitorDeviceId, state.Provider);
            }
            state.Provider?.Pause();
        }
    }

    public void ResumeMonitor(string monitorDeviceId)
    {
        if (_monitorStates.TryGetValue(monitorDeviceId, out var state))
        {
            if (state.Provider == null || state.Provider.State == WallpaperState.Unloaded)
            {
                if (state.Wallpaper != null)
                {
                    Log.Information("Reloading auto-unloaded wallpaper '{Name}' on monitor {DeviceId}",
                        state.Wallpaper.Name, monitorDeviceId);
                    _ = SetWallpaperAsync(monitorDeviceId, state.Wallpaper);
                }
                return;
            }

            if (!_settingsService.Settings.Playback.ResumeFromPreviousPosition)
            {
                state.Provider.SetResumePosition(TimeSpan.Zero);
            }
            state.Provider.Resume();
        }
    }

    public void UnloadMonitorResources(string monitorDeviceId)
    {
        if (_monitorStates.TryGetValue(monitorDeviceId, out var state) && state.Provider != null)
        {
            SaveProviderPosition(monitorDeviceId, state.Provider);
            try
            {
                state.Provider.UnloadAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error unloading resources for monitor {DeviceId}", monitorDeviceId);
            }

            if (state.Host != null && state.Host.IsCreated)
            {
                NativeMethods.ShowWindow(state.Host.Handle, NativeMethods.SW_HIDE);
            }

            Log.Debug("Unloaded resources for monitor {DeviceId}", monitorDeviceId);
        }
    }

    public async Task SetWallpaperOnAllMonitorsAsync(WallpaperInfo wallpaper, CancellationToken ct = default)
    {
        foreach (var monitor in _monitorService.Monitors)
        {
            await SetWallpaperAsync(monitor.DeviceId, wallpaper, ct);
        }
    }

    public void SetVolume(float volume)
    {
        foreach (var (deviceId, state) in _monitorStates)
        {
            if (volume < 0)
            {
                state.Provider?.SetVolume(-1);
            }
            else
            {
                ApplyAudioToMonitor(deviceId, state.Provider);
            }
        }
    }

    public void SetMonitorVolume(string monitorDeviceId, int volume)
    {
        volume = Math.Clamp(volume, 0, 100);
        if (!_settingsService.Settings.MonitorAssignments.TryGetValue(monitorDeviceId, out var assignment))
        {
            assignment = new Configuration.MonitorAssignment();
            _settingsService.Settings.MonitorAssignments[monitorDeviceId] = assignment;
        }
        assignment.Volume = volume;
        _ = _settingsService.SaveAsync();

        if (_monitorStates.TryGetValue(monitorDeviceId, out var state) && state.Provider != null)
        {
            ApplyAudioToMonitor(monitorDeviceId, state.Provider);
        }
        Log.Information("Monitor {DeviceId} volume set to {Volume}%", monitorDeviceId, volume);
    }

    public void SetMonitorMuted(string monitorDeviceId, bool muted)
    {
        if (!_settingsService.Settings.MonitorAssignments.TryGetValue(monitorDeviceId, out var assignment))
        {
            assignment = new Configuration.MonitorAssignment();
            _settingsService.Settings.MonitorAssignments[monitorDeviceId] = assignment;
        }
        assignment.AudioMuted = muted;
        _ = _settingsService.SaveAsync();

        if (_monitorStates.TryGetValue(monitorDeviceId, out var state) && state.Provider != null)
        {
            ApplyAudioToMonitor(monitorDeviceId, state.Provider);
        }
        Log.Information("Monitor {DeviceId} audio {State}", monitorDeviceId, muted ? "muted" : "unmuted");
    }

    public int GetMonitorVolume(string monitorDeviceId)
    {
        if (_settingsService.Settings.MonitorAssignments.TryGetValue(monitorDeviceId, out var assignment) && assignment.Volume.HasValue)
        {
            return assignment.Volume.Value;
        }
        return _settingsService.Settings.Audio.Volume;
    }

    public bool IsMonitorMuted(string monitorDeviceId)
    {
        if (!_settingsService.Settings.Audio.Enabled)
            return true;

        if (_settingsService.Settings.MonitorAssignments.TryGetValue(monitorDeviceId, out var assignment) && assignment.AudioMuted.HasValue)
        {
            return assignment.AudioMuted.Value;
        }
        return false;
    }

    private void ApplyAudioToMonitor(string monitorDeviceId, IWallpaperProvider? provider)
    {
        if (provider == null) return;

        bool masterEnabled = _settingsService.Settings.Audio.Enabled;
        bool isMuted = !masterEnabled || IsMonitorMuted(monitorDeviceId);

        if (isMuted)
        {
            provider.SetVolume(-1);
        }
        else
        {
            int vol = GetMonitorVolume(monitorDeviceId);
            provider.SetVolume(vol / 100f);
        }
    }

    private void SaveProviderPosition(string monitorDeviceId, IWallpaperProvider provider)
    {
        var pos = provider.PlaybackPosition;
        if (pos > TimeSpan.Zero)
        {
            var assignments = _settingsService.Settings.MonitorAssignments;
            if (assignments.TryGetValue(monitorDeviceId, out var assignment))
            {
                assignment.SavedPositionSeconds = pos.TotalSeconds;
            }
        }
    }

    public bool IsWallpaperActive(string wallpaperId)
    {
        return _monitorStates.Values.Any(s => s.Wallpaper != null && s.Wallpaper.Id == wallpaperId);
    }

    public IReadOnlyList<WallpaperInfo> GetAllActiveWallpapers()
    {
        return _monitorStates.Values
            .Where(s => s.Wallpaper != null)
            .Select(s => s.Wallpaper!)
            .ToList();
    }

    public WallpaperInfo? GetActiveWallpaper(string monitorDeviceId)
    {
        return _monitorStates.TryGetValue(monitorDeviceId, out var state) ? state.Wallpaper : null;
    }

    public async Task HandleMonitorChangeAsync()
    {
        _monitorService.Refresh();

        if (_monitorService.Monitors.Count == 0)
        {
            Log.Debug("No monitors currently detected (displays may be entering standby). Preserving wallpaper states.");
            return;
        }

        foreach (var state in _monitorStates.Values)
        {
            var monitor = _monitorService.Monitors.FirstOrDefault(m => m.DeviceId == state.MonitorDeviceId);
            if (monitor != null)
            {
                state.Host?.Reposition(monitor.Bounds.X, monitor.Bounds.Y,
                    monitor.Bounds.Width, monitor.Bounds.Height);
                state.Provider?.Resize(monitor.Bounds.Width, monitor.Bounds.Height);
            }
        }
    }

    public async Task RecoverFromExplorerRestartAsync()
    {
        Log.Information("Recovering from Explorer restart");

        await Task.Delay(1000);

        if (!_workerW.Reinitialize())
        {
            Log.Error("Failed to reinitialize WorkerW after Explorer restart");
            return;
        }

        var activeAssignments = _monitorStates.Values
            .Where(s => s.Wallpaper != null)
            .Select(s => (s.MonitorDeviceId, s.Wallpaper!))
            .ToList();

        foreach (var (monitorId, wallpaper) in activeAssignments)
        {
            try
            {
                await SetWallpaperAsync(monitorId, wallpaper);
                Log.Information("Restored wallpaper '{Name}' after Explorer restart", wallpaper.Name);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to restore wallpaper on {DeviceId} after Explorer restart", monitorId);
            }
        }
    }

    public async Task ReloadActiveWallpapersAsync()
    {
        Log.Information("Reloading active wallpapers across all monitors");
        var activeEntries = _monitorStates
            .Where(kv => kv.Value.Wallpaper != null)
            .Select(kv => (DeviceId: kv.Key, Wallpaper: kv.Value.Wallpaper!))
            .ToList();

        foreach (var (deviceId, wallpaper) in activeEntries)
        {
            await SetWallpaperAsync(deviceId, wallpaper);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var state in _monitorStates.Values)
        {
            try
            {
                state.Provider?.UnloadAsync().GetAwaiter().GetResult();
                state.Provider?.Dispose();
                state.Host?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error disposing wallpaper state for {DeviceId}", state.MonitorDeviceId);
            }
        }
        _monitorStates.Clear();
    }

    private sealed class MonitorWallpaperState
    {
        public required string MonitorDeviceId { get; init; }
        public required WallpaperInfo Wallpaper { get; init; }
        public WallpaperHostWindow? Host { get; init; }
        public IWallpaperProvider? Provider { get; init; }
    }
}
