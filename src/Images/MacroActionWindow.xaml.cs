using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Images.Localization;
using Images.Services;
using Microsoft.Win32;

namespace Images;

public partial class MacroActionWindow : Window
{
    private readonly MacroActionService _macro = new();
    private readonly ObservableCollection<string> _sourceRows = [];
    private readonly ObservableCollection<string> _resultRows = [];
    private readonly ObservableCollection<PipelineStepViewModel> _steps = [];
    private string? _outputFolder;
    private CancellationTokenSource? _cts;

    public MacroActionWindow()
    {
        InitializeComponent();

        SourceList.ItemsSource = _sourceRows;
        ResultList.ItemsSource = _resultRows;
        StepList.ItemsSource = _steps;

        // Seed with the default plan's actions.
        foreach (var action in MacroActionPlan.Default.Actions)
            _steps.Add(PipelineStepViewModel.From(action, _steps.Count + 1));

        RefreshEmptyState();
        RefreshSummary();

        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChrome.ApplyDarkCaption(hwnd);
        };
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        base.OnClosing(e);
    }

    // ---- Source management (unchanged logic) ----

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
            SetStatus(Strings.MacroCouldNotAddSource, MacroActionStatus.Warning);
        }
    }

    private void AddFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.MacroAddFilesDialogTitle,
            Filter = SupportedImageFormats.OpenDialogFilter,
            Multiselect = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        foreach (var path in dialog.FileNames)
            AddSource(path);
        SetStatus(Strings.Format(nameof(Strings.MacroAddedSourceFilesFormat), dialog.FileNames.Length, Plural(dialog.FileNames.Length)), MacroActionStatus.Ready);
    }

    private void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = PickFolder(Strings.MacroAddFolderDialogTitle);
        if (folder is null)
            return;

        AddSource(folder);
        SetStatus(Strings.MacroAddedSourceFolder, MacroActionStatus.Ready);
    }

    private void ClearSourcesButton_Click(object sender, RoutedEventArgs e)
    {
        _sourceRows.Clear();
        SetStatus(Strings.MacroSourcesCleared, MacroActionStatus.Ready);
    }

    private void ChooseOutputButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = PickFolder(Strings.MacroChooseOutputFolderDialogTitle);
        if (folder is null)
            return;

        _outputFolder = folder;
        OutputFolderText.Text = Strings.Format(nameof(Strings.MacroOutputFolderFormat), folder);
        OutputFolderText.ToolTip = folder;
    }

    // ---- Pipeline step management ----

    private void AddStripGpsStep_Click(object sender, RoutedEventArgs e)
        => AddStep(new MacroActionStep("strip-gps", new Dictionary<string, string>()));

    private void AddStripMetadataStep_Click(object sender, RoutedEventArgs e)
        => AddStep(new MacroActionStep(
            "strip-metadata",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["categories"] = "all"
            }));

    private void AddExportCopyStep_Click(object sender, RoutedEventArgs e)
        => AddStep(new MacroActionStep(
            "export-copy",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["extension"] = ".png",
                ["quality"] = "92",
                ["maxWidth"] = "0",
                ["maxHeight"] = "0"
            }));

    private void AddRenamePatternStep_Click(object sender, RoutedEventArgs e)
        => AddStep(new MacroActionStep(
            "rename-pattern",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["pattern"] = "{date}-{index}-{name}"
            }));

    private void AddStep(MacroActionStep action)
    {
        _steps.Add(PipelineStepViewModel.From(action, _steps.Count + 1));
        RefreshStepNumbers();
        RefreshEmptyState();
        RefreshSummary();
        SetStatus(Strings.Format(nameof(Strings.MacroAddedActionFormat), action.Kind), MacroActionStatus.Ready);
    }

    private void RemoveStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: PipelineStepViewModel vm })
            return;

        _steps.Remove(vm);
        RefreshStepNumbers();
        RefreshEmptyState();
        RefreshSummary();
    }

    private void MoveStepUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: PipelineStepViewModel vm })
            return;

        var index = _steps.IndexOf(vm);
        if (index <= 0)
            return;

        _steps.Move(index, index - 1);
        RefreshStepNumbers();
    }

    private void MoveStepDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: PipelineStepViewModel vm })
            return;

        var index = _steps.IndexOf(vm);
        if (index < 0 || index >= _steps.Count - 1)
            return;

        _steps.Move(index, index + 1);
        RefreshStepNumbers();
    }

    // ---- JSON import/export ----

    private void LoadPlanButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.MacroLoadDialogTitle,
            Filter = Strings.MacroLoadDialogFilter
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var json = File.ReadAllText(dialog.FileName);
            if (!MacroActionService.TryParse(json, out var plan, out var error))
            {
                SetStatus(error, MacroActionStatus.Error);
                return;
            }

            _steps.Clear();
            foreach (var action in plan.Actions)
                _steps.Add(PipelineStepViewModel.From(action, _steps.Count + 1));

            RefreshStepNumbers();
            RefreshEmptyState();
            RefreshSummary();
            SetStatus(Strings.Format(nameof(Strings.MacroValidResultFormat), plan.Actions.Count, Plural(plan.Actions.Count)), MacroActionStatus.Ready);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            SetStatus(Strings.MacroCouldNotLoad, MacroActionStatus.Error);
        }
    }

    private void SavePlanButton_Click(object sender, RoutedEventArgs e)
    {
        var plan = BuildPlanFromSteps();
        if (plan is null)
            return;

        var dialog = new SaveFileDialog
        {
            Title = Strings.MacroSaveDialogTitle,
            Filter = Strings.MacroSaveDialogFilter,
            FileName = "images-macro.json"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            File.WriteAllText(dialog.FileName, MacroActionService.Serialize(plan));
            SetStatus(Strings.MacroSaved, MacroActionStatus.Ready);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            SetStatus(Strings.MacroCouldNotSave, MacroActionStatus.Error);
        }
    }

    // ---- Preview ----

    private async void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        var plan = BuildPlanFromSteps();
        if (plan is null)
            return;

        var sources = CollectSourceFiles();
        if (sources.Count == 0)
        {
            SetStatus(Strings.MacroAddSourcesBeforeRunning, MacroActionStatus.Warning);
            return;
        }

        var options = new MacroRunOptions(_outputFolder, DryRun: true);
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        SetBusy(true);
        SetStatus(Strings.Format(nameof(Strings.MacroPreviewRunningFormat), sources.Count, Plural(sources.Count)), MacroActionStatus.Busy);
        PreviewHeading.Text = Strings.MacroPreviewResults;
        _resultRows.Clear();

        try
        {
            var result = await Task.Run(() => _macro.Run(plan, sources, options, token), token);
            foreach (var item in result.Items)
            {
                _resultRows.Add(item.Success
                    ? Strings.Format(nameof(Strings.MacroPreviewItemFormat), Path.GetFileName(item.SourcePath), item.FinalPath)
                    : Strings.Format(nameof(Strings.MacroPreviewItemFailedFormat), Path.GetFileName(item.SourcePath), item.Error));
                foreach (var message in item.Messages)
                    _resultRows.Add("  " + message);
            }

            SetStatus(Strings.Format(nameof(Strings.MacroPreviewCompleteFormat), result.Items.Count, Plural(result.Items.Count)), MacroActionStatus.Ready);
        }
        catch (OperationCanceledException)
        {
            SetStatus(Strings.MacroCancelled, MacroActionStatus.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ---- Run ----

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        var plan = BuildPlanFromSteps();
        if (plan is null)
            return;

        var sources = CollectSourceFiles();
        if (sources.Count == 0)
        {
            SetStatus(Strings.MacroAddSourcesBeforeRunning, MacroActionStatus.Warning);
            return;
        }

        var options = new MacroRunOptions(_outputFolder, DryRunCheckBox.IsChecked == true);
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        SetBusy(true);
        SetStatus(Strings.Format(nameof(Strings.MacroRunningFormat), plan.Actions.Count, Plural(plan.Actions.Count), sources.Count, Plural(sources.Count)), MacroActionStatus.Busy);
        PreviewHeading.Text = Strings.MacroRunLog;
        _resultRows.Clear();

        try
        {
            var result = await Task.Run(() => _macro.Run(plan, sources, options, token), token);
            foreach (var item in result.Items)
            {
                _resultRows.Add(item.Success
                    ? Strings.Format(nameof(Strings.MacroResultOkFormat), Path.GetFileName(item.SourcePath), item.FinalPath)
                    : Strings.Format(nameof(Strings.MacroResultFailedFormat), Path.GetFileName(item.SourcePath), item.Error));
                foreach (var message in item.Messages)
                    _resultRows.Add("  " + message);
            }

            SetStatus(
                Strings.Format(nameof(Strings.MacroCompleteFormat), result.SuccessCount, result.FailedCount),
                result.FailedCount == 0 ? MacroActionStatus.Ready : MacroActionStatus.Warning);
        }
        catch (OperationCanceledException)
        {
            SetStatus(Strings.MacroCancelled, MacroActionStatus.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ---- Cancel ----

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    // ---- Helpers ----

    private MacroActionPlan? BuildPlanFromSteps()
    {
        if (_steps.Count == 0)
        {
            SetStatus(Strings.MacroAddStepsFirst, MacroActionStatus.Warning);
            return null;
        }

        var actions = new List<MacroActionStep>(_steps.Count);
        foreach (var vm in _steps)
            actions.Add(vm.ToStep());

        return new MacroActionPlan("Images macro", actions);
    }

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
                _resultRows.Add(Strings.Format(nameof(Strings.MacroSkippedSourceFormat), row));
            }
        }

        return files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void RefreshStepNumbers()
    {
        for (var i = 0; i < _steps.Count; i++)
            _steps[i].StepNumber = i + 1;
    }

    private void RefreshEmptyState()
    {
        NoStepsText.Visibility = _steps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshSummary()
    {
        PlanSummaryText.Text = Strings.Format(nameof(Strings.MacroPlanSummaryFormat), "Images macro", _steps.Count, Plural(_steps.Count));
    }

    private void SetBusy(bool busy)
    {
        AddFilesButton.IsEnabled = !busy;
        AddFolderButton.IsEnabled = !busy;
        RunButton.IsEnabled = !busy;
        PreviewButton.IsEnabled = !busy;
        DryRunCheckBox.IsEnabled = !busy;
        CancelButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetStatus(string message, MacroActionStatus status)
    {
        StatusText.Text = message;
        StatusDot.Fill = status switch
        {
            MacroActionStatus.Busy => Brush("AccentBrush"),
            MacroActionStatus.Warning => Brush("YellowBrush"),
            MacroActionStatus.Error => Brush("RedBrush"),
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

    private static string Plural(int count) => count == 1 ? "" : "s";

    private enum MacroActionStatus
    {
        Ready,
        Busy,
        Warning,
        Error
    }
}

/// <summary>
/// Lightweight view-model for a single pipeline step displayed in the step list.
/// </summary>
public sealed class PipelineStepViewModel : INotifyPropertyChanged
{
    private int _stepNumber;

    public string Kind { get; }
    public Dictionary<string, string> Parameters { get; }

    public int StepNumber
    {
        get => _stepNumber;
        set
        {
            if (_stepNumber == value) return;
            _stepNumber = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StepNumber)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StepLabel)));
        }
    }

    public string StepLabel => _stepNumber.ToString();

    public string KindLabel => Kind switch
    {
        "strip-gps" => Strings.MacroStripGps,
        "strip-metadata" => Strings.MacroStripMetadata,
        "export-copy" => Strings.MacroExportCopy,
        "rename-pattern" => Strings.MacroRenamePattern,
        _ => Kind
    };

    public string ParameterSummary
    {
        get
        {
            if (Parameters.Count == 0)
                return "";

            return Kind switch
            {
                "strip-metadata" => FormatParam(Strings.MacroStripMetadataCategories, "categories", "all"),
                "export-copy" => string.Join(", ",
                    NonEmpty(
                        FormatParam(Strings.MacroExportExtension, "extension", ".png"),
                        FormatParam(Strings.MacroExportQuality, "quality", ""),
                        FormatMaxDimensions())),
                "rename-pattern" => FormatParam(Strings.MacroRenamePatternLabel, "pattern", ""),
                _ => string.Join(", ", Parameters.Select(p => $"{p.Key}: {p.Value}"))
            };
        }
    }

    private PipelineStepViewModel(string kind, Dictionary<string, string> parameters, int stepNumber)
    {
        Kind = kind;
        Parameters = parameters;
        _stepNumber = stepNumber;
    }

    public static PipelineStepViewModel From(MacroActionStep action, int stepNumber)
        => new(action.Kind, new Dictionary<string, string>(action.Parameters, StringComparer.OrdinalIgnoreCase), stepNumber);

    public MacroActionStep ToStep()
        => new(Kind, new Dictionary<string, string>(Parameters, StringComparer.OrdinalIgnoreCase));

    public event PropertyChangedEventHandler? PropertyChanged;

    private string FormatParam(string label, string key, string fallback)
    {
        var value = Parameters.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;
        return string.IsNullOrWhiteSpace(value) ? "" : $"{label}: {value}";
    }

    private string FormatMaxDimensions()
    {
        var w = Parameters.TryGetValue("maxWidth", out var wv) ? wv : "0";
        var h = Parameters.TryGetValue("maxHeight", out var hv) ? hv : "0";
        if (w is "0" or "" && h is "0" or "")
            return "";
        return $"max {w}x{h}";
    }

    private static IEnumerable<string> NonEmpty(params string[] values)
        => values.Where(v => !string.IsNullOrWhiteSpace(v));
}
