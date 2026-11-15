using System.Text.Json;
using System.Text.Json.Serialization;
using MorpheX.Core.Configuration;

namespace MorpheX.Core.Models;

public sealed class DesktopProfile
{
    public const int CurrentSchemaVersion = 2;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Profile";
    /// <summary>Profile data schema, migrated automatically when an older profile is loaded.</summary>
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Saved snapshot of WidgetSettings for this profile.
    /// </summary>
    public WidgetSettings Widgets { get; set; } = new();

    /// <summary>
    /// Legacy taskbar effect (Obsolete: feature removed)
    /// </summary>
    [JsonIgnore]
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
    /// Full wallpaper and playback configuration. Null only for profiles created by older versions;
    /// ProfileService migrates those profiles without deleting their saved wallpaper IDs.
    /// </summary>
    public ProfileWallpaperSettings? WallpaperSettings { get; set; }

    /// <summary>
    /// Creates a deep-cloned copy of WidgetSettings to decouple live edits from profile snapshots.
    /// </summary>
    public static WidgetSettings CloneWidgetSettings(WidgetSettings source)
    {
        if (source == null) return new WidgetSettings();
        return CloneSerialized(source) ?? new WidgetSettings();
    }

    public static ProfileWallpaperSettings CaptureWallpaperSettings(AppSettings source) => new()
    {
        Display = CloneSerialized(source.Display) ?? new(),
        Playback = CloneSerialized(source.Playback) ?? new(),
        Audio = CloneSerialized(source.Audio) ?? new(),
        Performance = CloneSerialized(source.Performance) ?? new(),
        Effects = CloneSerialized(source.Effects) ?? new(),
        AmbientDim = CloneSerialized(source.AmbientDim) ?? new(),
        Playlist = CloneSerialized(source.Playlist) ?? new(),
        MonitorAssignments = CloneSerialized(source.MonitorAssignments) ?? new()
    };

    public static ProfileWallpaperSettings CloneWallpaperSettings(ProfileWallpaperSettings? source) =>
        source == null ? new ProfileWallpaperSettings() : CloneSerialized(source) ?? new ProfileWallpaperSettings();

    public static void SyncLegacyWallpaperFields(DesktopProfile profile)
    {
        if (profile.WallpaperSettings == null) return;

        profile.PlacementMode = profile.WallpaperSettings.Display.PlacementMode;
        bool isSpan = string.Equals(profile.PlacementMode, "Span", StringComparison.OrdinalIgnoreCase);
        profile.MonitorWallpapers = profile.WallpaperSettings.MonitorAssignments
            .Where(entry => (entry.Key == "SPAN") == isSpan && !string.IsNullOrWhiteSpace(entry.Value.WallpaperId))
            .ToDictionary(entry => entry.Key, entry => entry.Value.WallpaperId!);
    }

    private static T? CloneSerialized<T>(T source)
    {
        try
        {
            var json = JsonSerializer.Serialize(source);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }
}

/// <summary>Settings that define how this profile's wallpapers look and play.</summary>
public sealed class ProfileWallpaperSettings
{
    public DisplaySettings Display { get; set; } = new();
    public PlaybackSettings Playback { get; set; } = new();
    public AudioSettings Audio { get; set; } = new();
    public PerformanceSettings Performance { get; set; } = new();
    public WallpaperEffectsSettings Effects { get; set; } = new();
    public AmbientDimSettings AmbientDim { get; set; } = new();
    public PlaylistSettings Playlist { get; set; } = new();
    public Dictionary<string, MonitorAssignment> MonitorAssignments { get; set; } = new();
}
