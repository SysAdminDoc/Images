using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Images.Services;
using Images.ViewModels;
using Microsoft.Win32;

namespace Images;

public partial class ImportInboxWindow : Window
{
    private readonly ImportInboxService _inbox = new();
    private readonly RecycleBinDeleteService _deleteService = new(SettingsService.Instance);
    private readonly ObservableCollection<ImportInboxRow> _rows = [];
    private readonly List<string> _sourceRoots = [];
    private CancellationTokenSource? _loadCancellation;
    private string? _destinationFolder;

    public ImportInboxWindow()
    {
        InitializeComponent();

        InboxList.ItemsSource = _rows;
        UpdateCounts();
        SetDetail(null);

        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChrome.ApplyDarkCaption(hwnd);
        };
        Closed += (_, _) => _loadCancellation?.Cancel();
    }

    public void AddSource(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var fullPath = Path.GetFullPath(path);
            if ((File.Exists(fullPath) || Directory.Exists(fullPath)) &&
                !_sourceRoots.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
            {
                _sourceRoots.Add(fullPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            SetStatus("Could not add that import source.", ImportInboxStatus.Warning);
        }
    }

    public async Task ReloadAsync()
    {
        if (_sourceRoots.Count == 0)
        {
            SetStatus("Add files or a folder before refreshing the import inbox.", ImportInboxStatus.Warning);
            return;
        }

        _loadCancellation?.Cancel();
        _loadCancellation = new CancellationTokenSource();
        var cancellation = _loadCancellation;
        var token = cancellation.Token;
        var roots = _sourceRoots.ToArray();
        var destination = _destinationFolder;

        SetBusy(true);
        SetStatus("Building import inbox...", ImportInboxStatus.Busy);

        try
        {
            var result = await Task.Run(
                () => _inbox.BuildInbox(roots, destination, token),
                token);

            _rows.Clear();
            foreach (var item in result.Items)
                AddRow(ImportInboxRow.FromItem(item));

            if (_rows.Count > 0)
                InboxList.SelectedIndex = 0;
            else
                SetDetail(null);

            UpdateCounts();
            SetStatus(
                $"Staged {result.SourceCount} file{Plural(result.SourceCount)}. {result.DuplicateCount} inbox duplicate{Plural(result.DuplicateCount)}, {result.DestinationDuplicateCount} destination duplicate{Plural(result.DestinationDuplicateCount)}.",
                result.SourceCount > 0 ? ImportInboxStatus.Ready : ImportInboxStatus.Warning);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Import inbox refresh canceled.", ImportInboxStatus.Warning);
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cancellation))
                _loadCancellation = null;
            SetBusy(false);
        }
    }

    private async void AddFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add files to import inbox",
            Filter = SupportedImageFormats.OpenDialogFilter,
            Multiselect = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        foreach (var path in dialog.FileNames)
            AddSource(path);
        await ReloadAsync();
    }

    private async void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = PickFolder("Add folder to import inbox");
        if (folder is null)
            return;

        AddSource(folder);
        await ReloadAsync();
    }

    private async void ChooseDestinationButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = PickFolder("Choose import destination");
        if (folder is null)
            return;

        _destinationFolder = folder;
        DestinationText.Text = folder;
        DestinationText.ToolTip = folder;
        if (_sourceRoots.Count > 0)
            await ReloadAsync();
        else
            SetStatus("Destination selected. Add files or a folder to stage imports.", ImportInboxStatus.Ready);
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        => await ReloadAsync();

    private void InboxList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => SetDetail(SelectedRow);

    private void ApplyToIncludedButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedRow;
        if (selected is null)
            return;

        foreach (var row in _rows.Where(row => row.IsIncluded && !ReferenceEquals(row, selected)))
        {
            row.TagsText = selected.TagsText;
            row.RatingValue = selected.RatingValue;
            row.WriteRating = selected.WriteRating;
            row.StripGps = selected.StripGps;
        }

        SetStatus("Applied selected metadata to included inbox files.", ImportInboxStatus.Ready);
    }

    private void RecycleSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = InboxList.SelectedItems.OfType<ImportInboxRow>().ToList();
        if (selected.Count == 0)
        {
            SetStatus("Select one or more staged files before recycling.", ImportInboxStatus.Warning);
            return;
        }

        var deleted = 0;
        var failed = 0;
        foreach (var row in selected)
        {
            var result = _deleteService.Delete(row.Path, this);
            switch (result.Status)
            {
                case RecycleBinDeleteStatus.Deleted:
                    deleted++;
                    _rows.Remove(row);
                    break;
                case RecycleBinDeleteStatus.Canceled:
                    SetStatus("Recycle canceled.", ImportInboxStatus.Warning);
                    UpdateCounts();
                    return;
                default:
                    failed++;
                    break;
            }
        }

        UpdateCounts();
        if (_rows.Count == 0)
            SetDetail(null);
        SetStatus(
            failed == 0
                ? $"Moved {deleted} staged file{Plural(deleted)} to Recycle Bin."
                : $"Moved {deleted} staged file{Plural(deleted)} to Recycle Bin. {failed} failed.",
            failed == 0 ? ImportInboxStatus.Ready : ImportInboxStatus.Warning);
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_destinationFolder))
        {
            SetStatus("Choose a destination folder before importing.", ImportInboxStatus.Warning);
            return;
        }

        var selected = _rows.Where(row => row.IsIncluded).ToList();
        if (selected.Count == 0)
        {
            SetStatus("Check at least one staged file before importing.", ImportInboxStatus.Warning);
            return;
        }

        var moveOriginals = MoveOriginalsCheckBox.IsChecked == true;
        var requests = selected
            .Select(row => new ImportInboxCommitRequest(
                row.Path,
                _destinationFolder,
                row.TagsText,
                row.WriteRating ? (int)Math.Round(row.RatingValue) : null,
                row.StripGps,
                moveOriginals))
            .ToList();

        SetBusy(true);
        SetStatus($"Importing {requests.Count} file{Plural(requests.Count)}...", ImportInboxStatus.Busy);

        try
        {
            var result = await Task.Run(() => _inbox.Commit(requests));
            var importedSources = result.Imported
                .Select(item => item.SourcePath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var row in _rows.Where(row => importedSources.Contains(row.Path)).ToList())
                _rows.Remove(row);

            UpdateCounts();
            if (_rows.Count == 0)
                SetDetail(null);

            SetStatus(
                result.FailedCount == 0
                    ? $"Imported {result.ImportedCount} file{Plural(result.ImportedCount)}."
                    : $"Imported {result.ImportedCount} file{Plural(result.ImportedCount)}. {result.FailedCount} failed.",
                result.FailedCount == 0 ? ImportInboxStatus.Ready : ImportInboxStatus.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private ImportInboxRow? SelectedRow => InboxList.SelectedItem as ImportInboxRow;

    private void AddRow(ImportInboxRow row)
    {
        row.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ImportInboxRow.IsIncluded))
                UpdateCounts();
        };
        _rows.Add(row);
    }

    private void SetDetail(ImportInboxRow? row)
    {
        DetailTitle.Text = row?.FileName ?? "No staged file selected";
        DetailSubtitle.Text = row?.StatusText ?? "Add files or folders, then select an item to tag, rate, strip GPS, or skip.";
        DetailSubtitle.ToolTip = row?.Path;
        MetadataEditorPanel.DataContext = row;
        TagsBox.DataContext = row;
        RatingSlider.DataContext = row;
        WriteRatingCheckBox.DataContext = row;
        StripGpsCheckBox.DataContext = row;
        SetImagePreview(row?.Path);
    }

    private void SetImagePreview(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            PreviewImage.Source = null;
            return;
        }

        try
        {
            PreviewImage.Source = ImageLoader.Load(path).Image;
            PreviewImage.ToolTip = path;
        }
        catch
        {
            PreviewImage.Source = null;
            PreviewImage.ToolTip = "Preview unavailable.";
        }
    }

    private void SetBusy(bool busy)
    {
        AddFilesButton.IsEnabled = !busy;
        AddFolderButton.IsEnabled = !busy;
        RefreshButton.IsEnabled = !busy;
        InboxList.IsEnabled = !busy;
        ImportButton.IsEnabled = !busy;
        RecycleSelectedButton.IsEnabled = !busy;
        ApplyToIncludedButton.IsEnabled = !busy;
    }

    private void UpdateCounts()
    {
        var included = _rows.Count(row => row.IsIncluded);
        InboxCountText.Text = _rows.Count.ToString(CultureInfo.InvariantCulture);
        ImportSummaryText.Text = _rows.Count == 0
            ? "Add files or a folder to begin staging imports."
            : $"{included} of {_rows.Count} staged file{Plural(_rows.Count)} included. Destination duplicates are skipped by default.";
    }

    private void SetStatus(string message, ImportInboxStatus status)
    {
        StatusText.Text = message;
        StatusDot.Fill = status switch
        {
            ImportInboxStatus.Busy => Brush("AccentBrush"),
            ImportInboxStatus.Warning => Brush("YellowBrush"),
            ImportInboxStatus.Error => Brush("RedBrush"),
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

    private enum ImportInboxStatus
    {
        Ready,
        Busy,
        Warning,
        Error
    }

    private sealed class ImportInboxRow : ObservableObject
    {
        private bool _isIncluded;
        private string _tagsText = "";
        private double _ratingValue;
        private bool _writeRating;
        private bool _stripGps = true;

        public required string Path { get; init; }
        public required string FileName { get; init; }
        public required string StatusText { get; init; }
        public required string SizeText { get; init; }

        public bool IsIncluded
        {
            get => _isIncluded;
            set => Set(ref _isIncluded, value);
        }

        public string TagsText
        {
            get => _tagsText;
            set => Set(ref _tagsText, value);
        }

        public double RatingValue
        {
            get => _ratingValue;
            set
            {
                if (Set(ref _ratingValue, Math.Clamp(Math.Round(value), -1, 5)))
                    Raise(nameof(RatingText));
            }
        }

        public bool WriteRating
        {
            get => _writeRating;
            set => Set(ref _writeRating, value);
        }

        public bool StripGps
        {
            get => _stripGps;
            set => Set(ref _stripGps, value);
        }

        public string RatingText => RatingValue < 0
            ? "Rejected (-1)"
            : $"{RatingValue:0} star{(Math.Abs(RatingValue - 1) < 0.1 ? "" : "s")}";

        public static ImportInboxRow FromItem(ImportInboxItem item)
        {
            return new ImportInboxRow
            {
                Path = item.Path,
                FileName = item.FileName,
                StatusText = item.StatusText,
                SizeText = item.SizeText,
                IsIncluded = !item.IsDuplicateInDestination && (!item.IsDuplicateInInbox || item.DuplicateOrdinal == 1)
            };
        }
    }
}
