using Serilog;

namespace MorpheX.Core.Desktop;

public sealed class WorkerWInterop
{
    private IntPtr _workerWHandle;
    private IntPtr _progmanHandle;

    public IntPtr WorkerWHandle => _workerWHandle;

    public bool IsAttached => _workerWHandle != IntPtr.Zero && NativeMethods.IsWindow(_workerWHandle);

    public bool Initialize()
    {
        Log.Information("Initializing WorkerW desktop integration");

        _progmanHandle = NativeMethods.FindWindow("Progman", null);
        if (_progmanHandle == IntPtr.Zero)
        {
            Log.Error("Failed to find Progman window");
            return false;
        }
        Log.Debug("Found Progman: 0x{Handle:X}", _progmanHandle);

        NativeMethods.SendMessageTimeout(
            _progmanHandle,
            0x052C,
            new IntPtr(0x0000000D),
            new IntPtr(1),
            NativeMethods.SMTO_NORMAL,
            1000,
            out _);

        NativeMethods.SendMessageTimeout(
            _progmanHandle,
            0x052C,
            IntPtr.Zero,
            IntPtr.Zero,
            NativeMethods.SMTO_NORMAL,
            1000,
            out _);

        Thread.Sleep(150);

        Log.Debug("Sent WorkerW spawn message to Progman");

        _workerWHandle = FindDesktopWorkerW();

        if (_workerWHandle == IntPtr.Zero)
        {
            Log.Error("Failed to find WorkerW window after spawn message");
            return false;
        }

        Log.Information("WorkerW integration initialized. Handle: 0x{Handle:X}", _workerWHandle);
        return true;
    }

    public bool Reinitialize()
    {
        Log.Information("Re-initializing WorkerW (Explorer may have restarted)");
        _workerWHandle = IntPtr.Zero;
        _progmanHandle = IntPtr.Zero;
        return Initialize();
    }

    private IntPtr FindDesktopWorkerW()
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (attempt > 0)
            {
                Log.Debug("WorkerW search attempt {Attempt}/5...", attempt + 1);
                Thread.Sleep(200);

                NativeMethods.SendMessageTimeout(
                    _progmanHandle, 0x052C,
                    new IntPtr(0xD), new IntPtr(1),
                    NativeMethods.SMTO_NORMAL, 1000, out _);
            }

            var childWorkerW = NativeMethods.FindWindowEx(_progmanHandle, IntPtr.Zero, "WorkerW", null);
            if (childWorkerW != IntPtr.Zero)
            {
                Log.Information("Found WorkerW child of Progman: 0x{Handle:X}", childWorkerW);
                NativeMethods.SetWindowPos(childWorkerW, new IntPtr(1) , 0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
                NativeMethods.UpdateWindow(childWorkerW);
                return childWorkerW;
            }

            IntPtr targetWorkerW = IntPtr.Zero;
            IntPtr defViewParent = IntPtr.Zero;

            NativeMethods.EnumWindows((hWnd, _) =>
            {
                var defView = NativeMethods.FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (defView != IntPtr.Zero)
                {
                    defViewParent = hWnd;

                    var childW = NativeMethods.FindWindowEx(hWnd, IntPtr.Zero, "WorkerW", null);
                    if (childW != IntPtr.Zero)
                    {
                        targetWorkerW = childW;
                        return false;
                    }

                    var siblingW = NativeMethods.FindWindowEx(IntPtr.Zero, hWnd, "WorkerW", null);
                    if (siblingW != IntPtr.Zero)
                    {
                        targetWorkerW = siblingW;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);

            if (targetWorkerW != IntPtr.Zero)
            {
                NativeMethods.SetWindowPos(targetWorkerW, new IntPtr(1) , 0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
                NativeMethods.UpdateWindow(targetWorkerW);
                Log.Information("Found desktop WorkerW: 0x{Handle:X} (parent of icons: 0x{Parent:X})",
                    targetWorkerW, defViewParent);
                return targetWorkerW;
            }
        }

        Log.Warning("Could not locate desktop WorkerW after 5 attempts, falling back to Progman 0x{Handle:X}", _progmanHandle);
        return _progmanHandle;
    }
}
