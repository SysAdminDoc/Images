using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Images.Services;

namespace Images;

public partial class RecoveryCenterWindow : Window
{
    private readonly RecoveryCenterService _recoveryCenter;
    private readonly ObservableCollection<RecoveryActionRecord> _records = [];

    public RecoveryCenterWindow()
        : this(null)
    {
    }

    internal RecoveryCenterWindow(RecoveryCenterService? recoveryCenter)
    {
        _recoveryCenter = recoveryCenter ?? new RecoveryCenterService();
        InitializeComponent();

        RecordsList.ItemsSource = _records;
        RulesText.Text = _recoveryCenter.CleanupRulesText;
        RefreshRecords();

        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChrome.ApplyDarkCaption(hwnd);
        };
    }

    private RecoveryActionRecord? SelectedRecord => RecordsList.SelectedItem as RecoveryActionRecord;

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
        => RefreshRecords();

    private void RecordsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => RenderSelectedRecord();

    private void RevealButton_Click(object sender, RoutedEventArgs e)
    {
        var record = SelectedRecord;
        if (record is null)
        {
            SetStatus("Select a recovery record first.", RecoveryStatus.Warning);
            return;
        }

        var path = _recoveryCenter.ResolveRevealPath(record.Id);
        if (string.IsNullOrWhiteSpace(path))
        {
            SetStatus("No existing path or parent folder is available for this record.", RecoveryStatus.Warning);
            return;
        }

        try
        {
            ShellIntegration.RevealPathInExplorer(path);
            SetStatus("Opened recovery location in Explorer.", RecoveryStatus.Ready);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            SetStatus("Reveal failed: " + ex.Message, RecoveryStatus.Error);
        }
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        var record = SelectedRecord;
        if (record is null)
        {
            SetStatus("Select a restorable recovery record first.", RecoveryStatus.Warning);
            return;
        }

        var result = _recoveryCenter.Restore(record.Id);
        if (!string.IsNullOrWhiteSpace(result.RestoredPath))
            ShellChangeNotificationService.NotifyFileUpdated(result.RestoredPath);

        RefreshRecords(result.Record?.Id ?? record.Id);
        SetStatus(
            result.Message,
            result.Status == RecoveryRestoreStatus.Restored || result.Status == RecoveryRestoreStatus.AlreadyRestored
                ? RecoveryStatus.Ready
                : result.Status == RecoveryRestoreStatus.Failed
                    ? RecoveryStatus.Error
                    : RecoveryStatus.Warning);
    }

    private void RefreshRecords(string? selectId = null)
    {
        selectId ??= SelectedRecord?.Id;
        _records.Clear();
        foreach (var record in _recoveryCenter.ListRecent())
            _records.Add(record);

        RecordCountText.Text = _records.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(selectId))
        {
            var match = _records.FirstOrDefault(record => record.Id.Equals(selectId, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                RecordsList.SelectedItem = match;
        }

        if (RecordsList.SelectedItem is null && _records.Count > 0)
            RecordsList.SelectedIndex = 0;

        RenderSelectedRecord();
        SetStatus(_records.Count == 0 ? "No recovery records yet." : "Recovery records refreshed.", RecoveryStatus.Ready);
    }

    private void RenderSelectedRecord()
    {
        var record = SelectedRecord;
        var hasRecord = record is not null;
        EmptyState.Visibility = hasRecord ? Visibility.Collapsed : Visibility.Visible;
        DetailPanel.Visibility = hasRecord ? Visibility.Visible : Visibility.Collapsed;
        RevealButton.IsEnabled = hasRecord;
        RestoreButton.IsEnabled = record?.CanRestoreNow == true;

        if (record is null)
            return;

        DetailTitle.Text = record.Title;
        DetailDescription.Text = record.Description;
        KindText.Text = record.KindText;
        CreatedText.Text = record.CreatedText;
        StatusValueText.Text = record.RestoreStateText;
        OriginalPathText.Text = string.IsNullOrWhiteSpace(record.OriginalPath) ? "Not recorded" : record.OriginalPath;
        CurrentPathText.Text = string.IsNullOrWhiteSpace(record.CurrentPath) ? "Not recorded" : record.CurrentPath;
        RestoreHintText.Text = record.RestoreHint;
        ExpirationText.Text = record.ExpirationText;
        SidecarsList.ItemsSource = record.Sidecars;
        SidecarsPanel.Visibility = record.Sidecars.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetStatus(string message, RecoveryStatus status)
    {
        StatusText.Text = message;
        StatusDot.Fill = status switch
        {
            RecoveryStatus.Warning => Brush("YellowBrush"),
            RecoveryStatus.Error => Brush("RedBrush"),
            _ => Brush("GreenBrush")
        };
    }

    private Brush Brush(string key)
        => TryFindResource(key) as Brush ?? Brushes.Transparent;

    private enum RecoveryStatus
    {
        Ready,
        Warning,
        Error
    }
}
