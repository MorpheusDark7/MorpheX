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
    [Obsolete("Taskbar styling feature removed.")]
    public TaskbarSettings Taskbar { get; set; } = new();
    public List<DesktopProfile> Profiles { get; set; } = new();
    public string? ActiveProfileId { get; set; }
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
    /// <summary>When true, displays DayCustomText instead of the auto weekday name.</summary>
    public bool DayUseCustomText { get; set; } = false;
    /// <summary>User-defined text shown when DayUseCustomText is true. Max 20 chars.</summary>
    public string DayCustomText { get; set; } = string.Empty;

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

    // ── Weather widget ─────────────────────────────────────────────────────
    public bool WeatherEnabled { get; set; } = false;
    public double WeatherX { get; set; } = 30;
    public double WeatherY { get; set; } = 760;
    public double WeatherLat { get; set; } = 0;
    public double WeatherLon { get; set; } = 0;
    public string WeatherLocationName { get; set; } = string.Empty;
    public bool WeatherUseFahrenheit { get; set; } = false;
    public bool WeatherShowHiLo { get; set; } = true;
    public bool WeatherShowHumidity { get; set; } = true;
    public bool WeatherShowWind { get; set; } = true;
    public bool WeatherShowBackground { get; set; } = true;
    public int WeatherScale { get; set; } = 1; // 0=Small (85%), 1=Normal (100%), 2=Large (120%)

    /// <summary>
    /// Copies all widget preferences, configurations, and positions in-place from another instance.
    /// </summary>
    public void CopyFrom(WidgetSettings? source)
    {
        if (source == null) return;

        WidgetTheme = source.WidgetTheme;
        WidgetsLocked = source.WidgetsLocked;

        // Clock
        ClockEnabled = source.ClockEnabled;
        ClockIs24Hour = source.ClockIs24Hour;
        ClockShowDate = source.ClockShowDate;
        ClockShowBackground = source.ClockShowBackground;
        ClockFontSize = source.ClockFontSize;
        ClockColorMode = source.ClockColorMode;
        ClockFontMode = source.ClockFontMode;
        ClockX = source.ClockX;
        ClockY = source.ClockY;

        // System Monitor
        SystemMonitorEnabled = source.SystemMonitorEnabled;
        SysMonSize = source.SysMonSize;
        SysMonColorMode = source.SysMonColorMode;
        SysMonOrientation = source.SysMonOrientation;
        SysMonShowBackground = source.SysMonShowBackground;
        SysMonShowCpu = source.SysMonShowCpu;
        SysMonShowRam = source.SysMonShowRam;
        SysMonShowGpu = source.SysMonShowGpu;
        SysMonShowDisk = source.SysMonShowDisk;
        SystemMonitorX = source.SystemMonitorX;
        SystemMonitorY = source.SystemMonitorY;

        // Media
        MediaEnabled = source.MediaEnabled;
        VisualizerStyle = source.VisualizerStyle;
        VisualizerColorMode = source.VisualizerColorMode;
        MediaShowBackground = source.MediaShowBackground;
        MediaShowTrackDetails = source.MediaShowTrackDetails;
        VisualizerGlow = source.VisualizerGlow;
        MediaX = source.MediaX;
        MediaY = source.MediaY;
        MediaWidth = source.MediaWidth;
        MediaHeight = source.MediaHeight;

        // Day
        DayEnabled = source.DayEnabled;
        DayFontSize = source.DayFontSize;
        DayColorMode = source.DayColorMode;
        DayFontMode = source.DayFontMode;
        DayCaseMode = source.DayCaseMode;
        DaySpacingMode = source.DaySpacingMode;
        DayShowBackground = source.DayShowBackground;
        DayUseCustomText = source.DayUseCustomText;
        DayCustomText = source.DayCustomText;
        DayX = source.DayX;
        DayY = source.DayY;

        // Calendar
        CalendarEnabled = source.CalendarEnabled;
        CalendarX = source.CalendarX;
        CalendarY = source.CalendarY;

        // Notes
        NotesEnabled = source.NotesEnabled;
        NotesX = source.NotesX;
        NotesY = source.NotesY;
        NotesWidth = source.NotesWidth;
        NotesHeight = source.NotesHeight;
        NotesText = source.NotesText;

        // Quote
        QuoteEnabled = source.QuoteEnabled;
        QuoteX = source.QuoteX;
        QuoteY = source.QuoteY;
        QuoteIndex = source.QuoteIndex;

        // Weather
        WeatherEnabled = source.WeatherEnabled;
        WeatherX = source.WeatherX;
        WeatherY = source.WeatherY;
        WeatherLat = source.WeatherLat;
        WeatherLon = source.WeatherLon;
        WeatherLocationName = source.WeatherLocationName;
        WeatherUseFahrenheit = source.WeatherUseFahrenheit;
        WeatherShowHiLo = source.WeatherShowHiLo;
        WeatherShowHumidity = source.WeatherShowHumidity;
        WeatherShowWind = source.WeatherShowWind;
        WeatherShowBackground = source.WeatherShowBackground;
        WeatherScale = source.WeatherScale;
    }
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

    /// <summary>
    /// Placement mode for multi-monitor setups:
    /// "PerDisplay" = individual wallpaper per display,
    /// "Duplicate" = clone same wallpaper across all displays,
    /// "Span" = stretch single wallpaper across entire virtual screen span.
    /// </summary>
    public string PlacementMode { get; set; } = "PerDisplay";
}

public sealed class AppearanceSettings
{
    /// <summary>"System" (default), "Dark", or "Light"</summary>
    public string Theme { get; set; } = "System";
    public string AccentColor { get; set; } = "System";
    public bool EnableAnimations { get; set; } = true;
    public bool ShowSystemStats { get; set; } = false;
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

/// <summary>
/// Per-monitor visual effects override. When set, these replace the global Effects for that display.
/// All values nullable — null means "inherit from global Effects".
/// Safe limits: Brightness 0.05–2.0, Contrast 0.05–2.5, Saturation 0.0–3.0, ColorTemp -50..+50
/// </summary>
public sealed class MonitorEffectsSettings
{
    public float? Brightness { get; set; }
    public float? Contrast { get; set; }
    public float? Saturation { get; set; }
    public int? ColorTemperature { get; set; }

    public bool HasAnyOverride =>
        Brightness.HasValue || Contrast.HasValue || Saturation.HasValue || ColorTemperature.HasValue;

    /// <summary>Merge with a global fallback, returning effective values.</summary>
    public (float brightness, float contrast, float saturation, int colorTemp)
        Resolve(WallpaperEffectsSettings global) => (
            Brightness   ?? global.Brightness,
            Contrast     ?? global.Contrast,
            Saturation   ?? global.Saturation,
            ColorTemperature ?? global.ColorTemperature);
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

    /// <summary>Per-monitor visual effects. Null = inherit global Effects settings.</summary>
    public MonitorEffectsSettings? Effects { get; set; }
}
