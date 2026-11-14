using System.Diagnostics;
using Serilog;

namespace MorpheX.Core.Utilities;

/// <summary>
/// Provides safe, non-blocking memory management.
/// Avoids aggressive EmptyWorkingSet calls that cause hard page faults,
/// video playback stutter/blurring, and DirectX/WPF surface invalidation.
/// </summary>
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
                if (elapsedSeconds < 60.0)
                    return;
            }
            _lastTrimTimestamp = now;
        }

        try
        {
            // Standard non-blocking garbage collection without hard page-fault inducing EmptyWorkingSet
            GC.Collect(2, GCCollectionMode.Default, blocking: false);
            Log.Debug("Memory cleanup performed (force={Force})", force);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Memory trimming skipped");
        }
    }
}
