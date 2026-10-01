using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MorpheX.Core.Desktop;
using Serilog;

namespace MorpheX.Services;

/// <summary>
/// Configures WPF windows to behave like true Rainmeter desktop widgets:
/// 1. Sets WS_EX_TOOLWINDOW so the widget NEVER appears in Alt+Tab or the taskbar.
/// 2. Sets WS_EX_NOACTIVATE so interacting with the widget never steals focus from active apps.
/// 3. Sets the desktop shell window as owner so the widget sits cleanly above the wallpaper and icons,
///    never disappears behind WorkerW, and stays visible when "Show Desktop" (Win+D) is pressed.
/// 4. Intercepts WM_WINDOWPOSCHANGING to ignore minimize coordinates (-32000, -32000).
/// </summary>
public static class WidgetWindowHelper
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;
    private const int WM_WINDOWPOSCHANGING = 0x0046;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    public static void SetupWidgetWindow(Window window)
    {
        try
        {
            var helper = new WindowInteropHelper(window);
            var hwnd = helper.EnsureHandle();

            // 1. Attach to Desktop shell window as owner
            IntPtr desktopHwnd = GetDesktopHandle();
            if (desktopHwnd != IntPtr.Zero)
            {
                helper.Owner = desktopHwnd;
                Log.Debug("Widget {Type} attached to desktop handle 0x{Handle:X}", window.GetType().Name, desktopHwnd);
            }

            // 2. Apply WS_EX_TOOLWINDOW (hides from Alt+Tab) and WS_EX_NOACTIVATE (no focus stealing)
            IntPtr currentExStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            long newExStyle = currentExStyle.ToInt64() | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(newExStyle));

            // 3. Hook WndProc for non-activating click & suppressing minimize during Show Desktop
            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to apply desktop widget styles to {Type}", window.GetType().Name);
        }
    }

    public static IntPtr GetDesktopHandle()
    {
        IntPtr progman = NativeMethods.FindWindow("Progman", null);
        IntPtr defView = NativeMethods.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (defView != IntPtr.Zero)
        {
            return progman;
        }

        IntPtr shellWindow = IntPtr.Zero;
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            var dv = NativeMethods.FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (dv != IntPtr.Zero)
            {
                shellWindow = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        return shellWindow != IntPtr.Zero ? shellWindow : (progman != IntPtr.Zero ? progman : NativeMethods.GetShellWindow());
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_MOUSEACTIVATE)
        {
            // Do not bring widget above other active application windows when clicked
            handled = true;
            return new IntPtr(MA_NOACTIVATE);
        }
        else if (msg == WM_WINDOWPOSCHANGING && lParam != IntPtr.Zero)
        {
            var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
            // When Win+D is pressed, Windows attempts to minimize windows by moving them to (-32000, -32000)
            if (pos.x == -32000 || pos.y == -32000)
            {
                pos.flags |= SWP_NOMOVE | SWP_NOSIZE;
                Marshal.StructureToPtr(pos, lParam, true);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }
}
