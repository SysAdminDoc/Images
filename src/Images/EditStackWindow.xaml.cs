using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Images.Localization;
using Images.Services;
using Microsoft.Win32;

namespace Images;

public partial class EditStackWindow : Window
{
    private readonly NonDestructiveEditService _editStack = new();
    private readonly ObservableCollection<EditCopyRow> _copyRows = [];
    private readonly ObservableCollection<EditOperationRow> _operationRows = [];
    private string? _imagePath;
    private EditStackSnapshot? _snapshot;
    private bool _busy;

    public EditStackWindow()
    {
        InitializeComponent();
        CopyList.ItemsSource = _copyRows;
        OperationList.ItemsSource = _operationRows;
        SetStatus(Strings.EditHistoryInitialStatus);

        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChrome.ApplyDarkCaption(hwnd);
        };
    }

    public void SetCurrentImage(string? path)
    {
        _imagePath = path;
        Reload();
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
        => Reload();

    private void CopyList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => RefreshOperations();

    private void RevealSidecarButton_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot is null)
        {
            SetStatus(Strings.EditNoSidecarAvailable);
            return;
        }

        try
        {
            ShellIntegration.RevealPathInExplorer(_snapshot.SidecarPath);
            SetStatus(Strings.EditSidecarLocationOpened);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            SetStatus(Strings.EditSidecarNotWritten);
        }
    }

    private void CopySummaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot is null)
        {
            SetStatus(Strings.EditNoHistoryToCopy);
            return;
        }

        Clipboard.SetText(BuildSummary(_snapshot));
        SetStatus(Strings.EditHistorySummaryCopied);
    }

    private void CreateVirtualCopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!CanMutate())
            return;

        var seedCopyId = SelectedCopyId;
        var result = _editStack.CreateVirtualCopy(_imagePath!, seedCopyId: seedCopyId);
        SetStatus(result.Message);
        Reload(result.VirtualCopy?.Id);
    }

    private void ToggleOperationButton_Click(object sender, RoutedEventArgs e)
    {
        if (!CanMutate())
            return;

        if (OperationList.SelectedItem is not EditOperationRow operation)
        {
            SetStatus(Strings.EditSelectOperationFirst);
            return;
        }

        var result = _editStack.SetOperationEnabled(
            _imagePath!,
            operation.Id,
            !operation.Enabled,
            SelectedCopyId);
        SetStatus(result.Message);
        Reload(SelectedCopyId);
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (!CanMutate() || _snapshot is null)
            return;

        var selectedCopy = SelectedCopy;
        var suffix = selectedCopy is null || selectedCopy.Id == NonDestructiveEditService.MasterCopyId
            ? "_edited"
            : "_" + SanitizeFileName(selectedCopy.Name);
        var imagePath = _imagePath!;
        var sourceExtension = ImageExportService.NormalizeExportExtension(Path.GetExtension(imagePath));

        var dialog = new SaveFileDialog
        {
            Title = Strings.ExportEditedCopyDialogTitle,
            FileName = Path.GetFileNameWithoutExtension(imagePath) + suffix + sourceExtension,
            Filter = ImageExportService.ExportFilter,
            DefaultExt = sourceExtension.TrimStart('.'),
            AddExtension = true,
            InitialDirectory = Path.GetDirectoryName(imagePath)
        };

        if (dialog.ShowDialog(this) != true)
            return;

        SetBusy(true, Strings.ExportEditedCopyStatus);
        try
        {
            var result = await Task.Run(() => _editStack.Export(_imagePath!, dialog.FileName, SelectedCopyId));
            SetStatus(result.Success
                ? Strings.Format("ExportEditedCopySuccessFormat", result.Message, Path.GetFileName(result.OutputPath))
                : Strings.Format("ExportEditedCopyFailedFormat", result.Message));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Reload(string? preferredCopyId = null)
    {
        _copyRows.Clear();
        _operationRows.Clear();

        if (string.IsNullOrWhiteSpace(_imagePath) || !File.Exists(_imagePath))
        {
            _snapshot = null;
            ImagePathText.Text = Strings.EditNoImageSelected;
            SidecarPathText.Text = "";
            OperationTitleText.Text = Strings.EditOperationsHeading;
            EmptyHintText.Text = "";
            SetStatus(Strings.EditOpenImageBeforeUsing);
            return;
        }

        try
        {
            _snapshot = _editStack.LoadSnapshot(_imagePath);
            ImagePathText.Text = Strings.Format("EditImagePathFormat", _imagePath);
            SidecarPathText.Text = Strings.Format("EditSidecarPathFormat", _snapshot.SidecarPath);

            _copyRows.Add(new EditCopyRow(
                NonDestructiveEditService.MasterCopyId,
                Strings.MasterCopyName,
                Strings.Format("EditOperationCountFormat", _snapshot.Operations.Count, Plural(_snapshot.Operations.Count))));

            foreach (var copy in _snapshot.VirtualCopies)
            {
                _copyRows.Add(new EditCopyRow(
                    copy.Id,
                    copy.Name,
                    Strings.Format("EditVirtualCopyWithDateFormat", copy.Operations.Count, Plural(copy.Operations.Count), copy.CreatedUtc.LocalDateTime)));
            }

            CopyList.SelectedItem = _copyRows.FirstOrDefault(row =>
                string.Equals(row.Id, preferredCopyId, StringComparison.OrdinalIgnoreCase)) ?? _copyRows.FirstOrDefault();

            RefreshOperations();
            SetStatus(File.Exists(_snapshot.SidecarPath)
                ? Strings.EditHistoryLoaded
                : Strings.EditNoSidecarYet);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            SetStatus(Strings.Format("EditReadFailedFormat", ex.Message));
        }
    }

    private void RefreshOperations()
    {
        _operationRows.Clear();
        if (_snapshot is null)
            return;

        var selectedCopyId = SelectedCopyId;
        var selectedName = SelectedCopy?.Name ?? Strings.MasterCopyName;
        var operations = NonDestructiveEditService.GetOperations(_snapshot, selectedCopyId);
        foreach (var operation in operations)
            _operationRows.Add(EditOperationRow.From(operation));

        OperationTitleText.Text = Strings.Format("EditOperationsHeadingFormat", selectedName);
        EmptyHintText.Text = operations.Count == 0
            ? Strings.EditNoOperationsYet
            : Strings.Format(
                "EditOperationEnabledExportFormat",
                operations.Count(operation => operation.Enabled),
                Plural(operations.Count(operation => operation.Enabled)));
    }

    private bool CanMutate()
    {
        if (_busy)
            return false;
        if (string.IsNullOrWhiteSpace(_imagePath) || !File.Exists(_imagePath))
        {
            SetStatus(Strings.EditOpenImageBeforeChanging);
            return false;
        }

        return true;
    }

    private EditCopyRow? SelectedCopy => CopyList.SelectedItem as EditCopyRow;

    private string SelectedCopyId => SelectedCopy?.Id ?? NonDestructiveEditService.MasterCopyId;

    private void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
        if (!string.IsNullOrWhiteSpace(message))
            SetStatus(message);
    }

    private void SetStatus(string message)
        => StatusText.Text = message;

    private static string BuildSummary(EditStackSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Strings.Format("EditSummaryImageFormat", snapshot.SourceFileName));
        sb.AppendLine(Strings.Format("EditSummarySidecarFormat", snapshot.SidecarPath));
        sb.AppendLine(Strings.Format("EditSummaryUpdatedFormat", snapshot.UpdatedUtc.LocalDateTime));
        sb.AppendLine(Strings.Format("EditSummaryMasterOperationsFormat", snapshot.Operations.Count));
        foreach (var operation in snapshot.Operations)
            sb.AppendLine("- " + NonDestructiveEditService.FormatOperation(operation));

        foreach (var copy in snapshot.VirtualCopies)
        {
            sb.AppendLine();
            sb.AppendLine($"{copy.Name} ({copy.Id})");
            foreach (var operation in copy.Operations)
                sb.AppendLine("- " + NonDestructiveEditService.FormatOperation(operation));
        }

        return sb.ToString();
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray()).Trim('-', ' ');
        return string.IsNullOrWhiteSpace(cleaned) ? Strings.EditVirtualCopyFileNameFallback : cleaned;
    }

    private static string Plural(int count) => count == 1 ? "" : "s";

    private sealed record EditCopyRow(string Id, string Name, string Detail);

    private sealed record EditOperationRow(
        string Id,
        bool Enabled,
        string State,
        string Label,
        string Parameters,
        string Created)
    {
        public static EditOperationRow From(EditOperation operation)
            => new(
                operation.Id,
                operation.Enabled,
                operation.Enabled ? Strings.EditOperationEnabledState : Strings.EditOperationDisabledState,
                string.IsNullOrWhiteSpace(operation.Label) ? operation.Kind : operation.Label,
                operation.Parameters.Count == 0
                    ? ""
                    : string.Join(", ", operation.Parameters
                        .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(item => $"{item.Key}={item.Value}")),
                operation.CreatedUtc.LocalDateTime.ToString("g"));
    }
}
