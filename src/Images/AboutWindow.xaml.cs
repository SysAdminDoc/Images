using System.Diagnostics;
using System.IO;
using System.Windows;
using Images.Services;

namespace Images;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var info = AppInfo.Current;
        VersionText.Text = $"v{info.DisplayVersion}";
        BuildText.Text   = info.ProductVersion;
        RuntimeText.Text = info.RuntimeDescription;
        OsText.Text      = info.OsDescription;

        // Dark native caption — same pattern as MainWindow so the About window matches.
        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChrome.ApplyDarkCaption(hwnd);
        };
    }

    private void GitHubButton_Click(object sender, RoutedEventArgs e)
    {
        // UseShellExecute=true is required for http: — and safe here because the URL is a
        // compile-time constant, not user input.
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/SysAdminDoc/Images",
            UseShellExecute = true,
        });
    }

    private void CrashLogButton_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.GetDirectoryName(CrashLog.LogPath);
        if (dir is null || !Directory.Exists(dir)) return;

        // /select, to pre-highlight the log file when it exists; fall back to opening the folder.
        var psi = new ProcessStartInfo { FileName = "explorer.exe", UseShellExecute = false };
        if (File.Exists(CrashLog.LogPath))
            psi.ArgumentList.Add("/select," + CrashLog.LogPath);
        else
            psi.ArgumentList.Add(dir);
        Process.Start(psi);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
