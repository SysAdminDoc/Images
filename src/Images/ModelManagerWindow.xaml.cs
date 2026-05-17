using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Images.Services;
using Microsoft.Win32;

namespace Images;

public partial class ModelManagerWindow : Window
{
    private readonly ModelManagerService _modelManager;
    private readonly ObservableCollection<LocalModelStatus> _models = [];
    private LocalModelManagerSnapshot? _snapshot;

    public ModelManagerWindow()
        : this(null)
    {
    }

    internal ModelManagerWindow(ModelManagerService? modelManager)
    {
        _modelManager = modelManager ?? new ModelManagerService();
        InitializeComponent();

        ModelsList.ItemsSource = _models;
        RefreshModels();

        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChrome.ApplyDarkCaption(hwnd);
        };
    }

    private LocalModelStatus? SelectedModel => ModelsList.SelectedItem as LocalModelStatus;

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
        => RefreshModels();

    private void ModelsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => RenderSelectedModel();

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var model = SelectedModel;
        if (model is null)
        {
            SetStatus("Select an approved model before importing.", ModelStatusTone.Warning);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Import approved ONNX model",
            Filter = "ONNX model (*.onnx)|*.onnx|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
            return;

        var result = _modelManager.ImportLocalModel(model.Definition.Id, dialog.FileName);
        RefreshModels(result.Model?.Definition.Id ?? model.Definition.Id);
        SetStatus(
            result.Message,
            result.Status == LocalModelImportStatus.Imported
                ? ModelStatusTone.Ready
                : result.Status == LocalModelImportStatus.HashMismatch
                    ? ModelStatusTone.Warning
                    : ModelStatusTone.Error);
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var model = SelectedModel;
        if (model is null)
        {
            SetStatus("Select a model before deleting local files.", ModelStatusTone.Warning);
            return;
        }

        var deleted = _modelManager.DeleteLocalModel(model.Definition.Id, out var message);
        RefreshModels(model.Definition.Id);
        SetStatus(message, deleted ? ModelStatusTone.Ready : ModelStatusTone.Error);
    }

    private void RevealRootButton_Click(object sender, RoutedEventArgs e)
    {
        var root = _modelManager.GetModelRoot();
        if (string.IsNullOrWhiteSpace(root))
        {
            SetStatus("Model storage is not available.", ModelStatusTone.Warning);
            return;
        }

        try
        {
            ShellIntegration.OpenFolder(root);
            SetStatus("Opened model storage folder.", ModelStatusTone.Ready);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            SetStatus("Could not open model storage: " + ex.Message, ModelStatusTone.Error);
        }
    }

    private void OpenSourceButton_Click(object sender, RoutedEventArgs e)
    {
        var model = SelectedModel;
        if (model is null)
        {
            SetStatus("Select a model first.", ModelStatusTone.Warning);
            return;
        }

        try
        {
            ShellIntegration.OpenShellTarget(model.Definition.SourceUrl);
            SetStatus("Opened approved model source.", ModelStatusTone.Ready);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            SetStatus("Could not open model source: " + ex.Message, ModelStatusTone.Error);
        }
    }

    private void RefreshModels(string? selectId = null)
    {
        selectId ??= SelectedModel?.Definition.Id;
        _snapshot = _modelManager.GetSnapshot();
        _models.Clear();
        foreach (var model in _snapshot.Models)
            _models.Add(model);

        RuntimeStatusText.Text = _snapshot.Runtime.StatusText;
        ModelCountText.Text = _snapshot.RegistrySummary;

        if (!string.IsNullOrWhiteSpace(selectId))
        {
            var match = _models.FirstOrDefault(model => model.Definition.Id.Equals(selectId, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                ModelsList.SelectedItem = match;
        }

        if (ModelsList.SelectedItem is null && _models.Count > 0)
            ModelsList.SelectedIndex = 0;

        RenderSelectedModel();
        SetStatus("Model manager refreshed.", ModelStatusTone.Ready);
    }

    private void RenderSelectedModel()
    {
        var model = SelectedModel;
        var hasModel = model is not null;
        EmptyState.Visibility = hasModel ? Visibility.Collapsed : Visibility.Visible;
        DetailPanel.Visibility = hasModel ? Visibility.Visible : Visibility.Collapsed;
        ImportButton.IsEnabled = hasModel;
        DeleteButton.IsEnabled = model?.CanDelete == true;

        if (model is null)
            return;

        DetailTitle.Text = model.DisplayName;
        DetailStatus.Text = model.StatusText;
        PurposeText.Text = model.Purpose;
        LicenseText.Text = model.Definition.License;
        RuntimeText.Text = model.Definition.RuntimeContract;
        SourceText.Text = model.Definition.SourceUrl;
        DownloadText.Text = model.Definition.DownloadUrl;
        ExpectedShaText.Text = model.Definition.ExpectedSha256;
        LocalShaText.Text = model.Sha256 ?? "Not imported";
        LocalPathText.Text = model.InstalledPath ?? "Not imported";
        ImportedText.Text = model.ImportedText;
        NotesText.Text = $"{model.Definition.Notes} Expected size: {model.Definition.ExpectedSizeText}.";
        ActionText.Text = model.ActionText;
    }

    private void SetStatus(string message, ModelStatusTone tone)
    {
        StatusText.Text = message;
        StatusDot.Fill = tone switch
        {
            ModelStatusTone.Warning => Brush("YellowBrush"),
            ModelStatusTone.Error => Brush("RedBrush"),
            _ => Brush("GreenBrush")
        };
    }

    private Brush Brush(string key)
        => TryFindResource(key) as Brush ?? Brushes.Transparent;

    private enum ModelStatusTone
    {
        Ready,
        Warning,
        Error
    }
}
