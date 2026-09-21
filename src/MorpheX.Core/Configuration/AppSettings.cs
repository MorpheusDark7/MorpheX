using MorpheX.Core.Models;

namespace MorpheX.Core.Configuration;

public sealed class AppSettings
{
    public GeneralSettings General { get; set; } = new();
    public PlaybackSettings Playback { get; set; } = new();
    public AudioSettings Audio { get; set; } = new();
    public PerformanceSettings Performance { get; set; } = new();
    public DisplaySettings Display { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public AdvancedSettings Advanced { get; set; } = new();
    public PlaylistSettings Playlist { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
    public WallpaperEffectsSettings Effects { get; set; } = new();
    public AmbientDimSettings AmbientDim { get; set; } = new();

    public Dictionary<string, MonitorAssignment> MonitorAssignments { get; set; } = new();
}

public sealed class GeneralSettings
{
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = true;
    public bool ShowTrayIcon { get; set; } = true;
    public bool CheckForUpdates { get; set; } = true;
}

public sealed class PlaybackSettings
{
    public int DefaultFps { get; set; } = 30;
    public bool HardwareAcceleration { get; set; } = true;
    public bool ResumeFromPreviousPosition { get; set; } = true;
    public bool UseNativeFrameRate { get; set; } = true;

    /// <summary>Playback speed multiplier for video wallpapers (0.25 – 2.0).</summary>
    public float PlaybackRate { get; set; } = 1.0f;

    /// <summary>Fade brightness from 0 to full over ~500 ms when a wallpaper first loads.</summary>
    public bool FadeInOnLoad { get; set; } = true;
}

public sealed class AudioSettings
{
    public bool Enabled { get; set; } = false;
    public int Volume { get; set; } = 50;
    public bool MuteOnOtherAudio { get; set; } = true;
}

public sealed class PerformanceSettings
{
    public PerformancePreset Preset { get; set; } = PerformancePreset.Balanced;
    public PauseRule PauseRule { get; set; } = PauseRule.OnFullscreen;
    public bool PauseOnBattery { get; set; } = true;
    public bool ReduceFpsWhenPartiallyCovered { get; set; } = true;
    public int ReducedFps { get; set; } = 10;
    public int FpsLimit { get; set; } = 0;
    public UnloadDelay UnloadPausedAfter { get; set; } = UnloadDelay.Never;
}

public sealed class DisplaySettings
{
    public ScalingMode DefaultScalingMode { get; set; } = ScalingMode.Fill;
}

public sealed class AppearanceSettings
{
    public string Theme { get; set; } = "Dark";
    public string AccentColor { get; set; } = "System";
    public bool EnableAnimations { get; set; } = true;
    public bool ShowSystemStats { get; set; } = true;
}

public sealed class AdvancedSettings
{
    public string? LibraryPath { get; set; }
    public string? CachePath { get; set; }
    public bool EnableLogging { get; set; } = true;
}

/// <summary>
/// Post-processing visual effects applied to every wallpaper type.
/// 1.0 = unchanged, 0.5 = half, 2.0 = double.
/// ColorTemperature: -50 = cool/blue, 0 = neutral, +50 = warm/orange.
/// </summary>
public sealed class WallpaperEffectsSettings
{
    public float Brightness { get; set; } = 1.0f;
    public float Contrast { get; set; } = 1.0f;
    public float Saturation { get; set; } = 1.0f;
    public int ColorTemperature { get; set; } = 0;

    public bool IsDefault =>
        Math.Abs(Brightness - 1.0f) < 0.01f &&
        Math.Abs(Contrast - 1.0f) < 0.01f &&
        Math.Abs(Saturation - 1.0f) < 0.01f &&
        ColorTemperature == 0;
}

/// <summary>Automatically dims the wallpaper when the system has been idle for a while.</summary>
public sealed class AmbientDimSettings
{
    public bool Enabled { get; set; } = false;
    public int IdleMinutes { get; set; } = 5;

    /// <summary>Target brightness multiplier when dimmed (0.1 – 0.9).</summary>
    public float DimLevel { get; set; } = 0.3f;
}

public enum PlaylistSource
{
    FavoritesOnly = 0,
    AllLibrary = 1,
    Collection = 2
}

public enum PlaylistOrder
{
    Shuffle = 0,
    Sequential = 1
}

public sealed class PlaylistSettings
{
    public bool Enabled { get; set; } = false;
    public int IntervalMinutes { get; set; } = 30;
    public PlaylistSource Source { get; set; } = PlaylistSource.FavoritesOnly;
    public string? CollectionId { get; set; }
    public PlaylistOrder Order { get; set; } = PlaylistOrder.Shuffle;
    public bool ChangeOnStartup { get; set; } = false;
    public bool SkipWhenPaused { get; set; } = true;
}

public sealed class HotkeySettings
{
    public HotkeyBinding PauseResume { get; set; } = new();
    public HotkeyBinding MuteUnmute { get; set; } = new();
    public HotkeyBinding NextWallpaper { get; set; } = new();
    public HotkeyBinding RandomWallpaper { get; set; } = new();
}

public sealed class HotkeyBinding
{
    public bool Enabled { get; set; } = false;
    public string Key { get; set; } = "None";
    public string Modifiers { get; set; } = "None";

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(Key) && Key != "None";

    public string DisplayText
    {
        get
        {
            if (!IsConfigured) return "Not Assigned";
            if (string.IsNullOrWhiteSpace(Modifiers) || Modifiers == "None") return Key;
            return $"{Modifiers} + {Key}";
        }
    }
}

public sealed class MonitorAssignment
{
    public string? WallpaperId { get; set; }
    public ScalingMode? ScalingMode { get; set; }
    public PauseRule? PauseRule { get; set; }
    public double? SavedPositionSeconds { get; set; }
    public bool? AudioMuted { get; set; }
    public int? Volume { get; set; }
}
