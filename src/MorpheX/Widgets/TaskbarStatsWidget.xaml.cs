using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using MorpheX.Core.Configuration;
using MorpheX.Core.Services;
using Application = System.Windows.Application;

namespace MorpheX.Widgets;

public partial class TaskbarStatsWidget : Window
{
    [DllImport("user32.dll")] private static extern IntPtr FindWindow(string cls, string? win);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }

    private readonly WidgetSettings _settings;
    private ISystemMetricsService? _metricsService;
    private PerformanceCounter? _diskCounter;

    public TaskbarStatsWidget(WidgetSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        Left = settings.TaskbarStatsX;
        Top  = settings.TaskbarStatsY;

        UpdateLockMenuHeader();

        // Initialize disk counter
        try
        {
            _diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total", true);
            _diskCounter.NextValue(); // first call always 0
        }
        catch { _diskCounter = null; }

        Loaded  += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        _metricsService = app.SystemMetricsService;
        _metricsService.MetricsUpdated += OnMetricsUpdated;
        _metricsService.Start();

        if (_metricsService.CurrentMetrics != null)
            ApplyMetrics(_metricsService.CurrentMetrics);
    }

    private void OnMetricsUpdated(object? sender, SystemMetrics m)
    {
        Dispatcher.InvokeAsync(() => ApplyMetrics(m));
    }

    private void ApplyMetrics(SystemMetrics m)
    {
        CpuText.Text = $"{m.CpuPercent:0}%";
        RamText.Text = $"{m.RamUsedGb:0.0} GB";
        GpuText.Text = $"{m.GpuPercent:0}%";

        // Disk (sampled separately — non-blocking)
        try
        {
            float disk = _diskCounter?.NextValue() ?? 0f;
            DiskText.Text = $"{Math.Clamp(disk, 0, 100):0}%";
        }
        catch { DiskText.Text = "–"; }
    }

    private void Widget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.WidgetsLocked) return;
        DragMove();
    }

    private void SnapToTaskbar_Click(object sender, RoutedEventArgs e)
    {
        // Find taskbar HWND and snap our widget just above it
        IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
        if (taskbarHwnd != IntPtr.Zero && GetWindowRect(taskbarHwnd, out RECT r))
        {
            // Position above the taskbar, right-aligned
            var screen = SystemParameters.PrimaryScreenWidth;
            var taskbarHeight = r.Bottom - r.Top;
            var taskbarTop    = r.Top;

            Left = screen - ActualWidth - 8;
            Top  = taskbarTop + (taskbarHeight - ActualHeight) / 2.0;
        }
        else
        {
            // Fallback: bottom-right corner
            Left = SystemParameters.PrimaryScreenWidth - ActualWidth - 8;
            Top  = SystemParameters.PrimaryScreenHeight - SystemParameters.PrimaryScreenHeight * 0.04 - ActualHeight / 2.0;
        }

        _settings.TaskbarStatsX = Left;
        _settings.TaskbarStatsY = Top;
        Save();
    }

    private void LockPosition_Click(object sender, RoutedEventArgs e)
    {
        _settings.WidgetsLocked = !_settings.WidgetsLocked;
        UpdateLockMenuHeader();
        Save();
    }

    private void UpdateLockMenuHeader()
    {
        LockMenuItem.Header = _settings.WidgetsLocked ? "🔓  Unlock Position" : "🔒  Lock Position";
    }

    private void CloseWidget_Click(object sender, RoutedEventArgs e)
    {
        _settings.TaskbarStatsEnabled = false;
        Save();
        Close();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_metricsService != null)
            _metricsService.MetricsUpdated -= OnMetricsUpdated;

        _diskCounter?.Dispose();

        _settings.TaskbarStatsX = Left;
        _settings.TaskbarStatsY = Top;
        Save();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        _settings.TaskbarStatsX = Left;
        _settings.TaskbarStatsY = Top;
    }

    private void Save()
    {
        var app = (App)Application.Current;
        _ = app.SettingsService.SaveAsync();
    }
}
