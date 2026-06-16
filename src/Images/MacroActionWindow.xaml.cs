using System.Collections.ObjectModel;
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
    private string? _outputFolder;

    public MacroActionWindow()
    {
        InitializeComponent();

        SourceList.ItemsSource = _sourceRows;
        ResultList.ItemsSource = _resultRows;
        MacroJsonBox.Text = MacroActionService.Serialize(MacroActionPlan.Default);
        RefreshSummary();

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

    private void AddStripGpsButton_Click(object sender, RoutedEventArgs e)
        => AppendAction(new MacroActionStep("strip-gps", new Dictionary<string, string>()));

    private void AddExportPngButton_Click(object sender, RoutedEventArgs e)
        => AppendAction(new MacroActionStep(
            "export-copy",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["extension"] = ".png",
                ["quality"] = "92",
                ["maxWidth"] = "0",
                ["maxHeight"] = "0"
            }));

    private void AddRenamePatternButton_Click(object sender, RoutedEventArgs e)
        => AppendAction(new MacroActionStep(
            "rename-pattern",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["pattern"] = "{date}-{index}-{name}"
            }));

    private void ValidatePlanButton_Click(object sender, RoutedEventArgs e)
    {
        if (!MacroActionService.TryParse(MacroJsonBox.Text, out var plan, out var error))
        {
            SetStatus(error, MacroActionStatus.Error);
            return;
        }

        SetStatus(Strings.Format(nameof(Strings.MacroValidResultFormat), plan.Actions.Count, Plural(plan.Actions.Count)), MacroActionStatus.Ready);
        RefreshSummary(plan);
    }

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
            MacroJsonBox.Text = File.ReadAllText(dialog.FileName);
            ValidatePlanButton_Click(sender, e);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            SetStatus(Strings.MacroCouldNotLoad, MacroActionStatus.Error);
        }
    }

    private void SavePlanButton_Click(object sender, RoutedEventArgs e)
    {
        if (!MacroActionService.TryParse(MacroJsonBox.Text, out var plan, out var error))
        {
            SetStatus(error, MacroActionStatus.Error);
            return;
        }

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

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (!MacroActionService.TryParse(MacroJsonBox.Text, out var plan, out var error))
        {
            SetStatus(error, MacroActionStatus.Error);
            return;
        }

        var sources = CollectSourceFiles();
        if (sources.Count == 0)
        {
            SetStatus(Strings.MacroAddSourcesBeforeRunning, MacroActionStatus.Warning);
            return;
        }

        var options = new MacroRunOptions(_outputFolder, DryRunCheckBox.IsChecked == true);
        SetBusy(true);
        SetStatus(Strings.Format(nameof(Strings.MacroRunningFormat), plan.Actions.Count, Plural(plan.Actions.Count), sources.Count, Plural(sources.Count)), MacroActionStatus.Busy);
        _resultRows.Clear();

        try
        {
            var result = await Task.Run(() => _macro.Run(plan, sources, options));
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
        finally
        {
            SetBusy(false);
        }
    }

    private void AppendAction(MacroActionStep action)
    {
        var plan = MacroActionService.TryParse(MacroJsonBox.Text, out var parsed, out _)
            ? parsed
            : MacroActionPlan.Default;
        var actions = plan.Actions.ToList();
        actions.Add(action);
        var updated = new MacroActionPlan(plan.Name, actions);
        MacroJsonBox.Text = MacroActionService.Serialize(updated);
        RefreshSummary(updated);
        SetStatus(Strings.Format(nameof(Strings.MacroAddedActionFormat), action.Kind), MacroActionStatus.Ready);
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

    private void RefreshSummary()
    {
        if (MacroActionService.TryParse(MacroJsonBox.Text, out var plan, out _))
            RefreshSummary(plan);
    }

    private void RefreshSummary(MacroActionPlan plan)
    {
        PlanSummaryText.Text = Strings.Format(nameof(Strings.MacroPlanSummaryFormat), plan.Name, plan.Actions.Count, Plural(plan.Actions.Count));
    }

    private void SetBusy(bool busy)
    {
        AddFilesButton.IsEnabled = !busy;
        AddFolderButton.IsEnabled = !busy;
        RunButton.IsEnabled = !busy;
        MacroJsonBox.IsEnabled = !busy;
        DryRunCheckBox.IsEnabled = !busy;
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
