using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Images.Services;
using Microsoft.Win32;

namespace Images;

public partial class DuplicateCleanupWindow : Window
{
    private readonly DuplicateCleanupService _cleanupService = new();
    private readonly RecycleBinDeleteService _deleteService = new(SettingsService.Instance);
    private readonly ObservableCollection<string> _scanFolders = [];
    private readonly ObservableCollection<string> _referenceFolders = [];
    private readonly ObservableCollection<DuplicateCleanupFinding> _findings = [];
    private CancellationTokenSource? _scanCancellation;

    public DuplicateCleanupWindow()
    {
        InitializeComponent();

        ScanFoldersList.ItemsSource = _scanFolders;
        ReferenceFoldersList.ItemsSource = _referenceFolders;
        FindingsList.ItemsSource = _findings;
        UpdateFindingCount();

        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChrome.ApplyDarkCaption(hwnd);
        };
        Closed += (_, _) => _scanCancellation?.Cancel();
    }

    public void AddScanFolder(string folder)
    {
        if (TryNormalizeFolder(folder, out var normalized) &&
            !_scanFolders.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            _scanFolders.Add(normalized);
            SetStatus($"Added scan folder: {normalized}", CleanupStatus.Ready);
        }
    }

    private void AddReferenceFolder(string folder)
    {
        if (TryNormalizeFolder(folder, out var normalized) &&
            !_referenceFolders.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            _referenceFolders.Add(normalized);
            SetStatus($"Added reference folder: {normalized}", CleanupStatus.Ready);
        }
    }

    private void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = PickFolder("Add folder to scan");
        if (folder is not null)
            AddScanFolder(folder);
    }

    private void AddReferenceFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = PickFolder("Add reference folder");
        if (folder is not null)
            AddReferenceFolder(folder);
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_scanFolders.Count == 0)
        {
            SetStatus("Add at least one folder before scanning.", CleanupStatus.Warning);
            return;
        }

        _scanCancellation?.Cancel();
        _scanCancellation = new CancellationTokenSource();
        var scanCancellation = _scanCancellation;
        var token = _scanCancellation.Token;
        var folders = _scanFolders.ToArray();
        var references = _referenceFolders.ToArray();
        var threshold = (int)Math.Round(ThresholdSlider.Value);

        SetBusy(true);
        _findings.Clear();
        ResetDetail();
        SetStatus("Scanning folders for exact duplicates and similar images...", CleanupStatus.Busy);

        try
        {
            var result = await Task.Run(
                () => _cleanupService.Scan(folders, references, threshold, token),
                token);

            _findings.Clear();
            foreach (var finding in result.Findings)
                _findings.Add(finding);

            UpdateFindingCount();
            if (_findings.Count > 0)
                FindingsList.SelectedIndex = 0;
            else
                ResetDetail();

            var failedText = result.FailedCount > 0
                ? $" {result.FailedCount} file or folder issue{Plural(result.FailedCount)} was skipped."
                : string.Empty;

            SetStatus(
                $"Scanned {result.FileCount} file{Plural(result.FileCount)}. Found {result.ExactGroupCount} exact group{Plural(result.ExactGroupCount)} and {result.SimilarPairCount} similar pair{Plural(result.SimilarPairCount)}.{failedText}",
                _findings.Count > 0 ? CleanupStatus.Ready : CleanupStatus.Warning);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Scan canceled.", CleanupStatus.Warning);
        }
        finally
        {
            if (ReferenceEquals(_scanCancellation, scanCancellation))
                _scanCancellation = null;
            SetBusy(false);
        }
    }

    private void CancelScanButton_Click(object sender, RoutedEventArgs e)
    {
        _scanCancellation?.Cancel();
        SetStatus("Canceling scan...", CleanupStatus.Busy);
    }

    private void RemoveScanFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (ScanFoldersList.SelectedItem is string folder)
            _scanFolders.Remove(folder);
    }

    private void ClearScanFoldersButton_Click(object sender, RoutedEventArgs e)
        => _scanFolders.Clear();

    private void RemoveReferenceFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (ReferenceFoldersList.SelectedItem is string folder)
            _referenceFolders.Remove(folder);
    }

    private void ClearReferenceFoldersButton_Click(object sender, RoutedEventArgs e)
        => _referenceFolders.Clear();

    private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ThresholdText is null)
            return;

        ThresholdText.Text = ((int)Math.Round(e.NewValue)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (_findings.Count > 0)
            SetStatus("Threshold changed. Scan again to recompute similar-image findings.", CleanupStatus.Warning);
    }

    private void FindingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => RenderSelectedFinding();

    private void MarkNotDuplicateButton_Click(object sender, RoutedEventArgs e)
    {
        var finding = SelectedFinding;
        if (finding is null)
            return;

        RemoveFinding(finding);
        SetStatus("Finding marked not duplicate for this session.", CleanupStatus.Ready);
    }

    private async void QuarantineExtrasButton_Click(object sender, RoutedEventArgs e)
    {
        var finding = SelectedFinding;
        var paths = ExtraExistingPaths(finding);
        if (paths.Count == 0)
        {
            SetStatus("No extra files are available to quarantine.", CleanupStatus.Warning);
            return;
        }

        SetBusy(true);
        SetStatus($"Moving {paths.Count} extra file{Plural(paths.Count)} to quarantine...", CleanupStatus.Busy);

        try
        {
            var result = await Task.Run(() => _cleanupService.Quarantine(paths));
            if (result.MovedCount > 0 && finding is not null)
                RemoveFinding(finding);

            if (!result.IsAvailable)
            {
                SetStatus("Quarantine storage is not available. No files were moved.", CleanupStatus.Error);
            }
            else if (result.FailedCount > 0)
            {
                SetStatus($"Moved {result.MovedCount} file{Plural(result.MovedCount)} to quarantine. {result.FailedCount} failed.", CleanupStatus.Warning);
            }
            else
            {
                SetStatus($"Moved {result.MovedCount} file{Plural(result.MovedCount)} to quarantine: {result.BatchDirectory}", CleanupStatus.Ready);
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RecycleExtrasButton_Click(object sender, RoutedEventArgs e)
    {
        var finding = SelectedFinding;
        var paths = ExtraExistingPaths(finding);
        if (paths.Count == 0)
        {
            SetStatus("No extra files are available to recycle.", CleanupStatus.Warning);
            return;
        }

        var deleted = 0;
        var failed = 0;
        foreach (var path in paths)
        {
            var result = _deleteService.Delete(path, this);
            switch (result.Status)
            {
                case RecycleBinDeleteStatus.Deleted:
                    deleted++;
                    break;
                case RecycleBinDeleteStatus.Canceled:
                    SetStatus($"Recycle canceled after {deleted} file{Plural(deleted)}.", CleanupStatus.Warning);
                    if (deleted > 0 && finding is not null)
                        RemoveFinding(finding);
                    return;
                case RecycleBinDeleteStatus.Failed:
                case RecycleBinDeleteStatus.Missing:
                    failed++;
                    break;
            }
        }

        if (deleted > 0 && finding is not null)
            RemoveFinding(finding);

        SetStatus(
            failed > 0
                ? $"Moved {deleted} file{Plural(deleted)} to the Recycle Bin. {failed} failed or was already missing."
                : $"Moved {deleted} file{Plural(deleted)} to the Recycle Bin.",
            failed > 0 ? CleanupStatus.Warning : CleanupStatus.Ready);
    }

    private DuplicateCleanupFinding? SelectedFinding => FindingsList.SelectedItem as DuplicateCleanupFinding;

    private void RenderSelectedFinding()
    {
        var finding = SelectedFinding;
        if (finding?.PrimaryCandidate is null || finding.SecondaryCandidate is null)
        {
            ResetDetail();
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;
        DetailTitle.Text = finding.Title;
        DetailSummary.Text = finding.Summary;
        DetailHelpText.Text = finding.ExtraCandidates.Count == 1
            ? "Cleanup actions target the extra candidate on the right."
            : $"Cleanup actions target {finding.ExtraCandidates.Count} extra candidates after the keep candidate.";

        RenderCandidate(finding.PrimaryCandidate, PrimaryPreview, PrimaryNameText, PrimaryFolderText, PrimarySizeText, PrimaryReferenceBadge, "Keep candidate");
        RenderCandidate(finding.SecondaryCandidate, SecondaryPreview, SecondaryNameText, SecondaryFolderText, SecondarySizeText, SecondaryReferenceBadge, "Extra candidate");
    }

    private static void RenderCandidate(
        DuplicateCleanupCandidate candidate,
        Image preview,
        TextBlock nameText,
        TextBlock folderText,
        TextBlock sizeText,
        Border badge,
        string role)
    {
        nameText.Text = candidate.FileName;
        nameText.ToolTip = candidate.Path;
        folderText.Text = candidate.Folder;
        folderText.ToolTip = candidate.Folder;
        sizeText.Text = $"{candidate.SizeText} - {candidate.ReferenceText} - SHA {candidate.ShortHash}";
        badge.Visibility = candidate.IsReference || role == "Keep candidate"
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (badge.Child is TextBlock badgeText)
            badgeText.Text = candidate.IsReference ? "Reference candidate" : role;

        try
        {
            preview.Source = ImageLoader.Load(candidate.Path).Image;
            preview.ToolTip = candidate.Path;
        }
        catch
        {
            preview.Source = null;
            preview.ToolTip = "Preview unavailable.";
        }
    }

    private void ResetDetail()
    {
        EmptyState.Visibility = Visibility.Visible;
        DetailPanel.Visibility = Visibility.Collapsed;
        PrimaryPreview.Source = null;
        SecondaryPreview.Source = null;
    }

    private void RemoveFinding(DuplicateCleanupFinding finding)
    {
        var index = _findings.IndexOf(finding);
        if (index < 0)
            return;

        _findings.RemoveAt(index);
        UpdateFindingCount();

        if (_findings.Count == 0)
        {
            ResetDetail();
            return;
        }

        FindingsList.SelectedIndex = Math.Min(index, _findings.Count - 1);
    }

    private static IReadOnlyList<string> ExtraExistingPaths(DuplicateCleanupFinding? finding)
        => finding?.ExtraCandidates
               .Select(candidate => candidate.Path)
               .Where(File.Exists)
               .ToArray()
           ?? [];

    private void SetBusy(bool busy)
    {
        AddFolderButton.IsEnabled = !busy;
        AddReferenceFolderButton.IsEnabled = !busy;
        ScanButton.IsEnabled = !busy;
        CancelScanButton.IsEnabled = busy;
        FindingsList.IsEnabled = !busy;
        DetailPanel.IsEnabled = !busy;
    }

    private void UpdateFindingCount()
        => FindingCountText.Text = _findings.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private void SetStatus(string message, CleanupStatus status)
    {
        StatusText.Text = message;
        StatusDot.Fill = status switch
        {
            CleanupStatus.Busy => Brush("AccentBrush"),
            CleanupStatus.Warning => Brush("YellowBrush"),
            CleanupStatus.Error => Brush("RedBrush"),
            _ => Brush("GreenBrush")
        };
    }

    private Brush Brush(string key)
        => TryFindResource(key) as Brush ?? Brushes.Transparent;

    private static string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    private static bool TryNormalizeFolder(string folder, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(folder))
            return false;

        try
        {
            var full = Path.GetFullPath(folder);
            if (!Directory.Exists(full))
                return false;

            normalized = full;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static string Plural(int count) => count == 1 ? string.Empty : "s";

    private enum CleanupStatus
    {
        Ready,
        Busy,
        Warning,
        Error
    }
}
