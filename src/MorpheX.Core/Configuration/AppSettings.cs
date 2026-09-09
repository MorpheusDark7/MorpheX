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

public enum PlaylistSource
{
    FavoritesOnly = 0,
    AllLibrary = 1
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
    public PlaylistOrder Order { get; set; } = PlaylistOrder.Shuffle;
    public bool ChangeOnStartup { get; set; } = false;
    public bool SkipWhenPaused { get; set; } = true;
}

public sealed class HotkeySettings
{
    public HotkeyBinding PauseResume { get; set; } = new();
    public HotkeyBinding MuteUnmute { get; set; } = new();
    public HotkeyBinding NextWallpaper { get; set; } = new();
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
