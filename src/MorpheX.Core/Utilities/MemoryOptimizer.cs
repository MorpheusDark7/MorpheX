using System.Diagnostics;
using System.Runtime;
using MorpheX.Core.Desktop;
using Serilog;

namespace MorpheX.Core.Utilities;

public static class MemoryOptimizer
{
    private static long _lastTrimTimestamp;
    private static readonly object _lock = new();

    public static void TrimWorkingSet(bool force = false)
    {
        lock (_lock)
        {
            var now = Stopwatch.GetTimestamp();
            if (!force && _lastTrimTimestamp > 0)
            {
                var elapsedSeconds = (now - _lastTrimTimestamp) / (double)Stopwatch.Frequency;
                if (elapsedSeconds < 5.0)
                    return;
            }
            _lastTrimTimestamp = now;
        }

        try
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();

            using var currentProcess = Process.GetCurrentProcess();
            NativeMethods.EmptyWorkingSet(currentProcess.Handle);
            Log.Debug("Working set memory trimmed successfully");
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Memory trimming skipped");
        }
    }
}
