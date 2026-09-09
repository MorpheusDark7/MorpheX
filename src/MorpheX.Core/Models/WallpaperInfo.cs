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
    public string? PreviewImagePath =>
        !string.IsNullOrEmpty(ThumbnailPath) && File.Exists(ThumbnailPath) ? ThumbnailPath :
        Type == WallpaperType.Image ? EffectivePath : null;

    [JsonIgnore]
    public bool HasPreviewImage => !string.IsNullOrEmpty(PreviewImagePath) && File.Exists(PreviewImagePath);
}
