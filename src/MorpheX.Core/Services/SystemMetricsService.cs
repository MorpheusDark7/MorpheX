using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;

namespace MorpheX.Core.Services;

public sealed record SystemMetrics
{
    public float CpuPercent { get; init; }
    public float GpuPercent { get; init; }
    public double RamUsedGb { get; init; }
    public double RamTotalGb { get; init; }
    public float RamPercent { get; init; }
    public long AppWorkingSetMb { get; init; }
}

public interface ISystemMetricsService : IDisposable
{
    void Start();
    void Stop();
    event EventHandler<SystemMetrics>? MetricsUpdated;
    SystemMetrics CurrentMetrics { get; }
}

public sealed class SystemMetricsService : ISystemMetricsService
{
    public event EventHandler<SystemMetrics>? MetricsUpdated;
    public SystemMetrics CurrentMetrics { get; private set; } = new();

    private Timer? _timer;
    private bool _isSampling;
    private bool _disposed;

    private ulong _prevIdleTime;
    private ulong _prevKernelTime;
    private ulong _prevUserTime;
    private bool _cpuInitialized;

    private readonly List<PerformanceCounter> _gpuCounters = new();
    private float _cachedGpuPercent = 0f;
    private bool _isGpuSampling;
    private DateTime _lastGpuSample = DateTime.MinValue;
    private DateTime _lastGpuCountersRefresh = DateTime.MinValue;

    public void Start()
    {
        if (_timer != null) return;

        SampleCpu();

        _timer = new Timer(async _ => await SampleAllMetricsAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1.0));
        Log.Debug("SystemMetricsService started");
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        DisposeGpuCounters();
        Log.Debug("SystemMetricsService stopped");
    }

    private async Task SampleAllMetricsAsync()
    {
        if (_isSampling || _disposed) return;
        _isSampling = true;

        try
        {
            float cpu = SampleCpu();
            (double ramUsed, double ramTotal, float ramPct) = SampleRam();
            long appRam = SampleAppWorkingSet();

            // Trigger non-blocking GPU sample every ~3s if not already sampling
            if (!_isGpuSampling && (DateTime.UtcNow - _lastGpuSample).TotalSeconds >= 3.0)
            {
                _isGpuSampling = true;
                _ = Task.Run(() =>
                {
                    try
                    {
                        _cachedGpuPercent = SampleGpu();
                        _lastGpuSample = DateTime.UtcNow;
                    }
                    catch { }
                    finally { _isGpuSampling = false; }
                });
            }

            CurrentMetrics = new SystemMetrics
            {
                CpuPercent = cpu,
                GpuPercent = _cachedGpuPercent,
                RamUsedGb = ramUsed,
                RamTotalGb = ramTotal,
                RamPercent = ramPct,
                AppWorkingSetMb = appRam
            };

            MetricsUpdated?.Invoke(this, CurrentMetrics);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "SystemMetricsService error during sampling");
        }
        finally
        {
            _isSampling = false;
        }
    }

    private float SampleCpu()
    {
        if (!GetSystemTimes(out var idleFt, out var kernelFt, out var userFt))
            return 0f;

        ulong currentIdle = ToUInt64(idleFt);
        ulong currentKernel = ToUInt64(kernelFt);
        ulong currentUser = ToUInt64(userFt);

        if (!_cpuInitialized)
        {
            _prevIdleTime = currentIdle;
            _prevKernelTime = currentKernel;
            _prevUserTime = currentUser;
            _cpuInitialized = true;
            return 0f;
        }

        ulong idleDelta = currentIdle - _prevIdleTime;
        ulong kernelDelta = currentKernel - _prevKernelTime;
        ulong userDelta = currentUser - _prevUserTime;

        _prevIdleTime = currentIdle;
        _prevKernelTime = currentKernel;
        _prevUserTime = currentUser;

        ulong totalTime = kernelDelta + userDelta;
        if (totalTime == 0) return 0f;

        ulong busyTime = totalTime > idleDelta ? totalTime - idleDelta : 0;
        float percent = (float)((double)busyTime / totalTime * 100.0);
        return Math.Clamp(percent, 0f, 100f);
    }

    private (double ramUsed, double ramTotal, float ramPercent) SampleRam()
    {
        var memStatus = new MEMORYSTATUSEX();
        if (!GlobalMemoryStatusEx(memStatus))
            return (0, 0, 0);

        double totalGb = memStatus.ullTotalPhys / (1024.0 * 1024 * 1024);
        double availGb = memStatus.ullAvailPhys / (1024.0 * 1024 * 1024);
        double usedGb = Math.Max(0, totalGb - availGb);
        // Use (used/total)*100 to match Task Manager exactly (dwMemoryLoad includes cache)
        float pct = totalGb > 0 ? (float)(usedGb / totalGb * 100.0) : 0f;

        return (Math.Round(usedGb, 1), Math.Round(totalGb, 1), Math.Clamp(pct, 0f, 100f));
    }

    private static long SampleAppWorkingSet()
    {
        try
        {
            return Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
        }
        catch
        {
            return 0;
        }
    }

    private float SampleGpu()
    {
        try
        {
            if ((DateTime.UtcNow - _lastGpuCountersRefresh).TotalSeconds > 45 || _gpuCounters.Count == 0)
            {
                RefreshGpuCounters();
            }

            if (_gpuCounters.Count == 0) return 0f;

            // Sum per-node counters (each represents a different node/engine type, not per-process)
            // Clamp to 100f in case multiple engines add up beyond
            float totalGpu = 0f;
            foreach (var counter in _gpuCounters)
            {
                try
                {
                    totalGpu += counter.NextValue();
                }
                catch
                {
                }
            }

            return Math.Clamp((float)Math.Round(totalGpu, 1), 0f, 100f);
        }
        catch
        {
            return 0f;
        }
    }

    private void RefreshGpuCounters()
    {
        DisposeGpuCounters();
        _lastGpuCountersRefresh = DateTime.UtcNow;

        try
        {
            // "GPU Engine" category has per-process per-engine instances; summing all leads to > 100%.
            // Instead, use "GPU Adapter Memory" or fall back to a single representative counter.
            // Best match for Task Manager: use one counter per unique GPU node (engtype_3D only)
            // and group by luid (adapter). We take one representative entry per luid.
            if (!PerformanceCounterCategory.Exists("GPU Engine")) return;

            var category = new PerformanceCounterCategory("GPU Engine");
            var instanceNames = category.GetInstanceNames();

            // Collect unique luid_phys_eng adapter+node combinations
            // Instance name format: pid_XXX_luid_0x000000000000XXXX_phys_X_eng_Y_engtype_3D
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in instanceNames)
            {
                if (!name.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Build a key that identifies the unique GPU node (luid + phys + eng), ignoring pid
                // so we get one entry per physical engine, not per process
                string nodeKey = name;
                int pidEnd = name.IndexOf('_', 4); // skip "pid_"
                if (pidEnd > 0)
                    nodeKey = name[(pidEnd + 1)..]; // strip "pid_XXX_" prefix

                // Only add one counter per unique GPU engine node
                if (seen.Contains(nodeKey)) continue;
                seen.Add(nodeKey);

                try
                {
                    var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", name, true);
                    counter.NextValue(); // prime
                    _gpuCounters.Add(counter);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "GPU performance counters unavailable");
        }
    }

    private void DisposeGpuCounters()
    {
        foreach (var counter in _gpuCounters)
        {
            try { counter.Dispose(); } catch { }
        }
        _gpuCounters.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    #region Win32 P/Invoke
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out System.Runtime.InteropServices.ComTypes.FILETIME lpIdleTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpKernelTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpUserTime);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
        public MEMORYSTATUSEX() { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    private static ulong ToUInt64(System.Runtime.InteropServices.ComTypes.FILETIME ft) =>
        ((ulong)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
    #endregion
}
