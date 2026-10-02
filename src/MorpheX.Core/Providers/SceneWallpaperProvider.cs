using LibVLCSharp.Shared;
using MorpheX.Core.Desktop;
using MorpheX.Core.Models;
using MorpheX.Core.Utilities;
using Serilog;
using System.Drawing.Imaging;
using System.Text.Json;

namespace MorpheX.Core.Providers;

/// <summary>
/// Provider for Wallpaper Engine Scene wallpapers (.pkg + project.json folders).
///
/// Features:
///   1. Renders the animated preview (preview.gif) or static preview (preview.png/jpg)
///      using GifWallpaperProvider or ImageWallpaperProvider.
///   2. Extracts background audio tracks (mp3, ogg, wav, flac) from scene.pkg and
///      plays them seamlessly in a loop via LibVLC.
///   3. Completely standalone and crash-proof.
/// </summary>
public sealed class SceneWallpaperProvider : IWallpaperProvider
{
    public WallpaperType Type => WallpaperType.Scene;
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".pkg", ".zip" };

    public WallpaperState State { get; private set; } = WallpaperState.Unloaded;
    public string? ErrorMessage { get; private set; }
    public TimeSpan PlaybackPosition => TimeSpan.Zero;
    public void SetResumePosition(TimeSpan position) { }

    private IWallpaperProvider? _visualProvider;
    private LibVLC? _libVLC;
    private MediaPlayer? _audioPlayer;
    private Media? _audioMedia;
    private string? _tempAudioPath;
    private float _volume = 1.0f;
    private bool _isMuted;
    private bool _audioEnabled = true;
    private bool _disposed;
    private readonly object _audioLock = new();

    // -------------------------------------------------------------------------
    // IWallpaperProvider - Load
    // -------------------------------------------------------------------------

    public async Task LoadAsync(WallpaperInfo wallpaper, IntPtr hostHandle,
                                int width, int height, ScalingMode scaling,
                                CancellationToken ct = default)
    {
        State = WallpaperState.Loading;

        var previewPath = ResolvePreviewPath(wallpaper);
        if (previewPath == null || !File.Exists(previewPath))
        {
            ErrorMessage = $"Scene preview not found for wallpaper '{wallpaper.Name}'.";
            State = WallpaperState.Error;
            Log.Error(ErrorMessage);
            throw new FileNotFoundException(ErrorMessage);
        }

        bool isGif = previewPath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
        if (isGif)
        {
            _visualProvider = new GifWallpaperProvider();
        }
        else
        {
            _visualProvider = new ImageWallpaperProvider();
        }

        var visualInfo = new WallpaperInfo
        {
            Id = wallpaper.Id,
            Name = wallpaper.Name,
            Type = isGif ? WallpaperType.AnimatedImage : WallpaperType.Image,
            SourcePath = previewPath
        };

        await _visualProvider.LoadAsync(visualInfo, hostHandle, width, height, scaling, ct);
        State = _visualProvider.State;

        // Start background audio asynchronously
        if (_audioEnabled)
        {
            _ = Task.Run(() => StartAudioAsync(wallpaper), ct);
        }

        Log.Information("SceneProvider loaded '{Name}' (preview={Preview}, audio={Audio})",
            wallpaper.Name, previewPath, _audioEnabled ? "enabled" : "disabled");
    }

    // -------------------------------------------------------------------------
    // Playback control
    // -------------------------------------------------------------------------

    public void Pause()
    {
        _visualProvider?.Pause();
        lock (_audioLock)
        {
            _audioPlayer?.Pause();
        }
        if (State == WallpaperState.Playing) State = WallpaperState.Paused;
    }

    public void Resume()
    {
        _visualProvider?.Resume();
        lock (_audioLock)
        {
            _audioPlayer?.Play();
        }
        if (State == WallpaperState.Paused) State = WallpaperState.Playing;
    }

    public void SetVolume(float volume)
    {
        lock (_audioLock)
        {
            if (volume < 0)
            {
                _isMuted = true;
                if (_audioPlayer != null)
                {
                    _audioPlayer.Mute = true;
                }
            }
            else
            {
                _isMuted = false;
                _volume = Math.Clamp(volume, 0f, 1f);
                if (_audioPlayer != null)
                {
                    _audioPlayer.Mute = false;
                    _audioPlayer.Volume = (int)(_volume * 100f);
                }
            }
        }
    }

    public void Resize(int width, int height)
    {
        _visualProvider?.Resize(width, height);
    }

    internal void SetColorMatrix(ColorMatrix? matrix)
    {
        if (_visualProvider is ImageWallpaperProvider img)
            img.SetColorMatrix(matrix);
        else if (_visualProvider is GifWallpaperProvider gif)
            gif.SetColorMatrix(matrix);
    }

    public Task UnloadAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // Audio helpers (LibVLC)
    // -------------------------------------------------------------------------

    private void StartAudioAsync(WallpaperInfo wallpaper)
    {
        try
        {
            var pkgPath = FindPkgFile(wallpaper);
            if (pkgPath == null) return;

            var audioEntries = PkgReader.GetAudioEntries(pkgPath).ToList();
            if (audioEntries.Count == 0)
            {
                Log.Debug("SceneProvider: no audio found in {Pkg}", pkgPath);
                return;
            }

            var audioEntry = audioEntries[0];
            Log.Information("SceneProvider: extracting audio '{Track}' from {Pkg}",
                audioEntry.FullPath, pkgPath);

            var cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MorpheX", "scene-audio");
            Directory.CreateDirectory(cacheDir);

            // Deterministic cache file name
            var cleanTrackName = string.Join("_", audioEntry.FullPath.Split(Path.GetInvalidFileNameChars()));
            var cacheKey = $"{Path.GetFileNameWithoutExtension(pkgPath)}_{cleanTrackName}";
            _tempAudioPath = Path.Combine(cacheDir, cacheKey);

            if (!File.Exists(_tempAudioPath))
            {
                var data = PkgReader.ExtractEntry(pkgPath, audioEntry.FullPath);
                if (data == null)
                {
                    Log.Warning("SceneProvider: audio extraction returned null for '{Track}'", audioEntry.FullPath);
                    return;
                }
                File.WriteAllBytes(_tempAudioPath, data);
                Log.Information("SceneProvider: cached audio at {Path}", _tempAudioPath);
            }

            lock (_audioLock)
            {
                if (_disposed) return;

                _libVLC = VideoWallpaperProvider.GetSharedLibVLC();
                _audioMedia = new Media(_libVLC, _tempAudioPath, FromType.FromPath);
                _audioMedia.AddOption(":no-video");
                _audioMedia.AddOption(":input-repeat=65535"); // loop seamlessly
                _audioMedia.AddOption(":file-caching=300");

                _audioPlayer = new MediaPlayer(_audioMedia)
                {
                    Volume = (int)(_volume * 100f),
                    Mute = _isMuted,
                    EnableMouseInput = false,
                    EnableKeyInput = false
                };

                _audioPlayer.Play();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SceneProvider: audio setup failed (non-fatal)");
        }
    }

    // -------------------------------------------------------------------------
    // Path resolution helpers
    // -------------------------------------------------------------------------

    public static string? ResolvePreviewPath(WallpaperInfo wallpaper)
    {
        var dir = Directory.Exists(wallpaper.SourcePath)
            ? wallpaper.SourcePath
            : Path.GetDirectoryName(wallpaper.SourcePath);

        if (dir == null || !Directory.Exists(dir))
        {
            if (File.Exists(wallpaper.SourcePath))
                return wallpaper.SourcePath;
            return null;
        }

        // 1. Try project.json preview field
        var projectJson = Path.Combine(dir, "project.json");
        if (File.Exists(projectJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(projectJson));
                if (doc.RootElement.TryGetProperty("preview", out var prevProp))
                {
                    var customPreview = prevProp.GetString();
                    if (!string.IsNullOrEmpty(customPreview))
                    {
                        var candidate = Path.Combine(dir, customPreview);
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            catch { /* fallback to standard scan */ }
        }

        // 2. Standard filenames in priority order
        string[] standardNames = { "preview.gif", "preview.png", "preview.jpg", "preview.jpeg" };
        foreach (var name in standardNames)
        {
            var p = Path.Combine(dir, name);
            if (File.Exists(p)) return p;
        }

        // 3. Any .gif in the folder
        var gif = Directory.EnumerateFiles(dir, "*.gif", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (gif != null) return gif;

        // 4. Any image in the folder
        var img = Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                 f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                 f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                 f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase));
        if (img != null) return img;

        // 5. Check ThumbnailPath if available
        if (!string.IsNullOrEmpty(wallpaper.ThumbnailPath) && File.Exists(wallpaper.ThumbnailPath))
            return wallpaper.ThumbnailPath;

        return null;
    }

    public static string? FindPkgFile(WallpaperInfo wallpaper)
    {
        var dir = Directory.Exists(wallpaper.SourcePath)
            ? wallpaper.SourcePath
            : Path.GetDirectoryName(wallpaper.SourcePath);

        if (dir == null || !Directory.Exists(dir)) return null;

        return Directory.EnumerateFiles(dir, "*.pkg", SearchOption.TopDirectoryOnly)
                        .FirstOrDefault();
    }

    // -------------------------------------------------------------------------
    // Dispose
    // -------------------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _visualProvider?.Dispose();
        _visualProvider = null;

        lock (_audioLock)
        {
            if (_audioPlayer != null)
            {
                try
                {
                    _audioPlayer.Stop();
                    _audioPlayer.Dispose();
                }
                catch { /* best effort */ }
                _audioPlayer = null;
            }

            _audioMedia?.Dispose();
            _audioMedia = null;
        }

        State = WallpaperState.Unloaded;
    }
}
