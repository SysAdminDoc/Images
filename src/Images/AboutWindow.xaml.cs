using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
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
        ApplyCodecSummary(CodecCapabilityService.BuildSummary());
        PopulateCapabilityMatrix();
        PopulateProvenance();

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
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/SysAdminDoc/Images",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            ShowUpdateStatus($"Could not open GitHub: {ex.Message}", "Warning");
        }
    }

    private void CrashLogButton_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.GetDirectoryName(CrashLog.LogPath);
        if (dir is null || !Directory.Exists(dir)) return;

        try
        {
            // /select, to pre-highlight the log file when it exists; fall back to opening the folder.
            var psi = new ProcessStartInfo { FileName = "explorer.exe", UseShellExecute = false };
            if (File.Exists(CrashLog.LogPath))
                psi.ArgumentList.Add("/select," + CrashLog.LogPath);
            else
                psi.ArgumentList.Add(dir);
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            ShowUpdateStatus($"Could not open crash logs: {ex.Message}", "Warning");
        }
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

    private void ApplyCodecSummary(CodecCapabilityService.CodecCapabilitySummary summary)
    {
        OpenCapabilityTitleText.Text = summary.OpenTitle;
        OpenCapabilityDetailText.Text = summary.OpenDetail;
        ExportCapabilityTitleText.Text = summary.ExportTitle;
        ExportCapabilityDetailText.Text = summary.ExportDetail;
        DocumentCapabilityTitleText.Text = summary.DocumentTitle;
        DocumentCapabilityDetailText.Text = summary.DocumentDetail;

        DocumentCapabilityIcon.Text = summary.DocumentReady ? "\uE73E" : "\uE783";
        DocumentCapabilityIcon.Foreground = ThemeBrush(summary.DocumentReady ? "GreenBrush" : "YellowBrush");
        DocumentCapabilityCard.BorderBrush = ThemeBrush(summary.DocumentReady ? "GreenBrush" : "YellowBrush");
        DocumentCapabilityCard.Background = ThemeBrush(summary.DocumentReady ? "SurfacePanelBrush" : "WarningPanelBrush");
    }

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
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _latestReleaseUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            ShowUpdateStatus($"Could not open release page: {ex.Message}", "Warning");
        }
    }

    private void ShowUpdateStatus(string message, string tone)
    {
        UpdateStatusCard.Visibility = Visibility.Visible;
        UpdateStatusText.Text = message;

        switch (tone)
        {
            case "Warning":
                UpdateStatusIcon.Text = "\uE783";
                UpdateStatusIcon.Foreground = ThemeBrush("YellowBrush");
                UpdateStatusCard.BorderBrush = ThemeBrush("YellowBrush");
                UpdateStatusCard.Background = ThemeBrush("WarningPanelBrush");
                break;
            case "Success":
                UpdateStatusIcon.Text = "\uE73E";
                UpdateStatusIcon.Foreground = ThemeBrush("GreenBrush");
                UpdateStatusCard.BorderBrush = ThemeBrush("GreenBrush");
                UpdateStatusCard.Background = new SolidColorBrush(Color.FromArgb(0x1F, 0xA6, 0xE3, 0xA1));
                break;
            case "Update":
                UpdateStatusIcon.Text = "\uE895";
                UpdateStatusIcon.Foreground = ThemeBrush("AccentBrush");
                UpdateStatusCard.BorderBrush = ThemeBrush("AccentBrush");
                UpdateStatusCard.Background = new SolidColorBrush(Color.FromArgb(0x1F, 0x89, 0xB4, 0xFA));
                break;
            default:
                UpdateStatusIcon.Text = "\uE930";
                UpdateStatusIcon.Foreground = ThemeBrush("AccentBrush");
                UpdateStatusCard.BorderBrush = ThemeBrush("HairlineBrush");
                UpdateStatusCard.Background = ThemeBrush("SurfacePanelBrush");
                break;
        }
    }

    private Brush ThemeBrush(string key) => (Brush)FindResource(key);

    private void PopulateCapabilityMatrix()
    {
        CapabilityMatrixItems.ItemsSource = CodecCapabilityService.BuildCapabilityMatrix()
            .Select(row => new
            {
                Family = row.Family,
                CountSummary = $"{row.OpenCount} open · {row.ExportCount} export",
                Facets = $"Animation: {Tri(row.Animation)} · Multi-page: {Tri(row.MultiPage)} · Metadata: {Tri(row.Metadata)}",
                RuntimeLine = $"Runtime: {row.Runtime}",
                Notes = row.Notes
            })
            .ToArray();
    }

    private static string Tri(bool? value) => value switch
    {
        true => "yes",
        false => "no",
        _ => "n/a"
    };

    /// <summary>
    /// Populates the provenance card with the live runtime snapshot — Magick.NET version +
    /// assembly path, Ghostscript path/source/version/SHA-256 — so the values match exactly
    /// what <c>--system-info</c> would print.
    /// </summary>
    private void PopulateProvenance()
    {
        ProvenancePanel.Children.Clear();
        var p = CodecCapabilityService.BuildProvenance();

        AddProvenanceRow("App directory", p.AppDirectory);
        AddProvenanceRow("Process arch", p.ProcessArchitecture);
        AddProvenanceRow("Magick.NET", p.MagickVersion);
        if (p.MagickAssemblyPath is not null)
            AddProvenanceRow("Magick.NET path", p.MagickAssemblyPath);

        AddProvenanceRow("Ghostscript", p.GhostscriptAvailable ? "available" : "not available");
        AddProvenanceRow("Ghostscript source", p.GhostscriptDirectory ?? p.GhostscriptSource);
        if (p.GhostscriptVersion is not null)
            AddProvenanceRow("Ghostscript ver", p.GhostscriptVersion);
        if (p.GhostscriptDllPath is not null)
            AddProvenanceRow("Ghostscript DLL", p.GhostscriptDllPath);
        if (p.GhostscriptDllSha256 is not null)
            AddProvenanceRow("DLL SHA-256", p.GhostscriptDllSha256);
    }

    private void AddProvenanceRow(string label, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelBlock = new TextBlock
        {
            Text = label,
            Style = (Style)FindResource("MetaLabel")
        };
        Grid.SetColumn(labelBlock, 0);
        grid.Children.Add(labelBlock);

        var valueBlock = new TextBlock
        {
            Text = value,
            Style = (Style)FindResource("MetaValue"),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(valueBlock);

        ProvenancePanel.Children.Add(grid);
    }
}
