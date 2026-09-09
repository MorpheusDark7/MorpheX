using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Serilog;

namespace MorpheX.Core.Desktop;

public sealed class WallpaperHostWindow : IDisposable
{
    private const string WindowClassName = "MorpheXWallpaperHost";
    private static bool _classRegistered;
    private static readonly object _classLock = new();
    private static readonly ConcurrentDictionary<IntPtr, Action<IntPtr>> _paintHandlers = new();

    private static NativeMethods.WndProc? _wndProcDelegate;

    private IntPtr _handle;
    private bool _disposed;

    public IntPtr Handle => _handle;

    public bool IsCreated => _handle != IntPtr.Zero && NativeMethods.IsWindow(_handle);

    public static void SetPaintHandler(IntPtr hWnd, Action<IntPtr>? handler)
    {
        if (hWnd == IntPtr.Zero) return;
        if (handler != null)
        {
            _paintHandlers[hWnd] = handler;
        }
        else
        {
            _paintHandlers.TryRemove(hWnd, out _);
        }
    }

    public bool Create(IntPtr workerWHandle, int x, int y, int width, int height)
    {
        if (_handle != IntPtr.Zero)
        {
            Log.Warning("WallpaperHostWindow.Create called but window already exists");
            return false;
        }

        EnsureClassRegistered();

        uint exStyle = NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT;
        uint style = NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE | NativeMethods.WS_CLIPCHILDREN | NativeMethods.WS_CLIPSIBLINGS;

        int clientX = x;
        int clientY = y;
        if (workerWHandle != IntPtr.Zero)
        {
            var pt = new NativeMethods.POINT { X = x, Y = y };
            if (NativeMethods.ScreenToClient(workerWHandle, ref pt))
            {
                clientX = pt.X;
                clientY = pt.Y;
            }
        }

        _handle = NativeMethods.CreateWindowEx(
            exStyle,
            WindowClassName,
            "MorpheX Wallpaper",
            style,
            clientX, clientY, width, height,
            workerWHandle != IntPtr.Zero ? workerWHandle : IntPtr.Zero,
            IntPtr.Zero,
            NativeMethods.GetModuleHandle(null),
            IntPtr.Zero);

        if (_handle == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            Log.Error("Failed to create wallpaper host window. Win32 error: {Error}", error);
            return false;
        }

        NativeMethods.SetLayeredWindowAttributes(_handle, 0, 255, NativeMethods.LWA_ALPHA);

        if (workerWHandle != IntPtr.Zero)
        {
            var prevParent = NativeMethods.SetParent(_handle, workerWHandle);
            Log.Debug("Parented host window 0x{Handle:X} to WorkerW 0x{WorkerW:X} (prev parent: 0x{Prev:X})",
                _handle, workerWHandle, prevParent);

            NativeMethods.SetWindowPos(_handle, IntPtr.Zero, clientX, clientY, width, height,
                NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
        }
        else
        {
            NativeMethods.SetWindowPos(_handle, new IntPtr(1) , x, y, width, height,
                NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
        }

        NativeMethods.UpdateWindow(_handle);

        Log.Information("Created wallpaper host window 0x{Handle:X} at Screen({X},{Y}) Client({CX},{CY}) {W}x{H} (Parent: 0x{Parent:X})",
            _handle, x, y, clientX, clientY, width, height, workerWHandle);
        return true;
    }

    public void Repaint()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.InvalidateRect(_handle, IntPtr.Zero, false);
            NativeMethods.UpdateWindow(_handle);
        }
    }

    public void Reposition(int x, int y, int width, int height)
    {
        if (_handle == IntPtr.Zero) return;

        var parent = NativeMethods.GetParent(_handle);
        int clientX = x;
        int clientY = y;
        if (parent != IntPtr.Zero)
        {
            var pt = new NativeMethods.POINT { X = x, Y = y };
            if (NativeMethods.ScreenToClient(parent, ref pt))
            {
                clientX = pt.X;
                clientY = pt.Y;
            }
        }

        var insertAfter = parent != IntPtr.Zero ? IntPtr.Zero  : new IntPtr(1) ;

        NativeMethods.SetWindowPos(_handle, insertAfter, clientX, clientY, width, height,
            NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOACTIVATE);
        NativeMethods.InvalidateRect(_handle, IntPtr.Zero, false);
        NativeMethods.UpdateWindow(_handle);

        Log.Debug("Repositioned wallpaper host 0x{Handle:X} to Screen({X},{Y}) Client({CX},{CY}) {W}x{H}",
            _handle, x, y, clientX, clientY, width, height);
    }

    public bool Reparent(IntPtr newWorkerWHandle)
    {
        if (_handle == IntPtr.Zero) return false;

        var result = NativeMethods.SetParent(_handle, newWorkerWHandle);
        if (result == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            Log.Error("Failed to reparent wallpaper host. Win32 error: {Error}", error);
            return false;
        }

        var style = NativeMethods.GetWindowLong(_handle, NativeMethods.GWL_STYLE);
        style = (int)((style & ~NativeMethods.WS_POPUP) | NativeMethods.WS_CHILD);
        NativeMethods.SetWindowLong(_handle, NativeMethods.GWL_STYLE, style);

        NativeMethods.ShowWindow(_handle, NativeMethods.SW_SHOW);
        NativeMethods.UpdateWindow(_handle);

        Log.Information("Reparented wallpaper host 0x{Handle:X} to WorkerW 0x{WorkerW:X}",
            _handle, newWorkerWHandle);
        return true;
    }

    private static void EnsureClassRegistered()
    {
        lock (_classLock)
        {
            if (_classRegistered) return;

            _wndProcDelegate = WndProcHandler;

            var wc = new NativeMethods.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
                style = 0,
                lpfnWndProc = _wndProcDelegate,
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = NativeMethods.GetModuleHandle(null),
                hIcon = IntPtr.Zero,
                hCursor = IntPtr.Zero,
                hbrBackground = IntPtr.Zero,
                lpszMenuName = null,
                lpszClassName = WindowClassName,
                hIconSm = IntPtr.Zero
            };

            var atom = NativeMethods.RegisterClassEx(ref wc);
            if (atom == 0)
            {
                var error = Marshal.GetLastWin32Error();
                if (error != 1410)
                {
                    Log.Error("Failed to register window class. Win32 error: {Error}", error);
                    throw new InvalidOperationException($"Failed to register window class: Win32 error {error}");
                }
            }

            _classRegistered = true;
            Log.Debug("Registered wallpaper host window class: {ClassName}", WindowClassName);
        }
    }

    private static IntPtr WndProcHandler(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case NativeMethods.WM_NCHITTEST:
                return (IntPtr)NativeMethods.HTTRANSPARENT;

            case NativeMethods.WM_ERASEBKGND:
                return (IntPtr)1;

            case NativeMethods.WM_PAINT:
                var hdc = NativeMethods.BeginPaint(hWnd, out var ps);
                try
                {
                    if (_paintHandlers.TryGetValue(hWnd, out var paintAction))
                    {
                        paintAction(hdc);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Exception inside WallpaperHostWindow paint callback");
                }
                finally
                {
                    NativeMethods.EndPaint(hWnd, ref ps);
                }
                return IntPtr.Zero;

            default:
                return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != IntPtr.Zero)
        {
            _paintHandlers.TryRemove(_handle, out _);
            NativeMethods.DestroyWindow(_handle);
            Log.Debug("Destroyed wallpaper host window 0x{Handle:X}", _handle);
            _handle = IntPtr.Zero;
        }
    }
}
