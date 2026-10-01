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
    public float DiskPercent { get; init; }
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

    private static readonly System.Text.RegularExpressions.Regex LuidRegex =
        new(@"luid_(0x[0-9a-fA-F]+_0x[0-9a-fA-F]+)", System.Text.RegularExpressions.RegexOptions.Compiled);
    private readonly List<(string adapter, PerformanceCounter counter)> _gpuCounters = new();
    private float _cachedGpuPercent = 0f;
    private bool _isGpuSampling;
    private DateTime _lastGpuSample = DateTime.MinValue;
    private DateTime _lastGpuCountersRefresh = DateTime.MinValue;
    private PerformanceCounter? _diskCounter;

    public void Start()
    {
        if (_timer != null) return;

        SampleCpu();
        InitDiskCounter();

        _timer = new Timer(async _ => await SampleAllMetricsAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1.0));
        Log.Debug("SystemMetricsService started");
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        DisposeGpuCounters();
        DisposeDiskCounter();
        Log.Debug("SystemMetricsService stopped");
    }

    private void InitDiskCounter()
    {
        if (_diskCounter != null) return;
        try
        {
            _diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total", true);
            _diskCounter.NextValue();
        }
        catch { _diskCounter = null; }
    }

    private void DisposeDiskCounter()
    {
        try { _diskCounter?.Dispose(); } catch { }
        _diskCounter = null;
    }

    private float SampleDisk()
    {
        if (_diskCounter == null)
        {
            InitDiskCounter();
            if (_diskCounter == null) return 0f;
        }

        try
        {
            return (float)Math.Clamp(_diskCounter.NextValue(), 0.0, 100.0);
        }
        catch { return 0f; }
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
            float disk = SampleDisk();

            // Trigger non-blocking GPU sample every ~1.5s if not already sampling
            if (!_isGpuSampling && (DateTime.UtcNow - _lastGpuSample).TotalSeconds >= 1.5)
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
                DiskPercent = disk,
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
            if ((DateTime.UtcNow - _lastGpuCountersRefresh).TotalSeconds > 30 || _gpuCounters.Count == 0)
            {
                RefreshGpuCounters();
            }

            if (_gpuCounters.Count == 0) return 0f;

            // Group utilization per GPU adapter LUID so multi-GPU setups accurately report the active GPU
            var adapterSums = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            for (int i = _gpuCounters.Count - 1; i >= 0; i--)
            {
                var (adapter, counter) = _gpuCounters[i];
                try
                {
                    float val = counter.NextValue();
                    adapterSums[adapter] = adapterSums.GetValueOrDefault(adapter) + val;
                }
                catch
                {
                    try { counter.Dispose(); } catch { }
                    _gpuCounters.RemoveAt(i);
                }
            }

            // Return the highest utilization among the GPUs (matching Task Manager for multi-GPU)
            float maxGpu = 0f;
            foreach (var kvp in adapterSums)
            {
                if (kvp.Value > maxGpu)
                    maxGpu = kvp.Value;
            }

            return Math.Clamp((float)Math.Round(maxGpu, 1), 0f, 100f);
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
            if (!PerformanceCounterCategory.Exists("GPU Engine")) return;

            var category = new PerformanceCounterCategory("GPU Engine");
            var instanceNames = category.GetInstanceNames();

            // Track 3D engine instances across all active processes and adapters
            foreach (var name in instanceNames)
            {
                if (!name.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                    continue;

                string adapter = "default";
                var match = LuidRegex.Match(name);
                if (match.Success)
                    adapter = match.Groups[1].Value;

                try
                {
                    var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", name, true);
                    counter.NextValue(); // prime
                    _gpuCounters.Add((adapter, counter));
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
        foreach (var (_, counter) in _gpuCounters)
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
