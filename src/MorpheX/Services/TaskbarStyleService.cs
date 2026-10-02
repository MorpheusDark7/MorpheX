using System.Runtime.InteropServices;
using System.Windows;
using Serilog;

namespace MorpheX.Services;

/// <summary>
/// Applies taskbar visual effects (Clear / Blur / Acrylic) to Shell_TrayWnd
/// and secondary monitor taskbars (Shell_SecondaryTrayWnd) using the
/// undocumented SetWindowCompositionAttribute API — the same technique
/// used by TranslucentTB.
/// </summary>
public sealed class TaskbarStyleService : IDisposable
{
    // ── Win32 ────────────────────────────────────────────────────────────────

    private const int WCA_ACCENT_POLICY = 19;

    private enum AccentState : int
    {
        ACCENT_DISABLED                   = 0,  // Default Windows style
        ACCENT_ENABLE_GRADIENT            = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND          = 3,  // Classic Aero Blur
        ACCENT_ENABLE_ACRYLICBLURBEHIND   = 4,  // Fluent Acrylic
        ACCENT_INVALID_STATE              = 5
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int         AccentFlags;
        public int         GradientColor;  // AABBGGRR — alpha in high byte
        public int         AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int    Attribute;
        public IntPtr Data;
        public int    SizeOfData;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(
        IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(
        IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Taskbar style options.</summary>
    public enum TaskbarStyle
    {
        /// <summary>Default Windows taskbar — no modification.</summary>
        Default  = 0,
        /// <summary>Fully transparent / clear taskbar.</summary>
        Clear    = 1,
        /// <summary>Gaussian blur (classic Aero style).</summary>
        Blur     = 2,
        /// <summary>Frosted-glass acrylic blur with a subtle tint.</summary>
        Acrylic  = 3
    }

    private TaskbarStyle _current = TaskbarStyle.Default;
    private bool         _disposed;

    /// <summary>
    /// Apply the chosen style to the primary and all secondary taskbars.
    /// Calling with <see cref="TaskbarStyle.Default"/> restores the native look.
    /// </summary>
    public void Apply(TaskbarStyle style)
    {
        _current = style;
        ApplyToAll(style);
    }

    /// <summary>Restore the native Windows taskbar appearance.</summary>
    public void Reset()
    {
        _current = TaskbarStyle.Default;
        ApplyToAll(TaskbarStyle.Default);
    }

    // ── Implementation ───────────────────────────────────────────────────────

    private static void ApplyToAll(TaskbarStyle style)
    {
        try
        {
            // Primary taskbar
            var primary = FindWindow("Shell_TrayWnd", null);
            if (primary != IntPtr.Zero)
                SetStyle(primary, style);

            // Secondary monitor taskbars
            var secondary = IntPtr.Zero;
            while (true)
            {
                secondary = FindWindowEx(IntPtr.Zero, secondary, "Shell_SecondaryTrayWnd", null);
                if (secondary == IntPtr.Zero) break;
                SetStyle(secondary, style);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TaskbarStyleService: failed to apply style {Style}", style);
        }
    }

    private static void SetStyle(IntPtr hwnd, TaskbarStyle style)
    {
        var accent = style switch
        {
            TaskbarStyle.Clear   => new AccentPolicy { AccentState = AccentState.ACCENT_ENABLE_TRANSPARENTGRADIENT, GradientColor = 0x00000000 },
            TaskbarStyle.Blur    => new AccentPolicy { AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND,           GradientColor = 0x00000000 },
            TaskbarStyle.Acrylic => new AccentPolicy { AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,    GradientColor = 0x44000000, AccentFlags = 2 },
            _                    => new AccentPolicy { AccentState = AccentState.ACCENT_DISABLED }
        };

        unsafe
        {
            var data = new WindowCompositionAttributeData
            {
                Attribute  = WCA_ACCENT_POLICY,
                Data       = new IntPtr(&accent),
                SizeOfData = Marshal.SizeOf<AccentPolicy>()
            };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { Reset(); } catch { }
    }
}
