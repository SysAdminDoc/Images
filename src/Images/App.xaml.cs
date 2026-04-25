using System.Windows;
using System.Windows.Threading;
using Images.Services;
using Microsoft.Extensions.Logging;

namespace Images;

public partial class App : Application
{
    private readonly Microsoft.Extensions.Logging.ILogger _log = Log.For<App>();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var info = AppInfo.Current;
        _log.LogInformation("Images {Version} starting — {Runtime} on {Os}",
            info.DisplayVersion, info.RuntimeDescription, info.OsDescription);

        // V15-09 + V02-06 + V02-07: fatal-exception channels go through both the structured
        // logger (for day-to-day diagnostics via Serilog rolling file) AND CrashLog (for the
        // plain-text dump + minidump + user-facing "Copy details" dialog). CrashLog is the
        // user-actionable surface; Log.For<App>() is the forensic surface.
        DispatcherUnhandledException += (_, args) =>
        {
            _log.LogError(args.Exception, "DispatcherUnhandledException");
            CrashLog.Append("DispatcherUnhandledException", args.Exception);
            var dumpPath = CrashLog.TryWriteMiniDump();
            CrashDialog.Show(args.Exception, dumpPath);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            _log.LogCritical(ex, "AppDomain.UnhandledException (IsTerminating={IsTerminating})", args.IsTerminating);
            CrashLog.Append("AppDomain.UnhandledException", ex, $"IsTerminating={args.IsTerminating}");
            if (args.IsTerminating)
            {
                CrashLog.TryWriteMiniDump();
                Log.Shutdown(); // flush Serilog buffers before the runtime tears us down
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _log.LogError(args.Exception, "TaskScheduler.UnobservedTaskException");
            CrashLog.Append("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        Exit += (_, _) => Log.Shutdown();

        var window = new MainWindow();
        window.Show();

        if (e.Args.Length > 0 && TryResolveArgPath(e.Args[0], out var resolved))
            window.OpenPath(resolved);
    }

    private static bool TryResolveArgPath(string raw, out string resolved)
    {
        resolved = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        // Reject device-namespace / UNC-style / registry-ish shapes before any Path API touches them.
        if (raw.StartsWith("\\\\?\\", StringComparison.Ordinal) ||
            raw.StartsWith("\\\\.\\", StringComparison.Ordinal))
            return false;

        string full;
        try { full = System.IO.Path.GetFullPath(raw); }
        catch { return false; }

        // GetFullPath resolves "..\..\whatever" into a canonical absolute path.
        // The File.Exists check then pins the outcome to a real file on disk;
        // the viewer's charter is to display a single image, not enumerate a tree.
        if (!System.IO.File.Exists(full)) return false;

        resolved = full;
        return true;
    }
}
