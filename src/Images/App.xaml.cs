using System.Windows;
using System.Windows.Threading;
using Images.Services;

namespace Images;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // V15-09: route all three fatal-exception channels through CrashLog so we capture
        // whatever killed the app regardless of where it fired. Dispatcher handler also
        // sets Handled=true so the user sees a dialog instead of the app disappearing.
        DispatcherUnhandledException += (_, args) =>
        {
            CrashLog.Append("DispatcherUnhandledException", args.Exception);
            MessageBox.Show(
                $"{args.Exception.Message}\n\nDetails written to:\n{CrashLog.LogPath}",
                "Images — unexpected error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        // Non-UI thread exceptions — can't Handle these, but we can at least log before the
        // runtime terminates us.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            CrashLog.Append("AppDomain.UnhandledException",
                args.ExceptionObject as Exception,
                $"IsTerminating={args.IsTerminating}");

        // Background Task faults that were never awaited. Setting Observed=true prevents the
        // process from being torn down by the TaskScheduler finalizer.
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            CrashLog.Append("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

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
