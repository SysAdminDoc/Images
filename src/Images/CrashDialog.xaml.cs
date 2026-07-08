using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Images.Localization;
using Images.Services;

namespace Images;

public partial class CrashDialog : Window
{
    private readonly string _detailsText;

    public CrashDialog(Exception ex, string? minidumpPath)
    {
        InitializeComponent();

        var info = AppInfo.Current;
        HeadlineText.Text = ex.Message.Length > 180 ? ex.Message[..180] + "..." : ex.Message;

        var sb = new StringBuilder();
        sb.AppendLine(Strings.Format("CrashDetailsAppFormat", info.DisplayVersion, info.ProductVersion));
        sb.AppendLine(Strings.Format("CrashDetailsRuntimeFormat", info.RuntimeDescription));
        sb.AppendLine(Strings.Format("CrashDetailsOsFormat", info.OsDescription));
        sb.AppendLine(Strings.Format("CrashDetailsTimeFormat", DateTime.Now));
        sb.AppendLine(Strings.Format("CrashDetailsLogFormat", CrashLog.LogPath));
        if (!string.IsNullOrEmpty(minidumpPath))
            sb.AppendLine(Strings.Format("CrashDetailsDumpFormat", minidumpPath));
        sb.AppendLine();
        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            sb.AppendLine($"{cur.GetType().FullName}: {cur.Message}");
            if (cur.StackTrace is not null) sb.AppendLine(cur.StackTrace);
            if (cur.InnerException is not null) sb.AppendLine(Strings.InnerExceptionSeparator);
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
                    Strings.Format("CrashMessageBoxDetailsFormat", ex.Message, CrashLog.LogPath),
                    Strings.CrashDialogTitle,
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
            ShowStatus(Strings.CrashDetailsCopied, "Success");
        }
        catch
        {
            ShowStatus(Strings.ClipboardBusyRetry, "Warning");
        }
    }

    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.GetDirectoryName(CrashLog.LogPath);
        if (dir is null || !Directory.Exists(dir))
        {
            ShowStatus(Strings.LogFolderUnavailable, "Warning");
            return;
        }

        try
        {
            ShellIntegration.OpenFolder(dir);
            ShowStatus(Strings.CrashLogFolderOpened, "Success");
        }
        catch (Exception ex)
        {
            ShowStatus(Strings.Format("CrashLogFolderOpenFailedFormat", ex.Message), "Warning");
        }
    }

    private void OpenIssueButton_Click(object sender, RoutedEventArgs e)
    {
        // Pre-fill the issue body with the collected details. Keep the URL under the ~8 KB
        // safe cap that GitHub's issue-new endpoint respects — truncate stacks if needed.
        var info = AppInfo.Current;
        var body = _detailsText.Length > 5500 ? _detailsText[..5500] + Strings.Get("CrashIssueTruncatedMarker") : _detailsText;
        var title = Strings.Format("CrashIssueTitleFormat", info.DisplayVersion);

        var url =
            "https://github.com/SysAdminDoc/Images/issues/new"
            + "?title=" + Uri.EscapeDataString(title)
            + "&body=" + Uri.EscapeDataString(body);

        try
        {
            ShellIntegration.OpenShellTarget(url);
            ShowStatus(Strings.CrashIssueOpened, "Success");
        }
        catch
        {
            try
            {
                ClipboardService.SetText(url);
                ShowStatus(Strings.CrashIssueBrowserFailedCopied, "Warning");
            }
            catch
            {
                ShowStatus(Strings.CrashIssueBrowserAndCopyFailed, "Warning");
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
                SetForegroundResource(StatusIcon, "GreenBrush");
                SetBorderResource(StatusCard, "GreenBrush");
                SetBackgroundResource(StatusCard, "SuccessPanelBrush");
                break;
            default:
                StatusIcon.Text = "\uE783";
                SetForegroundResource(StatusIcon, "YellowBrush");
                SetBorderResource(StatusCard, "YellowBrush");
                SetBackgroundResource(StatusCard, "WarningPanelBrush");
                break;
        }
    }

    private static void SetForegroundResource(TextBlock textBlock, string key)
        => textBlock.SetResourceReference(TextBlock.ForegroundProperty, key);

    private static void SetBackgroundResource(Border border, string key)
        => border.SetResourceReference(Border.BackgroundProperty, key);

    private static void SetBorderResource(Border border, string key)
        => border.SetResourceReference(Border.BorderBrushProperty, key);

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
