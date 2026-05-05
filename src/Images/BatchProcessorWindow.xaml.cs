using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Images.Services;
using Microsoft.Win32;

namespace Images;

public partial class BatchProcessorWindow : Window
{
    private readonly BatchProcessorService _batch = new();
    private readonly ObservableCollection<string> _sourceRows = [];
    private readonly ObservableCollection<BatchPreviewItem> _previewRows = [];
    private readonly ObservableCollection<string> _resultRows = [];
    private string? _outputFolder;
    private bool _loadingPreset;

    public BatchProcessorWindow()
    {
        InitializeComponent();

        SourceList.ItemsSource = _sourceRows;
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
            SetStatus("Could not add that batch source.", BatchProcessorStatus.Warning);
        }
    }

    private void AddFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add files to batch processor",
            Filter = SupportedImageFormats.OpenDialogFilter,
            Multiselect = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        foreach (var path in dialog.FileNames)
            AddSource(path);
        SetStatus($"Added {dialog.FileNames.Length} source file{Plural(dialog.FileNames.Length)}.", BatchProcessorStatus.Ready);
    }

    private void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = PickFolder("Add folder to batch processor");
        if (folder is null)
            return;

        AddSource(folder);
        SetStatus("Added source folder.", BatchProcessorStatus.Ready);
    }

    private void ClearSourcesButton_Click(object sender, RoutedEventArgs e)
    {
        _sourceRows.Clear();
        _previewRows.Clear();
        SetStatus("Sources cleared.", BatchProcessorStatus.Ready);
    }

    private void ChooseOutputButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = PickFolder("Choose batch output folder");
        if (folder is null)
            return;

        _outputFolder = folder;
        OutputFolderText.Text = "Output folder: " + folder;
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
        PreviewSummaryText.Text = "Settings changed. Build a new preview before running.";
    }

    private async void PreviewButton_Click(object sender, RoutedEventArgs e)
        => await BuildPreviewAsync();

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        var sources = CollectSourceFiles();
        if (sources.Count == 0)
        {
            SetStatus("Add supported source files before running a batch.", BatchProcessorStatus.Warning);
            return;
        }

        SetBusy(true);
        _resultRows.Clear();
        var preset = ReadPreset();
        var dryRun = DryRunCheckBox.IsChecked == true;
        SetStatus($"Running batch for {sources.Count} file{Plural(sources.Count)}...", BatchProcessorStatus.Busy);

        try
        {
            var result = await Task.Run(() => _batch.Run(sources, preset, _outputFolder, dryRun));
            foreach (var item in result.Items)
            {
                _resultRows.Add(item.Success
                    ? $"OK: {Path.GetFileName(item.SourcePath)} -> {item.FinalPath}"
                    : $"Failed: {Path.GetFileName(item.SourcePath)} - {item.Error}");
                foreach (var message in item.Messages)
                    _resultRows.Add("  " + message);
            }

            SetStatus(
                $"Batch complete: {result.SuccessCount} succeeded, {result.FailedCount} failed.",
                result.FailedCount == 0 ? BatchProcessorStatus.Ready : BatchProcessorStatus.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task BuildPreviewAsync()
    {
        var sources = CollectSourceFiles();
        if (sources.Count == 0)
        {
            SetStatus("Add supported source files before building a preview.", BatchProcessorStatus.Warning);
            return;
        }

        SetBusy(true);
        SetStatus("Building batch preview...", BatchProcessorStatus.Busy);
        try
        {
            var preset = ReadPreset();
            var result = await Task.Run(() => _batch.BuildPreview(sources, preset, _outputFolder));
            _previewRows.Clear();
            foreach (var item in result.Items)
                _previewRows.Add(item);

            PreviewSummaryText.Text = $"Preview: {result.Items.Count} file{Plural(result.Items.Count)}, {result.FailedCount} skipped.";
            SetStatus("Batch preview ready.", BatchProcessorStatus.Ready);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void LoadPresetButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load batch preset",
            Filter = "Batch preset JSON|*.json|All files|*.*"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            ApplyPreset(BatchProcessorService.ParsePreset(File.ReadAllText(dialog.FileName)));
            SetStatus("Batch preset loaded.", BatchProcessorStatus.Ready);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.Text.Json.JsonException)
        {
            SetStatus("Could not load batch preset.", BatchProcessorStatus.Error);
        }
    }

    private void SavePresetButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save batch preset",
            Filter = "Batch preset JSON|*.json|All files|*.*",
            FileName = "images-batch-preset.json"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            File.WriteAllText(dialog.FileName, BatchProcessorService.SerializePreset(ReadPreset()));
            SetStatus("Batch preset saved.", BatchProcessorStatus.Ready);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            SetStatus("Could not save batch preset.", BatchProcessorStatus.Error);
        }
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
        _loadingPreset = false;
        PreviewSummaryText.Text = "Preset ready. Build a preview to inspect output paths and dimensions.";
    }

    private BatchProcessorPreset ReadPreset()
        => BatchProcessorService.NormalizePreset(new BatchProcessorPreset(
            PresetNameBox.Text,
            ExtensionBox.Text,
            (int)Math.Round(QualitySlider.Value),
            Math.Max(0, ParseInt(MaxWidthBox.Text)),
            Math.Max(0, ParseInt(MaxHeightBox.Text))));

    private List<string> CollectSourceFiles()
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in _sourceRows)
        {
            try
            {
                if (File.Exists(row))
                {
                    if (SupportedImageFormats.IsSupported(row))
                        files.Add(Path.GetFullPath(row));
                    continue;
                }

                if (!Directory.Exists(row))
                    continue;

                foreach (var file in Directory.EnumerateFiles(row, "*", SearchOption.AllDirectories))
                {
                    if (SupportedImageFormats.IsSupported(file))
                        files.Add(Path.GetFullPath(file));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                _resultRows.Add($"Skipped source: {row}");
            }
        }

        return files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void SetBusy(bool busy)
    {
        AddFilesButton.IsEnabled = !busy;
        AddFolderButton.IsEnabled = !busy;
        PreviewButton.IsEnabled = !busy;
        RunButton.IsEnabled = !busy;
        PresetCombo.IsEnabled = !busy;
    }

    private void SetStatus(string message, BatchProcessorStatus status)
    {
        StatusText.Text = message;
        StatusDot.Fill = status switch
        {
            BatchProcessorStatus.Busy => Brush("AccentBrush"),
            BatchProcessorStatus.Warning => Brush("YellowBrush"),
            BatchProcessorStatus.Error => Brush("RedBrush"),
            _ => Brush("GreenBrush")
        };
    }

    private Brush Brush(string key)
        => TryFindResource(key) as Brush ?? Brushes.Transparent;

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

    private enum BatchProcessorStatus
    {
        Ready,
        Busy,
        Warning,
        Error
    }
}
