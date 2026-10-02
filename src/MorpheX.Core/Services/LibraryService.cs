using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using MorpheX.Core.Configuration;
using MorpheX.Core.Models;
using Serilog;

namespace MorpheX.Core.Services;

public interface ILibraryService
{
    IReadOnlyList<WallpaperInfo> Wallpapers { get; }
    IReadOnlyList<WallpaperCollection> Collections { get; }

    Task LoadAsync(CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);

    Task<WallpaperInfo?> AddWallpaperAsync(string filePath, bool copyToLibrary = false,
                                            CancellationToken ct = default);
    Task RemoveWallpaperAsync(string wallpaperId, bool deleteFile = false,
                               CancellationToken ct = default);
    WallpaperInfo? GetById(string wallpaperId);
    void SetFavorite(string wallpaperId, bool isFavorite);
    Task SetTagsAsync(string wallpaperId, IEnumerable<string> tags, CancellationToken ct = default);
    Task RenameWallpaperAsync(string wallpaperId, string newName, CancellationToken ct = default);

    WallpaperCollection? GetCollectionById(string collectionId);
    Task<WallpaperCollection?> CreateCollectionAsync(string name, CancellationToken ct = default);
    Task RenameCollectionAsync(string collectionId, string name, CancellationToken ct = default);
    Task DeleteCollectionAsync(string collectionId, CancellationToken ct = default);
    Task AddWallpaperToCollectionAsync(string collectionId, string wallpaperId, CancellationToken ct = default);
    Task RemoveWallpaperFromCollectionAsync(string collectionId, string wallpaperId, CancellationToken ct = default);

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

        // Wallpaper Engine Scene packages
        [".pkg"] = WallpaperType.Scene,
        [".zip"] = WallpaperType.Scene,  // WE workshop zip exports
    };

    public static bool IsSupportedExtension(string extension) =>
        !string.IsNullOrEmpty(extension) && ExtensionMap.ContainsKey(extension);

    // -------------------------------------------------------------------------
    // Scene (Wallpaper Engine .pkg) detection helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true when the path is a folder that looks like a WE wallpaper
    /// (contains project.json with "type":"Scene" and a preview.gif or scene.pkg).
    /// </summary>
    public static bool IsSceneFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return false;
        var projectJson = Path.Combine(folderPath, "project.json");
        return File.Exists(projectJson) &&
               (File.Exists(Path.Combine(folderPath, "preview.gif")) ||
                File.Exists(Path.Combine(folderPath, "preview.png")) ||
                File.Exists(Path.Combine(folderPath, "preview.jpg")) ||
                File.Exists(Path.Combine(folderPath, "preview.jpeg")) ||
                Directory.EnumerateFiles(folderPath, "*.pkg").Any());
    }

    /// <summary>
    /// Searches for a directory containing project.json (up to 4 levels deep).
    /// Handles unzipped archives with wrapper folders.
    /// </summary>
    public static string? FindSceneRoot(string rootDir)
    {
        if (!Directory.Exists(rootDir)) return null;
        if (IsSceneFolder(rootDir)) return rootDir;

        var queue = new Queue<string>();
        queue.Enqueue(rootDir);
        int maxDepth = 4;
        while (queue.Count > 0 && maxDepth-- > 0)
        {
            var dir = queue.Dequeue();
            if (IsSceneFolder(dir)) return dir;
            try
            {
                foreach (var sub in Directory.EnumerateDirectories(dir))
                {
                    if (IsSceneFolder(sub)) return sub;
                    queue.Enqueue(sub);
                }
            }
            catch { /* ignore inaccessible */ }
        }
        return null;
    }

    /// <summary>
    /// Returns true when the path is a .zip whose root or subfolder contains project.json.
    /// </summary>
    public static bool IsSceneZip(string zipPath)
    {
        if (!File.Exists(zipPath)) return false;
        if (!zipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            return archive.Entries.Any(e =>
                e.FullName.EndsWith("project.json", StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    private readonly string _libraryDir;
    private readonly string _manifestPath;
    private readonly string _wallpapersDir;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private LibraryManifest _manifest = new();

    public IReadOnlyList<WallpaperInfo> Wallpapers => _manifest.Wallpapers;
    public IReadOnlyList<WallpaperCollection> Collections => _manifest.Collections;

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
                        _manifest.Collections ??= new List<WallpaperCollection>();
                        foreach (var wallpaper in _manifest.Wallpapers)
                        {
                            wallpaper.Tags ??= new List<string>();
                        }
                        foreach (var collection in _manifest.Collections)
                        {
                            collection.WallpaperIds ??= new List<string>();
                        }
                        Log.Information("Loaded library with {Count} wallpaper(s)", _manifest.Wallpapers.Count);

                        // Generate any missing thumbnails in the background so startup
                        // is not blocked by slow shell probing (especially for videos).
                        var thumbDir = Path.Combine(_libraryDir, "thumbnails");
                        var wallpapersMissingThumbs = _manifest.Wallpapers
                            .Where(w => string.IsNullOrEmpty(w.ThumbnailPath) || !File.Exists(w.ThumbnailPath))
                            .ToList();

                        if (wallpapersMissingThumbs.Count > 0)
                        {
                            _ = Task.Run(async () =>
                            {
                                bool updated = false;
                                foreach (var w in wallpapersMissingThumbs)
                                {
                                    var generated = Utilities.ShellThumbnailHelper.GenerateThumbnail(
                                        w.EffectivePath, thumbDir, w.Id);
                                    if (generated != null)
                                    {
                                        w.ThumbnailPath = generated;
                                        updated = true;
                                    }
                                }
                                if (updated)
                                {
                                    await SaveAsync(ct);
                                    LibraryChanged?.Invoke(this, EventArgs.Empty);
                                }
                            }, ct);
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
        // 1. Handle directory input (scene folder directly or unzipped parent folder)
        if (Directory.Exists(filePath))
        {
            var sceneDir = FindSceneRoot(filePath);
            if (sceneDir != null)
                return await AddSceneFromFolderAsync(sceneDir, ct);

            Log.Warning("Folder does not contain a supported Wallpaper Engine scene: {Path}", filePath);
            return null;
        }

        // 2. Handle WE workshop .zip exports
        if (filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            if (IsSceneZip(filePath))
                return await AddSceneFromZipAsync(filePath, ct);

            Log.Warning("Zip archive is not a Wallpaper Engine package (no project.json found): {Path}", filePath);
            return null;
        }

        if (!File.Exists(filePath))
        {
            Log.Error("Cannot add wallpaper: file not found: {Path}", filePath);
            return null;
        }

        var extension = Path.GetExtension(filePath);

        // 3. If user picked project.json or scene.pkg or preview image from a scene folder
        var parentDir = Path.GetDirectoryName(filePath);
        if (parentDir != null && IsSceneFolder(parentDir))
        {
            if (string.Equals(Path.GetFileName(filePath), "project.json", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".pkg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(filePath), "preview.gif", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(filePath), "preview.png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(filePath), "preview.jpg", StringComparison.OrdinalIgnoreCase))
            {
                return await AddSceneFromFolderAsync(parentDir, ct);
            }
        }

        // 4. Standalone .pkg without a scene folder cannot be played
        if (extension.Equals(".pkg", StringComparison.OrdinalIgnoreCase))
        {
            Log.Warning("Standalone .pkg without project.json is not supported: {Path}", filePath);
            return null;
        }

        if (!ExtensionMap.TryGetValue(extension, out var type))
        {
            Log.Warning("Unsupported file format: {Extension}", extension);
            return null;
        }

        await _lock.WaitAsync(ct);
        try
        {
            var normalizedPath = Path.GetFullPath(filePath);
            var existing = _manifest.Wallpapers.FirstOrDefault(w =>
                PathsMatch(w.SourcePath, normalizedPath) || PathsMatch(w.ImportedPath, normalizedPath));
            if (existing != null)
            {
                Log.Information("Wallpaper already exists in library: {Path}", normalizedPath);
                return existing;
            }

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

            foreach (var collection in _manifest.Collections)
            {
                collection.WallpaperIds.RemoveAll(id => id == wallpaperId);
            }

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

    public async Task RenameWallpaperAsync(string wallpaperId, string newName, CancellationToken ct = default)
    {
        newName = newName.Trim();
        if (string.IsNullOrWhiteSpace(newName)) return;

        await _lock.WaitAsync(ct);
        try
        {
            var wallpaper = _manifest.Wallpapers.FirstOrDefault(w => w.Id == wallpaperId);
            if (wallpaper == null) return;

            wallpaper.Name = newName;
            await SaveInternalAsync(ct);
            LibraryChanged?.Invoke(this, EventArgs.Empty);
            Log.Information("Renamed wallpaper {Id} → '{Name}'", wallpaperId, newName);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetTagsAsync(string wallpaperId, IEnumerable<string> tags, CancellationToken ct = default)
    {
        var sanitizedTags = tags
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        await _lock.WaitAsync(ct);
        try
        {
            var wallpaper = _manifest.Wallpapers.FirstOrDefault(w => w.Id == wallpaperId);
            if (wallpaper == null) return;

            wallpaper.Tags = sanitizedTags;
            await SaveInternalAsync(ct);
            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _lock.Release();
        }
    }

    public WallpaperCollection? GetCollectionById(string collectionId) =>
        _manifest.Collections.FirstOrDefault(collection => collection.Id == collectionId);

    public async Task<WallpaperCollection?> CreateCollectionAsync(string name, CancellationToken ct = default)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return null;

        await _lock.WaitAsync(ct);
        try
        {
            var existing = _manifest.Collections.FirstOrDefault(collection =>
                string.Equals(collection.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;

            var collection = new WallpaperCollection { Name = name };
            _manifest.Collections.Add(collection);
            await SaveInternalAsync(ct);
            LibraryChanged?.Invoke(this, EventArgs.Empty);
            Log.Information("Created wallpaper collection '{Name}'", name);
            return collection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RenameCollectionAsync(string collectionId, string name, CancellationToken ct = default)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        await _lock.WaitAsync(ct);
        try
        {
            var collection = _manifest.Collections.FirstOrDefault(item => item.Id == collectionId);
            if (collection == null) return;

            bool nameTaken = _manifest.Collections.Any(item => item.Id != collectionId &&
                string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (nameTaken) return;

            collection.Name = name;
            await SaveInternalAsync(ct);
            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteCollectionAsync(string collectionId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var collection = _manifest.Collections.FirstOrDefault(item => item.Id == collectionId);
            if (collection == null) return;

            _manifest.Collections.Remove(collection);
            await SaveInternalAsync(ct);
            LibraryChanged?.Invoke(this, EventArgs.Empty);
            Log.Information("Deleted wallpaper collection '{Name}'", collection.Name);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddWallpaperToCollectionAsync(string collectionId, string wallpaperId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var collection = _manifest.Collections.FirstOrDefault(item => item.Id == collectionId);
            if (collection == null || !_manifest.Wallpapers.Any(wallpaper => wallpaper.Id == wallpaperId)) return;
            if (collection.WallpaperIds.Contains(wallpaperId)) return;

            collection.WallpaperIds.Add(wallpaperId);
            await SaveInternalAsync(ct);
            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveWallpaperFromCollectionAsync(string collectionId, string wallpaperId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var collection = _manifest.Collections.FirstOrDefault(item => item.Id == collectionId);
            if (collection == null || !collection.WallpaperIds.Remove(wallpaperId)) return;

            await SaveInternalAsync(ct);
            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _lock.Release();
        }
    }

    public static string GetFileFilter()
    {
        return "All Supported|*.mp4;*.webm;*.mkv;*.mov;*.avi;*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.tiff;*.tif;*.gif;*.html;*.htm;*.pkg;*.zip;project.json|" +
               "Wallpaper Engine (*.pkg;*.zip;project.json)|*.pkg;*.zip;project.json|" +
               "Videos|*.mp4;*.webm;*.mkv;*.mov;*.avi|" +
               "Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.tiff;*.tif|" +
               "Animated GIFs|*.gif|" +
               "Web|*.html;*.htm";
    }

    // -------------------------------------------------------------------------
    // Scene import helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds a WE wallpaper scene from an already-unzipped folder
    /// (the folder that directly contains project.json).
    /// </summary>
    public async Task<WallpaperInfo?> AddSceneFromFolderAsync(string folderPath,
                                                               CancellationToken ct = default)
    {
        var projectJsonPath = Path.Combine(folderPath, "project.json");

        if (!File.Exists(projectJsonPath))
        {
            Log.Warning("Scene import: no project.json in {Folder}", folderPath);
            return null;
        }

        // Parse project.json for metadata
        string wallpaperName = Path.GetFileName(folderPath);
        string? customPreview = null;
        string? projectType = null;
        string? targetFile = null;
        try
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(projectJsonPath, ct));
            if (doc.RootElement.TryGetProperty("title", out var titleEl))
                wallpaperName = titleEl.GetString() ?? wallpaperName;
            if (doc.RootElement.TryGetProperty("preview", out var prevEl))
                customPreview = prevEl.GetString();
            if (doc.RootElement.TryGetProperty("type", out var typeEl))
                projectType = typeEl.GetString();
            if (doc.RootElement.TryGetProperty("file", out var fileEl))
                targetFile = fileEl.GetString();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Scene import: could not parse project.json");
        }

        // Check if this WE item is actually a Video wallpaper
        if (string.Equals(projectType, "Video", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(targetFile))
        {
            var videoPath = Path.Combine(folderPath, targetFile);
            if (File.Exists(videoPath))
            {
                var videoWallpaper = await AddWallpaperAsync(videoPath, copyToLibrary: false, ct);
                if (videoWallpaper != null)
                {
                    videoWallpaper.Name = wallpaperName;
                    await SaveInternalAsync(ct);
                }
                return videoWallpaper;
            }
        }

        await _lock.WaitAsync(ct);
        try
        {
            var normalizedFolder = Path.GetFullPath(folderPath);

            // De-duplicate: check if already in library by folder path
            var existing = _manifest.Wallpapers.FirstOrDefault(w =>
                w.Type == WallpaperType.Scene &&
                PathsMatch(w.SourcePath, normalizedFolder));
            if (existing != null)
            {
                Log.Information("Scene wallpaper already in library: {Folder}", normalizedFolder);
                return existing;
            }

            // Resolve preview image or gif
            string? previewPath = null;
            if (!string.IsNullOrEmpty(customPreview))
            {
                var custom = Path.Combine(folderPath, customPreview);
                if (File.Exists(custom)) previewPath = custom;
            }

            if (previewPath == null)
            {
                string[] candidates = { "preview.gif", "preview.png", "preview.jpg", "preview.jpeg" };
                foreach (var c in candidates)
                {
                    var p = Path.Combine(folderPath, c);
                    if (File.Exists(p)) { previewPath = p; break; }
                }
            }

            long totalSize = 0;
            try
            {
                var dirInfo = new DirectoryInfo(folderPath);
                totalSize = dirInfo.EnumerateFiles("*", SearchOption.TopDirectoryOnly).Sum(f => f.Length);
            }
            catch { /* best effort */ }

            var wallpaper = new WallpaperInfo
            {
                Name       = wallpaperName,
                Type       = WallpaperType.Scene,
                SourcePath = normalizedFolder,
                FileSize   = totalSize > 0 ? totalSize : (previewPath != null && File.Exists(previewPath) ? new FileInfo(previewPath).Length : 0),
                DateAdded  = DateTimeOffset.UtcNow
            };

            if (previewPath != null && File.Exists(previewPath))
                wallpaper.ThumbnailPath = previewPath;

            _manifest.Wallpapers.Add(wallpaper);
            await SaveInternalAsync(ct);

            Log.Information("Added Scene wallpaper '{Name}' from {Folder}", wallpaper.Name, normalizedFolder);
            LibraryChanged?.Invoke(this, EventArgs.Empty);
            return wallpaper;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Adds a WE wallpaper from a .zip archive by extracting it to a directory under AppData\MorpheX\scenes\.
    /// </summary>
    public async Task<WallpaperInfo?> AddSceneFromZipAsync(string zipPath,
                                                            CancellationToken ct = default)
    {
        var scenesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MorpheX", "scenes");
        Directory.CreateDirectory(scenesDir);

        var extractName = Path.GetFileNameWithoutExtension(zipPath);
        var extractRoot = Path.Combine(scenesDir, extractName);

        Log.Information("Scene import: extracting zip {Zip} -> {Dest}", zipPath, extractRoot);
        try
        {
            await Task.Run(() =>
            {
                if (Directory.Exists(extractRoot))
                    Directory.Delete(extractRoot, recursive: true);
                ZipFile.ExtractToDirectory(zipPath, extractRoot, overwriteFiles: true);
            }, ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Scene import: failed to extract zip {Zip}", zipPath);
            return null;
        }

        var sceneFolder = FindSceneRoot(extractRoot);
        if (sceneFolder == null)
        {
            Log.Warning("Scene import: no project.json found after extracting {Zip}", zipPath);
            return null;
        }

        return await AddSceneFromFolderAsync(sceneFolder, ct);
    }

    private async Task SaveInternalAsync(CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(_manifest, JsonOptions);
        var tempPath = _manifestPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, ct);
        File.Move(tempPath, _manifestPath, overwrite: true);
    }

    private static bool PathsMatch(string? storedPath, string candidatePath)
    {
        if (string.IsNullOrWhiteSpace(storedPath)) return false;

        try
        {
            return string.Equals(Path.GetFullPath(storedPath), candidatePath,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
