using System.Drawing;
using Serilog;

namespace MorpheX.Core.Services;

public interface IMonitorService : IDisposable
{
    IReadOnlyList<Models.MonitorInfo> Monitors { get; }

    void Refresh();

    event EventHandler? MonitorsChanged;
}

public sealed class MonitorService : IMonitorService
{
    private List<Models.MonitorInfo> _monitors = new();
    private bool _disposed;

    public IReadOnlyList<Models.MonitorInfo> Monitors => _monitors;

    public event EventHandler? MonitorsChanged;

    public MonitorService()
    {
        Refresh();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Log.Information("DisplaySettingsChanged received from system — refreshing monitors");
        Refresh();
    }

    public void Refresh()
    {
        var newMonitors = new List<Models.MonitorInfo>();

        Desktop.NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr _, ref Desktop.NativeMethods.RECT __, IntPtr ___) =>
            {
                var monInfo = new Desktop.NativeMethods.MONITORINFOEX();
                monInfo.cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Desktop.NativeMethods.MONITORINFOEX>();

                if (Desktop.NativeMethods.GetMonitorInfo(hMonitor, ref monInfo))
                {
                    // ── DPI ─────────────────────────────────────────────────────────
                    // GetDpiForMonitor always returns the true per-monitor DPI
                    // regardless of process DPI awareness mode.
                    double dpiScale = 1.0;
                    if (Desktop.NativeMethods.GetDpiForMonitor(hMonitor,
                            Desktop.NativeMethods.MDT_EFFECTIVE_DPI,
                            out uint dpiX, out uint _dpiY) == 0)
                    {
                        dpiScale = dpiX / 96.0;
                    }

                    // ── Physical pixel size from EnumDisplaySettings ─────────────
                    // dmPelsWidth / dmPelsHeight always report the native panel
                    // resolution in physical pixels, completely independent of DPI
                    // scaling or process DPI awareness mode. This is the authoritative
                    // source for physical resolution.
                    //
                    // GetMonitorInfo.rcMonitor reports coordinates in the coordinate
                    // space of the calling process. With the PerMonitorV2 manifest,
                    // this IS physical pixels. Without it, it would be DPI-scaled
                    // logical pixels. Using EnumDisplaySettings for W/H makes the size
                    // correct even if the manifest is missing or the app is somehow
                    // run in a DPI-unaware context.
                    int physicalWidth  = monInfo.rcMonitor.Width;
                    int physicalHeight = monInfo.rcMonitor.Height;
                    int refreshRate    = 60;

                    var devMode = new Desktop.NativeMethods.DEVMODE();
                    devMode.dmSize = (short)System.Runtime.InteropServices.Marshal.SizeOf<Desktop.NativeMethods.DEVMODE>();
                    if (Desktop.NativeMethods.EnumDisplaySettings(monInfo.szDevice,
                            Desktop.NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode))
                    {
                        // dmPelsWidth/Height are always physical pixels — use them as
                        // the authoritative physical resolution source.
                        if (devMode.dmPelsWidth > 0 && devMode.dmPelsHeight > 0)
                        {
                            physicalWidth  = devMode.dmPelsWidth;
                            physicalHeight = devMode.dmPelsHeight;
                        }

                        refreshRate = devMode.dmDisplayFrequency > 0 ? devMode.dmDisplayFrequency : 60;
                    }

                    // ── Virtual-desktop position ─────────────────────────────────
                    // With PerMonitorV2 manifest, rcMonitor.Left/Top are physical
                    // pixel offsets in the virtual desktop — correct for Win32 calls.
                    // We intentionally take X/Y from rcMonitor (not dmPositionX/Y)
                    // because rcMonitor accounts for monitor arrangement correctly
                    // in the virtual desktop layout.
                    int posX = monInfo.rcMonitor.Left;
                    int posY = monInfo.rcMonitor.Top;

                    // ── Work area ───────────────────────────────────────────────
                    // rcWork is in the same coordinate space as rcMonitor.
                    // Use the physical width/height ratio to compute physical work area
                    // when we know the physical size differs from rcMonitor dimensions.
                    Rectangle workArea;
                    if (physicalWidth != monInfo.rcMonitor.Width || physicalHeight != monInfo.rcMonitor.Height)
                    {
                        // The process is not fully DPI-aware — scale work area to physical.
                        double scaleX = (double)physicalWidth  / monInfo.rcMonitor.Width;
                        double scaleY = (double)physicalHeight / monInfo.rcMonitor.Height;
                        workArea = new Rectangle(
                            posX,
                            posY,
                            (int)Math.Round(monInfo.rcWork.Width  * scaleX),
                            (int)Math.Round(monInfo.rcWork.Height * scaleY));
                    }
                    else
                    {
                        workArea = monInfo.rcWork.ToRectangle();
                    }

                    // ── Device / display name ────────────────────────────────────
                    string deviceId = monInfo.szDevice;
                    var displayDevice = new Desktop.NativeMethods.DISPLAY_DEVICE();
                    displayDevice.cb = System.Runtime.InteropServices.Marshal.SizeOf<Desktop.NativeMethods.DISPLAY_DEVICE>();
                    if (Desktop.NativeMethods.EnumDisplayDevices(monInfo.szDevice, 0, ref displayDevice, 0))
                    {
                        deviceId = !string.IsNullOrEmpty(displayDevice.DeviceID)
                            ? displayDevice.DeviceID
                            : monInfo.szDevice;
                    }

                    var monitor = new Models.MonitorInfo
                    {
                        DeviceId    = deviceId,
                        DisplayName = !string.IsNullOrEmpty(displayDevice.DeviceString)
                                        ? displayDevice.DeviceString
                                        : monInfo.szDevice,
                        // Bounds uses physical pixel dimensions (W/H from EnumDisplaySettings,
                        // X/Y from rcMonitor which is physical with PerMonitorV2 manifest).
                        Bounds      = new Rectangle(posX, posY, physicalWidth, physicalHeight),
                        WorkArea    = workArea,
                        DpiScale    = dpiScale,
                        IsPrimary   = (monInfo.dwFlags & Desktop.NativeMethods.MONITORINFOEX.MONITORINFOF_PRIMARY) != 0,
                        RefreshRate = refreshRate,
                        Handle      = hMonitor
                    };

                    Log.Debug(
                        "  Raw: rcMonitor={L},{T},{R},{B} (logical {LW}x{LH})  " +
                        "EnumDisplaySettings={PW}x{PH}  DPI={DPI}x ({Scale:P0})",
                        monInfo.rcMonitor.Left, monInfo.rcMonitor.Top,
                        monInfo.rcMonitor.Right, monInfo.rcMonitor.Bottom,
                        monInfo.rcMonitor.Width, monInfo.rcMonitor.Height,
                        physicalWidth, physicalHeight,
                        dpiX, dpiScale);

                    newMonitors.Add(monitor);
                }
                return true;
            }, IntPtr.Zero);

        // Primary first, then left-to-right by physical position
        newMonitors.Sort((a, b) =>
        {
            if (a.IsPrimary != b.IsPrimary) return a.IsPrimary ? -1 : 1;
            return a.Bounds.X.CompareTo(b.Bounds.X);
        });

        for (int i = 0; i < newMonitors.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(newMonitors[i].DisplayName) ||
                newMonitors[i].DisplayName.StartsWith("\\\\.\\"))
            {
                newMonitors[i].DisplayName = $"Display {i + 1}";
            }
        }

        bool changed = _monitors.Count != newMonitors.Count;
        if (!changed)
        {
            for (int i = 0; i < _monitors.Count; i++)
            {
                if (_monitors[i].DeviceId != newMonitors[i].DeviceId ||
                    _monitors[i].Bounds   != newMonitors[i].Bounds   ||
                    _monitors[i].WorkArea != newMonitors[i].WorkArea  ||
                    _monitors[i].Handle   != newMonitors[i].Handle)
                {
                    changed = true;
                    break;
                }
            }
        }

        _monitors = newMonitors;

        Log.Information("Detected {Count} monitor(s):", _monitors.Count);
        foreach (var m in _monitors)
        {
            Log.Information("  {Monitor}", m);
        }

        if (changed)
        {
            MonitorsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }
}
