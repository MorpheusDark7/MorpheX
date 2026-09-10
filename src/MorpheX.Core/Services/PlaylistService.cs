using MorpheX.Core.Configuration;
using MorpheX.Core.Models;
using Serilog;

namespace MorpheX.Core.Services;

public interface IPlaylistService : IDisposable
{
    void Start();
    void Stop();
    void UpdateSettings();
    Task TriggerNextWallpaperAsync();
}

public sealed class PlaylistService : IPlaylistService
{
    private readonly IWallpaperService _wallpaperService;
    private readonly ILibraryService _libraryService;
    private readonly IMonitorService _monitorService;
    private readonly ISettingsService _settingsService;
    private readonly IPlaybackService _playbackService;

    private Timer? _rotationTimer;
    private readonly SemaphoreSlim _rotationLock = new(1, 1);
    private readonly Random _random = new();
    private int _sequentialIndex;
    private string? _lastWallpaperId;
    private bool _disposed;

    public PlaylistService(
        IWallpaperService wallpaperService,
        ILibraryService libraryService,
        IMonitorService monitorService,
        ISettingsService settingsService,
        IPlaybackService playbackService)
    {
        _wallpaperService = wallpaperService;
        _libraryService = libraryService;
        _monitorService = monitorService;
        _settingsService = settingsService;
        _playbackService = playbackService;
    }

    public void Start()
    {
        var config = _settingsService.Settings.Playlist;
        if (!config.Enabled)
        {
            Log.Debug("PlaylistService: Auto-rotation disabled");
            return;
        }

        int minutes = Math.Max(1, config.IntervalMinutes);
        var interval = TimeSpan.FromMinutes(minutes);

        _rotationTimer?.Dispose();
        _rotationTimer = new Timer(async _ => await OnTimerElapsedAsync(), null, interval, interval);

        Log.Information("PlaylistService started: Interval={Minutes}m, Source={Source}, Order={Order}",
            minutes, config.Source, config.Order);

        if (config.ChangeOnStartup)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                await TriggerNextWallpaperAsync();
            });
        }
    }

    public void Stop()
    {
        _rotationTimer?.Dispose();
        _rotationTimer = null;
        Log.Debug("PlaylistService stopped");
    }

    public void UpdateSettings()
    {
        Stop();
        Start();
    }

    private async Task OnTimerElapsedAsync()
    {
        var config = _settingsService.Settings.Playlist;
        if (!config.Enabled) return;

        if (config.SkipWhenPaused)
        {
            if (_playbackService.IsManuallyPaused)
            {
                Log.Debug("PlaylistService: Skip rotation — playback manually paused");
                return;
            }

            bool anyMonitorPaused = _playbackService.MonitorPauseStates.Values.Any(isPaused => isPaused);
            if (anyMonitorPaused)
            {
                Log.Debug("PlaylistService: Skip rotation — fullscreen game or pause rule active");
                return;
            }
        }

        await TriggerNextWallpaperAsync();
    }

    public async Task TriggerNextWallpaperAsync()
    {
        // A slow provider load must not allow timer ticks or hotkeys to start a
        // second rotation concurrently and overwrite the first assignment.
        if (!await _rotationLock.WaitAsync(0))
        {
            Log.Debug("PlaylistService: Rotation already in progress");
            return;
        }

        try
        {
            var config = _settingsService.Settings.Playlist;
            var allWallpapers = _libraryService.Wallpapers;

            if (allWallpapers.Count == 0)
            {
                Log.Debug("PlaylistService: Library is empty, cannot rotate");
                return;
            }

            List<WallpaperInfo> candidates;
            if (config.Source == PlaylistSource.FavoritesOnly)
            {
                candidates = allWallpapers.Where(w => w.IsFavorite).ToList();
                if (candidates.Count == 0)
                {
                    candidates = allWallpapers.ToList();
                }
            }
            else
            {
                candidates = allWallpapers.ToList();
            }

            if (candidates.Count == 0) return;

            WallpaperInfo nextWallpaper;

            if (config.Order == PlaylistOrder.Shuffle)
            {
                if (candidates.Count == 1)
                {
                    nextWallpaper = candidates[0];
                }
                else
                {
                    var eligible = candidates.Where(w => w.Id != _lastWallpaperId).ToList();
                    nextWallpaper = eligible.Count > 0
                        ? eligible[_random.Next(eligible.Count)]
                        : candidates[_random.Next(candidates.Count)];
                }
            }
            else
            {
                _sequentialIndex = (_sequentialIndex + 1) % candidates.Count;
                nextWallpaper = candidates[_sequentialIndex];
            }

            _lastWallpaperId = nextWallpaper.Id;
            Log.Information("PlaylistService: Rotating to wallpaper '{Name}' (Order={Order})",
                nextWallpaper.Name, config.Order);

            var monitors = _monitorService.Monitors;
            foreach (var monitor in monitors)
            {
                await _wallpaperService.SetWallpaperAsync(monitor.DeviceId, nextWallpaper);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PlaylistService: Error rotating wallpaper");
        }
        finally
        {
            _rotationLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rotationTimer?.Dispose();
        _rotationTimer = null;
    }
}
