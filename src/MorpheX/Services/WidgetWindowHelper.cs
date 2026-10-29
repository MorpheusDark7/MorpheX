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
/// 3. Ensures WS_EX_LAYERED composition remains rock-solid against D3D resets and DWM changes.
/// 4. Intercepts WM_WINDOWPOSCHANGING to ignore minimize coordinates (-32000, -32000).
/// 5. Intercepts WM_ERASEBKGND to prevent the OS from painting an opaque black background rectangle.
/// </summary>
public static class WidgetWindowHelper
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const long WS_EX_LAYERED = 0x00080000L;

    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;
    private const int WM_WINDOWPOSCHANGING = 0x0046;
    private const int WM_ERASEBKGND = 0x0014;
    private const int WM_DWMCOMPOSITIONCHANGED = 0x031E;
    private const int WM_DISPLAYCHANGE = 0x007E;
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

            // 1. Apply WS_EX_TOOLWINDOW (hides from Alt+Tab) and WS_EX_NOACTIVATE (no focus stealing).
            // Explicitly preserve WS_EX_LAYERED so WPF never loses per-pixel alpha composition.
            IntPtr currentExStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            long newExStyle = currentExStyle.ToInt64() | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(newExStyle));

            // Inform OS of extended style changes so DWM updates layered composition immediately
            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER |
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);

            // 3. Hook WndProc for non-activating click, suppress minimize, and prevent black background artifacts
            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to apply desktop widget styles to {Type}", window.GetType().Name);
        }
    }

    public static void RefreshWidgetSurface(Window window)
    {
        try
        {
            var helper = new WindowInteropHelper(window);
            var hwnd = helper.Handle;
            if (hwnd == IntPtr.Zero) return;

            IntPtr currentExStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            long newExStyle = currentExStyle.ToInt64() | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(newExStyle));

            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER |
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
            NativeMethods.InvalidateRect(hwnd, IntPtr.Zero, true);
            window.InvalidateVisual();
        }
        catch { }
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
        else if (msg == WM_ERASEBKGND)
        {
            // Prevent Windows/GDI from drawing an opaque black background brush over transparent layered windows
            handled = true;
            return new IntPtr(1);
        }
        else if (msg == WM_DWMCOMPOSITIONCHANGED || msg == WM_DISPLAYCHANGE)
        {
            // Re-assert layered attributes and trigger frame refresh to restore transparent alpha if DWM resets
            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER |
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
            NativeMethods.InvalidateRect(hwnd, IntPtr.Zero, true);
        }

        return IntPtr.Zero;
    }
}
