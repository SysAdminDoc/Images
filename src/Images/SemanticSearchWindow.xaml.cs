using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Images.Services;
using Microsoft.Win32;

namespace Images;

public sealed record SemanticSearchOpenRequestedEventArgs(string Path);

public sealed record SemanticSearchResultRow(
    string SourcePath,
    string FileName,
    string Folder,
    string ScoreText,
    string MatchedText);

public partial class SemanticSearchWindow : Window
{
    private readonly SemanticSearchService _semanticSearch;
    private readonly ObservableCollection<string> _roots = [];
    private readonly ObservableCollection<SemanticSearchResultRow> _results = [];
    private CancellationTokenSource? _indexCancellation;

    public event EventHandler<SemanticSearchOpenRequestedEventArgs>? OpenRequested;

    public SemanticSearchWindow()
        : this(null)
    {
    }

    internal SemanticSearchWindow(SemanticSearchService? semanticSearch)
    {
        _semanticSearch = semanticSearch ?? new SemanticSearchService();
        InitializeComponent();

        RootsList.ItemsSource = _roots;
        ResultsList.ItemsSource = _results;
        RefreshStatus();
        UpdateResultState();

        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChrome.ApplyDarkCaption(hwnd);
        };
        Closed += (_, _) => _indexCancellation?.Cancel();
    }

    public void AddSearchRoot(string folder)
    {
        if (TryNormalizeFolder(folder, out var normalized) &&
            !_roots.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            _roots.Add(normalized);
            RootsList.SelectedItem = normalized;
            SetStatus($"Added search root: {normalized}", SearchStatus.Ready);
        }
    }

    private SemanticSearchResultRow? SelectedResult => ResultsList.SelectedItem as SemanticSearchResultRow;

    private void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = PickFolder("Add folder to semantic search");
        if (folder is not null)
            AddSearchRoot(folder);
    }

    private async void IndexButton_Click(object sender, RoutedEventArgs e)
    {
        if (_roots.Count == 0)
        {
            SetStatus("Add at least one folder before indexing.", SearchStatus.Warning);
            return;
        }

        _indexCancellation?.Cancel();
        _indexCancellation = new CancellationTokenSource();
        var indexCancellation = _indexCancellation;
        var token = indexCancellation.Token;
        var roots = _roots.ToArray();

        SetBusy(true);
        SetStatus("Building local semantic index...", SearchStatus.Busy);

        try
        {
            var result = await Task.Run(() => _semanticSearch.Rebuild(roots, token), token);
            RefreshStatus();
            SetStatus(
                $"Indexed {result.IndexedCount} of {result.CatalogedCount} cataloged asset{Plural(result.CatalogedCount)}. {result.FailedCount} file{Plural(result.FailedCount)} skipped.",
                result.IndexedCount > 0 ? SearchStatus.Ready : SearchStatus.Warning);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Indexing canceled; previous semantic index remains usable.", SearchStatus.Warning);
            RefreshStatus();
        }
        finally
        {
            if (ReferenceEquals(_indexCancellation, indexCancellation))
                _indexCancellation = null;
            SetBusy(false);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _indexCancellation?.Cancel();
        SetStatus("Canceling semantic indexing...", SearchStatus.Busy);
    }

    private void RemoveRootButton_Click(object sender, RoutedEventArgs e)
    {
        if (RootsList.SelectedItem is string folder)
            _roots.Remove(folder);
    }

    private void ClearRootsButton_Click(object sender, RoutedEventArgs e)
        => _roots.Clear();

    private void ClearIndexButton_Click(object sender, RoutedEventArgs e)
    {
        _semanticSearch.Clear();
        _results.Clear();
        RefreshStatus();
        UpdateResultState();
        SetStatus("Deleted the local semantic search index.", SearchStatus.Ready);
    }

    private void UseSelectedRootButton_Click(object sender, RoutedEventArgs e)
    {
        if (RootsList.SelectedItem is string folder)
        {
            FolderFilterTextBox.Text = folder;
            SetStatus("Search filter set to the selected root.", SearchStatus.Ready);
        }
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
        => RunSearch();

    private void QueryTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            RunSearch();
            e.Handled = true;
        }
    }

    private void OpenResultButton_Click(object sender, RoutedEventArgs e)
    {
        var result = SelectedResult;
        if (result is null)
        {
            SetStatus("Select a search result before opening.", SearchStatus.Warning);
            return;
        }

        if (!File.Exists(result.SourcePath))
        {
            SetStatus("The selected result no longer exists.", SearchStatus.Warning);
            return;
        }

        OpenRequested?.Invoke(this, new SemanticSearchOpenRequestedEventArgs(result.SourcePath));
        SetStatus($"Opened {result.FileName} in the viewer.", SearchStatus.Ready);
    }

    private void RevealResultButton_Click(object sender, RoutedEventArgs e)
    {
        var result = SelectedResult;
        if (result is null)
        {
            SetStatus("Select a search result before revealing it.", SearchStatus.Warning);
            return;
        }

        try
        {
            ShellIntegration.OpenShellTarget(result.SourcePath);
            SetStatus($"Revealed {result.FileName}.", SearchStatus.Ready);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            SetStatus("Could not reveal result: " + ex.Message, SearchStatus.Error);
        }
    }

    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        OpenResultButton.IsEnabled = SelectedResult is not null;
        RevealResultButton.IsEnabled = SelectedResult is not null;
    }

    private void RunSearch()
    {
        var query = QueryTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            SetStatus("Enter a local search query first.", SearchStatus.Warning);
            return;
        }

        var matches = _semanticSearch.Search(query, folderFilter: FolderFilterTextBox.Text);
        _results.Clear();
        foreach (var match in matches)
        {
            _results.Add(new SemanticSearchResultRow(
                match.SourcePath,
                match.FileName,
                match.Folder,
                match.Score.ToString("P1", CultureInfo.CurrentCulture),
                match.MatchedText));
        }

        UpdateResultState();
        if (_results.Count > 0)
            ResultsList.SelectedIndex = 0;

        SetStatus(
            _results.Count == 0
                ? "No local semantic matches found."
                : $"Found {_results.Count} local result{Plural(_results.Count)}.",
            _results.Count == 0 ? SearchStatus.Warning : SearchStatus.Ready);
    }

    private void RefreshStatus()
    {
        var status = _semanticSearch.GetStatus();
        ProviderStatusText.Text = $"{status.ProviderStatus} Model: {status.ModelId}; provider: {status.ProviderId}; dimensions: {status.Dimensions}.";
        IndexStatusText.Text = status.IsAvailable
            ? $"{status.IndexedCount} indexed asset{Plural(status.IndexedCount)} in {status.IndexPath}. Last indexed: {(status.LastIndexedUtc is null ? "never" : status.LastIndexedUtc.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture))}."
            : "Semantic search storage is unavailable for this session.";
    }

    private void SetBusy(bool busy)
    {
        AddFolderButton.IsEnabled = !busy;
        IndexButton.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;
        RootsList.IsEnabled = !busy;
        SearchButton.IsEnabled = !busy;
        ClearIndexButton.IsEnabled = !busy;
    }

    private void UpdateResultState()
    {
        EmptyState.Visibility = _results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ResultsList.Visibility = _results.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        OpenResultButton.IsEnabled = SelectedResult is not null;
        RevealResultButton.IsEnabled = SelectedResult is not null;
    }

    private void SetStatus(string message, SearchStatus status)
    {
        StatusText.Text = message;
        StatusDot.Fill = status switch
        {
            SearchStatus.Busy => Brush("AccentBrush"),
            SearchStatus.Warning => Brush("YellowBrush"),
            SearchStatus.Error => Brush("RedBrush"),
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

    private static bool TryNormalizeFolder(string folder, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(folder))
            return false;

        try
        {
            var full = Path.GetFullPath(folder);
            if (!Directory.Exists(full))
                return false;

            normalized = full;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static string Plural(int count) => count == 1 ? string.Empty : "s";

    private enum SearchStatus
    {
        Ready,
        Busy,
        Warning,
        Error
    }
}
