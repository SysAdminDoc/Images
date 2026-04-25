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

        // V20-37: CLI report modes (--system-info / --codec-report / --version / --help).
        // Resolved BEFORE codec runtime configuration so a missing/broken codec doesn't take
        // the diagnostic surface down with it; CliReport.Run reaches into CodecRuntime.Status
        // which configures lazily on first access. Once a CLI mode runs we exit immediately —
        // no MainWindow, no FSW, no SQLite session, no update check.
        var cliMode = CliReport.TryResolveMode(e.Args);
        if (cliMode is not null)
        {
            var exitCode = CliReport.Run(cliMode.Value);
            Shutdown(exitCode);
            return;
        }

        var info = AppInfo.Current;
        _log.LogInformation("Images {Version} starting — {Runtime} on {Os}",
            info.DisplayVersion, info.RuntimeDescription, info.OsDescription);

        var codecStatus = CodecRuntime.Configure();
        _log.LogInformation("Codec runtime: {MagickStatus}; {DocumentStatus}; GhostscriptDirectory={GhostscriptDirectory}",
            codecStatus.MagickStatus,
            codecStatus.DocumentStatus,
            codecStatus.GhostscriptDirectory ?? "(none)");

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

        // V20-32: `--peek <path>` enters chromeless preview mode (PowerToys Peek invocation
        // contract). Two-token form is the canonical PowerToys shape. Any non-peek single-path
        // argv falls through to the regular OpenPath flow below. Show() is deferred until after
        // EnterPeekMode rewires WindowStyle so the very first paint is borderless.
        if (TryResolvePeekArgs(e.Args, out var peekPath))
        {
            window.EnterPeekMode(peekPath);
            window.Show();
            return;
        }

        window.Show();

        if (e.Args.Length > 0 && TryResolveArgPath(e.Args[0], out var resolved))
            window.OpenPath(resolved);
    }

    /// <summary>
    /// V20-32: matches `--peek &lt;path&gt;` exactly (two argv tokens, exact-match flag,
    /// case-insensitive). Resolves the path through the same canonicalizer the regular open
    /// path uses so device-namespace shapes are rejected before downstream consumption.
    /// </summary>
    private static bool TryResolvePeekArgs(string[] args, out string resolved)
    {
        resolved = string.Empty;
        // Exact two-token contract: `--peek <path>`. Trailing junk (e.g. a third token) means
        // someone's passing flags we don't understand and we should NOT silently treat the
        // launch as a peek invocation — fall through to regular argv handling instead.
        if (args.Length != 2) return false;
        if (!string.Equals(args[0], "--peek", StringComparison.OrdinalIgnoreCase)) return false;
        return TryResolveArgPath(args[1], out resolved);
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
