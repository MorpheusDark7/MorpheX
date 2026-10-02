using System.Text.Json.Serialization;

namespace MorpheX.Core.Models;

public sealed class WallpaperInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public WallpaperType Type { get; set; }

    public string SourcePath { get; set; } = string.Empty;

    public string? ImportedPath { get; set; }

    public string? ThumbnailPath { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public double? Duration { get; set; }

    public long FileSize { get; set; }

    public bool IsFavorite { get; set; }

    /// <summary>
    /// User-defined labels used for searching and organizing the local library.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    public DateTimeOffset DateAdded { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public string EffectivePath => !string.IsNullOrEmpty(ImportedPath) ? ImportedPath : SourcePath;

    [JsonIgnore]
    public string? ResolutionText =>
        Width.HasValue && Height.HasValue ? $"{Width}×{Height}" : null;

    [JsonIgnore]
    public bool IsActive { get; set; }

    [JsonIgnore]
    public string FavoriteBrush => IsFavorite ? "#FFFF2D55" : "#88FFFFFF";

    [JsonIgnore]
    public string TagsText => Tags is { Count: > 0 } ? string.Join(" • ", Tags) : string.Empty;

    [JsonIgnore]
    public string? PreviewImagePath
    {
        get
        {
            if (!string.IsNullOrEmpty(ThumbnailPath) && File.Exists(ThumbnailPath))
                return ThumbnailPath;
            if (Type is WallpaperType.Image or WallpaperType.AnimatedImage && File.Exists(EffectivePath))
                return EffectivePath;
            if (Type == WallpaperType.Scene && Directory.Exists(SourcePath))
            {
                var gif = Path.Combine(SourcePath, "preview.gif");
                if (File.Exists(gif)) return gif;
                var png = Path.Combine(SourcePath, "preview.png");
                if (File.Exists(png)) return png;
                var jpg = Path.Combine(SourcePath, "preview.jpg");
                if (File.Exists(jpg)) return jpg;
            }
            return null;
        }
    }

    [JsonIgnore]
    public bool HasPreviewImage => !string.IsNullOrEmpty(PreviewImagePath) && File.Exists(PreviewImagePath);
}
