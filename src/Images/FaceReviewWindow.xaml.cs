using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using Images.Localization;
using Images.Services;
using Images.ViewModels;

namespace Images;

public partial class FaceReviewWindow : Window, INotifyPropertyChanged
{
    internal const int MaximumFolderImages = 100;
    private readonly string? _currentPath;
    private readonly IReadOnlyList<string> _folderPaths;
    private readonly Func<IReadOnlyList<string>, FaceReviewAnalysis> _analyze;
    private readonly Func<IReadOnlyList<FaceReviewCandidate>, IReadOnlyList<FaceReviewEntry>, FaceReviewMergeResult> _merge;
    private FaceReviewItemViewModel? _selectedItem;
    private ImageSource? _selectedImageSource;
    private double _selectedImageWidth = 1;
    private double _selectedImageHeight = 1;
    private string _statusText;
    private string _reviewSummary;
    private string _mergeGateText;
    private bool _isBusy;
    private bool _closed;

    public FaceReviewWindow()
        : this(null, [], null, null)
    {
    }

    internal FaceReviewWindow(
        string? currentPath,
        IReadOnlyList<string>? folderPaths,
        Func<IReadOnlyList<string>, FaceReviewAnalysis>? analyze,
        Func<IReadOnlyList<FaceReviewCandidate>, IReadOnlyList<FaceReviewEntry>, FaceReviewMergeResult>? merge)
    {
        _currentPath = string.IsNullOrWhiteSpace(currentPath) ? null : currentPath;
        _folderPaths = folderPaths ?? [];
        _analyze = analyze ?? (paths => FaceReviewService.Analyze(paths));
        _merge = merge ?? FaceReviewService.MergeReviewedRegions;
        _statusText = Strings.Get("FaceReviewReadyStatus");
        _reviewSummary = Strings.Get("FaceReviewNoRegions");
        _mergeGateText = Strings.Get("FaceReviewNoRegions");

        InitializeComponent();
        DataContext = this;
        var view = CollectionViewSource.GetDefaultView(Items);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FaceReviewItemViewModel.ClusterLabel)));
        FaceList.ItemsSource = view;
        AnalyzeCurrentButton.IsEnabled = _currentPath is not null;
        AnalyzeFolderButton.IsEnabled = _folderPaths.Count > 0;
        MergeButton.IsEnabled = false;

        Loaded += async (_, _) =>
        {
            if (_currentPath is not null && Items.Count == 0)
                await AnalyzeAsync([_currentPath]);
        };
        Closed += (_, _) => _closed = true;
        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChrome.ApplyDarkCaption(hwnd);
        };
    }

    public ObservableCollection<FaceReviewItemViewModel> Items { get; } = [];
    public ObservableCollection<FaceReviewOverlayItem> Overlays { get; } = [];

    public FaceReviewItemViewModel? SelectedItem
    {
        get => _selectedItem;
        private set
        {
            if (ReferenceEquals(_selectedItem, value)) return;
            _selectedItem = value;
            Raise(nameof(SelectedItem));
            RenderSelection();
        }
    }

    public ImageSource? SelectedImageSource
    {
        get => _selectedImageSource;
        private set { _selectedImageSource = value; Raise(nameof(SelectedImageSource)); }
    }

    public double SelectedImageWidth
    {
        get => _selectedImageWidth;
        private set { _selectedImageWidth = Math.Max(1, value); Raise(nameof(SelectedImageWidth)); }
    }

    public double SelectedImageHeight
    {
        get => _selectedImageHeight;
        private set { _selectedImageHeight = Math.Max(1, value); Raise(nameof(SelectedImageHeight)); }
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; Raise(nameof(StatusText)); }
    }

    public string ReviewSummary
    {
        get => _reviewSummary;
        private set { _reviewSummary = value; Raise(nameof(ReviewSummary)); }
    }

    public string MergeGateText
    {
        get => _mergeGateText;
        private set { _mergeGateText = value; Raise(nameof(MergeGateText)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private async void AnalyzeCurrentButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPath is not null)
            await AnalyzeAsync([_currentPath]);
    }

    private async void AnalyzeFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var paths = _folderPaths
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaximumFolderImages)
            .ToArray();
        await AnalyzeAsync(paths);
    }

    private async Task AnalyzeAsync(IReadOnlyList<string> paths)
    {
        if (_isBusy || paths.Count == 0) return;
        SetBusy(true);
        StatusText = Strings.Format("FaceReviewAnalyzingStatusFormat", paths.Count);
        try
        {
            var analysis = await Task.Run(() => _analyze(paths));
            if (_closed) return;

            foreach (var item in Items)
                item.ReviewChanged -= Item_ReviewChanged;
            Items.Clear();
            foreach (var candidate in analysis.Candidates)
            {
                var item = new FaceReviewItemViewModel(candidate);
                item.ReviewChanged += Item_ReviewChanged;
                Items.Add(item);
            }
            CollectionViewSource.GetDefaultView(Items).Refresh();
            FaceList.SelectedIndex = Items.Count > 0 ? 0 : -1;
            StatusText = analysis.Failures.Count == 0
                ? Strings.Format("FaceReviewAnalyzedStatusFormat", Items.Count, paths.Count)
                : Strings.Format("FaceReviewAnalyzedWithFailuresStatusFormat", Items.Count, analysis.Failures.Count);
            RefreshGate();
        }
        catch (Exception ex)
        {
            StatusText = Strings.Format("FaceReviewAnalyzeFailedFormat", ex.Message);
        }
        finally
        {
            if (!_closed) SetBusy(false);
        }
    }

    private void FaceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => SelectedItem = FaceList.SelectedItem as FaceReviewItemViewModel;

    private void AcceptButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem is null) return;
        SelectedItem.Decision = FaceReviewDecision.Accepted;
        NameBox.Focus();
    }

    private void RejectButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem is null) return;
        SelectedItem.Decision = FaceReviewDecision.Rejected;
    }

    private async void MergeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        var candidates = Items.Select(item => item.Candidate).ToArray();
        var reviews = Items.Select(item => item.ToReview()).ToArray();
        if (!FaceReviewService.CanMerge(candidates, reviews, out var gate))
        {
            StatusText = gate;
            RefreshGate();
            return;
        }

        SetBusy(true);
        StatusText = Strings.Get("FaceReviewMergingStatus");
        try
        {
            var result = await Task.Run(() => _merge(candidates, reviews));
            if (!_closed)
                StatusText = result.Message;
        }
        finally
        {
            if (!_closed) SetBusy(false);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Item_ReviewChanged(object? sender, EventArgs e)
    {
        RefreshGate();
        RenderOverlays();
    }

    private void RefreshGate()
    {
        var candidates = Items.Select(item => item.Candidate).ToArray();
        var reviews = Items.Select(item => item.ToReview()).ToArray();
        var canMerge = FaceReviewService.CanMerge(candidates, reviews, out var reason);
        var accepted = Items.Count(item => item.Decision == FaceReviewDecision.Accepted);
        var rejected = Items.Count(item => item.Decision == FaceReviewDecision.Rejected);
        var pending = Items.Count - accepted - rejected;
        ReviewSummary = Items.Count == 0
            ? Strings.Get("FaceReviewNoRegions")
            : Strings.Format("FaceReviewSummaryFormat", Items.Count, accepted, rejected, pending);
        MergeGateText = reason;
        MergeButton.IsEnabled = canMerge && !_isBusy;
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        AnalyzeCurrentButton.IsEnabled = !busy && _currentPath is not null;
        AnalyzeFolderButton.IsEnabled = !busy && _folderPaths.Count > 0;
        FaceList.IsEnabled = !busy;
        RefreshGate();
    }

    private void RenderSelection()
    {
        var selected = SelectedItem;
        PreviewEmptyState.Visibility = selected is null ? Visibility.Visible : Visibility.Collapsed;
        PreviewPanel.Visibility = selected is null ? Visibility.Collapsed : Visibility.Visible;
        if (selected is null)
        {
            SelectedImageSource = null;
            Overlays.Clear();
            return;
        }

        try
        {
            SelectedImageSource = ImageLoader.LoadPreviewImage(selected.Candidate.Key.SourcePath, 1600);
            using var image = MagickSafeReader.Read(
                selected.Candidate.Key.SourcePath,
                new MagickReadSettings { FrameIndex = 0, FrameCount = 1 });
            image.AutoOrient();
            SelectedImageWidth = image.Width;
            SelectedImageHeight = image.Height;
            selected.Thumbnail ??= BuildFaceThumbnail(
                SelectedImageSource as BitmapSource,
                selected.Candidate.Detection,
                SelectedImageWidth,
                SelectedImageHeight);
            RenderOverlays();
        }
        catch (Exception ex)
        {
            StatusText = Strings.Format("FaceReviewPreviewFailedFormat", ex.Message);
            SelectedImageSource = null;
            Overlays.Clear();
        }
    }

    private void RenderOverlays()
    {
        Overlays.Clear();
        if (SelectedItem is null) return;
        foreach (var item in Items.Where(item => string.Equals(
                     item.Candidate.Key.SourcePath,
                     SelectedItem.Candidate.Key.SourcePath,
                     StringComparison.OrdinalIgnoreCase)))
        {
            var face = item.Candidate.Detection;
            var selected = ReferenceEquals(item, SelectedItem);
            Overlays.Add(new FaceReviewOverlayItem(
                face.X,
                face.Y,
                face.Width,
                face.Height,
                item.DecisionBrush,
                new SolidColorBrush(Color.FromArgb(selected ? (byte)50 : (byte)24, 255, 255, 255)),
                selected ? 5 : 3));
        }
    }

    private static BitmapSource? BuildFaceThumbnail(
        BitmapSource? source,
        FaceDetection face,
        double sourceWidth,
        double sourceHeight)
    {
        if (source is null || sourceWidth <= 0 || sourceHeight <= 0) return null;
        var scaleX = source.PixelWidth / sourceWidth;
        var scaleY = source.PixelHeight / sourceHeight;
        var paddingX = face.Width * 0.18;
        var paddingY = face.Height * 0.18;
        var left = Math.Clamp((int)Math.Floor((face.X - paddingX) * scaleX), 0, source.PixelWidth - 1);
        var top = Math.Clamp((int)Math.Floor((face.Y - paddingY) * scaleY), 0, source.PixelHeight - 1);
        var right = Math.Clamp((int)Math.Ceiling((face.X + face.Width + paddingX) * scaleX), left + 1, source.PixelWidth);
        var bottom = Math.Clamp((int)Math.Ceiling((face.Y + face.Height + paddingY) * scaleY), top + 1, source.PixelHeight);
        var cropped = new CroppedBitmap(source, new Int32Rect(left, top, right - left, bottom - top));
        cropped.Freeze();
        return cropped;
    }

    private void Raise(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class FaceReviewItemViewModel : ObservableObject
{
    private static readonly Brush PendingBrush = FrozenBrush(Color.FromRgb(230, 164, 44));
    private static readonly Brush AcceptedBrush = FrozenBrush(Color.FromRgb(57, 191, 112));
    private static readonly Brush RejectedBrush = FrozenBrush(Color.FromRgb(222, 76, 86));
    private FaceReviewDecision _decision;
    private string _name = string.Empty;
    private BitmapSource? _thumbnail;

    public FaceReviewItemViewModel(FaceReviewCandidate candidate) => Candidate = candidate;

    public FaceReviewCandidate Candidate { get; }
    public string SourceFileName => Path.GetFileName(Candidate.Key.SourcePath);
    public string DisplayName => Strings.Format("FaceReviewRegionFormat", SourceFileName, Candidate.Key.FaceIndex + 1);
    public string ConfidenceText => Strings.Format("FaceReviewConfidenceFormat", Candidate.Detection.Confidence);
    public string ClusterLabel => Candidate.ClusterId is { } id
        ? Strings.Format("FaceReviewClusterFormat", id)
        : Strings.Get("FaceReviewUnclustered");
    public string QualityText => Candidate.QualityNote ?? Candidate.EmbeddingQuality.ToString();

    public FaceReviewDecision Decision
    {
        get => _decision;
        set
        {
            if (!Set(ref _decision, value)) return;
            Raise(nameof(DecisionText));
            Raise(nameof(DecisionBrush));
            ReviewChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            if (!Set(ref _name, value ?? string.Empty)) return;
            ReviewChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public BitmapSource? Thumbnail
    {
        get => _thumbnail;
        set => Set(ref _thumbnail, value);
    }

    public string DecisionText => Decision switch
    {
        FaceReviewDecision.Accepted => Strings.Get("FaceReviewAccepted"),
        FaceReviewDecision.Rejected => Strings.Get("FaceReviewRejected"),
        _ => Strings.Get("FaceReviewPending"),
    };

    public Brush DecisionBrush => Decision switch
    {
        FaceReviewDecision.Accepted => AcceptedBrush,
        FaceReviewDecision.Rejected => RejectedBrush,
        _ => PendingBrush,
    };

    public event EventHandler? ReviewChanged;

    public FaceReviewEntry ToReview() => new(Candidate.Key, Decision, Name);

    private static Brush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}

public sealed record FaceReviewOverlayItem(
    double X,
    double Y,
    double Width,
    double Height,
    Brush Brush,
    Brush Fill,
    double Thickness);
