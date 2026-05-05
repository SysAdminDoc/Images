using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
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
        SetStatus("Open an image to inspect its edit history.");

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
            SetStatus("No edit sidecar is available.");
            return;
        }

        try
        {
            ShellIntegration.RevealPathInExplorer(_snapshot.SidecarPath);
            SetStatus("Sidecar location opened.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            SetStatus("Sidecar has not been written yet.");
        }
    }

    private void CopySummaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot is null)
        {
            SetStatus("No edit history to copy.");
            return;
        }

        Clipboard.SetText(BuildSummary(_snapshot));
        SetStatus("Edit history summary copied.");
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
            SetStatus("Select an operation first.");
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
            Title = "Export edited copy",
            FileName = Path.GetFileNameWithoutExtension(imagePath) + suffix + sourceExtension,
            Filter = ImageExportService.ExportFilter,
            DefaultExt = sourceExtension.TrimStart('.'),
            AddExtension = true,
            InitialDirectory = Path.GetDirectoryName(imagePath)
        };

        if (dialog.ShowDialog(this) != true)
            return;

        SetBusy(true, "Exporting selected edit copy...");
        try
        {
            var result = await Task.Run(() => _editStack.Export(_imagePath!, dialog.FileName, SelectedCopyId));
            SetStatus(result.Success
                ? $"{result.Message} Output: {Path.GetFileName(result.OutputPath)}"
                : "Export failed: " + result.Message);
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
            ImagePathText.Text = "No image selected.";
            SidecarPathText.Text = "";
            OperationTitleText.Text = "Operations";
            EmptyHintText.Text = "";
            SetStatus("Open an image before using edit history.");
            return;
        }

        try
        {
            _snapshot = _editStack.LoadSnapshot(_imagePath);
            ImagePathText.Text = "Image: " + _imagePath;
            SidecarPathText.Text = "Sidecar: " + _snapshot.SidecarPath;

            _copyRows.Add(new EditCopyRow(
                NonDestructiveEditService.MasterCopyId,
                "Master",
                $"{_snapshot.Operations.Count} operation{Plural(_snapshot.Operations.Count)}"));

            foreach (var copy in _snapshot.VirtualCopies)
            {
                _copyRows.Add(new EditCopyRow(
                    copy.Id,
                    copy.Name,
                    $"{copy.Operations.Count} operation{Plural(copy.Operations.Count)} · {copy.CreatedUtc.LocalDateTime:g}"));
            }

            CopyList.SelectedItem = _copyRows.FirstOrDefault(row =>
                string.Equals(row.Id, preferredCopyId, StringComparison.OrdinalIgnoreCase)) ?? _copyRows.FirstOrDefault();

            RefreshOperations();
            SetStatus(File.Exists(_snapshot.SidecarPath)
                ? "Edit history loaded."
                : "No sidecar exists yet. Future edits will create one.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            SetStatus("Could not read edit history: " + ex.Message);
        }
    }

    private void RefreshOperations()
    {
        _operationRows.Clear();
        if (_snapshot is null)
            return;

        var selectedCopyId = SelectedCopyId;
        var selectedName = SelectedCopy?.Name ?? "Master";
        var operations = NonDestructiveEditService.GetOperations(_snapshot, selectedCopyId);
        foreach (var operation in operations)
            _operationRows.Add(EditOperationRow.From(operation));

        OperationTitleText.Text = $"{selectedName} operations";
        EmptyHintText.Text = operations.Count == 0
            ? "No edit operations yet. Crop, selection, and adjustment tools will add operations here instead of changing the source file."
            : $"{operations.Count(operation => operation.Enabled)} enabled operation{Plural(operations.Count(operation => operation.Enabled))} will apply when this copy is exported.";
    }

    private bool CanMutate()
    {
        if (_busy)
            return false;
        if (string.IsNullOrWhiteSpace(_imagePath) || !File.Exists(_imagePath))
        {
            SetStatus("Open an image before changing edit history.");
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
        sb.AppendLine($"Image: {snapshot.SourceFileName}");
        sb.AppendLine($"Sidecar: {snapshot.SidecarPath}");
        sb.AppendLine($"Updated: {snapshot.UpdatedUtc.LocalDateTime:g}");
        sb.AppendLine($"Master operations: {snapshot.Operations.Count}");
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
        return string.IsNullOrWhiteSpace(cleaned) ? "virtual-copy" : cleaned;
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
                operation.Enabled ? "Enabled" : "Disabled",
                string.IsNullOrWhiteSpace(operation.Label) ? operation.Kind : operation.Label,
                operation.Parameters.Count == 0
                    ? ""
                    : string.Join(", ", operation.Parameters
                        .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(item => $"{item.Key}={item.Value}")),
                operation.CreatedUtc.LocalDateTime.ToString("g"));
    }
}
