using Serilog;
using Serilog.Events;

namespace MorpheX.Core.Logging;

public static class LogManager
{
    private static bool _initialized;

    public static void Initialize(bool enableFileLogging = true, string? logDirectory = null)
    {
        if (_initialized) return;

        var logDir = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MorpheX", "logs");

        var config = new LoggerConfiguration()
            .MinimumLevel.Information()
#if DEBUG
            .MinimumLevel.Debug()
#endif
            .Enrich.WithProperty("Application", "MorpheX");

        if (enableFileLogging)
        {
            Directory.CreateDirectory(logDir);
            config.WriteTo.File(
                path: Path.Combine(logDir, "morphex-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                restrictedToMinimumLevel: LogEventLevel.Information,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
        }

        Log.Logger = config.CreateLogger();
        _initialized = true;

        Log.Information("MorpheX logging initialized. Log directory: {LogDir}", logDir);
    }

    public static void Shutdown()
    {
        Log.Information("MorpheX shutting down logging");
        Log.CloseAndFlush();
        _initialized = false;
    }
}
