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
            _media.AddOption(":file-caching=300");
            _media.AddOption(":clock-jitter=0");
            _media.AddOption(":clock-synchro=0");

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

            switch (scaling)
            {
                case ScalingMode.Stretch:
                    // Stretch (Fill): Forces video to fill display grid, which may warp aspect ratio
                    _mediaPlayer.CropGeometry = null;
                    _mediaPlayer.AspectRatio = (width > 0 && height > 0) ? $"{width}:{height}" : null;
                    break;

                case ScalingMode.Fit:
                    // Fit to Screen (Letterbox/Contain): Preserves uncropped frame with neutral bars
                    _mediaPlayer.CropGeometry = null;
                    _mediaPlayer.AspectRatio = null;
                    break;

                case ScalingMode.Fill:
                default:
                    // Crop to Center (Center Crop): Zooms in uniformly to fill screen without distortion, cropping outer edges
                    _mediaPlayer.CropGeometry = (width > 0 && height > 0) ? $"{width}:{height}" : null;
                    _mediaPlayer.AspectRatio = null;
                    break;
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

            // Fade-in: ramp VLC brightness from 0 → 1 over 500 ms
            if (FadeInOnLoad)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        const int steps = 10;
                        for (int i = 0; i <= steps; i++)
                        {
                            if (State != WallpaperState.Playing) break;
                            float b = i / (float)steps;
                            _mediaPlayer?.SetAdjustFloat(VideoAdjustOption.Brightness, b);
                            _mediaPlayer?.SetAdjustInt(VideoAdjustOption.Enable, 1);
                            await Task.Delay(50);
                        }
                        // After fade-in, disable adjust unless effects are active
                        if (!_adjustActive)
                            _mediaPlayer?.SetAdjustInt(VideoAdjustOption.Enable, 0);
                    }
                    catch { }
                });
            }


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

        // Volume fade-in: ramp from 0 → target over ~800 ms so audio doesn't slam back
        if (AudioEnabled && _mediaPlayer != null)
        {
            int targetVolume = _mediaPlayer.Volume;
            _ = Task.Run(async () =>
            {
                try
                {
                    const int steps = 16;
                    _mediaPlayer.Volume = 0;
                    for (int i = 1; i <= steps; i++)
                    {
                        if (State != WallpaperState.Playing) break;
                        _mediaPlayer.Volume = (int)Math.Round(targetVolume * (i / (float)steps));
                        await Task.Delay(50);
                    }
                }
                catch { }
            });
        }
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

    // ─── Effects & Rate ────────────────────────────────────────────────────

    /// <summary>Whether fade-in should play when wallpaper first loads.</summary>
    public bool FadeInOnLoad { get; set; } = true;

    private bool _adjustActive;

    /// <summary>
    /// Apply VLC Adjust filter for brightness / contrast / saturation / hue.
    /// Pass all 1.0 / 0 to disable the filter.
    /// </summary>
    public void SetVlcAdjust(float brightness, float contrast, float saturation, float hue)
    {
        if (_mediaPlayer == null) return;

        bool isDefault = Math.Abs(brightness - 1f) < 0.01f &&
                         Math.Abs(contrast  - 1f) < 0.01f &&
                         Math.Abs(saturation - 1f) < 0.01f &&
                         Math.Abs(hue) < 0.5f;

        if (isDefault)
        {
            _mediaPlayer.SetAdjustInt(VideoAdjustOption.Enable, 0);
            _adjustActive = false;
            return;
        }

        _mediaPlayer.SetAdjustFloat(VideoAdjustOption.Brightness, Math.Clamp(brightness, 0f, 2f));
        _mediaPlayer.SetAdjustFloat(VideoAdjustOption.Contrast,   Math.Clamp(contrast,   0f, 2f));
        _mediaPlayer.SetAdjustFloat(VideoAdjustOption.Saturation, Math.Clamp(saturation, 0f, 3f));
        _mediaPlayer.SetAdjustFloat(VideoAdjustOption.Hue,        Math.Clamp(hue, -180f, 180f));
        _mediaPlayer.SetAdjustInt(VideoAdjustOption.Enable, 1);
        _adjustActive = true;
    }

    /// <summary>Set playback speed (0.25 – 2.0). 1.0 = normal.</summary>
    public void SetPlaybackRate(float rate)
    {
        if (_mediaPlayer == null) return;
        _mediaPlayer.SetRate(Math.Clamp(rate, 0.25f, 2.0f));
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

    internal static LibVLC GetSharedLibVLC()
    {
        EnsureLibVLCInitialized();
        return _sharedLibVLC!;
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
                "--file-caching=300",
                "--live-caching=0",
                "--disc-caching=0",
                "--network-caching=0",
                "--clock-jitter=0",
                "--clock-synchro=0",
                "--no-spu"
            );

            Log.Information("LibVLC initialized with GPU hardware acceleration. Version: {Version}", _sharedLibVLC.Version);
        }
    }

    /// <summary>
    /// Probes a video file's dimensions and duration using native Windows Media Properties (WinRT).
    /// Executes safely in microseconds without LibVLC native parse concurrency risks.
    /// Returns (width, height, durationSeconds) or null if unable to determine.
    /// </summary>
    public static async Task<(int width, int height, double? durationSeconds)?> ProbeVideoMetadataAsync(string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
            var props = await file.Properties.GetVideoPropertiesAsync();
            if (props.Width > 0 && props.Height > 0)
            {
                double? duration = props.Duration.TotalSeconds > 0 ? props.Duration.TotalSeconds : null;
                return ((int)props.Width, (int)props.Height, duration);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to probe video metadata for {Path}", filePath);
        }

        return null;
    }

    /// <summary>
    /// Probes a video file's tracks to extract pixel width and height asynchronously.
    /// Returns (width, height) or null if unable to determine.
    /// </summary>
    public static async Task<(int width, int height)?> ProbeVideoDimensionsAsync(string filePath, CancellationToken ct = default)
    {
        var meta = await ProbeVideoMetadataAsync(filePath, ct);
        return meta.HasValue ? (meta.Value.width, meta.Value.height) : null;
    }

    /// <summary>
    /// Probes a video file to extract pixel width and height synchronously.
    /// Returns (width, height) or null if unable to determine.
    /// </summary>
    public static (int width, int height)? ProbeVideoDimensions(string filePath)
    {
        try
        {
            return Task.Run(async () => await ProbeVideoDimensionsAsync(filePath)).GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }


    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnloadAsync().GetAwaiter().GetResult();
    }
}
