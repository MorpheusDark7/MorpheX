using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using MorpheX.Core.Models;
using Serilog;

namespace MorpheX.Core.Providers;

public sealed class GifWallpaperProvider : IWallpaperProvider
{
    public WallpaperType Type => WallpaperType.AnimatedImage;
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".gif" };

    private Image? _gifImage;
    private Timer? _frameTimer;
    private IntPtr _hostHandle;
    private int _frameCount;
    private int _currentFrame;
    private int[] _frameDelays = Array.Empty<int>();
    private int _width, _height;
    private bool _disposed;
    private readonly object _renderLock = new();

    public WallpaperState State { get; private set; } = WallpaperState.Unloaded;
    public string? ErrorMessage { get; private set; }

    public TimeSpan PlaybackPosition => TimeSpan.Zero;
    public void SetResumePosition(TimeSpan position) {  }

    public Task LoadAsync(WallpaperInfo wallpaper, IntPtr hostHandle,
                           int width, int height, ScalingMode scaling,
                           CancellationToken ct = default)
    {
        State = WallpaperState.Loading;
        _hostHandle = hostHandle;
        _width = width;
        _height = height;

        try
        {
            _gifImage = Image.FromFile(wallpaper.EffectivePath);
            var dimension = new FrameDimension(_gifImage.FrameDimensionsList[0]);
            _frameCount = _gifImage.GetFrameCount(dimension);
            _frameDelays = ExtractFrameDelays(_gifImage, _frameCount);
            _currentFrame = 0;

            DrawCurrentFrame();

            if (_frameCount > 1)
                ScheduleNextFrame();

            State = WallpaperState.Playing;
            Log.Debug("GifProvider loaded: {Path} ({Frames} frames)", wallpaper.EffectivePath, _frameCount);
            Utilities.MemoryOptimizer.TrimWorkingSet(force: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = $"Failed to load GIF: {ex.Message}";
            State = WallpaperState.Error;
            Log.Error(ex, "GifProvider load failed");
            throw;
        }

        return Task.CompletedTask;
    }

    public void Pause()
    {
        if (State != WallpaperState.Playing) return;
        _frameTimer?.Dispose();
        _frameTimer = null;
        State = WallpaperState.Paused;
    }

    public void Resume()
    {
        if (State != WallpaperState.Paused) return;
        if (_frameCount > 1)
            ScheduleNextFrame();
        State = WallpaperState.Playing;
    }

    public void SetVolume(float volume) {  }

    public void Resize(int width, int height)
    {
        _width = width;
        _height = height;
        if (State is WallpaperState.Playing or WallpaperState.Paused)
            DrawCurrentFrame();
    }

    public Task UnloadAsync()
    {
        State = WallpaperState.Stopping;
        _frameTimer?.Dispose();
        _frameTimer = null;
        _gifImage?.Dispose();
        _gifImage = null;
        ErrorMessage = null;
        State = WallpaperState.Unloaded;
        return Task.CompletedTask;
    }

    private void ScheduleNextFrame()
    {
        if (State != WallpaperState.Playing || _frameDelays.Length == 0) return;
        int delay = _frameDelays[_currentFrame];
        if (delay <= 0) delay = 100;
        _frameTimer?.Dispose();
        _frameTimer = new Timer(OnFrameTick, null, delay, Timeout.Infinite);
    }

    private void OnFrameTick(object? state)
    {
        if (State != WallpaperState.Playing) return;
        lock (_renderLock)
        {
            _currentFrame = (_currentFrame + 1) % _frameCount;
            DrawCurrentFrame();
        }
        ScheduleNextFrame();
    }

    private void DrawCurrentFrame()
    {
        if (_gifImage == null || _hostHandle == IntPtr.Zero) return;
        try
        {
            lock (_renderLock)
            {
                var dimension = new FrameDimension(_gifImage.FrameDimensionsList[0]);
                _gifImage.SelectActiveFrame(dimension, _currentFrame);

                using var graphics = Graphics.FromHwnd(_hostHandle);
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.Clear(Color.Black);
                graphics.DrawImage(_gifImage, 0, 0, _width, _height);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error drawing GIF frame {Frame}", _currentFrame);
        }
    }

    private static int[] ExtractFrameDelays(Image image, int frameCount)
    {
        try
        {
            var prop = image.GetPropertyItem(0x5100);
            if (prop?.Value != null)
            {
                var delays = new int[frameCount];
                for (int i = 0; i < frameCount && i * 4 < prop.Value.Length; i++)
                {
                    delays[i] = BitConverter.ToInt32(prop.Value, i * 4) * 10;
                    if (delays[i] <= 0) delays[i] = 100;
                }
                return delays;
            }
        }
        catch {  }
        return Enumerable.Repeat(100, frameCount).ToArray();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _frameTimer?.Dispose();
        _gifImage?.Dispose();
    }
}
