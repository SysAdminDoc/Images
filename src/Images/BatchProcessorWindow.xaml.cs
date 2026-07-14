using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Images.Localization;
using Images.Services;
using Microsoft.Win32;

namespace Images;

public partial class BatchProcessorWindow : Window
{
    private readonly BatchProcessorService _batch = new();
    private readonly ObservableCollection<string> _sourceRows = [];
    private readonly ObservableCollection<BatchOperationRow> _operationRows = [];
    private readonly ObservableCollection<BatchPreviewItem> _previewRows = [];
    private readonly ObservableCollection<string> _resultRows = [];
    private readonly IReadOnlyList<BatchOperationOption> _operationOptions =
    [
        new(BatchOperationKinds.Resize, "Resize", "Max width", "2400", "Max height", "2400"),
        new(BatchOperationKinds.Rotate, "Rotate", "Degrees", "90", null, null),
        new(BatchOperationKinds.FlipHorizontal, "Flip horizontal", null, null, null, null),
        new(BatchOperationKinds.FlipVertical, "Flip vertical", null, null, null, null),
        new(BatchOperationKinds.StripMetadata, "Strip metadata", "Categories", "all", null, null),
        new(BatchOperationKinds.RenamePattern, "Rename pattern", "Pattern", "{name}-{index}", null, null),
        new(BatchOperationKinds.ExportCopy, "Export copy", "Extension", ".jpg", "Quality", "88")
    ];
    private string? _outputFolder;
    private CancellationTokenSource? _batchCts;
    private bool _loadingPreset;

    public BatchProcessorWindow()
    {
        InitializeComponent();

        SourceList.ItemsSource = _sourceRows;
        OperationList.ItemsSource = _operationRows;
        OperationKindCombo.ItemsSource = _operationOptions;
        OperationKindCombo.SelectedIndex = 0;
        PreviewList.ItemsSource = _previewRows;
        ResultList.ItemsSource = _resultRows;
        PresetCombo.ItemsSource = BatchProcessorPreset.Defaults;
        PresetCombo.DisplayMemberPath = nameof(BatchProcessorPreset.Name);
        PresetCombo.SelectedIndex = 0;
        ApplyPreset(BatchProcessorPreset.Defaults[0]);

        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChrome.ApplyDarkCaption(hwnd);
        };
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Closing the window must stop the batch — it writes files, and
        // without this the run keeps producing output with no way to cancel.
        _batchCts?.Cancel();
        base.OnClosing(e);
    }

    public void AddSource(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var fullPath = Path.GetFullPath(path);
            if ((File.Exists(fullPath) || Directory.Exists(fullPath)) &&
                !_sourceRows.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
            {
                _sourceRows.Add(fullPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            SetStatus(Strings.BatchCouldNotAddSource, BatchProcessorStatus.Warning);
        }
    }

    private void AddFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.BatchAddFilesDialogTitle,
            Filter = SupportedImageFormats.OpenDialogFilter,
            Multiselect = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        foreach (var path in dialog.FileNames)
            AddSource(path);
        SetStatus(Strings.Format("BatchAddedSourceFilesFormat", dialog.FileNames.Length, Plural(dialog.FileNames.Length)), BatchProcessorStatus.Ready);
    }

    private void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = PickFolder(Strings.BatchAddFolderDialogTitle);
        if (folder is null)
            return;

        AddSource(folder);
        SetStatus(Strings.BatchAddedSourceFolder, BatchProcessorStatus.Ready);
    }

    private void ClearSourcesButton_Click(object sender, RoutedEventArgs e)
    {
        _sourceRows.Clear();
        _previewRows.Clear();
        SetStatus(Strings.BatchSourcesCleared, BatchProcessorStatus.Ready);
    }

    private void ChooseOutputButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = PickFolder(Strings.BatchChooseOutputFolderDialogTitle);
        if (folder is null)
            return;

        _outputFolder = folder;
        OutputFolderText.Text = Strings.Format("BatchOutputFolderFormat", folder);
        OutputFolderText.ToolTip = folder;
    }

    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingPreset)
            return;
        if (PresetCombo.SelectedItem is BatchProcessorPreset preset)
            ApplyPreset(preset);
    }

    private void QualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (QualityText is not null)
            QualityText.Text = ((int)Math.Round(e.NewValue)).ToString(CultureInfo.InvariantCulture);
        SettingsChanged(sender, e);
    }

    private void SettingsChanged(object sender, RoutedEventArgs e)
    {
        if (_loadingPreset)
            return;
        SyncLegacyControlsToOperations();
        PreviewSummaryText.Text = Strings.BatchSettingsChanged;
    }

    private async void PreviewButton_Click(object sender, RoutedEventArgs e)
        => await BuildPreviewAsync();

    private void CancelButton_Click(object sender, RoutedEventArgs e)
        => _batchCts?.Cancel();

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        var sources = CollectSourceFiles();
        if (sources.Count == 0)
        {
            SetStatus(Strings.BatchAddSourcesBeforeRunning, BatchProcessorStatus.Warning);
            return;
        }

        SetBusy(true);
        _resultRows.Clear();
        var preset = ReadPreset();
        var dryRun = DryRunCheckBox.IsChecked == true;
        SetStatus(Strings.Format("BatchRunningFormat", sources.Count, Plural(sources.Count)), BatchProcessorStatus.Busy);
        _batchCts = new CancellationTokenSource();

        try
        {
            var token = _batchCts.Token;
            var progress = new Progress<BatchProgressUpdate>(update =>
                SetStatus($"{update.CompletedCount}/{update.TotalCount} — {update.FileName}", BatchProcessorStatus.Busy));
            var result = await _batch.RunAsync(sources, preset, _outputFolder, dryRun, progress: progress, cancellationToken: token);
            foreach (var row in BuildResultRows(result.Items))
                _resultRows.Add(row);

            SetStatus(
                Strings.Format("BatchCompleteFormat", result.SuccessCount, result.FailedCount),
                result.FailedCount == 0 ? BatchProcessorStatus.Ready : BatchProcessorStatus.Warning);
        }
        catch (OperationCanceledException)
        {
            SetStatus(Strings.BatchCanceled, BatchProcessorStatus.Warning);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var message = Strings.Format("BatchResultFailedFormat", Strings.BatchRunBatch, ex.Message);
            _resultRows.Add(message);
            SetStatus(message, BatchProcessorStatus.Error);
        }
        finally
        {
            _batchCts?.Dispose();
            _batchCts = null;
            SetBusy(false);
        }
    }

    internal static IReadOnlyList<string> BuildResultRows(IEnumerable<MacroRunItemResult?> items)
    {
        var rows = new List<string>();
        foreach (var item in items)
        {
            if (item is null)
                continue;

            rows.Add(item.Success
                ? Strings.Format("BatchResultOkFormat", Path.GetFileName(item.SourcePath), item.FinalPath)
                : Strings.Format("BatchResultFailedFormat", Path.GetFileName(item.SourcePath), item.Error));
            foreach (var message in item.Messages)
                rows.Add("  " + message);
        }

        return rows;
    }

    private async Task BuildPreviewAsync()
    {
        var sources = CollectSourceFiles();
        if (sources.Count == 0)
        {
            SetStatus(Strings.BatchAddSourcesBeforePreview, BatchProcessorStatus.Warning);
            return;
        }

        SetBusy(true);
        SetStatus(Strings.BatchBuildingPreview, BatchProcessorStatus.Busy);
        _batchCts = new CancellationTokenSource();
        try
        {
            var preset = ReadPreset();
            var token = _batchCts.Token;
            var result = await Task.Run(() => _batch.BuildPreview(sources, preset, _outputFolder, token), token);
            _previewRows.Clear();
            foreach (var item in result.Items)
                _previewRows.Add(item);

            PreviewSummaryText.Text = Strings.Format("BatchPreviewSummaryFormat", result.Items.Count, Plural(result.Items.Count), result.FailedCount);
            SetStatus(Strings.BatchPreviewReady, BatchProcessorStatus.Ready);
        }
        catch (OperationCanceledException)
        {
            SetStatus(Strings.BatchCanceled, BatchProcessorStatus.Warning);
        }
        finally
        {
            _batchCts?.Dispose();
            _batchCts = null;
            SetBusy(false);
        }
    }

    private void LoadPresetButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.BatchLoadPresetDialogTitle,
            Filter = $"{Strings.BatchPresetFilterLabel}|*.json|{Strings.BatchAllFilesFilterLabel}|*.*"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var json = BoundedTextFileReader.ReadUtf8(
                dialog.FileName,
                BoundedTextFileReader.MaxWorkflowImportBytes,
                "Batch preset");
            ApplyPreset(BatchProcessorService.ParsePreset(json));
            SetStatus(Strings.BatchPresetLoaded, BatchProcessorStatus.Ready);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.Text.Json.JsonException)
        {
            SetStatus(Strings.BatchCouldNotLoadPreset, BatchProcessorStatus.Error);
        }
    }

    private void SavePresetButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = Strings.BatchSavePresetDialogTitle,
            Filter = $"{Strings.BatchPresetFilterLabel}|*.json|{Strings.BatchAllFilesFilterLabel}|*.*",
            FileName = "images-batch-preset.json"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            File.WriteAllText(dialog.FileName, BatchProcessorService.SerializePreset(ReadPreset()));
            SetStatus(Strings.BatchPresetSaved, BatchProcessorStatus.Ready);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            SetStatus(Strings.BatchCouldNotSavePreset, BatchProcessorStatus.Error);
        }
    }

    private void OperationKindCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OperationKindCombo.SelectedItem is not BatchOperationOption option)
            return;

        SetOperationParameterControls(option);
    }

    private void AddOperationButton_Click(object sender, RoutedEventArgs e)
    {
        if (OperationKindCombo.SelectedItem is not BatchOperationOption option)
            return;

        _operationRows.Add(new BatchOperationRow(CreateOperation(option)));
        PreviewSummaryText.Text = Strings.BatchSettingsChanged;
    }

    private void MoveOperationUpButton_Click(object sender, RoutedEventArgs e)
    {
        var index = OperationList.SelectedIndex;
        if (index <= 0)
            return;

        _operationRows.Move(index, index - 1);
        OperationList.SelectedIndex = index - 1;
        PreviewSummaryText.Text = Strings.BatchSettingsChanged;
    }

    private void MoveOperationDownButton_Click(object sender, RoutedEventArgs e)
    {
        var index = OperationList.SelectedIndex;
        if (index < 0 || index >= _operationRows.Count - 1)
            return;

        _operationRows.Move(index, index + 1);
        OperationList.SelectedIndex = index + 1;
        PreviewSummaryText.Text = Strings.BatchSettingsChanged;
    }

    private void RemoveOperationButton_Click(object sender, RoutedEventArgs e)
    {
        var index = OperationList.SelectedIndex;
        if (index < 0 || index >= _operationRows.Count)
            return;

        _operationRows.RemoveAt(index);
        OperationList.SelectedIndex = Math.Min(index, _operationRows.Count - 1);
        PreviewSummaryText.Text = Strings.BatchSettingsChanged;
    }

    private void ApplyPreset(BatchProcessorPreset preset)
    {
        _loadingPreset = true;
        preset = BatchProcessorService.NormalizePreset(preset);
        PresetNameBox.Text = preset.Name;
        ExtensionBox.Text = preset.Extension;
        QualitySlider.Value = preset.Quality;
        QualityText.Text = preset.Quality.ToString(CultureInfo.InvariantCulture);
        MaxWidthBox.Text = preset.MaxWidth.ToString(CultureInfo.InvariantCulture);
        MaxHeightBox.Text = preset.MaxHeight.ToString(CultureInfo.InvariantCulture);
        LoadOperations(preset.Operations ?? []);
        _loadingPreset = false;
        PreviewSummaryText.Text = Strings.BatchPresetReady;
    }

    private BatchProcessorPreset ReadPreset()
    {
        SyncLegacyControlsToOperations();
        return BatchProcessorService.NormalizePreset(new BatchProcessorPreset(
            PresetNameBox.Text,
            ExtensionBox.Text,
            (int)Math.Round(QualitySlider.Value),
            Math.Max(0, ParseInt(MaxWidthBox.Text)),
            Math.Max(0, ParseInt(MaxHeightBox.Text)),
            _operationRows.Select(row => row.Step).ToList()));
    }

    private void LoadOperations(IReadOnlyList<BatchOperationStep> operations)
    {
        _operationRows.Clear();
        foreach (var operation in operations)
            _operationRows.Add(new BatchOperationRow(operation));
    }

    private void SyncLegacyControlsToOperations()
    {
        if (_loadingPreset || _operationRows.Count == 0)
            return;

        var extension = RenameService.NormalizeExtension(ExtensionBox.Text);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".png";

        var quality = Math.Clamp((int)Math.Round(QualitySlider.Value), 1, 100);
        var maxWidth = Math.Max(0, ParseInt(MaxWidthBox.Text));
        var maxHeight = Math.Max(0, ParseInt(MaxHeightBox.Text));

        UpsertOperation(
            BatchOperationKinds.ExportCopy,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["extension"] = extension,
                ["quality"] = quality.ToString(CultureInfo.InvariantCulture)
            },
            insertBeforeExport: false);

        var resizeIndex = FindOperationIndex(BatchOperationKinds.Resize);
        if (maxWidth > 0 || maxHeight > 0)
        {
            UpsertOperation(
                BatchOperationKinds.Resize,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["maxWidth"] = maxWidth.ToString(CultureInfo.InvariantCulture),
                    ["maxHeight"] = maxHeight.ToString(CultureInfo.InvariantCulture)
                },
                insertBeforeExport: true);
        }
        else if (resizeIndex >= 0)
        {
            _operationRows.RemoveAt(resizeIndex);
        }
    }

    private void UpsertOperation(string kind, IReadOnlyDictionary<string, string> parameters, bool insertBeforeExport)
    {
        var index = FindOperationIndex(kind);
        var row = new BatchOperationRow(new BatchOperationStep(kind, parameters));
        if (index >= 0)
        {
            _operationRows[index] = row;
            return;
        }

        if (!insertBeforeExport)
        {
            _operationRows.Add(row);
            return;
        }

        var exportIndex = FindOperationIndex(BatchOperationKinds.ExportCopy);
        if (exportIndex >= 0)
            _operationRows.Insert(exportIndex, row);
        else
            _operationRows.Add(row);
    }

    private int FindOperationIndex(string kind)
    {
        for (var i = 0; i < _operationRows.Count; i++)
        {
            if (string.Equals(_operationRows[i].Step.Kind, kind, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private BatchOperationStep CreateOperation(BatchOperationOption option)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        switch (option.Kind)
        {
            case BatchOperationKinds.Resize:
                parameters["maxWidth"] = string.IsNullOrWhiteSpace(OperationParamOneBox.Text) ? "2400" : OperationParamOneBox.Text.Trim();
                parameters["maxHeight"] = string.IsNullOrWhiteSpace(OperationParamTwoBox.Text) ? "2400" : OperationParamTwoBox.Text.Trim();
                break;
            case BatchOperationKinds.Rotate:
                parameters["degrees"] = string.IsNullOrWhiteSpace(OperationParamOneBox.Text) ? "90" : OperationParamOneBox.Text.Trim();
                break;
            case BatchOperationKinds.StripMetadata:
                parameters["categories"] = string.IsNullOrWhiteSpace(OperationParamOneBox.Text) ? "all" : OperationParamOneBox.Text.Trim();
                break;
            case BatchOperationKinds.RenamePattern:
                parameters["pattern"] = string.IsNullOrWhiteSpace(OperationParamOneBox.Text) ? "{name}-{index}" : OperationParamOneBox.Text.Trim();
                break;
            case BatchOperationKinds.ExportCopy:
                parameters["extension"] = string.IsNullOrWhiteSpace(OperationParamOneBox.Text) ? ".jpg" : OperationParamOneBox.Text.Trim();
                parameters["quality"] = string.IsNullOrWhiteSpace(OperationParamTwoBox.Text) ? "88" : OperationParamTwoBox.Text.Trim();
                break;
        }

        return BatchProcessorService.NormalizePreset(new BatchProcessorPreset(
            "Operation",
            ExtensionBox.Text,
            (int)Math.Round(QualitySlider.Value),
            ParseInt(MaxWidthBox.Text),
            ParseInt(MaxHeightBox.Text),
            [new BatchOperationStep(option.Kind, parameters)])).Operations![0];
    }

    private void SetOperationParameterControls(BatchOperationOption option)
    {
        var hasOne = !string.IsNullOrWhiteSpace(option.ParameterOneLabel);
        var hasTwo = !string.IsNullOrWhiteSpace(option.ParameterTwoLabel);
        OperationParameterGrid.Visibility = hasOne || hasTwo ? Visibility.Visible : Visibility.Collapsed;

        OperationParamOneLabel.Visibility = hasOne ? Visibility.Visible : Visibility.Collapsed;
        OperationParamOneBox.Visibility = hasOne ? Visibility.Visible : Visibility.Collapsed;
        OperationParamOneLabel.Text = option.ParameterOneLabel ?? "";
        OperationParamOneBox.Text = option.ParameterOneDefault ?? "";

        OperationParamTwoLabel.Visibility = hasTwo ? Visibility.Visible : Visibility.Collapsed;
        OperationParamTwoBox.Visibility = hasTwo ? Visibility.Visible : Visibility.Collapsed;
        OperationParamTwoLabel.Text = option.ParameterTwoLabel ?? "";
        OperationParamTwoBox.Text = option.ParameterTwoDefault ?? "";
    }

    private List<string> CollectSourceFiles()
    {
        var result = WorkflowSourceCollector.Collect(_sourceRows);
        foreach (var source in result.SkippedSources)
            _resultRows.Add(Strings.Format("BatchSkippedSourceFormat", source));
        return result.Files.ToList();
    }

    private void SetBusy(bool busy)
    {
        AddFilesButton.IsEnabled = !busy;
        AddFolderButton.IsEnabled = !busy;
        PreviewButton.IsEnabled = !busy;
        RunButton.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;
        PresetCombo.IsEnabled = !busy;
        OperationKindCombo.IsEnabled = !busy;
        AddOperationButton.IsEnabled = !busy;
        MoveOperationUpButton.IsEnabled = !busy;
        MoveOperationDownButton.IsEnabled = !busy;
        RemoveOperationButton.IsEnabled = !busy;
    }

    private void SetStatus(string message, BatchProcessorStatus status)
    {
        StatusText.Text = message;
        StatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, status switch
        {
            BatchProcessorStatus.Busy => "AccentBrush",
            BatchProcessorStatus.Warning => "YellowBrush",
            BatchProcessorStatus.Error => "RedBrush",
            _ => "GreenBrush"
        });
    }

    private static int ParseInt(string value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;

    private static string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    private static string Plural(int count) => count == 1 ? "" : "s";

    private sealed record BatchOperationOption(
        string Kind,
        string Name,
        string? ParameterOneLabel,
        string? ParameterOneDefault,
        string? ParameterTwoLabel,
        string? ParameterTwoDefault);

    private sealed class BatchOperationRow(BatchOperationStep step)
    {
        public BatchOperationStep Step { get; } = step;
        public string Name => Step.Kind switch
        {
            BatchOperationKinds.Resize => "Resize",
            BatchOperationKinds.Rotate => "Rotate",
            BatchOperationKinds.FlipHorizontal => "Flip horizontal",
            BatchOperationKinds.FlipVertical => "Flip vertical",
            BatchOperationKinds.StripMetadata => "Strip metadata",
            BatchOperationKinds.RenamePattern => "Rename pattern",
            BatchOperationKinds.ExportCopy => "Export copy",
            _ => Step.Kind
        };

        public string Summary => BatchProcessorService.DescribeOperation(Step);
    }

    private enum BatchProcessorStatus
    {
        Ready,
        Busy,
        Warning,
        Error
    }
}
