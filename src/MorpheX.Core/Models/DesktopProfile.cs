using System.Text.Json;
using MorpheX.Core.Configuration;

namespace MorpheX.Core.Models;

public sealed class DesktopProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Profile";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Saved snapshot of WidgetSettings for this profile.
    /// </summary>
    public WidgetSettings Widgets { get; set; } = new();

    /// <summary>
    /// Legacy taskbar effect (Obsolete: feature removed)
    /// </summary>
    [Obsolete("Taskbar styling feature removed.")]
    public int TaskbarStyle { get; set; } = 0;

    /// <summary>
    /// Whether activating this profile should also attempt to restore saved wallpapers.
    /// </summary>
    public bool IncludeWallpaper { get; set; } = true;

    /// <summary>
    /// Placement mode snapshot for this profile ("PerDisplay" or "Span").
    /// </summary>
    public string PlacementMode { get; set; } = "PerDisplay";

    /// <summary>
    /// Monitor assignment snapshots: MonitorId -> WallpaperId or FilePath
    /// </summary>
    public Dictionary<string, string> MonitorWallpapers { get; set; } = new();

    /// <summary>
    /// Creates a deep-cloned copy of WidgetSettings to decouple live edits from profile snapshots.
    /// </summary>
    public static WidgetSettings CloneWidgetSettings(WidgetSettings source)
    {
        if (source == null) return new WidgetSettings();
        try
        {
            var json = JsonSerializer.Serialize(source);
            return JsonSerializer.Deserialize<WidgetSettings>(json) ?? new WidgetSettings();
        }
        catch
        {
            return new WidgetSettings();
        }
    }
}
