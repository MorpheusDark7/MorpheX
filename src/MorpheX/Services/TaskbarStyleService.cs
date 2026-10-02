using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Win32;
using Serilog;

namespace MorpheX.Services;

/// <summary>
/// Applies taskbar visual effects using a multi-layer approach:
///
///   Layer 1 – SetWindowCompositionAttribute (SWCA): Works on Windows 10 and
///              Windows 11 builds prior to 22H2 (build 22621). Also works when
///              ExplorerPatcher is installed. Kept for best-effort compatibility.
///
///   Layer 2 – Registry EnableTransparency: Toggles the system "Transparency effects"
///              setting (same as Settings › Personalization › Colors › Transparency).
///              This engages native OS blur on Start, Action Center, and gives SWCA
///              a better baseline to work from.
///
/// On Windows 11 22H2+ (build ≥ 22621) the XAML-hosted taskbar ignores SWCA
/// applied to the Shell_TrayWnd parent. The registry toggle still provides some
/// visual improvement and is fully safe.
///
/// A 900 ms refresh timer keeps SWCA alive against shell repaints.
/// </summary>
public sealed class TaskbarStyleService : IDisposable
{
    // ── Win32 ────────────────────────────────────────────────────────────────

    private const int WCA_ACCENT_POLICY = 19;

    private enum AccentState : int
    {
        ACCENT_DISABLED                   = 0,
        ACCENT_ENABLE_GRADIENT            = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,   // Clear / fully transparent
        ACCENT_ENABLE_BLURBEHIND          = 3,   // Classic Aero blur
        ACCENT_ENABLE_ACRYLICBLURBEHIND   = 4,   // Fluent Acrylic
        ACCENT_ENABLE_HOSTBACKDROP        = 5,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public uint        AccentFlags;    // 2 = apply to full window rect
        public uint        GradientColor;  // AABBGGRR byte order
        public int         AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int    Attribute;
        public IntPtr Data;
        public int    SizeOfData;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowCompositionAttribute(
        IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(
        IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    // ── Windows 11 Detection ─────────────────────────────────────────────────

    /// <summary>
    /// True on Windows 11 22H2 and later (build ≥ 22621) where the taskbar is
    /// XAML-hosted and ignores SetWindowCompositionAttribute on the parent HWND.
    /// On these builds, the registry EnableTransparency toggle still has effect.
    /// </summary>
    public static bool IsModernWindows11 { get; } = CheckIsModernWindows11();

    private static bool CheckIsModernWindows11()
    {
        var v = Environment.OSVersion.Version;
        // Win11 starts at build 22000; XAML taskbar locked down from build 22621 (22H2)
        return v.Major == 10 && v.Build >= 22621;
    }

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
        Acrylic  = 3,
    }

    private TaskbarStyle     _current  = TaskbarStyle.Default;
    private DispatcherTimer? _refresher;
    private bool             _disposed;

    /// <summary>
    /// Apply the chosen style to the primary and all secondary taskbars.
    /// Also toggles the OS "Transparency effects" registry setting.
    /// Starts a 900 ms refresh timer to fight shell repaints.
    /// Calling with <see cref="TaskbarStyle.Default"/> restores the native look.
    /// </summary>
    public void Apply(TaskbarStyle style)
    {
        _current = style;
        ApplyAll(style);

        if (style == TaskbarStyle.Default)
            StopRefresher();
        else
            StartRefresher();
    }

    /// <summary>Restore the native Windows taskbar appearance and stop the refresher.</summary>
    public void Reset()
    {
        StopRefresher();
        _current = TaskbarStyle.Default;
        ApplyAll(TaskbarStyle.Default);
    }

    // ── Refresher ────────────────────────────────────────────────────────────

    private void StartRefresher()
    {
        if (_refresher != null) return;

        _refresher = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _refresher.Tick += (_, _) =>
        {
            if (_current != TaskbarStyle.Default)
                ApplySwca(_current);   // registry is already set; just keep SWCA alive
        };
        _refresher.Start();
    }

    private void StopRefresher()
    {
        _refresher?.Stop();
        _refresher = null;
    }

    // ── Implementation ───────────────────────────────────────────────────────

    private static void ApplyAll(TaskbarStyle style)
    {
        // Layer 1 – Registry "EnableTransparency" toggle
        SetRegistryTransparency(style != TaskbarStyle.Default);

        // Layer 2 – SetWindowCompositionAttribute on every taskbar HWND
        ApplySwca(style);
    }

    /// <summary>
    /// Toggle the Windows "Transparency effects" setting via the registry.
    /// Equivalent to Settings → Personalization → Colors → Transparency effects.
    /// Does not require elevation; takes effect immediately for most shell elements.
    /// </summary>
    private static void SetRegistryTransparency(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                writable: true);
            if (key == null) return;

            key.SetValue("EnableTransparency", enable ? 1 : 0, RegistryValueKind.DWord);
            Log.Debug("TaskbarStyleService: EnableTransparency={Enable}", enable);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TaskbarStyleService: could not write EnableTransparency registry key");
        }
    }

    private static void ApplySwca(TaskbarStyle style)
    {
        try
        {
            var primary = FindWindow("Shell_TrayWnd", null);
            if (primary != IntPtr.Zero)
                SetStyleOnHwnd(primary, style);

            var secondary = IntPtr.Zero;
            while (true)
            {
                secondary = FindWindowEx(IntPtr.Zero, secondary, "Shell_SecondaryTrayWnd", null);
                if (secondary == IntPtr.Zero) break;
                SetStyleOnHwnd(secondary, style);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TaskbarStyleService: SWCA apply failed for style {Style}", style);
        }
    }

    private static void SetStyleOnHwnd(IntPtr hwnd, TaskbarStyle style)
    {
        // GradientColor is AABBGGRR (NOT AARRGGBB).
        // AccentFlags = 2 → apply accent to the full window rectangle.
        var accent = style switch
        {
            TaskbarStyle.Clear   => new AccentPolicy
            {
                AccentState   = AccentState.ACCENT_ENABLE_TRANSPARENTGRADIENT,
                AccentFlags   = 2,
                GradientColor = 0x02000000,   // near-zero alpha, activates transparent mode
            },
            TaskbarStyle.Blur    => new AccentPolicy
            {
                AccentState   = AccentState.ACCENT_ENABLE_BLURBEHIND,
                AccentFlags   = 2,
                GradientColor = 0x01000000,   // minimal tint, maximum blur
            },
            TaskbarStyle.Acrylic => new AccentPolicy
            {
                AccentState   = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                AccentFlags   = 2,
                GradientColor = 0x441A1A1A,   // ~27% dark tint (frosted glass feel)
            },
            _                    => new AccentPolicy
            {
                AccentState   = AccentState.ACCENT_DISABLED,
                AccentFlags   = 0,
                GradientColor = 0,
            }
        };

        int size = Marshal.SizeOf<AccentPolicy>();
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, ptr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute  = WCA_ACCENT_POLICY,
                Data       = ptr,
                SizeOfData = size
            };
            int result = SetWindowCompositionAttribute(hwnd, ref data);
            if (result == 0)
                Log.Debug("TaskbarStyleService: SWCA returned 0 for hwnd={Hwnd:X} — may be ignored by Win11 XAML shell", hwnd);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { Reset(); } catch { }
    }
}
