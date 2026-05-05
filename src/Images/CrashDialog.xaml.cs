using System.IO;
using System.Text;
using System.Windows;
using Images.Services;

namespace Images;

public partial class CrashDialog : Window
{
    private readonly string _detailsText;

    public CrashDialog(Exception ex, string? minidumpPath)
    {
        InitializeComponent();

        var info = AppInfo.Current;
        HeadlineText.Text = ex.Message.Length > 180 ? ex.Message[..180] + "…" : ex.Message;

        var sb = new StringBuilder();
        sb.AppendLine($"Images {info.DisplayVersion} (build {info.ProductVersion})");
        sb.AppendLine($"Runtime: {info.RuntimeDescription}");
        sb.AppendLine($"OS:      {info.OsDescription}");
        sb.AppendLine($"Time:    {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"Log:     {CrashLog.LogPath}");
        if (!string.IsNullOrEmpty(minidumpPath))
            sb.AppendLine($"Dump:    {minidumpPath}");
        sb.AppendLine();
        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            sb.AppendLine($"{cur.GetType().FullName}: {cur.Message}");
            if (cur.StackTrace is not null) sb.AppendLine(cur.StackTrace);
            if (cur.InnerException is not null) sb.AppendLine("--- caused by ---");
        }
        _detailsText = sb.ToString();
        DetailsBox.Text = _detailsText;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChrome.ApplyDarkCaption(hwnd);
        };
    }

    /// <summary>
    /// Shows the dialog on the UI thread. Safe to call from any handler — the dispatcher hops
    /// to the main thread if needed. No-op if shutdown is already underway.
    /// </summary>
    public static void Show(Exception ex, string? minidumpPath)
    {
        var app = Application.Current;
        if (app is null) return;

        void ShowInternal()
        {
            try
            {
                var dlg = new CrashDialog(ex, minidumpPath) { Owner = app.MainWindow };
                dlg.ShowDialog();
            }
            catch
            {
                // If even the dialog construction fails — fall back to the old MessageBox
                // path so the user sees SOMETHING. Log path is still written.
                MessageBox.Show(
                    $"{ex.Message}\n\nDetails written to:\n{CrashLog.LogPath}",
                    "Images — unexpected error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        if (app.Dispatcher.CheckAccess()) ShowInternal();
        else app.Dispatcher.Invoke(ShowInternal);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ClipboardService.SetText(_detailsText);
            ShowStatus("Copied crash details to the clipboard.", "Success");
        }
        catch
        {
            ShowStatus("Clipboard is busy. Try again in a moment.", "Warning");
        }
    }

    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.GetDirectoryName(CrashLog.LogPath);
        if (dir is null || !Directory.Exists(dir))
        {
            ShowStatus("The log folder is not available yet.", "Warning");
            return;
        }

        try
        {
            ShellIntegration.OpenFolder(dir);
            ShowStatus("Opened the crash log folder.", "Success");
        }
        catch (Exception ex)
        {
            ShowStatus($"Could not open the log folder: {ex.Message}", "Warning");
        }
    }

    private void OpenIssueButton_Click(object sender, RoutedEventArgs e)
    {
        // Pre-fill the issue body with the collected details. Keep the URL under the ~8 KB
        // safe cap that GitHub's issue-new endpoint respects — truncate stacks if needed.
        var info = AppInfo.Current;
        var body = _detailsText.Length > 5500 ? _detailsText[..5500] + "\n\n…truncated — see crash log for full details…" : _detailsText;
        var title = $"[crash] {info.DisplayVersion}";

        var url =
            "https://github.com/SysAdminDoc/Images/issues/new"
            + "?title=" + Uri.EscapeDataString(title)
            + "&body=" + Uri.EscapeDataString(body);

        try
        {
            ShellIntegration.OpenShellTarget(url);
            ShowStatus("Opened a pre-filled GitHub issue.", "Success");
        }
        catch
        {
            try
            {
                ClipboardService.SetText(url);
                ShowStatus("Could not open the browser. The issue URL was copied to the clipboard.", "Warning");
            }
            catch
            {
                ShowStatus("Could not open the browser or copy the issue URL.", "Warning");
            }
        }
    }

    private void ShowStatus(string message, string tone)
    {
        StatusCard.Visibility = Visibility.Visible;
        StatusText.Text = message;

        switch (tone)
        {
            case "Success":
                StatusIcon.Text = "\uE73E";
                StatusIcon.Foreground = (System.Windows.Media.Brush)FindResource("GreenBrush");
                StatusCard.BorderBrush = (System.Windows.Media.Brush)FindResource("GreenBrush");
                StatusCard.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x1F, 0xA6, 0xE3, 0xA1));
                break;
            default:
                StatusIcon.Text = "\uE783";
                StatusIcon.Foreground = (System.Windows.Media.Brush)FindResource("YellowBrush");
                StatusCard.BorderBrush = (System.Windows.Media.Brush)FindResource("YellowBrush");
                StatusCard.Background = (System.Windows.Media.Brush)FindResource("WarningPanelBrush");
                break;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
