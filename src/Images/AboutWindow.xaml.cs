using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Images.Localization;
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
        OcrStatusText.Text = OcrCapabilityService.BuildOverviewText();
        PopulateDiagnostics();
        ApplyCodecSummary(CodecCapabilityService.BuildSummary());
        PopulateCapabilityMatrix();
        PopulateProvenance();

        UpdateCheckCheckBox.IsChecked = UpdateCheckService.OptedIn;

        PopulateNetworkActivity();
        PopulateJobs();

        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChrome.ApplyDarkCaption(hwnd);
        };
    }

    private void PopulateJobs()
    {
        JobsSummaryText.Text = BackgroundJobsService.BuildSummaryText();
        JobsList.ItemsSource = BackgroundJobsService.GetAll();
    }

    private void GitHubButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ShellIntegration.OpenShellTarget("https://github.com/SysAdminDoc/Images");
        }
        catch (Exception ex)
        {
            ShowUpdateStatus(Strings.Format(nameof(Strings.AboutCouldNotOpenGitHubFormat), ex.Message), "Warning");
        }
    }

    private void CrashLogButton_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.GetDirectoryName(CrashLog.LogPath);
        if (dir is null || !Directory.Exists(dir)) return;

        try
        {
            if (File.Exists(CrashLog.LogPath))
                ShellIntegration.RevealPathInExplorer(CrashLog.LogPath);
            else
                ShellIntegration.OpenFolder(dir);
        }
        catch (Exception ex)
        {
            ShowUpdateStatus(Strings.Format(nameof(Strings.AboutCouldNotOpenCrashLogsFormat), ex.Message), "Warning");
        }
    }

    private void CopyCodecReportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ClipboardService.SetText(CodecCapabilityService.BuildClipboardReport());
            ShowUpdateStatus(Strings.AboutCodecReportCopied, "Success");
        }
        catch (Exception ex)
        {
            ShowUpdateStatus(Strings.Format(nameof(Strings.AboutCouldNotCopyCodecReportFormat), ex.Message), "Warning");
        }
    }

    private void CopySystemInfoButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ClipboardService.SetText(CliReport.BuildSystemInfo());
            ShowUpdateStatus(Strings.AboutSystemInfoCopied, "Success");
        }
        catch (Exception ex)
        {
            ShowUpdateStatus(Strings.Format(nameof(Strings.AboutCouldNotCopySystemInfoFormat), ex.Message), "Warning");
        }
    }

    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        var dir = AppStorage.TryGetAppDirectory("Logs") ?? Path.GetDirectoryName(CrashLog.LogPath);
        if (string.IsNullOrWhiteSpace(dir))
        {
            ShowUpdateStatus(Strings.AboutLogFolderUnavailable, "Warning");
            return;
        }

        try
        {
            ShellIntegration.OpenFolder(dir);
        }
        catch (Exception ex)
        {
            ShowUpdateStatus(Strings.Format(nameof(Strings.AboutCouldNotOpenLogsFormat), ex.Message), "Warning");
        }
    }

    private void ExportSupportBundleButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = SupportBundleService.Build();
            ShellIntegration.RevealPathInExplorer(path);
            ShowUpdateStatus($"Support bundle saved to {path}", "Success");
        }
        catch (Exception ex)
        {
            ShowUpdateStatus($"Could not export support bundle: {ex.Message}", "Warning");
        }
    }

    private void SaveSystemInfoButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = AppStorage.TryGetAppDirectory("diagnostics") ?? Path.GetTempPath();
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
            var path = Path.Combine(dir, $"images-system-info-{stamp}-{Guid.NewGuid():N}.txt");
            File.WriteAllText(path, CliReport.BuildSystemInfo(), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            ShellIntegration.RevealPathInExplorer(path);

            ShowUpdateStatus(Strings.Format(nameof(Strings.AboutSavedToFormat), path), "Success");
        }
        catch (Exception ex)
        {
            ShowUpdateStatus(Strings.Format(nameof(Strings.AboutCouldNotSaveSystemInfoFormat), ex.Message), "Warning");
        }
    }

    private void OpenAppDataButton_Click(object sender, RoutedEventArgs e)
    {
        var dir = AppStorage.TryGetAppDirectory() ?? Path.GetTempPath();
        try
        {
            ShellIntegration.OpenFolder(dir);
        }
        catch (Exception ex)
        {
            ShowUpdateStatus(Strings.Format(nameof(Strings.AboutCouldNotOpenDataFolderFormat), ex.Message), "Warning");
        }
    }

    private void OpenThumbnailCacheButton_Click(object sender, RoutedEventArgs e)
    {
        var dir = ThumbnailCache.Instance.GetHealth().Root ?? AppStorage.TryGetAppDirectory("thumbs");
        if (string.IsNullOrWhiteSpace(dir))
        {
            ShowUpdateStatus(Strings.AboutThumbnailCacheFolderUnavailable, "Warning");
            return;
        }

        try
        {
            Directory.CreateDirectory(dir);
            ShellIntegration.OpenFolder(dir);
        }
        catch (Exception ex)
        {
            ShowUpdateStatus(Strings.Format(nameof(Strings.AboutCouldNotOpenThumbnailCacheFormat), ex.Message), "Warning");
        }
    }

    private async void ClearThumbnailCacheButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;

        var health = ThumbnailCache.Instance.GetHealth();
        if (!health.IsAvailable)
        {
            ShowUpdateStatus(Strings.AboutThumbnailCacheStorageUnavailable, "Warning");
            return;
        }

        if (health.FileCount == 0 && health.TempFileCount == 0)
        {
            ShowUpdateStatus(Strings.AboutThumbnailCacheAlreadyEmpty, "Success");
            return;
        }

        var prompt = Strings.Format(nameof(Strings.AboutClearThumbnailCachePromptFormat), health.FileCount, health.TempFileCount);
        var answer = MessageBox.Show(
            this,
            prompt,
            Strings.AboutClearThumbnailCacheTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
            return;

        button.IsEnabled = false;
        ShowUpdateStatus(Strings.AboutClearingThumbnailCache, "Info");

        try
        {
            var result = await BackgroundTaskTracker
                .Run("thumbnail-cache-clear", ThumbnailCache.Instance.Clear)
                .ConfigureAwait(true);
            PopulateDiagnostics();

            if (!result.IsAvailable)
            {
                ShowUpdateStatus(Strings.AboutThumbnailCacheStorageUnavailable, "Warning");
            }
            else if (result.FailedCount > 0)
            {
                ShowUpdateStatus(
                    Strings.Format(nameof(Strings.AboutThumbnailCacheClearedPartialFormat), result.DeletedCount, FormatBytes(result.DeletedBytes), result.FailedCount),
                    "Warning");
            }
            else
            {
                ShowUpdateStatus(
                    Strings.Format(nameof(Strings.AboutThumbnailCacheClearedFormat), result.DeletedCount, FormatBytes(result.DeletedBytes)),
                    "Success");
            }
        }
        catch (Exception ex)
        {
            ShowUpdateStatus(Strings.Format(nameof(Strings.AboutCouldNotClearThumbnailCacheFormat), ex.Message), "Warning");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private void PopulateNetworkActivity()
    {
        NetworkActivityItems.ItemsSource = NetworkEgressService.Entries;
        var count = NetworkEgressService.Entries.Count;
        if (count == 0)
        {
            NetworkActivitySummary.Text = Strings.AboutNetworkActivityEmpty;
        }
        else
        {
            var totalBytes = NetworkEgressService.TotalBytes;
            NetworkActivitySummary.Text = Strings.Format(
                nameof(Strings.AboutNetworkActivitySummaryFormat),
                count,
                FormatBytes(totalBytes));
        }
    }

    private void CopyNetworkLogButton_Click(object sender, RoutedEventArgs e)
    {
        if (NetworkEgressService.Entries.Count == 0)
        {
            ShowUpdateStatus(Strings.AboutNetworkActivityNothingToCopy, "Info");
            return;
        }

        try
        {
            ClipboardService.SetText(NetworkEgressService.BuildClipboardText());
            ShowUpdateStatus(Strings.AboutNetworkActivityCopied, "Success");
        }
        catch (Exception ex)
        {
            ShowUpdateStatus($"Could not copy: {ex.Message}", "Warning");
        }
    }

    private void ClearNetworkLogButton_Click(object sender, RoutedEventArgs e)
    {
        NetworkEgressService.ClearAll();
        PopulateNetworkActivity();
        ShowUpdateStatus(Strings.AboutNetworkActivityCleared, "Success");
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

        DocumentCapabilityIcon.Text = summary.DocumentReady ? "" : "";
        DocumentCapabilityIcon.Foreground = ThemeBrush(summary.DocumentReady ? "GreenBrush" : "YellowBrush");
        DocumentCapabilityCard.BorderBrush = ThemeBrush(summary.DocumentReady ? "GreenBrush" : "YellowBrush");
        DocumentCapabilityCard.Background = ThemeBrush(summary.DocumentReady ? "SurfacePanelBrush" : "WarningPanelBrush");
    }

    private void PopulateDiagnostics()
    {
        DiagnosticsItems.ItemsSource = DiagnosticsStatusService.BuildStatusItems();
    }

    private void UpdateCheckOptIn_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox cb)
        {
            UpdateCheckService.OptedIn = cb.IsChecked == true;
            PopulateDiagnostics();
        }
    }

    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        btn.IsEnabled = false;
        CheckUpdatesButtonText.Text = Strings.AboutChecking;
        _latestReleaseUrl = null;
        UpdateReleaseButton.Visibility = Visibility.Collapsed;
        ShowUpdateStatus(Strings.AboutCheckingGitHubReleases, "Info");
        try
        {
            var result = await UpdateCheckService.CheckAsync();
            UpdateCheckService.RecordLastCheckedIfAppropriate(result);
            if (result.Error is not null)
            {
                ShowUpdateStatus(Strings.Format(nameof(Strings.AboutUpdateCheckFailedFormat), result.Error), "Warning");
            }
            else if (result.NewerAvailable && result.LatestHtmlUrl is not null)
            {
                _latestReleaseUrl = result.LatestHtmlUrl;
                UpdateReleaseButton.Visibility = Visibility.Visible;
                ShowUpdateStatus(Strings.Format(nameof(Strings.AboutNewerVersionAvailableFormat), result.LatestTag), "Update");
            }
            else
            {
                ShowUpdateStatus(Strings.AboutOnLatestVersion, "Success");
            }
        }
        finally
        {
            PopulateDiagnostics();
            PopulateNetworkActivity();
            btn.IsEnabled = true;
            CheckUpdatesButtonText.Text = Strings.AboutCheckUpdates;
        }
    }

    private void UpdateReleaseButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_latestReleaseUrl)) return;
        try
        {
            ShellIntegration.OpenShellTarget(_latestReleaseUrl);
        }
        catch (Exception ex)
        {
            ShowUpdateStatus(Strings.Format(nameof(Strings.AboutCouldNotOpenReleasePageFormat), ex.Message), "Warning");
        }
    }

    private void ShowUpdateStatus(string message, string tone)
    {
        UpdateStatusCard.Visibility = Visibility.Visible;
        UpdateStatusText.Text = message;

        switch (tone)
        {
            case "Warning":
                UpdateStatusIcon.Text = "";
                UpdateStatusIcon.Foreground = ThemeBrush("YellowBrush");
                UpdateStatusCard.BorderBrush = ThemeBrush("YellowBrush");
                UpdateStatusCard.Background = ThemeBrush("WarningPanelBrush");
                break;
            case "Success":
                UpdateStatusIcon.Text = "";
                UpdateStatusIcon.Foreground = ThemeBrush("GreenBrush");
                UpdateStatusCard.BorderBrush = ThemeBrush("GreenBrush");
                UpdateStatusCard.Background = new SolidColorBrush(Color.FromArgb(0x1F, 0xA6, 0xE3, 0xA1));
                break;
            case "Update":
                UpdateStatusIcon.Text = "";
                UpdateStatusIcon.Foreground = ThemeBrush("AccentBrush");
                UpdateStatusCard.BorderBrush = ThemeBrush("AccentBrush");
                UpdateStatusCard.Background = new SolidColorBrush(Color.FromArgb(0x1F, 0x89, 0xB4, 0xFA));
                break;
            default:
                UpdateStatusIcon.Text = "";
                UpdateStatusIcon.Foreground = ThemeBrush("AccentBrush");
                UpdateStatusCard.BorderBrush = ThemeBrush("HairlineBrush");
                UpdateStatusCard.Background = ThemeBrush("SurfacePanelBrush");
                break;
        }
    }

    private Brush ThemeBrush(string key) => (Brush)FindResource(key);

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        var value = Math.Max(0, bytes);
        var displayValue = (double)value;
        var unitIndex = 0;

        while (displayValue >= 1024 && unitIndex < units.Length - 1)
        {
            displayValue /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{value} {units[unitIndex]}"
            : $"{displayValue:0.#} {units[unitIndex]}";
    }

    private void PopulateCapabilityMatrix()
    {
        CapabilityMatrixItems.ItemsSource = CodecCapabilityService.BuildCapabilityMatrix()
            .Select(row => new
            {
                Family = row.Family,
                CountSummary = Strings.Format(nameof(Strings.AboutCapabilityMatrixCountSummaryFormat), row.OpenCount, row.ExportCount),
                Facets = Strings.Format(nameof(Strings.AboutCapabilityMatrixFacetsFormat), Tri(row.Animation), Tri(row.MultiPage), Tri(row.Metadata)),
                RuntimeLine = Strings.Format(nameof(Strings.AboutCapabilityMatrixRuntimeFormat), row.Runtime),
                Notes = row.Notes
            })
            .ToArray();
    }

    private static string Tri(bool? value) => value switch
    {
        true => Strings.AboutTriYes,
        false => Strings.AboutTriNo,
        _ => Strings.AboutTriNA
    };

    private void PopulateProvenance()
    {
        ProvenancePanel.Children.Clear();
        var p = CodecCapabilityService.BuildProvenance();

        AddProvenanceRow(Strings.AboutAppDirectoryLabel, p.AppDirectory);
        AddProvenanceRow(Strings.AboutProcessArchLabel, p.ProcessArchitecture);

        foreach (var row in CodecCapabilityService.BuildDependencyProvenanceRows(p, OcrCapabilityService.GetStatus()))
            AddDependencyProvenanceRow(row);
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

    private void AddDependencyProvenanceRow(CodecCapabilityService.DependencyProvenanceRow row)
    {
        var grid = new Grid { Margin = new Thickness(0, 8, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelBlock = new TextBlock
        {
            Text = row.Name,
            Style = (Style)FindResource("MetaLabel"),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(labelBlock, 0);
        grid.Children.Add(labelBlock);

        var na = Strings.AboutProvenanceNotApplicable;
        var value = new StringBuilder()
            .AppendLine(Strings.Format(nameof(Strings.AboutProvenanceDependencyKindVersionFormat), row.Kind, row.Version))
            .AppendLine(Strings.Format(nameof(Strings.AboutProvenanceSourceFormat), row.Source))
            .AppendLine(Strings.Format(nameof(Strings.AboutProvenancePathFormat), row.Path ?? na))
            .AppendLine(Strings.Format(nameof(Strings.AboutProvenanceSha256Format), row.Sha256 ?? na))
            .AppendLine(Strings.Format(nameof(Strings.AboutProvenanceAdvisoryFormat), row.AdvisoryStatus))
            .Append(Strings.Format(nameof(Strings.AboutProvenanceActionFormat), row.Action))
            .ToString();

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
