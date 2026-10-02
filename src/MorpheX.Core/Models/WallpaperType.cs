namespace MorpheX.Core.Models;

public enum WallpaperType
{
    Image,

    Video,

    AnimatedImage,

    Web,

    /// <summary>
    /// Wallpaper Engine scene wallpaper (.pkg + project.json folder).
    /// Displayed via its preview.gif; audio extracted from the .pkg on demand.
    /// </summary>
    Scene
}
