using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using MorpheX.Core.Desktop;
using MorpheX.Core.Models;
using Serilog;

namespace MorpheX.Core.Providers;

public sealed class GifWallpaperProvider : IWallpaperProvider
{
    public WallpaperType Type => WallpaperType.AnimatedImage;
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".gif" };

    private Image? _gifImage;
    private FrameDimension? _gifDimension;
    private Bitmap? _frameBuffer;
    private Timer? _frameTimer;
    private IntPtr _hostHandle;
    private int _frameCount;
    private int _currentFrame;
    private int[] _frameDelays = Array.Empty<int>();
    private int _width, _height;
    private ScalingMode _scaling = ScalingMode.Fill;
    private ColorMatrix? _colorMatrix;
    private bool _disposed;
    private readonly object _renderLock = new();

    public WallpaperState State { get; private set; } = WallpaperState.Unloaded;
    public string? ErrorMessage { get; private set; }

    public TimeSpan PlaybackPosition => TimeSpan.Zero;
    public void SetResumePosition(TimeSpan position) { }

    public Task LoadAsync(WallpaperInfo wallpaper, IntPtr hostHandle,
                           int width, int height, ScalingMode scaling,
                           CancellationToken ct = default)
    {
        State = WallpaperState.Loading;
        _hostHandle = hostHandle;
        _width = width;
        _height = height;
        _scaling = scaling;

        try
        {
            _gifImage = Image.FromFile(wallpaper.EffectivePath);
            ct.ThrowIfCancellationRequested();

            _gifDimension = new FrameDimension(_gifImage.FrameDimensionsList[0]);
            _frameCount = _gifImage.GetFrameCount(_gifDimension);
            _frameDelays = ExtractFrameDelays(_gifImage, _frameCount);
            _currentFrame = 0;

            _frameBuffer = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            RenderFrameToBuffer();

            WallpaperHostWindow.SetPaintHandler(hostHandle, PaintToHdc);

            NativeMethods.InvalidateRect(hostHandle, IntPtr.Zero, false);
            NativeMethods.UpdateWindow(hostHandle);

            State = WallpaperState.Playing;

            if (_frameCount > 1)
                ScheduleNextFrame();

            Log.Information("GifProvider loaded: {Path} ({W}x{H} \u2192 {TW}x{TH}, {Frames} frames)",
                wallpaper.EffectivePath, _gifImage.Width, _gifImage.Height, width, height, _frameCount);

            Utilities.MemoryOptimizer.TrimWorkingSet(force: true);
        }
        catch (OutOfMemoryException)
        {
            ErrorMessage = $"Cannot load GIF (corrupt or unsupported): {wallpaper.EffectivePath}";
            State = WallpaperState.Error;
            Log.Error(ErrorMessage);
            throw new InvalidOperationException(ErrorMessage);
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
        State = WallpaperState.Playing;
        if (_frameCount > 1)
            ScheduleNextFrame();
    }

    public void SetVolume(float volume) { }

    /// <summary>Apply a post-processing color matrix. Pass null to clear.</summary>
    public void SetColorMatrix(ColorMatrix? matrix)
    {
        _colorMatrix = matrix;
        if (_hostHandle != IntPtr.Zero)
        {
            NativeMethods.InvalidateRect(_hostHandle, IntPtr.Zero, false);
            NativeMethods.UpdateWindow(_hostHandle);
        }
    }

    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        _width = width;
        _height = height;

        lock (_renderLock)
        {
            _frameBuffer?.Dispose();
            _frameBuffer = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            RenderFrameToBuffer();
        }

        if (_hostHandle != IntPtr.Zero)
        {
            NativeMethods.InvalidateRect(_hostHandle, IntPtr.Zero, false);
            NativeMethods.UpdateWindow(_hostHandle);
        }
    }

    public Task UnloadAsync()
    {
        State = WallpaperState.Stopping;

        _frameTimer?.Dispose();
        _frameTimer = null;

        if (_hostHandle != IntPtr.Zero)
        {
            WallpaperHostWindow.SetPaintHandler(_hostHandle, null);
        }

        lock (_renderLock)
        {
            _frameBuffer?.Dispose();
            _frameBuffer = null;
            _gifImage?.Dispose();
            _gifImage = null;
            _gifDimension = null;
        }

        _hostHandle = IntPtr.Zero;
        ErrorMessage = null;
        State = WallpaperState.Unloaded;
        return Task.CompletedTask;
    }

    private void ScheduleNextFrame()
    {
        if (State != WallpaperState.Playing || _frameCount <= 1 || _frameDelays.Length == 0 || _disposed) return;

        int delay = _frameDelays[_currentFrame % _frameDelays.Length];
        if (delay <= 10) delay = 100;

        try
        {
            if (_frameTimer == null)
            {
                _frameTimer = new Timer(OnFrameTick, null, delay, Timeout.Infinite);
            }
            else
            {
                _frameTimer.Change(delay, Timeout.Infinite);
            }
        }
        catch (ObjectDisposedException) { }
    }

    private void OnFrameTick(object? state)
    {
        if (State != WallpaperState.Playing || _hostHandle == IntPtr.Zero || _disposed) return;

        lock (_renderLock)
        {
            _currentFrame = (_currentFrame + 1) % _frameCount;
            RenderFrameToBuffer();
        }

        NativeMethods.InvalidateRect(_hostHandle, IntPtr.Zero, false);
        NativeMethods.UpdateWindow(_hostHandle);

        ScheduleNextFrame();
    }

    private void RenderFrameToBuffer()
    {
        if (_gifImage == null || _frameBuffer == null || _gifDimension == null) return;

        try
        {
            _gifImage.SelectActiveFrame(_gifDimension, _currentFrame);

            using var g = Graphics.FromImage(_frameBuffer);
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Black);

            var (destRect, srcRect) = CalculateDestRect(_gifImage.Width, _gifImage.Height, _width, _height, _scaling);
            g.DrawImage(_gifImage, destRect, srcRect, GraphicsUnit.Pixel);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error rendering GIF frame {Frame} to buffer", _currentFrame);
        }
    }

    private void PaintToHdc(IntPtr hdc)
    {
        if (_frameBuffer == null || hdc == IntPtr.Zero || _disposed) return;

        try
        {
            lock (_renderLock)
            {
                using var graphics = Graphics.FromHdc(hdc);
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.InterpolationMode = InterpolationMode.Default;

                if (_colorMatrix != null)
                {
                    using var ia = new ImageAttributes();
                    ia.SetColorMatrix(_colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    var rect = new System.Drawing.Rectangle(0, 0, _width, _height);
                    graphics.DrawImage(_frameBuffer, rect, 0, 0, _width, _height, GraphicsUnit.Pixel, ia);
                }
                else
                {
                    graphics.DrawImage(_frameBuffer, 0, 0);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to paint GIF buffer to HDC");
        }
    }

    private static (RectangleF destRect, RectangleF srcRect) CalculateDestRect(
        int srcWidth, int srcHeight, int targetWidth, int targetHeight, ScalingMode mode)
    {
        var srcRect = new RectangleF(0, 0, srcWidth, srcHeight);
        RectangleF destRect;

        switch (mode)
        {
            case ScalingMode.Fill:
            {
                float scale = Math.Max((float)targetWidth / srcWidth, (float)targetHeight / srcHeight);
                float scaledW = srcWidth * scale;
                float scaledH = srcHeight * scale;
                destRect = new RectangleF(
                    (targetWidth - scaledW) / 2f,
                    (targetHeight - scaledH) / 2f,
                    scaledW, scaledH);
                break;
            }
            case ScalingMode.Fit:
            {
                float scale = Math.Min((float)targetWidth / srcWidth, (float)targetHeight / srcHeight);
                float scaledW = srcWidth * scale;
                float scaledH = srcHeight * scale;
                destRect = new RectangleF(
                    (targetWidth - scaledW) / 2f,
                    (targetHeight - scaledH) / 2f,
                    scaledW, scaledH);
                break;
            }
            case ScalingMode.Stretch:
                destRect = new RectangleF(0, 0, targetWidth, targetHeight);
                break;
            case ScalingMode.Center:
                destRect = new RectangleF(
                    (targetWidth - srcWidth) / 2f,
                    (targetHeight - srcHeight) / 2f,
                    srcWidth, srcHeight);
                break;
            default:
                destRect = new RectangleF(0, 0, targetWidth, targetHeight);
                break;
        }

        return (destRect, srcRect);
    }

    private static int[] ExtractFrameDelays(Image image, int frameCount)
    {
        try
        {
            var prop = image.GetPropertyItem(0x5100);
            if (prop?.Value != null)
            {
                var delays = new int[frameCount];
                int delayCount = prop.Value.Length / 4;
                for (int i = 0; i < frameCount; i++)
                {
                    if (i < delayCount)
                    {
                        delays[i] = BitConverter.ToInt32(prop.Value, i * 4) * 10;
                    }
                    else
                    {
                        delays[i] = delays.Length > 0 ? delays[0] : 100;
                    }

                    if (delays[i] <= 10) delays[i] = 100;
                }
                return delays;
            }
        }
        catch { }
        return Enumerable.Repeat(100, frameCount).ToArray();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _frameTimer?.Dispose();
        _frameTimer = null;

        if (_hostHandle != IntPtr.Zero)
        {
            WallpaperHostWindow.SetPaintHandler(_hostHandle, null);
        }

        lock (_renderLock)
        {
            _frameBuffer?.Dispose();
            _frameBuffer = null;
            _gifImage?.Dispose();
            _gifImage = null;
            _gifDimension = null;
        }
    }
}
