using System.Text.Json.Serialization;

namespace MorpheX.Core.Configuration;

public sealed class LibraryManifest
{
    public int Version { get; set; } = 2;

    public List<Models.WallpaperInfo> Wallpapers { get; set; } = new();

    public List<WallpaperCollection> Collections { get; set; } = new();
}
