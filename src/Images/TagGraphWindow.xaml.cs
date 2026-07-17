using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Images.Localization;
using Images.Services;

namespace Images;

public partial class TagGraphWindow : Window
{
    private readonly TagGraphService _tagGraph;
    private readonly KeywordSetService _categorySets;
    private readonly ObservableCollection<string> _aliasRows = [];
    private readonly ObservableCollection<string> _parentRows = [];
    private readonly ObservableCollection<string> _previewRows = [];
    private string? _currentImagePath;

    public TagGraphWindow()
        : this(null, null)
    {
    }

    internal TagGraphWindow(TagGraphService? tagGraph, KeywordSetService? categorySets)
    {
        _tagGraph = tagGraph ?? new TagGraphService();
        _categorySets = categorySets ?? new KeywordSetService();
        InitializeComponent();

        AliasList.ItemsSource = _aliasRows;
        ParentList.ItemsSource = _parentRows;
        PreviewList.ItemsSource = _previewRows;
        RefreshCategorySets();
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
            CurrentImageText.Text = Strings.TagGraphNoImageSelected;
            CurrentImagePathText.Text = Strings.TagGraphNoImageHint;
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
        SetStatus(Strings.TagGraphRefreshed, TagGraphStatus.Ready);
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
            SetStatus(Strings.TagGraphOpenImageBeforeImporting, TagGraphStatus.Warning);
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
            SetStatus(Strings.TagGraphOpenImageBeforeExporting, TagGraphStatus.Warning);
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

    private void SaveCategorySetButton_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveCategorySet(CategorySetNameBox.Text, TagInputBox.Text))
        {
            SetStatus(Strings.TagGraphCategorySetSaveFailed, TagGraphStatus.Warning);
            return;
        }

        SetStatus(Strings.Format(nameof(Strings.TagGraphCategorySetSavedFormat), CategorySetNameBox.Text.Trim()), TagGraphStatus.Ready);
    }

    private void UseCategorySetButton_Click(object sender, RoutedEventArgs e)
    {
        if (!UseSelectedCategorySet())
        {
            SetStatus(Strings.TagGraphSelectCategorySet, TagGraphStatus.Warning);
            return;
        }

        SetStatus(Strings.Format(nameof(Strings.TagGraphCategorySetLoadedFormat), CategorySetNameBox.Text), TagGraphStatus.Ready);
    }

    private void DeleteCategorySetButton_Click(object sender, RoutedEventArgs e)
    {
        if (!DeleteSelectedCategorySet())
        {
            SetStatus(Strings.TagGraphSelectCategorySet, TagGraphStatus.Warning);
            return;
        }

        SetStatus(Strings.TagGraphCategorySetDeleted, TagGraphStatus.Ready);
    }

    private void CategorySetComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CategorySetComboBox.SelectedItem is KeywordSetDefinition selected)
            CategorySetNameBox.Text = selected.Name;
    }

    internal bool SaveCategorySet(string name, string tagInput)
    {
        var tags = TagGraphService.ParseTagInput(tagInput);
        if (!_categorySets.Upsert(name, tags))
            return false;

        RefreshCategorySets(name.Trim());
        return true;
    }

    internal bool UseSelectedCategorySet()
    {
        if (CategorySetComboBox.SelectedItem is not KeywordSetDefinition selected)
            return false;

        CategorySetNameBox.Text = selected.Name;
        TagInputBox.Text = string.Join(Environment.NewLine, selected.Keywords);
        PreviewExpansion();
        return true;
    }

    internal bool DeleteSelectedCategorySet()
    {
        if (CategorySetComboBox.SelectedItem is not KeywordSetDefinition selected || !_categorySets.Remove(selected.Name))
            return false;

        RefreshCategorySets();
        CategorySetNameBox.Clear();
        return true;
    }

    private void RefreshCategorySets(string? selectedName = null)
    {
        CategorySetComboBox.ItemsSource = null;
        CategorySetComboBox.ItemsSource = _categorySets.Sets;
        CategorySetComboBox.SelectedItem = _categorySets.Sets.FirstOrDefault(
            set => string.Equals(set.Name, selectedName, StringComparison.OrdinalIgnoreCase));
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
            _aliasRows.Add(Strings.TagGraphNoNamespaceOrAlias);

        _parentRows.Clear();
        foreach (var item in snapshot.Parents)
            _parentRows.Add($"{item.Tag} -> {item.Parent}");
        if (_parentRows.Count == 0)
            _parentRows.Add(Strings.TagGraphNoParentRelationships);

        GraphSummaryText.Text = Strings.Format(nameof(Strings.TagGraphSummaryFormat),
            snapshot.Namespaces.Count, Plural(snapshot.Namespaces.Count),
            snapshot.Aliases.Count, Plural(snapshot.Aliases.Count),
            snapshot.Parents.Count, Plural(snapshot.Parents.Count));
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
            _previewRows.Add(Strings.TagGraphEnterTagsToPreview);
            SetStatus(Strings.TagGraphEnterAtLeastOneTag, TagGraphStatus.Warning);
            return;
        }

        foreach (var expansion in _tagGraph.ExpandMany(tags))
        {
            var parents = expansion.Parents.Count == 0
                ? Strings.TagGraphNoParents
                : string.Join(", ", expansion.Parents);
            _previewRows.Add($"{expansion.Original} => {expansion.Canonical} ({parents})");
        }

        SetStatus(Strings.Format(nameof(Strings.TagGraphPreviewedTagsFormat), tags.Count, Plural(tags.Count)), TagGraphStatus.Ready);
    }

    private void SetStatus(string message, TagGraphStatus status)
    {
        StatusText.Text = message;
        StatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, status switch
        {
            TagGraphStatus.Warning => "YellowBrush",
            TagGraphStatus.Error => "RedBrush",
            _ => "GreenBrush"
        });
    }

    private static string Plural(int count) => count == 1 ? "" : "s";

    private enum TagGraphStatus
    {
        Ready,
        Warning,
        Error
    }
}
