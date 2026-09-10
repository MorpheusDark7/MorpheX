namespace MorpheX.Core.Configuration;

/// <summary>
/// A named, local grouping of wallpapers. The library owns the referenced IDs so
/// collections automatically stay consistent when a wallpaper is removed.
/// </summary>
public sealed class WallpaperCollection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public List<string> WallpaperIds { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
