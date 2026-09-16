using System.Drawing;
using Serilog;

namespace MorpheX.Core.Models;

public sealed class MonitorInfo
{
    public string DeviceId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Physical pixel bounds of the monitor in the virtual desktop coordinate space.
    /// X/Y are the position of this monitor's top-left corner in the virtual desktop
    /// (physical pixels, can be negative for monitors to the left of/above primary).
    /// Width/Height are the native physical pixel resolution of the panel.
    /// This is safe to pass directly to Win32 APIs such as MoveWindow, SetWindowPos,
    /// and CreateWindowEx which all operate in physical pixel space.
    /// </summary>
    public Rectangle Bounds { get; set; }

    /// <summary>
    /// Physical pixel work area (excludes taskbar) in the virtual desktop coordinate space.
    /// </summary>
    public Rectangle WorkArea { get; set; }

    /// <summary>
    /// The DPI scale factor for this monitor (e.g. 1.0 = 100%, 1.25 = 125%, 1.5 = 150%).
    /// Derived from GetDpiForMonitor(MDT_EFFECTIVE_DPI) / 96.0.
    /// </summary>
    public double DpiScale { get; set; } = 1.0;

    public bool IsPrimary { get; set; }

    public int RefreshRate { get; set; } = 60;

    [System.Text.Json.Serialization.JsonIgnore]
    public IntPtr Handle { get; set; }

    public override string ToString() =>
        $"{DisplayName} ({Bounds.Width}x{Bounds.Height} @ {RefreshRate}Hz, {DpiScale * 100:0}% DPI, Pos: {Bounds.X},{Bounds.Y})";
}
