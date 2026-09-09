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
                    double dpiScale = 1.0;
                    if (Desktop.NativeMethods.GetDpiForMonitor(hMonitor,
                            Desktop.NativeMethods.MDT_EFFECTIVE_DPI,
                            out uint dpiX, out uint _dpiY) == 0)
                    {
                        dpiScale = dpiX / 96.0;
                    }

                    int refreshRate = 60;
                    var devMode = new Desktop.NativeMethods.DEVMODE();
                    devMode.dmSize = (short)System.Runtime.InteropServices.Marshal.SizeOf<Desktop.NativeMethods.DEVMODE>();
                    if (Desktop.NativeMethods.EnumDisplaySettings(monInfo.szDevice,
                            Desktop.NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode))
                    {
                        refreshRate = devMode.dmDisplayFrequency > 0 ? devMode.dmDisplayFrequency : 60;
                    }

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
                        DeviceId = deviceId,
                        DisplayName = !string.IsNullOrEmpty(displayDevice.DeviceString)
                            ? displayDevice.DeviceString
                            : monInfo.szDevice,
                        Bounds = monInfo.rcMonitor.ToRectangle(),
                        WorkArea = monInfo.rcWork.ToRectangle(),
                        DpiScale = dpiScale,
                        IsPrimary = (monInfo.dwFlags & Desktop.NativeMethods.MONITORINFOEX.MONITORINFOF_PRIMARY) != 0,
                        RefreshRate = refreshRate,
                        Handle = hMonitor
                    };

                    newMonitors.Add(monitor);
                }
                return true;
            }, IntPtr.Zero);

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
                    _monitors[i].Bounds != newMonitors[i].Bounds ||
                    _monitors[i].WorkArea != newMonitors[i].WorkArea ||
                    _monitors[i].Handle != newMonitors[i].Handle)
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
