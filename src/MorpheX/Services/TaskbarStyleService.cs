using System.Runtime.InteropServices;
using System.Windows.Threading;
using Serilog;

namespace MorpheX.Services;

/// <summary>
/// Applies taskbar visual effects (Clear / Blur / Acrylic) to Shell_TrayWnd
/// and secondary monitor taskbars (Shell_SecondaryTrayWnd) using the
/// undocumented SetWindowCompositionAttribute API — the same technique
/// used by TranslucentTB.
///
/// Windows 11 aggressively resets taskbar composition every time the shell
/// repaints (window focus changes, app launches, etc.) so we use a short
/// refresh timer to keep the effect alive, just like TranslucentTB does.
/// </summary>
public sealed class TaskbarStyleService : IDisposable
{
    // ── Win32 ────────────────────────────────────────────────────────────────

    private const int WCA_ACCENT_POLICY = 19;

    private enum AccentState : int
    {
        ACCENT_DISABLED                   = 0,  // Default Windows style
        ACCENT_ENABLE_GRADIENT            = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,  // Clear (fully transparent)
        ACCENT_ENABLE_BLURBEHIND          = 3,  // Classic Aero Blur
        ACCENT_ENABLE_ACRYLICBLURBEHIND   = 4,  // Fluent Acrylic
        ACCENT_ENABLE_HOSTBACKDROP        = 5,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public uint        AccentFlags;    // 2 = draw on border / enable gradient rect
        public uint        GradientColor;  // AABBGGRR — alpha in high byte
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

    private TaskbarStyle     _current  = TaskbarStyle.Default;
    private DispatcherTimer? _refresher;
    private bool             _disposed;

    /// <summary>
    /// Apply the chosen style to the primary and all secondary taskbars.
    /// Starts a 900ms periodic refresh to keep the effect alive against
    /// Windows 11's aggressive shell repaints.
    /// Calling with <see cref="TaskbarStyle.Default"/> restores the native look.
    /// </summary>
    public void Apply(TaskbarStyle style)
    {
        _current = style;
        ApplyToAll(style);

        if (style == TaskbarStyle.Default)
        {
            StopRefresher();
        }
        else
        {
            StartRefresher();
        }
    }

    /// <summary>Restore the native Windows taskbar appearance and stop the refresher.</summary>
    public void Reset()
    {
        StopRefresher();
        _current = TaskbarStyle.Default;
        ApplyToAll(TaskbarStyle.Default);
    }

    // ── Refresher ────────────────────────────────────────────────────────────

    private void StartRefresher()
    {
        if (_refresher != null) return;  // already running

        _refresher = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(900)
        };
        _refresher.Tick += (_, _) =>
        {
            if (_current != TaskbarStyle.Default)
                ApplyToAll(_current);
        };
        _refresher.Start();
    }

    private void StopRefresher()
    {
        _refresher?.Stop();
        _refresher = null;
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
        // AccentFlags = 2 tells DWM to apply the accent to the entire window rectangle
        // GradientColor uses AABBGGRR byte order, NOT AARRGGBB.
        //   0x02000000 = barely-opaque black — the minimum needed to activate the Clear effect.
        //   0x01000000 = nearly-zero alpha tint for Blur (lets the blur through cleanly).
        //   0x44000000 = ~27% black tint for Acrylic — matches Windows frosted glass feel.
        var accent = style switch
        {
            TaskbarStyle.Clear   => new AccentPolicy
            {
                AccentState   = AccentState.ACCENT_ENABLE_TRANSPARENTGRADIENT,
                AccentFlags   = 2,
                GradientColor = 0x02000000,   // near-zero alpha to activate transparent mode
            },
            TaskbarStyle.Blur    => new AccentPolicy
            {
                AccentState   = AccentState.ACCENT_ENABLE_BLURBEHIND,
                AccentFlags   = 2,
                GradientColor = 0x01000000,   // minimal tint, full blur
            },
            TaskbarStyle.Acrylic => new AccentPolicy
            {
                AccentState   = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                AccentFlags   = 2,
                GradientColor = 0x44000000,   // ~27% opaque dark tint (frosted glass look)
            },
            _                    => new AccentPolicy
            {
                AccentState   = AccentState.ACCENT_DISABLED,
                AccentFlags   = 0,
                GradientColor = 0,
            }
        };

        var accentSize = Marshal.SizeOf<AccentPolicy>();
        var accentPtr  = Marshal.AllocHGlobal(accentSize);
        try
        {
            Marshal.StructureToPtr(accent, accentPtr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute  = WCA_ACCENT_POLICY,
                Data       = accentPtr,
                SizeOfData = accentSize
            };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(accentPtr);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { Reset(); } catch { }
    }
}
