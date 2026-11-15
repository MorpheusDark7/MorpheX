using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Serilog;

namespace MorpheX.Services;

/// <summary>
/// WPF only sends WM_WINDOWPOSCHANGED to the window whose own position changed.
/// Listen for system window visibility/minimize/move events so the Clock can repaint
/// after another window has stopped covering it.
/// </summary>
internal sealed class ClockSurfaceRecovery : IDisposable
{
    private const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;
    private const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
    private const uint EVENT_OBJECT_SHOW = 0x8002;
    private const uint EVENT_OBJECT_HIDE = 0x8003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const int OBJID_WINDOW = 0;

    private readonly Window _window;
    private readonly IntPtr _clockHwnd;
    private readonly WinEventDelegate _callback;
    private readonly DispatcherTimer _refreshTimer;
    private readonly IntPtr _systemHook;
    private readonly IntPtr _visibilityHook;
    private bool _disposed;

    public ClockSurfaceRecovery(Window window, IntPtr clockHwnd)
    {
        _window = window;
        _clockHwnd = clockHwnd;
        _callback = OnWinEvent;
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(140)
        };
        _refreshTimer.Tick += OnRefreshTimerTick;

        _systemHook = SetWinEventHook(
            EVENT_SYSTEM_MOVESIZEEND,
            EVENT_SYSTEM_MINIMIZEEND,
            IntPtr.Zero,
            _callback,
            0,
            0,
            WINEVENT_OUTOFCONTEXT);
        _visibilityHook = SetWinEventHook(
            EVENT_OBJECT_SHOW,
            EVENT_OBJECT_HIDE,
            IntPtr.Zero,
            _callback,
            0,
            0,
            WINEVENT_OUTOFCONTEXT);

        if (_systemHook == IntPtr.Zero || _visibilityHook == IntPtr.Zero)
            Log.Warning("Clock surface recovery could not install all Windows event hooks");
    }

    private void OnWinEvent(
        IntPtr hook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint eventThread,
        uint eventTime)
    {
        if (_disposed || hwnd == IntPtr.Zero || hwnd == _clockHwnd)
            return;

        if ((eventType == EVENT_OBJECT_SHOW || eventType == EVENT_OBJECT_HIDE)
            && (idObject != OBJID_WINDOW || idChild != 0))
            return;

        try
        {
            _window.Dispatcher.BeginInvoke(() =>
            {
                if (!_disposed && _window.IsVisible)
                {
                    _refreshTimer.Stop();
                    _refreshTimer.Start();
                }
            }, DispatcherPriority.Background);
        }
        catch (InvalidOperationException)
        {
            // The Clock may be closing while the out-of-context event is delivered.
        }
    }

    private void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        _refreshTimer.Stop();
        if (_disposed || !_window.IsVisible || HwndSource.FromHwnd(_clockHwnd) == null)
            return;

        WidgetWindowHelper.RefreshWidgetSurface(_window);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _refreshTimer.Stop();

        if (_systemHook != IntPtr.Zero) UnhookWinEvent(_systemHook);
        if (_visibilityHook != IntPtr.Zero) UnhookWinEvent(_visibilityHook);

        GC.KeepAlive(_callback);
    }

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint eventThread,
        uint eventTime);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
}
