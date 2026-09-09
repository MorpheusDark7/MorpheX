using MorpheX.Core.Models;

namespace MorpheX.Core.Providers;

public interface IWallpaperProvider : IDisposable
{
    WallpaperType Type { get; }

    IReadOnlyList<string> SupportedExtensions { get; }

    WallpaperState State { get; }

    Task LoadAsync(WallpaperInfo wallpaper, IntPtr hostHandle,
                   int width, int height, ScalingMode scaling,
                   CancellationToken ct = default);

    void Pause();

    void Resume();

    void SetVolume(float volume);

    void Resize(int width, int height);

    TimeSpan PlaybackPosition { get; }

    void SetResumePosition(TimeSpan position);

    Task UnloadAsync();

    string? ErrorMessage { get; }
}
