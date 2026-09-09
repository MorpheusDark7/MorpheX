using System.Drawing;

namespace MorpheX.Core.Models;

public sealed class MonitorInfo
{
    public string DeviceId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public Rectangle Bounds { get; set; }

    public Rectangle WorkArea { get; set; }

    public double DpiScale { get; set; } = 1.0;

    public bool IsPrimary { get; set; }

    public int RefreshRate { get; set; } = 60;

    [System.Text.Json.Serialization.JsonIgnore]
    public IntPtr Handle { get; set; }

    public override string ToString() =>
        $"{DisplayName} ({Bounds.Width}x{Bounds.Height} @ {RefreshRate}Hz, {DpiScale * 100:0}% DPI)";
}
