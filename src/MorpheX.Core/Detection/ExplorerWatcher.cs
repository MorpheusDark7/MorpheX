using System.Diagnostics;
using Serilog;

namespace MorpheX.Core.Detection;

public sealed class ExplorerWatcher : IDisposable
{
    private Timer? _timer;
    private int _lastExplorerPid;
    private bool _disposed;

    public event EventHandler? ExplorerRestarted;

    public void Start()
    {
        _lastExplorerPid = GetExplorerPid();
        _timer = new Timer(CheckExplorer, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        Log.Debug("Explorer watcher started. Current Explorer PID: {Pid}", _lastExplorerPid);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void CheckExplorer(object? state)
    {
        try
        {
            var currentPid = GetExplorerPid();

            if (currentPid != _lastExplorerPid && currentPid != 0)
            {
                Log.Information("Explorer.exe restarted. Old PID: {OldPid}, New PID: {NewPid}",
                    _lastExplorerPid, currentPid);
                _lastExplorerPid = currentPid;

                Thread.Sleep(1000);

                ExplorerRestarted?.Invoke(this, EventArgs.Empty);
            }
            else if (currentPid != 0)
            {
                _lastExplorerPid = currentPid;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Explorer watch cycle error (non-fatal)");
        }
    }

    private static int GetExplorerPid()
    {
        try
        {
            var explorers = Process.GetProcessesByName("explorer");
            return explorers.Length > 0 ? explorers[0].Id : 0;
        }
        catch
        {
            return 0;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
