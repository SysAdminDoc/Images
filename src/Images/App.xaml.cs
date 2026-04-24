using System.Windows;
using System.Windows.Threading;

namespace Images;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                var log = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Images", "crash.log");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(log)!);
                System.IO.File.AppendAllText(log, $"[{DateTime.Now:O}]\n{args.Exception}\n\n");
            }
            catch { }

            MessageBox.Show(
                args.Exception.ToString(),
                "Images — unexpected error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
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
