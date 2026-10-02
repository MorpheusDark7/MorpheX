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
    public WidgetSettings Widgets { get; set; } = new();
    public TaskbarSettings Taskbar { get; set; } = new();
}

/// <summary>
/// Controls the visual style applied to the Windows taskbar.
/// 0 = Default (no modification), 1 = Clear, 2 = Blur, 3 = Acrylic.
/// </summary>
public sealed class TaskbarSettings
{
    /// <summary>0 = Default, 1 = Clear, 2 = Blur, 3 = Acrylic</summary>
    public int Style { get; set; } = 0;
}


public sealed class WidgetSettings
{
    // ── Global widget appearance ───────────────────────────────────────
    /// <summary>0 = Dark (default frosted dark glass), 1 = Light (frosted white/cream glass)</summary>
    public int WidgetTheme { get; set; } = 0;

    // ── Clock ──────────────────────────────────────────────────────────
    public bool ClockEnabled { get; set; } = false;
    public bool ClockIs24Hour { get; set; } = true;
    public bool ClockShowDate { get; set; } = true;
    public bool ClockShowBackground { get; set; } = false;
    /// <summary>0=Small(40px) 1=Medium(64px) 2=Large(88px) 3=Huge(112px)</summary>
    public int ClockFontSize { get; set; } = 1;
    /// <summary>0=White 1=Dim Slate 2=Electric Cyan 3=Neon Mint 4=Cyber Violet 5=Sunset Orange 6=Amber Gold</summary>
    public int ClockColorMode { get; set; } = 0;
    /// <summary>0=Bahnschrift Light 1=Segoe UI Light 2=Consolas</summary>
    public int ClockFontMode { get; set; } = 0;

    // ── System Monitor (Simplicity Circles) ───────────────────────────
    public bool SystemMonitorEnabled { get; set; } = false;
    /// <summary>0=Small(44px) 1=Medium(56px) 2=Large(72px) 3=ExtraLarge(88px)</summary>
    public int SysMonSize { get; set; } = 1;
    /// <summary>0=White 1=Cyan Neon 2=Emerald Mint 3=Cyber Violet 4=Sunset Amber</summary>
    public int SysMonColorMode { get; set; } = 0;
    /// <summary>0=Horizontal Row 1=Vertical Column 2=Grid 2x2</summary>
    public int SysMonOrientation { get; set; } = 0;
    public bool SysMonShowBackground { get; set; } = false;
    public bool SysMonShowCpu { get; set; } = true;
    public bool SysMonShowRam { get; set; } = true;
    public bool SysMonShowGpu { get; set; } = true;
    public bool SysMonShowDisk { get; set; } = true;

    // ── Audio Visualizer & Media ───────────────────────────────────────
    public bool MediaEnabled { get; set; } = false;
    /// <summary>Visualizer style: 0=Bars 1=Mirrored 2=Waveform</summary>
    public int VisualizerStyle { get; set; } = 0;
    /// <summary>Visualizer color: 0=White 1=Neon Cyan 2=Violet Glow 3=Emerald Mint 4=Sunset Amber</summary>
    public int VisualizerColorMode { get; set; } = 0;
    public bool MediaShowBackground { get; set; } = false;
    public bool MediaShowTrackDetails { get; set; } = true;
    public bool VisualizerGlow { get; set; } = false;

    // ── Day Name ───────────────────────────────────────────────────────
    public bool DayEnabled { get; set; } = false;
    /// <summary>0=Small(42) 1=Medium(64) 2=Large(86) 3=Huge(110) 4=Giant(140)</summary>
    public int DayFontSize { get; set; } = 1;
    /// <summary>0=Pure White 1=Dim Slate 2=Electric Cyan 3=Neon Mint 4=Cyber Violet 5=Sunset Orange 6=Amber Gold 7=Rose Pink</summary>
    public int DayColorMode { get; set; } = 0;
    /// <summary>0=Anurati (Mond) 1=Bahnschrift Light 2=Segoe UI Light</summary>
    public int DayFontMode { get; set; } = 0;
    /// <summary>0=UPPERCASE 1=Title Case 2=lowercase</summary>
    public int DayCaseMode { get; set; } = 0;
    /// <summary>0=Compact 1=Wide (Mond style) 2=Ultra Wide</summary>
    public int DaySpacingMode { get; set; } = 1;
    public bool DayShowBackground { get; set; } = false;

    // ── Other widgets ──────────────────────────────────────────────────
    public bool CalendarEnabled { get; set; } = false;
    public bool NotesEnabled { get; set; } = false;
    public bool QuoteEnabled { get; set; } = false;

    [Obsolete("Merged into SystemMonitor Simplicity Circles")]
    public bool TaskbarStatsEnabled { get; set; } = false;

    public bool WidgetsLocked { get; set; } = false;

    // ── Saved positions & dimensions ───────────────────────────────────
    [Obsolete] public double TaskbarStatsX { get; set; } = 800;
    [Obsolete] public double TaskbarStatsY { get; set; } = 0;

    public double ClockX { get; set; } = 20;
    public double ClockY { get; set; } = 20;

    public double SystemMonitorX { get; set; } = 20;
    public double SystemMonitorY { get; set; } = 140;

    public double MediaX { get; set; } = 20;
    public double MediaY { get; set; } = 240;
    public double MediaWidth { get; set; } = 300;
    public double MediaHeight { get; set; } = 130;

    public double DayX { get; set; } = 30;
    public double DayY { get; set; } = 30;

    public double CalendarX { get; set; } = 30;
    public double CalendarY { get; set; } = 160;

    public double NotesX { get; set; } = 30;
    public double NotesY { get; set; } = 420;
    public double NotesWidth { get; set; } = 220;
    public double NotesHeight { get; set; } = 160;
    public string NotesText { get; set; } = string.Empty;

    public double QuoteX { get; set; } = 30;
    public double QuoteY { get; set; } = 600;
    public int QuoteIndex { get; set; } = 0;
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
