using System.Diagnostics;
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
        try { Clipboard.SetText(_detailsText); }
        catch { /* clipboard in-use is a known WPF race; ignore */ }
    }

    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.GetDirectoryName(CrashLog.LogPath);
        if (dir is null || !Directory.Exists(dir)) return;
        var psi = new ProcessStartInfo { FileName = "explorer.exe", UseShellExecute = false };
        psi.ArgumentList.Add(dir);
        Process.Start(psi);
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
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            Clipboard.SetText(url);
            MessageBox.Show(
                "Couldn't open the browser. The issue URL was copied to your clipboard — paste it into a browser.",
                "Images", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
