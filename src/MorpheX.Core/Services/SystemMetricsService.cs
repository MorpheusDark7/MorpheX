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
    private DateTime _lastGpuCountersRefresh = DateTime.MinValue;

    public void Start()
    {
        if (_timer != null) return;

        SampleCpu();

        _timer = new Timer(async _ => await SampleAllMetricsAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(2.5));
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
            await Task.Run(() =>
            {
                float cpu = SampleCpu();
                (double ramUsed, double ramTotal, float ramPct) = SampleRam();
                long appRam = SampleAppWorkingSet();
                float gpu = SampleGpu();

                CurrentMetrics = new SystemMetrics
                {
                    CpuPercent = cpu,
                    GpuPercent = gpu,
                    RamUsedGb = ramUsed,
                    RamTotalGb = ramTotal,
                    RamPercent = ramPct,
                    AppWorkingSetMb = appRam
                };

                MetricsUpdated?.Invoke(this, CurrentMetrics);
            });
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
        float pct = memStatus.dwMemoryLoad;

        return (Math.Round(usedGb, 1), Math.Round(totalGb, 1), pct);
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
            if (!PerformanceCounterCategory.Exists("GPU Engine")) return;

            var category = new PerformanceCounterCategory("GPU Engine");
            var instanceNames = category.GetInstanceNames();

            foreach (var name in instanceNames)
            {
                if (name.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("engtype_VideoDecode", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", name, true);
                        counter.NextValue();
                        _gpuCounters.Add(counter);
                    }
                    catch { }
                }
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
