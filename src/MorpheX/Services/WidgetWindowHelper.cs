using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MorpheX.Core.Desktop;
using MorpheX.Widgets;
using Serilog;

namespace MorpheX.Services;

/// <summary>
/// Configures WPF windows to behave like true Rainmeter desktop widgets:
/// 1. Sets WS_EX_TOOLWINDOW so the widget NEVER appears in Alt+Tab or the taskbar.
/// 2. Sets WS_EX_NOACTIVATE so interacting with the widget never steals focus from active apps.
/// 3. Retains clean WPF per-pixel alpha transparency without DWM glass interference or opaque fallback boxes.
/// 4. Intercepts WM_WINDOWPOSCHANGING to ignore minimize coordinates (-32000, -32000).
/// 5. Intercepts WM_ERASEBKGND, WM_NCPAINT, and WM_NCACTIVATE to prevent Windows/GDI from painting opaque rectangles.
/// 6. Configures DWM attributes (DWMSBT_NONE, DWMWCP_DONOTROUND, DWMWA_TRANSITIONS_FORCEDISABLED) to prevent frame glitches.
/// </summary>
public static class WidgetWindowHelper
{
    public static readonly System.Windows.Media.SolidColorBrush MicroAlphaBrush =
        new(System.Windows.Media.Color.FromArgb(1, 0, 0, 0));

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const long WS_EX_LAYERED = 0x00080000L;

    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;
    private const int WM_WINDOWPOSCHANGING = 0x0046;
    private const int WM_WINDOWPOSCHANGED = 0x0047;
    private const int WM_ERASEBKGND = 0x0014;
    private const int WM_NCPAINT = 0x0085;
    private const int WM_NCACTIVATE = 0x0086;
    private const int WM_DWMCOMPOSITIONCHANGED = 0x031E;
    private const int WM_DISPLAYCHANGE = 0x007E;
    private const int WM_SETTINGCHANGE = 0x001A;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_HIDEWINDOW = 0x0080;

    private const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_DONOTROUND = 1;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMSBT_NONE = 1;

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

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [DllImport("gdi32.dll")]
    private static extern IntPtr GetStockObject(int fnObject);
    private const int NULL_BRUSH = 5;
    private const int GCLP_HBRBACKGROUND = -10;

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetClassLong", SetLastError = true)]
    private static extern int SetClassLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetClassLongPtr", SetLastError = true)]
    private static extern IntPtr SetClassLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    private static IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8 ? SetClassLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetClassLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    public static void SetupWidgetWindow(Window window)
    {
        try
        {
            window.ShowActivated = false;
            if (window is not NotesWidget)
            {
                window.Focusable = false;
            }
            window.Background = MicroAlphaBrush;

            var helper = new WindowInteropHelper(window);
            var hwnd = helper.EnsureHandle();

            // 1. Disable DWM window transition animations for this widget.
            // When other apps minimize or restore, DWM will not generate transition snapshots for this window.
            try
            {
                int disableTransitions = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_TRANSITIONS_FORCEDISABLED, ref disableTransitions, sizeof(int));
            }
            catch { }

            // 2. Prevent Windows 11 DWM from synthesizing rounded corners or frames on this transparent window.
            try
            {
                int cornerPreference = DWMWCP_DONOTROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));
            }
            catch { }

            // 3. Clear any DWM system backdrop (Mica / Acrylic)
            try
            {
                int backdrop = DWMSBT_NONE;
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            }
            catch { }

            // 4. Ensure window class background brush is NULL so Windows/GDI never paints COLOR_WINDOW (solid white)
            try
            {
                IntPtr nullBrush = GetStockObject(NULL_BRUSH);
                SetClassLongPtr(hwnd, GCLP_HBRBACKGROUND, nullBrush);
            }
            catch { }

            // 5. Ensure HwndSource CompositionTarget background is pure Transparent.
            // For ClockWidget, SoftwareOnly rendering uses native UpdateLayeredWindow (ARGB DIB),
            // making DWM swapchain occlusion culling and un-occlusion white boxes physically impossible.
            var source = HwndSource.FromHwnd(hwnd);
            if (source?.CompositionTarget != null)
            {
                source.CompositionTarget.BackgroundColor = System.Windows.Media.Colors.Transparent;
                if (window is ClockWidget)
                {
                    source.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
                }
            }

            // 6. Apply WS_EX_TOOLWINDOW (hides from Alt+Tab) and WS_EX_NOACTIVATE (no focus stealing).
            // Preserves WS_EX_LAYERED for WPF's per-pixel alpha composition.
            IntPtr currentExStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            long newExStyle = currentExStyle.ToInt64() | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(newExStyle));

            // Inform OS of extended style changes
            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

            // 7. Hook WndProc for non-activating click, suppress minimize, and intercept all GDI erase/NC paint messages
            source?.AddHook((IntPtr h, int m, IntPtr w, IntPtr l, ref bool hnd) => HandleWndProc(window, h, m, w, l, ref hnd));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to apply desktop widget styles to {Type}", window.GetType().Name);
        }
    }

    /// <summary>
    /// Performs a safe WPF visual refresh on the widget window without destructive Win32 visibility cycling.
    /// Never call SWP_HIDEWINDOW / SWP_SHOWWINDOW on an active WPF layered window.
    /// </summary>
    public static void RefreshWidgetSurface(Window window)
    {
        try
        {
            if (window == null) return;
            window.Dispatcher.InvokeAsync(() =>
            {
                window.Background = MicroAlphaBrush;
                window.InvalidateVisual();
            });
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

    private static IntPtr HandleWndProc(Window window, IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
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
            // Prevent Windows/GDI from erasing the background with COLOR_WINDOW (white/dark box)
            handled = true;
            return new IntPtr(1);
        }
        else if (msg == WM_NCPAINT)
        {
            // Suppress non-client frame painting so Windows never draws an opaque box
            handled = true;
            return IntPtr.Zero;
        }
        else if (msg == WM_NCACTIVATE)
        {
            // Prevent Windows from repainting non-client areas when other apps minimize or close
            handled = true;
            return new IntPtr(1);
        }
        else if (msg == WM_WINDOWPOSCHANGED)
        {
            // When an occluding window minimizes, closes, or uncovers this widget, force visual refresh
            window.Dispatcher.InvokeAsync(() => window.InvalidateVisual());
        }
        else if (msg == WM_DWMCOMPOSITIONCHANGED || msg == WM_DISPLAYCHANGE)
        {
            RefreshWidgetSurface(window);
        }

        return IntPtr.Zero;
    }
}
