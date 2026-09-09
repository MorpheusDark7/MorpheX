using System.Drawing;
using System.Text;
using Serilog;

namespace MorpheX.Core.Detection;

public sealed class FullscreenDetector
{
    private static readonly int CurrentPid = Environment.ProcessId;

    public bool Check(Rectangle monitorBounds, Rectangle workArea, IntPtr monitorHandle,
                      out bool isFullscreen, out bool isMaximized)
    {
        isFullscreen = false;
        isMaximized = false;

        var fgWindow = Desktop.NativeMethods.GetForegroundWindow();
        if (fgWindow != IntPtr.Zero && !IsIgnoredWindow(fgWindow))
        {
            var fgMonitor = Desktop.NativeMethods.MonitorFromWindow(fgWindow,
                Desktop.NativeMethods.MONITOR_DEFAULTTONEAREST);

            if (fgMonitor == monitorHandle)
            {
                EvaluateWindow(fgWindow, monitorBounds, workArea, out isFullscreen, out isMaximized);
                if (isFullscreen || isMaximized)
                {
                    return true;
                }
            }
        }

        bool foundFullscreen = false;
        bool foundMaximized = false;

        Desktop.NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!Desktop.NativeMethods.IsWindowVisible(hWnd) || Desktop.NativeMethods.IsIconic(hWnd))
                return true;

            if (IsIgnoredWindow(hWnd))
                return true;

            var hMon = Desktop.NativeMethods.MonitorFromWindow(hWnd,
                Desktop.NativeMethods.MONITOR_DEFAULTTONEAREST);

            if (hMon != monitorHandle)
                return true;

            if (!Desktop.NativeMethods.GetWindowRect(hWnd, out var wr))
                return true;

            int width = wr.Right - wr.Left;
            int height = wr.Bottom - wr.Top;
            if (width < 150 || height < 150)
                return true;

            EvaluateWindow(hWnd, monitorBounds, workArea, out foundFullscreen, out foundMaximized);

            return false;
        }, IntPtr.Zero);

        isFullscreen = foundFullscreen;
        isMaximized = foundMaximized;
        return true;
    }

    private static void EvaluateWindow(IntPtr hWnd, Rectangle monitorBounds, Rectangle workArea,
                                       out bool fullscreen, out bool maximized)
    {
        fullscreen = false;
        maximized = false;

        if (!Desktop.NativeMethods.GetWindowRect(hWnd, out var windowRect))
            return;

        var winBounds = windowRect.ToRectangle();

        fullscreen = winBounds.Left <= monitorBounds.Left &&
                     winBounds.Top <= monitorBounds.Top &&
                     winBounds.Right >= monitorBounds.Right &&
                     winBounds.Bottom >= monitorBounds.Bottom;

        if (fullscreen)
        {
            maximized = true;
            return;
        }

        if (Desktop.NativeMethods.IsZoomed(hWnd))
        {
            maximized = true;
            return;
        }

        var style = Desktop.NativeMethods.GetWindowLong(hWnd, Desktop.NativeMethods.GWL_STYLE);
        const int WS_MAXIMIZE = 0x01000000;
        if ((style & WS_MAXIMIZE) != 0)
        {
            maximized = true;
            return;
        }

        if (winBounds.Left <= workArea.Left + 10 &&
            winBounds.Top <= workArea.Top + 10 &&
            winBounds.Right >= workArea.Right - 10 &&
            winBounds.Bottom >= workArea.Bottom - 10)
        {
            maximized = true;
        }
    }

    private static bool IsIgnoredWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return true;

        Desktop.NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == (uint)CurrentPid) return true;

        var className = new StringBuilder(256);
        Desktop.NativeMethods.GetClassName(hWnd, className, 256);
        var name = className.ToString();

        return name is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd"
            or "NotifyIconOverflowWindow" or "Windows.UI.Core.CoreWindow" or "Shell_CharmWindow"
            or "XamlExplorerHostIslandWindow" or "DV2ControlHost" or "MorpheXWallpaperHost";
    }
}
