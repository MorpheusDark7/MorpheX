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

            bool matchesMonitor = (monitorHandle != IntPtr.Zero && fgMonitor == monitorHandle);
            if (!matchesMonitor && Desktop.NativeMethods.GetWindowRect(fgWindow, out var fgr))
            {
                var fgRect = fgr.ToRectangle();
                var intersect = Rectangle.Intersect(monitorBounds, fgRect);
                if (intersect.Width > 150 && intersect.Height > 150)
                {
                    matchesMonitor = true;
                }
            }

            if (matchesMonitor)
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

            if (!Desktop.NativeMethods.GetWindowRect(hWnd, out var wr))
                return true;

            int width = wr.Right - wr.Left;
            int height = wr.Bottom - wr.Top;
            if (width < 150 || height < 150)
                return true;

            var hMon = Desktop.NativeMethods.MonitorFromWindow(hWnd,
                Desktop.NativeMethods.MONITOR_DEFAULTTONEAREST);

            bool matchesMonitor = (monitorHandle != IntPtr.Zero && hMon == monitorHandle);
            if (!matchesMonitor)
            {
                var winRect = wr.ToRectangle();
                var intersect = Rectangle.Intersect(monitorBounds, winRect);
                if (intersect.Width > 150 && intersect.Height > 150)
                {
                    long intersectArea = (long)intersect.Width * intersect.Height;
                    long monArea = (long)monitorBounds.Width * monitorBounds.Height;
                    long winArea = (long)winRect.Width * winRect.Height;
                    if (intersectArea > monArea * 0.4 || (winArea > 0 && intersectArea > winArea * 0.5))
                    {
                        matchesMonitor = true;
                    }
                }
            }

            if (!matchesMonitor)
                return true;

            EvaluateWindow(hWnd, monitorBounds, workArea, out foundFullscreen, out foundMaximized);
            if (foundFullscreen || foundMaximized)
                return false;

            return true;
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

        // 1. Fullscreen check (covers entire monitor bounds, allowing small invisible borders)
        fullscreen = winBounds.Left <= monitorBounds.Left + 10 &&
                     winBounds.Top <= monitorBounds.Top + 10 &&
                     winBounds.Right >= monitorBounds.Right - 10 &&
                     winBounds.Bottom >= monitorBounds.Bottom - 10;

        if (fullscreen)
        {
            maximized = true;
            return;
        }

        // 2. Maximized window check (IsZoomed or WS_MAXIMIZE)
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

        // 3. WorkArea coverage check (snapped or borderless window filling work area)
        if (winBounds.Left <= workArea.Left + 15 &&
            winBounds.Top <= workArea.Top + 15 &&
            winBounds.Right >= workArea.Right - 15 &&
            winBounds.Bottom >= workArea.Bottom - 15)
        {
            maximized = true;
        }
    }

    private static bool IsIgnoredWindow(IntPtr hWnd)
    {
        return ShellWindowHelper.IsShellOrDesktopWindow(hWnd);
    }
}
