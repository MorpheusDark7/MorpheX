using System.Text;
using MorpheX.Core.Desktop;

namespace MorpheX.Core.Detection;

public static class ShellWindowHelper
{
    private static readonly int CurrentPid = Environment.ProcessId;

    public static bool IsShellOrDesktopWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return true;

        var shellWnd = NativeMethods.GetShellWindow();
        var desktopWnd = NativeMethods.GetDesktopWindow();

        if (hWnd == shellWnd || hWnd == desktopWnd)
            return true;

        NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == (uint)CurrentPid)
            return true;

        var classNameBuilder = new StringBuilder(256);
        NativeMethods.GetClassName(hWnd, classNameBuilder, 256);
        var className = classNameBuilder.ToString();

        if (className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd"
            or "NotifyIconOverflowWindow" or "TopLevelWindowForOverflowXamlIsland"
            or "Windows.UI.Core.CoreWindow" or "Shell_CharmWindow" or "XamlExplorerHostIslandWindow"
            or "DesktopWindowXamlSource" or "DV2ControlHost" or "MorpheXWallpaperHost"
            or "SHELLDLL_DefView" or "SysListView32" or "#32768" or "#32769" or "Tooltips_class32")
        {
            return true;
        }

        // Check root ancestor window (e.g. child controls or islands inside Progman/WorkerW)
        var rootWnd = NativeMethods.GetAncestor(hWnd, NativeMethods.GA_ROOT);
        if (rootWnd != IntPtr.Zero && rootWnd != hWnd)
        {
            if (rootWnd == shellWnd || rootWnd == desktopWnd)
                return true;

            var rootClassBuilder = new StringBuilder(256);
            NativeMethods.GetClassName(rootWnd, rootClassBuilder, 256);
            var rootClassName = rootClassBuilder.ToString();
            if (rootClassName is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
                return true;
        }

        // Any window owned by the Windows Shell process (explorer.exe) that is NOT a File Explorer browsing window
        // is part of the desktop/taskbar/system shell UI (e.g. desktop views, context menus, flyouts).
        if (shellWnd != IntPtr.Zero)
        {
            NativeMethods.GetWindowThreadProcessId(shellWnd, out uint shellPid);
            if (shellPid != 0 && pid == shellPid)
            {
                // CabinetWClass and ExploreWClass are actual File Explorer folder windows where the user browses files
                if (className is not "CabinetWClass" and not "ExploreWClass")
                {
                    return true;
                }
            }
        }

        return false;
    }
}
