using LibVLCSharp.Shared;
using MorpheX.Core.Models;
using Serilog;

namespace MorpheX.Core.Providers;

public sealed class VideoWallpaperProvider : IWallpaperProvider
{
    private static LibVLC? _sharedLibVLC;
    private static readonly object _libVlcLock = new();

    public WallpaperType Type => WallpaperType.Video;

    public IReadOnlyList<string> SupportedExtensions { get; } = new[]
    {
        ".mp4", ".webm", ".mkv", ".mov", ".avi"
    };

    private MediaPlayer? _mediaPlayer;
    private Media? _media;
    private IntPtr _hostHandle;
    private TimeSpan _resumePosition = TimeSpan.Zero;
    private bool _disposed;

    public WallpaperState State { get; private set; } = WallpaperState.Unloaded;
    public string? ErrorMessage { get; private set; }

    public bool AudioEnabled { get; set; } = false;

    public bool HardwareAccelerationEnabled { get; set; } = true;

    public TimeSpan PlaybackPosition
    {
        get
        {
            if (_mediaPlayer is { IsPlaying: true })
                return TimeSpan.FromMilliseconds(_mediaPlayer.Time);
            return _resumePosition;
        }
    }

    public void SetResumePosition(TimeSpan position)
    {
        _resumePosition = position;
    }

    public async Task LoadAsync(WallpaperInfo wallpaper, IntPtr hostHandle,
                                 int width, int height, ScalingMode scaling,
                                 CancellationToken ct = default)
    {
        State = WallpaperState.Loading;
        _hostHandle = hostHandle;

        try
        {
            EnsureLibVLCInitialized();
            ct.ThrowIfCancellationRequested();

            _media = new Media(_sharedLibVLC!, wallpaper.EffectivePath, FromType.FromPath);
            _media.AddOption(":no-video-title-show");
            _media.AddOption(":input-repeat=65535");
            _media.AddOption(":no-mouse-events");
            _media.AddOption(":file-caching=100");
            _media.AddOption(":clock-jitter=0");
            _media.AddOption(":clock-synchro=0");
            _media.AddOption(":avcodec-fast");
            _media.AddOption(":avcodec-skiploopfilter=4");

            if (HardwareAccelerationEnabled)
            {
                _media.AddOption(":avcodec-hw=any");
                _media.AddOption(":vout=direct3d11");
                _media.AddOption(":directx-hw-yuv");
                _media.AddOption(":direct3d11-hw-blending");
            }
            else
            {
                _media.AddOption(":avcodec-hw=none");
                _media.AddOption(":vout=any");
            }

            _mediaPlayer = new MediaPlayer(_media)
            {
                Hwnd = hostHandle,
                EnableMouseInput = false,
                EnableKeyInput = false,
                Volume = AudioEnabled ? 100 : 0,
                Mute = !AudioEnabled
            };

            if (scaling is ScalingMode.Fill or ScalingMode.Stretch && width > 0 && height > 0)
            {
                _mediaPlayer.AspectRatio = $"{width}:{height}";
            }
            else
            {
                _mediaPlayer.AspectRatio = null;
            }

            _mediaPlayer.EncounteredError += (_, _) =>
            {
                ErrorMessage = "Video playback error (hardware acceleration or format unsupported)";
                State = WallpaperState.Error;
                Log.Error("LibVLC encountered error for: {Path}", wallpaper.EffectivePath);
            };

            _mediaPlayer.EndReached += (_, _) =>
            {
                Task.Run(() =>
                {
                    try
                    {
                        if (_mediaPlayer != null && State == WallpaperState.Playing)
                        {
                            _mediaPlayer.Stop();
                            _mediaPlayer.Play();
                        }
                    }
                    catch { }
                });
            };

            _mediaPlayer.Play();

            if (_resumePosition > TimeSpan.Zero)
            {
                await Task.Delay(200, ct);
                if (_mediaPlayer.IsPlaying && _mediaPlayer.IsSeekable)
                {
                    _mediaPlayer.Time = (long)_resumePosition.TotalMilliseconds;
                    Log.Debug("Video resumed at position: {Position}", _resumePosition);
                }
                _resumePosition = TimeSpan.Zero;
            }

            State = WallpaperState.Playing;
            Log.Information("VideoProvider loaded: {Path}", wallpaper.EffectivePath);

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(2500);
                    Utilities.MemoryOptimizer.TrimWorkingSet(force: true);
                }
                catch { }
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = $"Failed to load video: {ex.Message}";
            State = WallpaperState.Error;
            Log.Error(ex, "VideoProvider load failed: {Path}", wallpaper.EffectivePath);
            throw;
        }
    }

    public void Pause()
    {
        if (State != WallpaperState.Playing) return;

        if (_mediaPlayer != null)
        {
            if (_mediaPlayer.IsPlaying)
                _resumePosition = TimeSpan.FromMilliseconds(_mediaPlayer.Time);

            _mediaPlayer.Pause();
        }

        State = WallpaperState.Paused;
        Log.Debug("Video paused at {Position}", _resumePosition);
    }

    public void Resume()
    {
        if (State != WallpaperState.Paused) return;

        if (_mediaPlayer != null && !_mediaPlayer.IsPlaying)
        {
            if (_resumePosition == TimeSpan.Zero && _mediaPlayer.IsSeekable)
            {
                _mediaPlayer.Time = 0;
            }
            _mediaPlayer.Play();
        }

        State = WallpaperState.Playing;
        Log.Debug("Video resumed");
    }

    public void SetVolume(float volume)
    {
        if (_mediaPlayer == null) return;

        if (volume < 0)
        {
            AudioEnabled = false;
            _mediaPlayer.Mute = true;
        }
        else
        {
            AudioEnabled = true;
            _mediaPlayer.Volume = Math.Clamp((int)Math.Round(volume * 100), 0, 100);
            _mediaPlayer.Mute = false;
        }
    }

    public void Resize(int width, int height)
    {
        if (_mediaPlayer != null && width > 0 && height > 0)
        {
            _mediaPlayer.AspectRatio = $"{width}:{height}";
        }
    }

    public Task UnloadAsync()
    {
        State = WallpaperState.Stopping;

        try
        {
            if (_mediaPlayer != null)
            {
                if (_mediaPlayer.IsPlaying)
                    _resumePosition = TimeSpan.FromMilliseconds(_mediaPlayer.Time);

                _mediaPlayer.Stop();
                _mediaPlayer.Hwnd = IntPtr.Zero;
                _mediaPlayer.Dispose();
                _mediaPlayer = null;
            }

            _media?.Dispose();
            _media = null;

            _hostHandle = IntPtr.Zero;
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error during VideoProvider unload");
        }

        State = WallpaperState.Unloaded;
        Log.Debug("VideoProvider unloaded (saved position: {Position})", _resumePosition);
        return Task.CompletedTask;
    }

    private static void EnsureLibVLCInitialized()
    {
        if (_sharedLibVLC != null) return;

        lock (_libVlcLock)
        {
            if (_sharedLibVLC != null) return;

            LibVLCSharp.Shared.Core.Initialize();

            _sharedLibVLC = new LibVLC(
                "--quiet",
                "--no-osd",
                "--no-snapshot-preview",
                "--no-stats",
                "--no-sub-autodetect-file",
                "--no-disable-screensaver",
                "--no-video-title-show",
                "--avcodec-hw=any",
                "--vout=direct3d11",
                "--directx-hw-yuv",
                "--direct3d11-hw-blending",
                "--avcodec-fast",
                "--avcodec-skiploopfilter=4",
                "--file-caching=100",
                "--live-caching=0",
                "--disc-caching=0",
                "--network-caching=0",
                "--clock-jitter=0",
                "--clock-synchro=0",
                "--no-spu",
                "--drop-late-frames",
                "--skip-frames",
                "--avcodec-threads=2"
            );

            Log.Information("LibVLC initialized with GPU hardware acceleration. Version: {Version}", _sharedLibVLC.Version);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnloadAsync().GetAwaiter().GetResult();
    }
}
