using Microsoft.Win32;
using Serilog;

namespace MorpheX.Core.Services;

public interface IStartupService
{
    bool IsRegistered { get; }
    void Register();
    void Unregister();
}

public sealed class StartupService : IStartupService
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "MorpheX";

    public bool IsRegistered
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                return key?.GetValue(AppName) != null;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to check startup registration");
                return false;
            }
        }
    }

    public void Register()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                Log.Error("Cannot register startup: unable to determine executable path");
                return;
            }

            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            key.SetValue(AppName, $"\"{exePath}\" --minimized");

            Log.Information("Registered MorpheX for startup: {Path}", exePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to register startup");
        }
    }

    public void Unregister()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key?.GetValue(AppName) != null)
            {
                key.DeleteValue(AppName);
                Log.Information("Unregistered MorpheX from startup");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to unregister startup");
        }
    }
}
