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

        UpdateCheckCheckBox.IsChecked = UpdateCheckService.OptedIn;

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

    private void UpdateCheckOptIn_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox cb)
            UpdateCheckService.OptedIn = cb.IsChecked == true;
    }

    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        // Manual check bypasses the 24-h throttle. Gate the button against concurrent clicks.
        if (sender is not System.Windows.Controls.Button btn) return;
        btn.IsEnabled = false;
        try
        {
            var result = await UpdateCheckService.CheckAsync();
            UpdateCheckService.LastCheckedUtc = DateTime.UtcNow;
            if (result.Error is not null)
            {
                System.Windows.MessageBox.Show(this, $"Update check failed: {result.Error}",
                    "Images", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            else if (result.NewerAvailable && result.LatestHtmlUrl is not null)
            {
                var r = System.Windows.MessageBox.Show(this,
                    $"A newer version is available: {result.LatestTag}\n\nOpen the release page?",
                    "Images — update available",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Information);
                if (r == System.Windows.MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = result.LatestHtmlUrl, UseShellExecute = true
                    });
                }
            }
            else
            {
                System.Windows.MessageBox.Show(this, "You're on the latest version.",
                    "Images", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }
}
