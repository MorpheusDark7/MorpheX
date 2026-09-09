namespace MorpheX.Core.Models;

public enum PauseRule
{
    Never,

    OnFullscreen,

    OnMaximized,

    OnAnyFocused,

    OnCompletelyCovered
}

public enum PerformancePreset
{
    MaximumQuality,

    Balanced,

    MaximumEfficiency
}

public enum UnloadDelay
{
    Never,
    FiveMinutes,
    FifteenMinutes,
    ThirtyMinutes,
    OneHour
}
