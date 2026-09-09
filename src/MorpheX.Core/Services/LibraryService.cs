using System.Text.Json;
using System.Text.Json.Serialization;
using MorpheX.Core.Configuration;
using MorpheX.Core.Models;
using Serilog;

namespace MorpheX.Core.Services;

public interface ILibraryService
{
    IReadOnlyList<WallpaperInfo> Wallpapers { get; }

    Task LoadAsync(CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);

    Task<WallpaperInfo?> AddWallpaperAsync(string filePath, bool copyToLibrary = false,
                                            CancellationToken ct = default);
    Task RemoveWallpaperAsync(string wallpaperId, bool deleteFile = false,
                               CancellationToken ct = default);
    WallpaperInfo? GetById(string wallpaperId);
    void SetFavorite(string wallpaperId, bool isFavorite);

    event EventHandler? LibraryChanged;
}

public sealed class LibraryService : ILibraryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly Dictionary<string, WallpaperType> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = WallpaperType.Image,
        [".jpg"] = WallpaperType.Image,
        [".jpeg"] = WallpaperType.Image,
        [".bmp"] = WallpaperType.Image,
        [".webp"] = WallpaperType.Image,
        [".tiff"] = WallpaperType.Image,
        [".tif"] = WallpaperType.Image,

        [".mp4"] = WallpaperType.Video,
        [".webm"] = WallpaperType.Video,
        [".mkv"] = WallpaperType.Video,
        [".mov"] = WallpaperType.Video,
        [".avi"] = WallpaperType.Video,

        [".gif"] = WallpaperType.AnimatedImage,

        [".html"] = WallpaperType.Web,
        [".htm"] = WallpaperType.Web,
    };

    public static bool IsSupportedExtension(string extension) =>
        !string.IsNullOrEmpty(extension) && ExtensionMap.ContainsKey(extension);

    private readonly string _libraryDir;
    private readonly string _manifestPath;
    private readonly string _wallpapersDir;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private LibraryManifest _manifest = new();

    public IReadOnlyList<WallpaperInfo> Wallpapers => _manifest.Wallpapers;

    public event EventHandler? LibraryChanged;

    public LibraryService(string? libraryDirectory = null)
    {
        _libraryDir = libraryDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MorpheX");
        _manifestPath = Path.Combine(_libraryDir, "library.json");
        _wallpapersDir = Path.Combine(_libraryDir, "wallpapers");

        Directory.CreateDirectory(_libraryDir);
        Directory.CreateDirectory(_wallpapersDir);
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (File.Exists(_manifestPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_manifestPath, ct);
                    var loaded = JsonSerializer.Deserialize<LibraryManifest>(json, JsonOptions);
                    if (loaded != null)
                    {
                        _manifest = loaded;
                        Log.Information("Loaded library with {Count} wallpaper(s)", _manifest.Wallpapers.Count);

                        var thumbDir = Path.Combine(_libraryDir, "thumbnails");
                        bool manifestUpdated = false;
                        foreach (var w in _manifest.Wallpapers)
                        {
                            if (string.IsNullOrEmpty(w.ThumbnailPath) || !File.Exists(w.ThumbnailPath))
                            {
                                var generated = Utilities.ShellThumbnailHelper.GenerateThumbnail(w.EffectivePath, thumbDir, w.Id);
                                if (generated != null)
                                {
                                    w.ThumbnailPath = generated;
                                    manifestUpdated = true;
                                }
                            }
                        }
                        if (manifestUpdated)
                        {
                            await SaveInternalAsync(ct);
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to parse library manifest, starting fresh");
                }
            }

            _manifest = new LibraryManifest();
            await SaveInternalAsync(ct);
            Log.Information("Created new library manifest");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await SaveInternalAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<WallpaperInfo?> AddWallpaperAsync(string filePath, bool copyToLibrary = false,
                                                         CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            Log.Error("Cannot add wallpaper: file not found: {Path}", filePath);
            return null;
        }

        var extension = Path.GetExtension(filePath);
        if (!ExtensionMap.TryGetValue(extension, out var type))
        {
            Log.Warning("Unsupported file format: {Extension}", extension);
            return null;
        }

        await _lock.WaitAsync(ct);
        try
        {
            var fileInfo = new FileInfo(filePath);
            var wallpaper = new WallpaperInfo
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                Type = type,
                SourcePath = Path.GetFullPath(filePath),
                FileSize = fileInfo.Length,
                DateAdded = DateTimeOffset.UtcNow
            };

            if (copyToLibrary)
            {
                var destPath = Path.Combine(_wallpapersDir, $"{wallpaper.Id}{extension}");
                await Task.Run(() => File.Copy(filePath, destPath, overwrite: false), ct);
                wallpaper.ImportedPath = destPath;
                Log.Information("Imported wallpaper to library: {Dest}", destPath);
            }

            var thumbDir = Path.Combine(_libraryDir, "thumbnails");
            var thumb = Utilities.ShellThumbnailHelper.GenerateThumbnail(filePath, thumbDir, wallpaper.Id);
            if (thumb != null)
            {
                wallpaper.ThumbnailPath = thumb;
            }

            if (type == WallpaperType.Image)
            {
                try
                {
                    using var img = System.Drawing.Image.FromFile(filePath);
                    wallpaper.Width = img.Width;
                    wallpaper.Height = img.Height;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Could not extract image metadata for {Path}", filePath);
                }
            }

            _manifest.Wallpapers.Add(wallpaper);
            await SaveInternalAsync(ct);

            Log.Information("Added wallpaper '{Name}' ({Type}) to library", wallpaper.Name, wallpaper.Type);
            LibraryChanged?.Invoke(this, EventArgs.Empty);

            return wallpaper;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveWallpaperAsync(string wallpaperId, bool deleteFile = false,
                                            CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var wallpaper = _manifest.Wallpapers.FirstOrDefault(w => w.Id == wallpaperId);
            if (wallpaper == null) return;

            _manifest.Wallpapers.Remove(wallpaper);

            if (deleteFile && !string.IsNullOrEmpty(wallpaper.ImportedPath) &&
                File.Exists(wallpaper.ImportedPath))
            {
                try
                {
                    File.Delete(wallpaper.ImportedPath);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to delete imported file: {Path}", wallpaper.ImportedPath);
                }
            }

            await SaveInternalAsync(ct);
            Log.Information("Removed wallpaper '{Name}' from library", wallpaper.Name);
            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _lock.Release();
        }
    }

    public WallpaperInfo? GetById(string wallpaperId)
    {
        return _manifest.Wallpapers.FirstOrDefault(w => w.Id == wallpaperId);
    }

    public void SetFavorite(string wallpaperId, bool isFavorite)
    {
        var wallpaper = _manifest.Wallpapers.FirstOrDefault(w => w.Id == wallpaperId);
        if (wallpaper != null)
        {
            wallpaper.IsFavorite = isFavorite;
            _ = SaveAsync();
            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public static string GetFileFilter()
    {
        var allExts = string.Join(";", ExtensionMap.Keys.Select(e => $"*{e}"));
        return $"All Supported|{allExts}|" +
               "Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.tiff;*.tif|" +
               "Videos|*.mp4;*.webm;*.mkv;*.mov;*.avi|" +
               "Animated|*.gif|" +
               "Web|*.html;*.htm";
    }

    private async Task SaveInternalAsync(CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(_manifest, JsonOptions);
        var tempPath = _manifestPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, ct);
        File.Move(tempPath, _manifestPath, overwrite: true);
    }
}
