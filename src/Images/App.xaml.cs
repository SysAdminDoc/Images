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

        if (e.Args.Length > 0 && System.IO.File.Exists(e.Args[0]))
            window.OpenPath(e.Args[0]);
    }
}
