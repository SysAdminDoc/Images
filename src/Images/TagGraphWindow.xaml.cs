using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Images.Services;

namespace Images;

public partial class TagGraphWindow : Window
{
    private readonly TagGraphService _tagGraph = new();
    private readonly ObservableCollection<string> _aliasRows = [];
    private readonly ObservableCollection<string> _parentRows = [];
    private readonly ObservableCollection<string> _previewRows = [];
    private string? _currentImagePath;

    public TagGraphWindow()
    {
        InitializeComponent();

        AliasList.ItemsSource = _aliasRows;
        ParentList.ItemsSource = _parentRows;
        PreviewList.ItemsSource = _previewRows;
        RefreshSnapshot();

        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChrome.ApplyDarkCaption(hwnd);
        };
    }

    public void SetCurrentImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _currentImagePath = null;
            CurrentImageText.Text = "No image selected";
            CurrentImagePathText.Text = "Open an image first to import or export its XMP sidecar tags.";
            CurrentImagePathText.ToolTip = null;
            return;
        }

        _currentImagePath = Path.GetFullPath(path);
        CurrentImageText.Text = Path.GetFileName(_currentImagePath);
        CurrentImagePathText.Text = _currentImagePath;
        CurrentImagePathText.ToolTip = _currentImagePath;
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshSnapshot();
        SetStatus("Tag graph refreshed.", TagGraphStatus.Ready);
    }

    private void AddNamespaceButton_Click(object sender, RoutedEventArgs e)
    {
        var result = _tagGraph.AddNamespace(NamespacePrefixBox.Text, NamespaceLabelBox.Text);
        ApplyMutationResult(result);
        if (!result.Success)
            return;

        NamespacePrefixBox.Clear();
        NamespaceLabelBox.Clear();
    }

    private void AddAliasButton_Click(object sender, RoutedEventArgs e)
    {
        var result = _tagGraph.AddAlias(AliasBox.Text, AliasTargetBox.Text);
        ApplyMutationResult(result);
        if (!result.Success)
            return;

        AliasBox.Clear();
        AliasTargetBox.Clear();
    }

    private void AddParentButton_Click(object sender, RoutedEventArgs e)
    {
        var result = _tagGraph.AddParent(ParentTagBox.Text, ParentBox.Text);
        ApplyMutationResult(result);
        if (!result.Success)
            return;

        ParentTagBox.Clear();
        ParentBox.Clear();
    }

    private void PreviewButton_Click(object sender, RoutedEventArgs e)
        => PreviewExpansion();

    private void ImportSidecarButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentImagePath is null)
        {
            SetStatus("Open an image before importing sidecar tags.", TagGraphStatus.Warning);
            return;
        }

        var result = _tagGraph.ImportSidecarTags(_currentImagePath);
        if (result.Success)
        {
            TagInputBox.Text = string.Join(Environment.NewLine, result.Tags);
            PreviewExpansion();
            SetStatus(result.Message, result.Tags.Count > 0 ? TagGraphStatus.Ready : TagGraphStatus.Warning);
            return;
        }

        SetStatus(result.Message, TagGraphStatus.Warning);
    }

    private void ExportSidecarButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentImagePath is null)
        {
            SetStatus("Open an image before exporting sidecar tags.", TagGraphStatus.Warning);
            return;
        }

        var tags = TagGraphService.ParseTagInput(TagInputBox.Text);
        var result = _tagGraph.ExportSidecarTags(
            _currentImagePath,
            tags,
            IncludeParentsCheckBox.IsChecked == true);

        SetStatus(result.Message, result.Success ? TagGraphStatus.Ready : TagGraphStatus.Error);
        if (result.Success)
            PreviewExpansion();
    }

    private void ApplyMutationResult(TagGraphMutationResult result)
    {
        RefreshSnapshot();
        SetStatus(result.Message, result.Success ? TagGraphStatus.Ready : TagGraphStatus.Warning);
    }

    private void RefreshSnapshot()
    {
        var snapshot = _tagGraph.Snapshot;

        _aliasRows.Clear();
        foreach (var item in snapshot.Namespaces)
            _aliasRows.Add($"{item.Prefix}:  {item.Label}");
        if (snapshot.Namespaces.Count > 0 && snapshot.Aliases.Count > 0)
            _aliasRows.Add("");
        foreach (var item in snapshot.Aliases)
            _aliasRows.Add($"{item.Alias} -> {item.Target}");
        if (_aliasRows.Count == 0)
            _aliasRows.Add("No namespace or alias rules yet.");

        _parentRows.Clear();
        foreach (var item in snapshot.Parents)
            _parentRows.Add($"{item.Tag} -> {item.Parent}");
        if (_parentRows.Count == 0)
            _parentRows.Add("No parent relationships yet.");

        GraphSummaryText.Text =
            $"{snapshot.Namespaces.Count} namespace{Plural(snapshot.Namespaces.Count)}, " +
            $"{snapshot.Aliases.Count} alias{Plural(snapshot.Aliases.Count)}, " +
            $"{snapshot.Parents.Count} parent{Plural(snapshot.Parents.Count)}";
    }

    private void PreviewExpansion()
    {
        var source = !string.IsNullOrWhiteSpace(PreviewTagBox.Text)
            ? PreviewTagBox.Text
            : TagInputBox.Text;
        var tags = TagGraphService.ParseTagInput(source);
        _previewRows.Clear();

        if (tags.Count == 0)
        {
            _previewRows.Add("Enter tags to preview.");
            SetStatus("Enter at least one tag to preview.", TagGraphStatus.Warning);
            return;
        }

        foreach (var expansion in _tagGraph.ExpandMany(tags))
        {
            var parents = expansion.Parents.Count == 0
                ? "no parents"
                : string.Join(", ", expansion.Parents);
            _previewRows.Add($"{expansion.Original} => {expansion.Canonical} ({parents})");
        }

        SetStatus($"Previewed {tags.Count} tag{Plural(tags.Count)}.", TagGraphStatus.Ready);
    }

    private void SetStatus(string message, TagGraphStatus status)
    {
        StatusText.Text = message;
        StatusDot.Fill = status switch
        {
            TagGraphStatus.Warning => Brush("YellowBrush"),
            TagGraphStatus.Error => Brush("RedBrush"),
            _ => Brush("GreenBrush")
        };
    }

    private Brush Brush(string key)
        => TryFindResource(key) as Brush ?? Brushes.Transparent;

    private static string Plural(int count) => count == 1 ? "" : "s";

    private enum TagGraphStatus
    {
        Ready,
        Warning,
        Error
    }
}
