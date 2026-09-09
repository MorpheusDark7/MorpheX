using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using MorpheX.Core.Desktop;
using MorpheX.Core.Models;
using Serilog;

namespace MorpheX.Core.Providers;

public sealed class ImageWallpaperProvider : IWallpaperProvider
{
    public WallpaperType Type => WallpaperType.Image;

    public IReadOnlyList<string> SupportedExtensions { get; } = new[]
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tiff", ".tif"
    };

    private IntPtr _hostHandle;
    private Bitmap? _scaledBitmap;
    private bool _disposed;

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

        try
        {
            using var sourceImage = Image.FromFile(wallpaper.EffectivePath);
            ct.ThrowIfCancellationRequested();

            _scaledBitmap = ScaleImage(sourceImage, width, height, scaling);

            WallpaperHostWindow.SetPaintHandler(hostHandle, PaintToHdc);

            NativeMethods.InvalidateRect(hostHandle, IntPtr.Zero, false);
            NativeMethods.UpdateWindow(hostHandle);

            State = WallpaperState.Playing;
            Log.Information("ImageProvider loaded and painted: {Path} ({W}x{H} → {TW}x{TH})",
                wallpaper.EffectivePath, sourceImage.Width, sourceImage.Height, width, height);

            Utilities.MemoryOptimizer.TrimWorkingSet(force: true);
        }
        catch (OutOfMemoryException)
        {
            ErrorMessage = $"Cannot load image (corrupt or unsupported): {wallpaper.EffectivePath}";
            State = WallpaperState.Error;
            Log.Error(ErrorMessage);
            throw new InvalidOperationException(ErrorMessage);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = $"Failed to load image: {ex.Message}";
            State = WallpaperState.Error;
            Log.Error(ex, "ImageProvider load failed");
            throw;
        }

        return Task.CompletedTask;
    }

    public void Pause()
    {
        if (State == WallpaperState.Playing)
            State = WallpaperState.Paused;
    }

    public void Resume()
    {
        if (State == WallpaperState.Paused)
        {
            State = WallpaperState.Playing;
            if (_hostHandle != IntPtr.Zero)
            {
                NativeMethods.InvalidateRect(_hostHandle, IntPtr.Zero, false);
                NativeMethods.UpdateWindow(_hostHandle);
            }
        }
    }

    public void SetVolume(float volume) {  }

    public void Resize(int width, int height)
    {
        if (State is WallpaperState.Playing or WallpaperState.Paused && _hostHandle != IntPtr.Zero)
        {
            NativeMethods.InvalidateRect(_hostHandle, IntPtr.Zero, false);
            NativeMethods.UpdateWindow(_hostHandle);
        }
    }

    public Task UnloadAsync()
    {
        State = WallpaperState.Stopping;

        if (_hostHandle != IntPtr.Zero)
        {
            WallpaperHostWindow.SetPaintHandler(_hostHandle, null);
        }

        _scaledBitmap?.Dispose();
        _scaledBitmap = null;
        _hostHandle = IntPtr.Zero;
        ErrorMessage = null;
        State = WallpaperState.Unloaded;
        return Task.CompletedTask;
    }

    private void PaintToHdc(IntPtr hdc)
    {
        if (_scaledBitmap == null || hdc == IntPtr.Zero) return;

        try
        {
            using var graphics = Graphics.FromHdc(hdc);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.InterpolationMode = InterpolationMode.Default;
            graphics.DrawImage(_scaledBitmap, 0, 0);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to paint image to HDC");
        }
    }

    private static Bitmap ScaleImage(Image source, int targetWidth, int targetHeight, ScalingMode mode)
    {
        var target = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb);

        using var g = Graphics.FromImage(target);
        g.Clear(Color.Black);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var srcRect = new RectangleF(0, 0, source.Width, source.Height);
        RectangleF destRect;

        switch (mode)
        {
            case ScalingMode.Fill:
            {
                float scale = Math.Max((float)targetWidth / source.Width,
                                       (float)targetHeight / source.Height);
                float scaledW = source.Width * scale;
                float scaledH = source.Height * scale;
                destRect = new RectangleF(
                    (targetWidth - scaledW) / 2f, (targetHeight - scaledH) / 2f,
                    scaledW, scaledH);
                break;
            }
            case ScalingMode.Fit:
            {
                float scale = Math.Min((float)targetWidth / source.Width,
                                       (float)targetHeight / source.Height);
                float scaledW = source.Width * scale;
                float scaledH = source.Height * scale;
                destRect = new RectangleF(
                    (targetWidth - scaledW) / 2f, (targetHeight - scaledH) / 2f,
                    scaledW, scaledH);
                break;
            }
            case ScalingMode.Stretch:
                destRect = new RectangleF(0, 0, targetWidth, targetHeight);
                break;
            case ScalingMode.Center:
                destRect = new RectangleF(
                    (targetWidth - source.Width) / 2f, (targetHeight - source.Height) / 2f,
                    source.Width, source.Height);
                break;
            default:
                destRect = new RectangleF(0, 0, targetWidth, targetHeight);
                break;
        }

        g.DrawImage(source, destRect, srcRect, GraphicsUnit.Pixel);
        return target;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hostHandle != IntPtr.Zero)
        {
            WallpaperHostWindow.SetPaintHandler(_hostHandle, null);
        }

        _scaledBitmap?.Dispose();
        _scaledBitmap = null;
    }
}
