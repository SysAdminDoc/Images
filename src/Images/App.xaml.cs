using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Images.Localization;
using Images.Services;
using Microsoft.Extensions.Logging;

namespace Images;

public partial class App : Application
{
    private readonly Microsoft.Extensions.Logging.ILogger _log = Log.For<App>();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LaunchTiming.Log(_log, "app-startup-entered", $"args={e.Args.Length}");

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

        var magickPolicy = MagickSecurityPolicy.Inspect(codecStatus.GhostscriptAvailable, codecStatus.GhostscriptSource);
        _log.LogInformation("Magick.NET security policy: {Policy}; ResourceLimits={ResourceLimits}; BlockedWriteTargets={BlockedWriteTargets}",
            magickPolicy.EnforcementText,
            magickPolicy.ResourceLimitSummary,
            magickPolicy.BlockedWriteSummary);
        var jpegTranStatus = JpegTranRuntime.Inspect();
        _log.LogInformation("JPEG transform runtime: {Status}; Path={Path}",
            jpegTranStatus.StatusText,
            jpegTranStatus.ExecutablePath ?? "(none)");

        // V15-09 + V02-06 + V02-07: fatal-exception channels go through both the structured
        // logger (for day-to-day diagnostics via Serilog rolling file) AND CrashLog (for the
        // plain-text dump + minidump + user-facing "Copy details" dialog). CrashLog is the
        // user-actionable surface; Log.For<App>() is the forensic surface.
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = ReportDispatcherUnhandledException(
                args.Exception,
                ex => _log.LogCritical(ex, "DispatcherUnhandledException"),
                ex => CrashLog.Append("DispatcherUnhandledException", ex),
                CrashLog.TryWriteMiniDump,
                CrashDialog.Show,
                Log.Shutdown);
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

        // I-05: apply persisted locale before any UI is created so x:Static bindings
        // resolve against the chosen culture from the first paint.
        var persistedLocale = SettingsService.Instance.GetString(Keys.Locale, string.Empty);
        if (!string.IsNullOrEmpty(persistedLocale))
        {
            try
            {
                Strings.Culture = new CultureInfo(persistedLocale);
                _log.LogInformation("Locale override applied: {Locale}", persistedLocale);
            }
            catch (CultureNotFoundException ex)
            {
                _log.LogWarning(ex, "Persisted locale '{Locale}' is invalid — falling back to system default", persistedLocale);
            }
        }

        ThemeService.Initialize(this, SettingsService.Instance);

        var window = new MainWindow();
        LaunchTiming.Log(_log, "main-window-created");

        // V20-32: `--peek <path>` enters chromeless preview mode (PowerToys Peek invocation
        // contract). Two-token form is the canonical PowerToys shape. Any non-peek single-path
        // argv falls through to the regular OpenPath flow below. Show() is deferred until after
        // EnterPeekMode rewires WindowStyle so the very first paint is borderless.
        if (TryResolvePeekArgs(e.Args, out var peekPath))
        {
            window.EnterPeekMode(peekPath);
            LaunchTiming.Log(_log, "peek-window-prepared", Path.GetFileName(peekPath));
            window.Show();
            LaunchTiming.Log(_log, "peek-window-shown", Path.GetFileName(peekPath));
            return;
        }

        // V20-31: `--listen <port>` / `-l <port>` starts a loopback TCP listener that accepts
        // file paths for live open/refresh. Parse before Show so the indicator is visible from
        // the first paint. Remaining non-flag args still go through the normal OpenPath flow.
        int? listenPort = null;
        var resolvedPaths = new List<string>();
        for (int i = 0; i < e.Args.Length; i++)
        {
            if ((string.Equals(e.Args[i], "-l", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(e.Args[i], "--listen", StringComparison.OrdinalIgnoreCase))
                && i + 1 < e.Args.Length)
            {
                if (int.TryParse(e.Args[i + 1], out var port) && port > 0 && port <= 65535)
                    listenPort = port;
                i++;
            }
            else if (TryResolveArgPath(e.Args[i], out var r))
            {
                resolvedPaths.Add(r);
            }
        }

        window.Show();
        LaunchTiming.Log(_log, "main-window-shown");

        _ = Task.Run(() => NetworkEgressService.LoadPersistedEntries());

        if (listenPort is not null)
        {
            window.StartListenMode(listenPort.Value);
            LaunchTiming.Log(_log, "listen-mode-started", $"port={listenPort.Value}");
        }

        if (resolvedPaths.Count > 1)
        {
            window.OpenPathList(resolvedPaths);
            LaunchTiming.Log(_log, "argv-multi-open-complete", $"{resolvedPaths.Count} files");
        }
        else if (resolvedPaths.Count == 1)
        {
            window.OpenPath(resolvedPaths[0]);
            LaunchTiming.Log(_log, "argv-open-complete", Path.GetFileName(resolvedPaths[0]));
        }
    }

    /// <summary>
    /// V20-32: matches `--peek &lt;path&gt;` exactly (two argv tokens, exact-match flag,
    /// case-insensitive). Resolves the path through the same canonicalizer the regular open
    /// path uses so device-namespace shapes are rejected before downstream consumption.
    /// </summary>
    internal static bool TryResolvePeekArgs(string[] args, out string resolved)
    {
        resolved = string.Empty;
        // Exact two-token contract: `--peek <path>`. Trailing junk (e.g. a third token) means
        // someone's passing flags we don't understand and we should NOT silently treat the
        // launch as a peek invocation — fall through to regular argv handling instead.
        if (args.Length != 2) return false;
        if (!string.Equals(args[0], "--peek", StringComparison.OrdinalIgnoreCase)) return false;
        return TryResolveArgPath(args[1], out resolved);
    }

    internal static bool TryResolveArgPath(string raw, out string resolved)
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

    internal static bool ReportDispatcherUnhandledException(
        Exception exception,
        Action<Exception> logException,
        Action<Exception> appendCrashLog,
        Func<string?> writeMiniDump,
        Action<Exception, string?> showCrashDialog,
        Action flushLogs)
    {
        try
        {
            logException(exception);
            appendCrashLog(exception);
            var dumpPath = writeMiniDump();
            showCrashDialog(exception, dumpPath);
        }
        finally
        {
            flushLogs();
        }

        return false;
    }
}
