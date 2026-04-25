using System.Diagnostics;
using System.IO;
using System.Windows;
using Images.Services;

namespace Images;

public partial class AboutWindow : Window
{
    private string? _latestReleaseUrl;

    public AboutWindow()
    {
        InitializeComponent();

        var info = AppInfo.Current;
        VersionText.Text = $"v{info.DisplayVersion}";
        BuildText.Text   = info.ProductVersion;
        RuntimeText.Text = info.RuntimeDescription;
        OsText.Text      = info.OsDescription;

        CodecText.Text = CodecCapabilityService.BuildOverviewText();
        DocumentCodecText.Text = CodecCapabilityService.BuildDocumentStatusText();

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

    private void CopyCodecReportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(CodecCapabilityService.BuildClipboardReport());
            ShowUpdateStatus("Codec report copied to clipboard.", "Success");
        }
        catch (Exception ex)
        {
            ShowUpdateStatus($"Could not copy codec report: {ex.Message}", "Warning");
        }
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
        CheckUpdatesButtonText.Text = "Checking...";
        _latestReleaseUrl = null;
        UpdateReleaseButton.Visibility = Visibility.Collapsed;
        ShowUpdateStatus("Checking GitHub Releases...", "Info");
        try
        {
            var result = await UpdateCheckService.CheckAsync();
            UpdateCheckService.LastCheckedUtc = DateTime.UtcNow;
            if (result.Error is not null)
            {
                ShowUpdateStatus($"Update check failed: {result.Error}", "Warning");
            }
            else if (result.NewerAvailable && result.LatestHtmlUrl is not null)
            {
                _latestReleaseUrl = result.LatestHtmlUrl;
                UpdateReleaseButton.Visibility = Visibility.Visible;
                ShowUpdateStatus($"A newer version is available: {result.LatestTag}.", "Update");
            }
            else
            {
                ShowUpdateStatus("You're on the latest version.", "Success");
            }
        }
        finally
        {
            btn.IsEnabled = true;
            CheckUpdatesButtonText.Text = "Check updates";
        }
    }

    private void UpdateReleaseButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_latestReleaseUrl)) return;
        Process.Start(new ProcessStartInfo
        {
            FileName = _latestReleaseUrl,
            UseShellExecute = true,
        });
    }

    private void ShowUpdateStatus(string message, string tone)
    {
        UpdateStatusCard.Visibility = Visibility.Visible;
        UpdateStatusText.Text = message;

        switch (tone)
        {
            case "Warning":
                UpdateStatusIcon.Text = "\uE783";
                UpdateStatusIcon.Foreground = (System.Windows.Media.Brush)FindResource("YellowBrush");
                UpdateStatusCard.BorderBrush = (System.Windows.Media.Brush)FindResource("YellowBrush");
                UpdateStatusCard.Background = (System.Windows.Media.Brush)FindResource("WarningPanelBrush");
                break;
            case "Success":
                UpdateStatusIcon.Text = "\uE73E";
                UpdateStatusIcon.Foreground = (System.Windows.Media.Brush)FindResource("GreenBrush");
                UpdateStatusCard.BorderBrush = (System.Windows.Media.Brush)FindResource("GreenBrush");
                UpdateStatusCard.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x1F, 0xA6, 0xE3, 0xA1));
                break;
            case "Update":
                UpdateStatusIcon.Text = "\uE895";
                UpdateStatusIcon.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
                UpdateStatusCard.BorderBrush = (System.Windows.Media.Brush)FindResource("AccentBrush");
                UpdateStatusCard.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x1F, 0x89, 0xB4, 0xFA));
                break;
            default:
                UpdateStatusIcon.Text = "\uE930";
                UpdateStatusIcon.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
                UpdateStatusCard.BorderBrush = (System.Windows.Media.Brush)FindResource("HairlineBrush");
                UpdateStatusCard.Background = (System.Windows.Media.Brush)FindResource("SurfacePanelBrush");
                break;
        }
    }
}
