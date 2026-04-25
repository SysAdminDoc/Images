using System.IO;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Images.Services;

/// <summary>
/// V02-06 / O-01: structured logging on top of Serilog, surfaced via
/// <see cref="ILogger"/>. Rolling file lives at
/// <c>%LOCALAPPDATA%\Images\Logs\images-<yyyy-MM-dd>.log</c> with 14-day retention. Callers
/// request a typed logger via <see cref="For{T}"/> or the generic <see cref="Get"/>; no DI
/// container needed for a single-assembly viewer.
/// </summary>
public static class Log
{
    private static readonly Lazy<ILoggerFactory> _factory = new(Build);

    private static ILoggerFactory Build()
    {
        var config = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext();

        var logDir = AppStorage.TryGetAppDirectory("Logs");
        if (logDir is not null)
        {
            var logPath = Path.Combine(logDir, "images-.log"); // "-" lets RollingInterval.Day append yyyyMMdd
            config = config.WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true, // multiple instances (rare for a viewer, but safe default)
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} — {Message:lj}{NewLine}{Exception}");
        }

        var serilog = config.CreateLogger();

        // Bridge Serilog into Microsoft.Extensions.Logging so call-sites use the abstract
        // ILogger<T> instead of pinning on Serilog's API. If we ever swap sinks later, no
        // call-site changes.
        return new SerilogLoggerFactory(serilog, dispose: true);
    }

    public static ILogger<T> For<T>() => _factory.Value.CreateLogger<T>();

    public static ILogger Get(string categoryName) => _factory.Value.CreateLogger(categoryName);

    /// <summary>Dispose Serilog at app exit to flush buffered writes.</summary>
    public static void Shutdown()
    {
        if (_factory.IsValueCreated) _factory.Value.Dispose();
    }
}
