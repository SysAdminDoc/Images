using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Images.Services;
using Microsoft.Win32;

namespace Images;

public partial class FileHealthScanWindow : Window
{
    private readonly FileHealthScanService _healthScan = new();
    private readonly ObservableCollection<string> _scanFolders = [];
    private readonly ObservableCollection<FileHealthFinding> _findings = [];
    private CancellationTokenSource? _scanCancellation;

    public FileHealthScanWindow()
    {
        InitializeComponent();

        ScanFoldersList.ItemsSource = _scanFolders;
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
            SetStatus($"Added scan folder: {normalized}", FileHealthStatus.Ready);
        }
    }

    private void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = PickFolder("Add folder to scan");
        if (folder is not null)
            AddScanFolder(folder);
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_scanFolders.Count == 0)
        {
            SetStatus("Add at least one folder before scanning.", FileHealthStatus.Warning);
            return;
        }

        _scanCancellation?.Cancel();
        _scanCancellation = new CancellationTokenSource();
        var scanCancellation = _scanCancellation;
        var token = scanCancellation.Token;
        var folders = _scanFolders.ToArray();

        SetBusy(true);
        _findings.Clear();
        ResetDetail();
        SetStatus("Scanning selected folders for file health issues...", FileHealthStatus.Busy);

        try
        {
            var result = await Task.Run(() => _healthScan.Scan(folders, token), token);
            _findings.Clear();
            foreach (var finding in result.Findings)
                _findings.Add(finding);

            UpdateFindingCount();
            if (_findings.Count > 0)
                FindingsList.SelectedIndex = 0;
            else
                ResetDetail();

            var failed = result.FailedCount > 0
                ? $" {result.FailedCount} file or folder issue{Plural(result.FailedCount)} was skipped."
                : string.Empty;

            SetStatus(
                $"Scanned {result.ScannedCount} file{Plural(result.ScannedCount)}. Found {_findings.Count} health issue{Plural(_findings.Count)}.{failed}",
                _findings.Count > 0 ? FileHealthStatus.Ready : FileHealthStatus.Warning);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Scan canceled.", FileHealthStatus.Warning);
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
        SetStatus("Canceling scan...", FileHealthStatus.Busy);
    }

    private void RemoveFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (ScanFoldersList.SelectedItem is string folder)
            _scanFolders.Remove(folder);
    }

    private void ClearFoldersButton_Click(object sender, RoutedEventArgs e)
        => _scanFolders.Clear();

    private void FindingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => RenderSelectedFinding();

    private void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        var finding = SelectedFinding;
        if (finding is null)
            return;

        var result = _healthScan.RenameToSuggestedExtension(finding);
        if (result.Status == FileHealthActionStatus.Completed)
        {
            RemoveFinding(finding);
            SetStatus($"Renamed extension: {Path.GetFileName(result.DestinationPath)}", FileHealthStatus.Ready);
            return;
        }

        SetStatus(result.Error ?? "Rename failed.", FileHealthStatus.Error);
    }

    private void MarkReviewedButton_Click(object sender, RoutedEventArgs e)
    {
        var finding = SelectedFinding;
        if (finding is null)
            return;

        RemoveFinding(finding);
        SetStatus("Finding marked reviewed for this session.", FileHealthStatus.Ready);
    }

    private void QuarantineButton_Click(object sender, RoutedEventArgs e)
    {
        var finding = SelectedFinding;
        if (finding is null)
            return;

        var result = _healthScan.Quarantine(finding);
        if (result.Status == FileHealthActionStatus.Completed)
        {
            RemoveFinding(finding);
            SetStatus($"Moved to quarantine: {result.DestinationPath}", FileHealthStatus.Ready);
            return;
        }

        SetStatus(result.Error ?? "Quarantine failed.", FileHealthStatus.Error);
    }

    private FileHealthFinding? SelectedFinding => FindingsList.SelectedItem as FileHealthFinding;

    private void RenderSelectedFinding()
    {
        var finding = SelectedFinding;
        if (finding is null)
        {
            ResetDetail();
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;
        DetailTitle.Text = finding.Title;
        DetailSummary.Text = finding.Summary;
        FileNameText.Text = finding.FileName;
        FileNameText.ToolTip = finding.Path;
        FolderText.Text = finding.Folder;
        FolderText.ToolTip = finding.Folder;
        SizeText.Text = finding.SizeText;
        DetailText.Text = finding.Detail;
        DetailText.ToolTip = finding.Detail;
        RenameButton.IsEnabled = finding.CanRename;
        DetailHelpText.Text = finding.CanRename
            ? $"Detected {finding.DetectedFormat}; Rename extension will move this file to {Path.ChangeExtension(finding.Path, finding.SuggestedExtension)}."
            : "Quarantine moves the file to app-local recovery storage. Mark reviewed hides this finding for the current window.";

        try
        {
            PreviewImage.Source = ImageLoader.Load(finding.Path).Image;
            PreviewImage.ToolTip = finding.Path;
        }
        catch
        {
            PreviewImage.Source = null;
            PreviewImage.ToolTip = "Preview unavailable.";
        }
    }

    private void ResetDetail()
    {
        EmptyState.Visibility = Visibility.Visible;
        DetailPanel.Visibility = Visibility.Collapsed;
        PreviewImage.Source = null;
    }

    private void RemoveFinding(FileHealthFinding finding)
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

    private void SetBusy(bool busy)
    {
        AddFolderButton.IsEnabled = !busy;
        ScanButton.IsEnabled = !busy;
        CancelScanButton.IsEnabled = busy;
        FindingsList.IsEnabled = !busy;
        DetailPanel.IsEnabled = !busy;
    }

    private void UpdateFindingCount()
        => FindingCountText.Text = _findings.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private void SetStatus(string message, FileHealthStatus status)
    {
        StatusText.Text = message;
        StatusDot.Fill = status switch
        {
            FileHealthStatus.Busy => Brush("AccentBrush"),
            FileHealthStatus.Warning => Brush("YellowBrush"),
            FileHealthStatus.Error => Brush("RedBrush"),
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

    private enum FileHealthStatus
    {
        Ready,
        Busy,
        Warning,
        Error
    }
}
