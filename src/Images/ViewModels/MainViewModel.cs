using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Images.Services;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace Images.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private static readonly ILogger _log = Log.For<MainViewModel>();
    private readonly DirectoryNavigator _nav;
    private readonly RenameService _rename = new();
    private readonly PreloadService _preload = new();
    private readonly OcrService _ocr = new();
    private readonly Dispatcher _uiDispatcher = Dispatcher.CurrentDispatcher;
    private readonly SettingsService _settings;
    private readonly ClipboardImportService _clipboardImport;
    private readonly FolderPreviewController _folderPreview;
    private readonly Action<string> _sendToRecycleBin;
    private readonly Func<Window?, string, ConfirmDialog.ConfirmationResult> _confirmRecycleBinMove;
    private readonly DispatcherTimer _renameTimer;
    private readonly DispatcherTimer _toastTimer;

    private bool _suppressStemChange;
    private string _committedStemOnDisk = string.Empty;
    private bool _isDisposed;
    private int _metadataGeneration;
    private bool _isFilmstripVisible;
    private bool _isMetadataHudVisible;
    private bool _isOcrMode;
    private bool _isOcrBusy;
    private ObservableCollection<OcrTextLine>? _ocrOverlayLines;
    private CancellationTokenSource? _ocrCts;
    private int _ocrGeneration;
    private const int MetadataTimeoutSeconds = 5;
    private readonly DispatcherTimer _hintTimer;
    private System.IO.FileSystemWatcher? _externalEditWatcher;
    private readonly DispatcherTimer _externalEditDebounce;

    public MainViewModel()
        : this(SettingsService.Instance)
    {
    }

    public MainViewModel(
        SettingsService settings,
        ClipboardImportService? clipboardImport = null,
        DirectoryNavigator? navigator = null,
        Action<string>? sendToRecycleBin = null,
        Func<Window?, string, ConfirmDialog.ConfirmationResult>? confirmRecycleBinMove = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _clipboardImport = clipboardImport ?? new ClipboardImportService();
        _nav = navigator ?? new DirectoryNavigator();
        _sendToRecycleBin = sendToRecycleBin ?? SendToRecycleBin;
        _confirmRecycleBinMove = confirmRecycleBinMove ?? ConfirmDialog.ConfirmRecycleBinMove;
        _folderPreview = new FolderPreviewController(_uiDispatcher, () => _isDisposed);
        _folderPreview.StateChanged += (_, _) => RaiseFolderPreviewState();

        _renameTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _renameTimer.Tick += (_, _) => { _renameTimer.Stop(); FlushPendingRename(); };

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); ToastMessage = null; };

        _hintTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2400) };
        _hintTimer.Tick += (_, _) => { _hintTimer.Stop(); ShowGestureHint = false; };

        // Item 61: debounce timer for external-edit reload — fires 800 ms after the last
        // FileSystemWatcher.Changed event so rapid saves (e.g. from Photoshop's incremental
        // writes) don't trigger multiple reloads.
        _externalEditDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _externalEditDebounce.Tick += (_, _) =>
        {
            _externalEditDebounce.Stop();
            if (_isDisposed) return;
            if (ReloadCurrentSilent())
                Toast("Reloaded after external edit");
        };

        _isFilmstripVisible = _settings.GetBool(Keys.FilmstripVisible, true);
        _isMetadataHudVisible = _settings.GetBool(Keys.MetadataHudVisible, false);

        OpenCommand = new RelayCommand(OpenFileDialog);
        NextCommand = new RelayCommand(Next, () => HasImage);
        PrevCommand = new RelayCommand(Prev, () => HasImage);
        FirstCommand = new RelayCommand(First, () => HasImage);
        LastCommand = new RelayCommand(Last, () => HasImage);
        NextPageCommand = new RelayCommand(NextPage, () => HasNextPage);
        PrevPageCommand = new RelayCommand(PrevPage, () => HasPreviousPage);
        FirstPageCommand = new RelayCommand(FirstPage, () => HasPreviousPage);
        LastPageCommand = new RelayCommand(LastPage, () => HasNextPage);
        DeleteCommand = new RelayCommand(DeleteCurrent, () => HasImage);
        RotateCwCommand = new RelayCommand(() => Rotate(90), () => HasDisplayImage);
        RotateCcwCommand = new RelayCommand(() => Rotate(-90), () => HasDisplayImage);
        Rotate180Command = new RelayCommand(() => Rotate(180), () => HasDisplayImage);
        FlipHorizontalCommand = new RelayCommand(() => { FlipHorizontal = !FlipHorizontal; }, () => HasDisplayImage);
        FlipVerticalCommand = new RelayCommand(() => { FlipVertical = !FlipVertical; }, () => HasDisplayImage);
        RevealCommand = new RelayCommand(RevealInExplorer, () => HasImage);
        CopyPathCommand = new RelayCommand(CopyPath, () => HasImage);
        SetAsWallpaperCommand = new RelayCommand(SetAsWallpaper, () => HasImage);
        ReloadCommand = new RelayCommand(ReloadCurrent, () => HasImage);
        PrintCommand = new RelayCommand(PrintCurrent, () => HasDisplayImage);
        SaveAsCopyCommand = new RelayCommand(SaveAsCopy, () => HasDisplayImage);
        CheckForUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync(userInitiated: true), () => true);
        OpenLatestUpdateCommand = new RelayCommand(OpenLatestUpdate, () => HasUpdateAvailable);
        RefreshCommand = new RelayCommand(RefreshFolder, () => HasImage);
        CommitRenameCommand = new RelayCommand(() => { _renameTimer.Stop(); FlushPendingRename(); });
        CancelRenameCommand = new RelayCommand(CancelRenameEdit);
        UnlockExtensionCommand = new RelayCommand(() => IsExtensionUnlocked = !IsExtensionUnlocked);
        UndoRenameCommand = new RelayCommand(p => UndoOne(p as RenameService.UndoEntry), p => p is RenameService.UndoEntry);
        AboutCommand = new RelayCommand(ShowAboutWindow);
        OpenRecentFolderCommand = new RelayCommand(p => OpenRecentFolder(p as string), p => p is string);
        OpenPreviewItemCommand = new RelayCommand(p => OpenPreviewItem(p as FolderPreviewItem), p => p is FolderPreviewItem);
        RevealPreviewItemCommand = new RelayCommand(p => RevealPreviewItem(p as FolderPreviewItem), p => p is FolderPreviewItem);
        CopyPreviewPathCommand = new RelayCommand(p => CopyPreviewPath(p as FolderPreviewItem), p => p is FolderPreviewItem);
        EnsurePreviewThumbnailCommand = new RelayCommand(p => EnsurePreviewThumbnail(p as FolderPreviewItem), p => p is FolderPreviewItem);
        SetFolderSortCommand = new RelayCommand(SetFolderSort, p => DirectorySortModeInfo.TryParseCommandParameter(p, out _));
        ToggleFilmstripCommand = new RelayCommand(ToggleFilmstrip, () => CanToggleFilmstrip);
        ToggleMetadataHudCommand = new RelayCommand(ToggleMetadataHud, () => CanToggleMetadataHud);
        PasteFromClipboardCommand = new RelayCommand(PasteFromClipboard);
        OpenInDefaultAppCommand = new RelayCommand(OpenInDefaultApp, () => HasImage);
        StripLocationCommand = new RelayCommand(async () => await StripLocationAsync(), () => HasImage);
        SettingsCommand = new RelayCommand(ShowSettingsWindow);
        ExtractTextCommand = new RelayCommand(async () => await ExtractTextAsync(), () => HasImage);

        // V20-02 UI consumer: seed RecentFolders from SettingsService at startup so the side
        // panel renders prior-session folders before the user opens anything.
        RefreshRecentFolders();

        _rename.Renamed += (_, e) => PushUndoEntry(e);

        // External file add/remove/rename from Explorer or another app re-enumerates the folder.
        // Resync position chip + stale-current guard (if our current file vanished, advance).
        _nav.ListChanged += OnDirectoryListChanged;
    }

    private void OnDirectoryListChanged(object? sender, EventArgs e)
    {
        if (_isDisposed) return;

        Raise(nameof(PositionText));
        if (CurrentPath is not null && !File.Exists(CurrentPath))
        {
            // Current file was deleted externally — pick whatever slot the navigator landed on.
            if (_nav.CurrentPath is not null) { ResetPageState(); LoadCurrent(); }
            else ClearCurrentState();
        }
        else
        {
            RefreshFolderPreview();
        }
    }

    // -------------------- Image state --------------------

    private ImageSource? _currentImage;
    public ImageSource? CurrentImage
    {
        get => _currentImage;
        private set
        {
            if (Set(ref _currentImage, value))
            {
                Raise(nameof(HasDisplayImage));
                Raise(nameof(CanToggleMetadataHud));
                Raise(nameof(ShowMetadataHud));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    // Animated-frame payload. Non-null when the current file is a multi-frame GIF / APNG /
    // animated WebP. ZoomPanImage consumes this to drive an ObjectAnimationUsingKeyFrames on
    // the inner Image.SourceProperty. CurrentImage still carries the first frame so dimension
    // + decoder readouts continue to work even when Magick.NET's animated path is live.
    private AnimationSequence? _currentAnimation;
    public AnimationSequence? CurrentAnimation
    {
        get => _currentAnimation;
        private set
        {
            if (Set(ref _currentAnimation, value))
            {
                Raise(nameof(IsAnimated));
                Raise(nameof(AnimationFrameCountText));
            }
        }
    }

    // IsAnimated mirrors ZoomPanImage.OnAnimationChanged's `Frames.Count < 2` early-return so
    // a defensive 1-frame sequence never claims animation in the chip when the canvas isn't
    // actually playing. ImageLoader.TryLoadAnimated already returns null for <2 frames; this
    // guards future code paths that bypass the loader.
    public bool IsAnimated => CurrentAnimation is { Frames.Count: >= 2 };

    // V20-15-Loop: surface LoopCount on the existing animated chip. GIF convention is
    // LoopCount=0 → infinite (rendered as "loops"); any positive value is the exact iteration
    // count (rendered as "plays Mx"). Mirrors the ternary in ZoomPanImage.OnAnimationChanged
    // where we already honor the count via RepeatBehavior.Forever vs new RepeatBehavior(N).
    public string AnimationFrameCountText
    {
        get
        {
            // Same gate as IsAnimated — chip text is meaningful only when the canvas would
            // actually animate.
            if (!IsAnimated) return "";
            var n = CurrentAnimation!.Frames.Count;
            var frames = $"{n} frames";
            var loop = CurrentAnimation.LoopCount <= 0
                ? "loops"
                : $"plays {CurrentAnimation.LoopCount}\u00D7";
            return $"{frames} \u00B7 {loop}";
        }
    }

    private int _pageIndex;
    public int PageIndex
    {
        get => _pageIndex;
        private set
        {
            if (Set(ref _pageIndex, value))
            {
                RaisePageState();
            }
        }
    }

    private int _pageCount = 1;
    public int PageCount
    {
        get => _pageCount;
        private set
        {
            if (Set(ref _pageCount, Math.Max(1, value)))
            {
                RaisePageState();
            }
        }
    }

    private string _pageLabel = "Page";
    public string PageLabel
    {
        get => _pageLabel;
        private set
        {
            if (Set(ref _pageLabel, string.IsNullOrWhiteSpace(value) ? "Page" : value))
            {
                Raise(nameof(PagePositionText));
            }
        }
    }

    public bool HasMultiplePages => PageCount > 1;
    public bool HasPreviousPage => HasMultiplePages && PageIndex > 0;
    public bool HasNextPage => HasMultiplePages && PageIndex < PageCount - 1;
    public string PagePositionText => HasMultiplePages ? $"{PageLabel} {PageIndex + 1} / {PageCount}" : "";

    private void RaisePageState()
    {
        Raise(nameof(HasMultiplePages));
        Raise(nameof(HasPreviousPage));
        Raise(nameof(HasNextPage));
        Raise(nameof(PagePositionText));
        CommandManager.InvalidateRequerySuggested();
    }

    private string? _currentPath;
    public string? CurrentPath
    {
        get => _currentPath;
        private set
        {
            if (Set(ref _currentPath, value))
            {
                Raise(nameof(HasImage));
                Raise(nameof(CurrentFileName));
                Raise(nameof(CurrentFolder));
                Raise(nameof(PositionText));
                Raise(nameof(IsViewerEmpty));
                Raise(nameof(WindowTitle));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool HasImage => !string.IsNullOrEmpty(CurrentPath) && File.Exists(CurrentPath);
    public bool HasDisplayImage => CurrentImage is not null;
    public bool IsViewerEmpty => CurrentPath is null;

    // First-run gesture hint. Flipped true exactly once — the first time an image successfully
    // lands in the viewport. The view animates the pill in, then fades it out after 2.4 s.
    private bool _hasShownGestureHint;
    private bool _showGestureHint;
    public bool ShowGestureHint
    {
        get => _showGestureHint;
        private set => Set(ref _showGestureHint, value);
    }

    // V15-03: keyboard cheatsheet overlay toggled by `?`. Dismissed on any key or click.
    private bool _showCheatsheet;
    public bool ShowCheatsheet
    {
        get => _showCheatsheet;
        set => Set(ref _showCheatsheet, value);
    }

    // V15-07: fullscreen toggled by F11. The view collapses the side panel + floats the toolbar
    // when fullscreen, restores everything on exit.
    private bool _isFullscreen;
    public bool IsFullscreen
    {
        get => _isFullscreen;
        set => Set(ref _isFullscreen, value);
    }

    // V20-32: peek-mode flag — true when launched via `Images.exe --peek <path>` for chromeless,
    // topmost preview integrations (PowerToys Peek-style external-tool callout). Set ONCE at
    // construction by MainWindow.EnterPeekMode; never toggled at runtime. Drives toolbar +
    // bottom-status-row Visibility via the inverse converter so peek windows render image-only.
    private bool _isPeekMode;
    public bool IsPeekMode
    {
        get => _isPeekMode;
        set
        {
            if (Set(ref _isPeekMode, value))
            {
                Raise(nameof(CanToggleMetadataHud));
                Raise(nameof(ShowMetadataHud));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
    public string CurrentFileName => CurrentPath is null ? "" : Path.GetFileName(CurrentPath);
    public string CurrentFolder => CurrentPath is null ? "" : Path.GetDirectoryName(CurrentPath) ?? "";

    // Window title — filename first (Windows convention), app name second, em-dash separator.
    // Falls back to bare "Images" when no file is open.
    public string WindowTitle =>
        CurrentPath is null ? "Images" : $"{Path.GetFileName(CurrentPath)} — Images";

    private string? _loadErrorMessage;
    public string? LoadErrorMessage
    {
        get => _loadErrorMessage;
        private set
        {
            if (Set(ref _loadErrorMessage, value))
            {
                Raise(nameof(HasLoadError));
            }
        }
    }

    private string _loadErrorTitle = "This image couldn't be displayed";
    public string LoadErrorTitle { get => _loadErrorTitle; private set => Set(ref _loadErrorTitle, value); }

    private string _loadErrorHelpText = "";
    public string LoadErrorHelpText { get => _loadErrorHelpText; private set => Set(ref _loadErrorHelpText, value); }

    private bool _loadErrorShowsCodecDetails;
    public bool LoadErrorShowsCodecDetails
    {
        get => _loadErrorShowsCodecDetails;
        private set => Set(ref _loadErrorShowsCodecDetails, value);
    }

    public bool HasLoadError => !string.IsNullOrWhiteSpace(LoadErrorMessage);

    private bool _isDropTargetActive;
    public bool IsDropTargetActive
    {
        get => _isDropTargetActive;
        set => Set(ref _isDropTargetActive, value);
    }

    private bool _isDropAccepted;
    public bool IsDropAccepted
    {
        get => _isDropAccepted;
        set
        {
            if (Set(ref _isDropAccepted, value))
            {
                Raise(nameof(DropOverlayTitle));
                Raise(nameof(DropOverlayMessage));
            }
        }
    }

    public string DropOverlayTitle => IsDropAccepted ? "Drop to open file" : "Unsupported file";
    public string DropOverlayMessage => IsDropAccepted
        ? "Images will load this file and scan the folder for navigation."
        : SupportedImageFormats.DropUnsupportedMessage;

    private int _pixelWidth;
    public int PixelWidth { get => _pixelWidth; private set { if (Set(ref _pixelWidth, value)) Raise(nameof(DimensionsText)); } }

    private int _pixelHeight;
    public int PixelHeight { get => _pixelHeight; private set { if (Set(ref _pixelHeight, value)) Raise(nameof(DimensionsText)); } }

    public string DimensionsText => PixelWidth > 0 ? $"{PixelWidth} × {PixelHeight}" : "";

    private long _fileSize;
    public string FileSizeText => _fileSize <= 0 ? "" : FormatSize(_fileSize);

    public string PositionText =>
        _nav.Count == 0 ? "" : $"{_nav.CurrentIndex + 1} / {_nav.Count}";

    private double _rotation;
    public double Rotation { get => _rotation; private set => Set(ref _rotation, value); }

    // V15-02/V15-08: FlipHorizontal / FlipVertical are exposed as independent booleans so a
    // double-flip via the context menu toggles cleanly. ZoomPanImage consumes both via bindings
    // and multiplies them into its flip ScaleTransform — composing with rotate + zoom without
    // disturbing either.
    private bool _flipHorizontal;
    public bool FlipHorizontal { get => _flipHorizontal; private set => Set(ref _flipHorizontal, value); }

    private bool _flipVertical;
    public bool FlipVertical { get => _flipVertical; private set => Set(ref _flipVertical, value); }

    private string? _decoderUsed;
    public string? DecoderUsed { get => _decoderUsed; private set => Set(ref _decoderUsed, value); }

    // -------------------- Rename editor state --------------------

    private string _editableStem = string.Empty;
    public string EditableStem
    {
        get => _editableStem;
        set
        {
            if (!Set(ref _editableStem, value)) return;
            if (_suppressStemChange) return;
            Raise(nameof(RenamePreview));
            RenameStatus = RenameStatusKind.Pending;
            _renameTimer.Stop();
            _renameTimer.Start();
        }
    }

    private string _extension = string.Empty;
    public string Extension
    {
        get => _extension;
        set
        {
            var normalized = RenameService.NormalizeExtension(value ?? string.Empty);
            if (!Set(ref _extension, normalized)) return;
            Raise(nameof(RenamePreview));
            Raise(nameof(ExtensionLockText));
            if (IsExtensionUnlocked)
            {
                RenameStatus = RenameStatusKind.Pending;
                _renameTimer.Stop();
                _renameTimer.Start();
            }
        }
    }

    private bool _isExtensionUnlocked;
    public bool IsExtensionUnlocked
    {
        get => _isExtensionUnlocked;
        set
        {
            if (Set(ref _isExtensionUnlocked, value))
            {
                Raise(nameof(ExtensionLockText));
                Raise(nameof(ExtensionLockHelpText));
            }
        }
    }

    public string ExtensionLockText => IsExtensionUnlocked
        ? $"Extension editing unlocked: {Extension}"
        : $"Extension locked: {Extension}";

    public string ExtensionLockHelpText => IsExtensionUnlocked
        ? "Extension changes rename the file but do not convert image format."
        : "Unlock only if you need to rename the file extension.";

    /// <summary>
    /// Preview of what the target name will be after commit — including any " (2)" conflict suffix.
    /// </summary>
    public string RenamePreview
    {
        get
        {
            if (CurrentPath is null) return "";
            var clean = RenameService.Sanitize(EditableStem);
            if (string.IsNullOrEmpty(clean)) return "";
            var target = RenameService.ResolveTargetPath(
                Path.GetDirectoryName(CurrentPath)!, clean, Extension, CurrentPath);
            var targetName = Path.GetFileName(target);
            return string.Equals(targetName, Path.GetFileName(CurrentPath), StringComparison.OrdinalIgnoreCase)
                ? ""
                : $"→ {targetName}";
        }
    }

    public enum RenameStatusKind { Idle, Pending, Saved, Conflict, Error }

    private RenameStatusKind _renameStatus = RenameStatusKind.Idle;
    public RenameStatusKind RenameStatus
    {
        get => _renameStatus;
        set
        {
            if (Set(ref _renameStatus, value))
            {
                Raise(nameof(RenameStatusText));
            }
        }
    }

    public string RenameStatusText => RenameStatus switch
    {
        RenameStatusKind.Pending => "Unsaved changes",
        RenameStatusKind.Saved => "Saved",
        RenameStatusKind.Conflict => "Saved with safe suffix",
        RenameStatusKind.Error => "Needs attention",
        _ when HasImage => "Name is current",
        _ => "Open an image to rename"
    };

    // -------------------- Recent folders (V20-02 UI consumer) --------------------

    // Mirror of SettingsService.GetRecentFolders() suitable for ItemsControl binding. Refreshed
    // on construction + after every OpenFile. Stays a string collection; folder basename is
    // resolved at render time via PathToFileNameConverter.
    public ObservableCollection<string> RecentFolders { get; } = new();

    private void RefreshRecentFolders()
    {
        var fresh = _settings.GetRecentFolders();
        RecentFolders.Clear();
        foreach (var f in fresh) RecentFolders.Add(f);
    }

    // -------------------- Recent renames --------------------

    public ObservableCollection<RenameService.UndoEntry> RecentRenames { get; } = new();

    // -------------------- Photo metadata --------------------

    public ObservableCollection<MetadataFact> PhotoMetadataRows { get; } = new();

    private string _metadataStatusText = "";
    public string MetadataStatusText
    {
        get => _metadataStatusText;
        private set => Set(ref _metadataStatusText, value);
    }

    private bool _isMetadataLoading;
    public bool IsMetadataLoading
    {
        get => _isMetadataLoading;
        private set => Set(ref _isMetadataLoading, value);
    }

    public bool CanToggleMetadataHud => HasDisplayImage && !IsPeekMode;

    public bool IsMetadataHudVisible
    {
        get => _isMetadataHudVisible;
        private set
        {
            if (Set(ref _isMetadataHudVisible, value))
            {
                _settings.SetBool(Keys.MetadataHudVisible, value);
                Raise(nameof(ShowMetadataHud));
                Raise(nameof(MetadataHudToggleTooltip));
            }
        }
    }

    public bool ShowMetadataHud => CanToggleMetadataHud && IsMetadataHudVisible;

    public string MetadataHudToggleTooltip => IsMetadataHudVisible
        ? "Hide metadata HUD (I)"
        : "Show metadata HUD (I)";

    private void ToggleMetadataHud()
    {
        if (!CanToggleMetadataHud) return;

        IsMetadataHudVisible = !IsMetadataHudVisible;
        Toast(IsMetadataHudVisible ? "Metadata HUD on" : "Metadata HUD off");
    }

    private void RefreshPhotoMetadata(string path)
    {
        var generation = ++_metadataGeneration;
        PhotoMetadataRows.Clear();
        IsMetadataLoading = true;
        MetadataStatusText = "Reading photo metadata...";

        _ = LoadPhotoMetadataAsync(path, generation);
    }

    private async Task LoadPhotoMetadataAsync(string path, int generation)
    {
        PhotoMetadata metadata;
        string? statusOverride = null;
        try
        {
            metadata = await Task.Run(() => ImageMetadataService.Read(path))
                .WaitAsync(TimeSpan.FromSeconds(MetadataTimeoutSeconds))
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            metadata = PhotoMetadata.Empty;
            statusOverride = "Metadata read timed out.";
        }
        catch
        {
            metadata = PhotoMetadata.Empty;
        }

        await _uiDispatcher.InvokeAsync(() =>
        {
            if (_isDisposed || generation != _metadataGeneration ||
                !string.Equals(path, CurrentPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            PhotoMetadataRows.Clear();
            foreach (var row in metadata.Rows)
                PhotoMetadataRows.Add(row);

            IsMetadataLoading = false;
            MetadataStatusText = statusOverride ?? (PhotoMetadataRows.Count == 0
                ? "No embedded camera metadata."
                : "");
        });
    }

    private void ClearPhotoMetadata()
    {
        _metadataGeneration++;
        PhotoMetadataRows.Clear();
        IsMetadataLoading = false;
        MetadataStatusText = "";
    }

    // -------------------- Folder preview strip --------------------

    public ObservableCollection<FolderPreviewItem> FolderPreviewItems => _folderPreview.Items;

    public bool IsFilmstripVisible
    {
        get => _isFilmstripVisible;
        private set
        {
            if (!Set(ref _isFilmstripVisible, value)) return;
            _settings.SetBool(Keys.FilmstripVisible, value);
            RaiseFolderPreviewState();
        }
    }

    public bool CanToggleFilmstrip => _nav.Count > 1;

    public bool ShowFilmstrip => IsFilmstripVisible && FolderPreviewItems.Count > 0;

    public bool ShowSideFolderPreview => !IsFilmstripVisible && FolderPreviewItems.Count > 0;

    public string FilmstripToggleTooltip => IsFilmstripVisible ? "Hide filmstrip (T)" : "Show filmstrip (T)";

    public DirectorySortMode CurrentSortMode => _nav.SortMode;

    public string FolderSortLabel => DirectorySortModeInfo.ShortLabel(_nav.SortMode);

    public string FolderSortTooltip => $"Sort folder by {DirectorySortModeInfo.DisplayName(_nav.SortMode)}";

    private void RefreshFolderPreview()
    {
        _folderPreview.Refresh(_nav.Files, _nav.CurrentIndex);
    }

    private void EnsurePreviewThumbnail(FolderPreviewItem? item)
    {
        _folderPreview.EnsureThumbnail(item);
    }

    private void OpenPreviewItem(FolderPreviewItem? item)
    {
        if (item is null || !File.Exists(item.Path)) return;
        OpenFile(item.Path);
    }

    private void RevealPreviewItem(FolderPreviewItem? item)
    {
        RevealPathInExplorer(item?.Path);
    }

    private void CopyPreviewPath(FolderPreviewItem? item)
    {
        CopyPath(item?.Path);
    }

    private void ToggleFilmstrip()
    {
        if (!CanToggleFilmstrip) return;

        IsFilmstripVisible = !IsFilmstripVisible;
        Toast(IsFilmstripVisible ? "Filmstrip shown" : "Filmstrip hidden");
    }

    private void SetFolderSort(object? parameter)
    {
        if (!DirectorySortModeInfo.TryParseCommandParameter(parameter, out var mode)) return;
        if (!_nav.SetSortMode(mode)) return;

        RaiseFolderPreviewState();
        Toast($"Sorted by {DirectorySortModeInfo.DisplayName(mode)}");
    }

    private void RaiseFolderPreviewState()
    {
        Raise(nameof(CanToggleFilmstrip));
        Raise(nameof(ShowFilmstrip));
        Raise(nameof(ShowSideFolderPreview));
        Raise(nameof(FilmstripToggleTooltip));
        Raise(nameof(CurrentSortMode));
        Raise(nameof(FolderSortLabel));
        Raise(nameof(FolderSortTooltip));
        CommandManager.InvalidateRequerySuggested();
    }

    private void PushUndoEntry(RenameService.UndoEntry entry)
    {
        RecentRenames.Insert(0, entry);
        while (RecentRenames.Count > 10) RecentRenames.RemoveAt(RecentRenames.Count - 1);
    }

    private void UndoOne(RenameService.UndoEntry? entry)
    {
        if (entry is null) return;
        try
        {
            var result = _rename.Revert(entry);
            if (result is null)
            {
                Toast("Cannot undo — file no longer exists at that path");
                RecentRenames.Remove(entry);
                return;
            }

            RecentRenames.Remove(entry);

            // If the reverted file is the currently open one, follow it.
            if (string.Equals(CurrentPath, entry.FromPath, StringComparison.OrdinalIgnoreCase))
            {
                CurrentPath = result.ToPath;
                _nav.UpdateCurrentPath(result.ToPath);
                SyncRenameEditorFromDisk();
            }
            Toast($"Reverted to {Path.GetFileName(result.ToPath)}");
        }
        catch (Exception ex)
        {
            Toast($"Undo failed: {ex.Message}");
        }
    }

    // -------------------- Toast --------------------

    public enum ToastToneKind { Info, Success, Warning, Error }

    private string? _toast;
    public string? ToastMessage { get => _toast; private set => Set(ref _toast, value); }

    private ToastToneKind _toastTone = ToastToneKind.Info;
    public ToastToneKind ToastTone { get => _toastTone; private set => Set(ref _toastTone, value); }

    private void Toast(string message)
    {
        ToastTone = InferToastTone(message);
        ToastMessage = message;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private static ToastToneKind InferToastTone(string message)
    {
        var m = message.ToLowerInvariant();
        if (m.Contains("failed") ||
            m.Contains("could not") ||
            m.Contains("cannot") ||
            m.Contains("unreachable") ||
            m.Contains("no longer exists") ||
            m.Contains("busy"))
            return ToastToneKind.Error;

        if (m.Contains("no images") ||
            m.Contains("no text") ||
            m.Contains("no gps") ||
            m.Contains("unavailable") ||
            m.Contains("canceled") ||
            m.Contains("choose a different") ||
            m.Contains("unsupported") ||
            m.Contains("name taken") ||
            m.Contains("new version"))
            return ToastToneKind.Warning;

        if (m.Contains("copied") ||
            m.Contains("saved") ||
            m.Contains("renamed") ||
            m.Contains("reverted") ||
            m.Contains("sent") ||
            m.Contains("set as wallpaper") ||
            m.Contains("reloaded") ||
            m.Contains("refreshed") ||
            m.Contains("latest version"))
            return ToastToneKind.Success;

        return ToastToneKind.Info;
    }

    public void DismissToast()
    {
        _toastTimer.Stop();
        ToastMessage = null;
    }

    /// <summary>View-facing toast helper so the code-behind doesn't have to reach through Set().</summary>
    public void ShowToast(string message) => Toast(message);

    // -------------------- Commands --------------------

    public ICommand OpenCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand PrevCommand { get; }
    public ICommand FirstCommand { get; }
    public ICommand LastCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PrevPageCommand { get; }
    public ICommand FirstPageCommand { get; }
    public ICommand LastPageCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RotateCwCommand { get; }
    public ICommand RotateCcwCommand { get; }
    public ICommand Rotate180Command { get; }
    public ICommand FlipHorizontalCommand { get; }
    public ICommand FlipVerticalCommand { get; }
    public ICommand RevealCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand SetAsWallpaperCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand SaveAsCopyCommand { get; }
    public ICommand CheckForUpdatesCommand { get; }
    public ICommand OpenLatestUpdateCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand CommitRenameCommand { get; }
    public ICommand CancelRenameCommand { get; }
    public ICommand UnlockExtensionCommand { get; }
    public ICommand UndoRenameCommand { get; }
    public ICommand AboutCommand { get; }
    public ICommand OpenRecentFolderCommand { get; }
    public ICommand OpenPreviewItemCommand { get; }
    public ICommand RevealPreviewItemCommand { get; }
    public ICommand CopyPreviewPathCommand { get; }
    public ICommand EnsurePreviewThumbnailCommand { get; }
    public ICommand SetFolderSortCommand { get; }
    public ICommand ToggleFilmstripCommand { get; }
    public ICommand ToggleMetadataHudCommand { get; }
    public ICommand PasteFromClipboardCommand { get; }
    public ICommand OpenInDefaultAppCommand { get; }
    public ICommand StripLocationCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand ExtractTextCommand { get; }

    // -------------------- OCR --------------------

    public bool IsOcrMode
    {
        get => _isOcrMode;
        set
        {
            if (Set(ref _isOcrMode, value))
            {
                Raise(nameof(OcrModeTooltip));
                RaiseOcrStatusState();
            }
        }
    }

    public bool IsOcrBusy
    {
        get => _isOcrBusy;
        private set
        {
            if (Set(ref _isOcrBusy, value))
            {
                Raise(nameof(OcrModeTooltip));
                RaiseOcrStatusState();
            }
        }
    }

    public bool ShowOcrStatusPanel => IsOcrBusy || IsOcrMode;

    public string OcrModeTooltip => IsOcrBusy
        ? "Cancel text extraction (E)"
        : IsOcrMode
            ? "Hide text overlay (E)"
            : "Extract text locally (E)";

    public string OcrStatusTitle => IsOcrBusy
        ? "Extracting text locally"
        : "Text overlay active";

    public string OcrStatusDetail => IsOcrBusy
        ? "Windows OCR is reading this image on your PC. Press E again to cancel."
        : $"{OcrRegionCountText}. Select a text box and press Ctrl+C to copy.";

    public string OcrRegionCountText
    {
        get
        {
            var count = OcrOverlayLines?.Count ?? 0;
            return count == 1 ? "1 text region found" : $"{count} text regions found";
        }
    }

    public ObservableCollection<OcrTextLine>? OcrOverlayLines
    {
        get => _ocrOverlayLines;
        set
        {
            if (Set(ref _ocrOverlayLines, value))
                RaiseOcrStatusState();
        }
    }

    private void RaiseOcrStatusState()
    {
        Raise(nameof(ShowOcrStatusPanel));
        Raise(nameof(OcrStatusTitle));
        Raise(nameof(OcrStatusDetail));
        Raise(nameof(OcrRegionCountText));
    }

    // -------------------- Navigation --------------------

    public void OpenFile(string path)
    {
        FlushPendingRename();

        if (!SupportedImageFormats.IsSupported(path))
        {
            // V20-37 / item 86: human-readable suggestion when a recognized non-image type is
            // dropped (video, archive, document) so the user understands why nothing happened.
            var ext = Path.GetExtension(path);
            var suggestion = SupportedImageFormats.SuggestionForUnsupported(ext);
            Toast(suggestion is null
                ? $"Unsupported file type: {ext}"
                : $"Unsupported {ext}. {suggestion}");
            return;
        }

        var previousFolder = _nav.Folder;
        if (!_nav.Open(path))
        {
            Toast(File.Exists(path)
                ? $"Could not open {Path.GetFileName(path)}"
                : "File no longer exists");
            return;
        }

        if (!string.Equals(previousFolder, _nav.Folder, StringComparison.OrdinalIgnoreCase))
            _preload.Reset();

        ResetPageState();
        LoadCurrent();
        ArmFileWatcher(path);

        // V20-02: persist containing folder to recent-folders MRU. Silent on any failure —
        // recent-folders is a convenience, not critical.
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
        {
            _settings.TouchRecentFolder(folder);
            // V20-02 UI consumer: keep the side-panel "Recent folders" list current within the
            // session — TouchRecentFolder reorders the underlying table, this re-pulls the
            // top-N for the UI binding.
            RefreshRecentFolders();
        }
    }

    // V20-02 UI consumer: open the first supported image in <folder>. Click handler for the
    // side-panel "Recent folders" cards. EnumerateFiles avoids materializing a full list when
    // the folder is huge (medium-DAM scenario — we only need the first match).
    private void OpenRecentFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return;
        if (!Directory.Exists(folder))
        {
            Toast("Folder no longer exists");
            RefreshRecentFolders(); // GetRecentFolders filters missing folders — re-pull.
            return;
        }

        try
        {
            var first = DirectoryNavigator.FirstSupportedImageInFolder(folder);
            if (first is null)
            {
                Toast($"No images in {Path.GetFileName(folder)}");
                return;
            }
            OpenFile(first);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Toast("Folder unreachable");
        }
    }

    private void PasteFromClipboard()
    {
        var result = _clipboardImport.Import();
        if (result.Path is not null)
            OpenFile(result.Path);

        if (!string.IsNullOrWhiteSpace(result.Message))
            Toast(result.Message);
    }

    private void OpenInDefaultApp()
    {
        if (CurrentPath is null) return;
        try
        {
            ShellIntegration.OpenShellTarget(CurrentPath);
        }
        catch (Exception ex)
        {
            Toast($"Could not open: {ex.Message}");
        }
    }

    private void OpenFileDialog()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open image or preview",
            Filter = SupportedImageFormats.OpenDialogFilter,
            FilterIndex = 1
        };
        if (dlg.ShowDialog() == true) OpenFile(dlg.FileName);
    }

    private void Next() { FlushPendingRename(); if (_nav.MoveNext()) { ResetPageState(); LoadCurrent(); } }
    private void Prev() { FlushPendingRename(); if (_nav.MovePrevious()) { ResetPageState(); LoadCurrent(); } }
    private void First() { FlushPendingRename(); if (_nav.MoveFirst()) { ResetPageState(); LoadCurrent(); } }
    private void Last() { FlushPendingRename(); if (_nav.MoveLast()) { ResetPageState(); LoadCurrent(); } }

    private void NextPage()
    {
        if (!HasNextPage) return;
        FlushPendingRename();
        PageIndex++;
        LoadCurrent();
    }

    private void PrevPage()
    {
        if (!HasPreviousPage) return;
        FlushPendingRename();
        PageIndex--;
        LoadCurrent();
    }

    private void FirstPage()
    {
        if (!HasPreviousPage) return;
        FlushPendingRename();
        PageIndex = 0;
        LoadCurrent();
    }

    private void LastPage()
    {
        if (!HasNextPage) return;
        FlushPendingRename();
        PageIndex = PageCount - 1;
        LoadCurrent();
    }

    private bool LoadCurrent()
    {
        var path = _nav.CurrentPath;
        if (path is null)
        {
            ClearCurrentState();
            return false;
        }

        ClearOcrOverlay(cancelExtraction: true);
        var loaded = false;

        try
        {
            // V20-03: try the preload ring first — a hit is instant, a miss falls through to
            // the direct load. Either way we re-enqueue the new neighbors.
            var res = PageIndex == 0
                ? _preload.TryGet(path) ?? ImageLoader.Load(path, PageIndex)
                : ImageLoader.Load(path, PageIndex);
            // Order matters: CurrentImage first so ZoomPanImage.OnSourceChanged runs and clears
            // any animation from the previous file; then CurrentAnimation, which either applies
            // new keyframes or stays null for a static image.
            CurrentImage = res.Image;
            CurrentAnimation = res.Animation;
            PixelWidth = res.PixelWidth;
            PixelHeight = res.PixelHeight;
            DecoderUsed = res.DecoderUsed;
            ApplyPageSequence(res.Pages);
            Rotation = 0;
            FlipHorizontal = false;
            FlipVertical = false;
            ClearLoadError();
            loaded = true;

            // First-run only — surface the gesture hint pill the first time an image lands.
            if (!_hasShownGestureHint)
            {
                _hasShownGestureHint = true;
                ShowGestureHint = true;
                _hintTimer.Stop();
                _hintTimer.Start();
            }
        }
        catch (Exception ex)
        {
            CurrentImage = null;
            CurrentAnimation = null;
            PixelWidth = PixelHeight = 0;
            DecoderUsed = "Unavailable";
            ResetPageState();
            SetLoadError(ex);
        }

        CurrentPath = path;
        try { _fileSize = new FileInfo(path).Length; } catch { _fileSize = 0; }
        Raise(nameof(FileSizeText));
        RefreshFolderPreview();
        if (CurrentImage is null)
            ClearPhotoMetadata();
        else
            RefreshPhotoMetadata(path);

        SyncRenameEditorFromDisk();

        // V20-03: after loading, enqueue neighbours so the next arrow-press is instant.
        // The preload itself runs off the UI thread.
        EnqueueNeighbours();
        return loaded;
    }

    private void SetLoadError(Exception ex)
    {
        var path = _nav.CurrentPath;
        var ext = path is null ? "" : Path.GetExtension(path);

        // Ghostscript dependency — special case with codec-details link.
        if (ex.Message.Contains("requires Ghostscript", StringComparison.OrdinalIgnoreCase))
        {
            LoadErrorTitle = "Document preview needs Ghostscript";
            LoadErrorMessage = "This file type depends on Ghostscript for document and Adobe Illustrator previews. Images can use a bundled runtime, IMAGES_GHOSTSCRIPT_DIR, or an installed Ghostscript copy.";
            LoadErrorHelpText = "Open codec details to see the active runtime status and copy a support report.";
            LoadErrorShowsCodecDetails = true;
            Toast("Document preview needs Ghostscript");
            return;
        }

        // Specific exception types carry their own actionable message.
        if (ex is FileNotFoundException)
        {
            LoadErrorTitle = "File not found";
            LoadErrorMessage = "This file no longer exists. It may have been moved, renamed, or deleted.";
            LoadErrorHelpText = "Navigate to another image or open a new file.";
            LoadErrorShowsCodecDetails = false;
            Toast("File not found");
            return;
        }

        if (ex is UnauthorizedAccessException)
        {
            LoadErrorTitle = "Access denied";
            LoadErrorMessage = "You do not have permission to read this file.";
            LoadErrorHelpText = "Check the file's security properties in Explorer, or try running Images as Administrator.";
            LoadErrorShowsCodecDetails = false;
            Toast("Access denied");
            return;
        }

        if (ex is OutOfMemoryException)
        {
            LoadErrorTitle = "Image too large";
            LoadErrorMessage = "This image is too large to fit in available memory.";
            LoadErrorHelpText = "Close other applications to free memory, or try reopening the file.";
            LoadErrorShowsCodecDetails = false;
            Toast("Image too large for available memory");
            return;
        }

        // Item 86 enhancement: format-specific decode hints for supported-but-failing types.
        var decodeHint = string.IsNullOrEmpty(ext)
            ? null
            : SupportedImageFormats.SuggestionForDecodeFailure(ext);

        LoadErrorTitle = "This image couldn't be displayed";
        LoadErrorMessage = $"This file could not be decoded. {ex.Message}";
        LoadErrorHelpText = decodeHint
            ?? "Try another file, reveal the file in Explorer, or reload after another app finishes writing it.";
        LoadErrorShowsCodecDetails = false;
        Toast("Could not decode this file");
    }

    private void ClearLoadError()
    {
        LoadErrorTitle = "This image couldn't be displayed";
        LoadErrorHelpText = "";
        LoadErrorShowsCodecDetails = false;
        LoadErrorMessage = null;
    }

    private void ApplyPageSequence(PageSequence? pages)
    {
        if (pages is null)
        {
            ResetPageState();
            return;
        }

        PageLabel = pages.Label;
        PageCount = pages.PageCount;
        PageIndex = pages.PageIndex;
    }

    private void ResetPageState()
    {
        PageLabel = "Page";
        PageCount = 1;
        PageIndex = 0;
    }

    private void EnqueueNeighbours()
    {
        if (_nav.Count < 2) return;
        var idx = _nav.CurrentIndex;
        var n = _nav.Count;
        // Wrap-aware: folder nav wraps at ends, so we want N+1 and N-1 to wrap too.
        var next = _nav.Files[(idx + 1) % n];
        var prev = _nav.Files[(idx - 1 + n) % n];
        _preload.Enqueue(next);
        if (prev != next) _preload.Enqueue(prev);
    }

    private void SyncRenameEditorFromDisk()
    {
        if (CurrentPath is null)
        {
            Extension = "";
            _committedStemOnDisk = "";
            _suppressStemChange = true;
            EditableStem = "";
            _suppressStemChange = false;
            IsExtensionUnlocked = false;
            Raise(nameof(RenamePreview));
            return;
        }
        var stem = Path.GetFileNameWithoutExtension(CurrentPath);
        var ext = Path.GetExtension(CurrentPath);
        _committedStemOnDisk = stem;
        Extension = ext;
        IsExtensionUnlocked = false;

        _suppressStemChange = true;
        EditableStem = stem;
        _suppressStemChange = false;
        RenameStatus = RenameStatusKind.Idle;
        Raise(nameof(RenamePreview));
    }

    // -------------------- Rename --------------------

    public void FlushPendingRename()
    {
        _renameTimer.Stop();
        if (CurrentPath is null) return;
        if (string.Equals(EditableStem, _committedStemOnDisk, StringComparison.Ordinal) && !IsExtensionUnlocked)
        {
            RenameStatus = RenameStatusKind.Idle;
            return;
        }

        try
        {
            if (string.IsNullOrEmpty(RenameService.Sanitize(EditableStem)))
            {
                RenameStatus = RenameStatusKind.Error;
                Toast("Filename needs at least one valid character");
                return;
            }

            var newPath = _rename.Commit(CurrentPath, EditableStem, Extension);
            if (string.Equals(newPath, CurrentPath, StringComparison.OrdinalIgnoreCase))
            {
                RenameStatus = RenameStatusKind.Idle;
                return;
            }

            _nav.UpdateCurrentPath(newPath);
            CurrentPath = newPath;
            _committedStemOnDisk = Path.GetFileNameWithoutExtension(newPath);
            Extension = Path.GetExtension(newPath);

            var conflicted = !string.Equals(
                _committedStemOnDisk,
                RenameService.Sanitize(EditableStem),
                StringComparison.Ordinal);

            RenameStatus = conflicted ? RenameStatusKind.Conflict : RenameStatusKind.Saved;
            Toast(conflicted
                ? $"Saved as {Path.GetFileName(newPath)} (name taken)"
                : $"Renamed → {Path.GetFileName(newPath)}");

            if (conflicted)
            {
                _suppressStemChange = true;
                EditableStem = _committedStemOnDisk;
                _suppressStemChange = false;
            }
            Raise(nameof(RenamePreview));
        }
        catch (Exception ex)
        {
            RenameStatus = RenameStatusKind.Error;
            Toast($"Rename failed: {ex.Message}");
        }
    }

    private void CancelRenameEdit()
    {
        _renameTimer.Stop();
        _suppressStemChange = true;
        EditableStem = _committedStemOnDisk;
        _suppressStemChange = false;
        RenameStatus = RenameStatusKind.Idle;
        Raise(nameof(RenamePreview));
    }

    // -------------------- Other actions --------------------

    private void DeleteCurrent()
    {
        if (CurrentPath is null || !File.Exists(CurrentPath)) return;
        _renameTimer.Stop();

        var toDelete = CurrentPath;
        if (!ConfirmRecycleBinDelete(toDelete))
        {
            Toast("Delete canceled");
            return;
        }

        try
        {
            _sendToRecycleBin(toDelete);

            _nav.RemoveCurrent();
            Toast($"Sent to Recycle Bin: {Path.GetFileName(toDelete)}");
            if (_nav.CurrentPath is null) { ClearCurrentState(); return; }
            ResetPageState();
            LoadCurrent();
        }
        catch (Exception ex)
        {
            Toast($"Delete failed: {ex.Message}");
        }
    }

    private bool ConfirmRecycleBinDelete(string path)
    {
        if (!_settings.GetBool(Keys.ConfirmRecycleBinDelete, true))
            return true;

        var result = _confirmRecycleBinMove(Application.Current?.MainWindow, path);
        if (result.DoNotAskAgain)
        {
            _settings.SetBool(Keys.ConfirmRecycleBinDelete, false);
            Toast("Recycle Bin confirmation disabled");
        }

        return result.Confirmed;
    }

    private static void SendToRecycleBin(string path)
    {
        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
            path,
            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,
            Microsoft.VisualBasic.FileIO.UICancelOption.DoNothing);
    }

    private void Rotate(double delta)
    {
        Rotation = (Rotation + delta) % 360;
    }

    // V15-04: Reload re-enumerates the current file through the loader, re-applying WIC /
    // Magick / animated-GIF path as appropriate. Useful after external edit (Photoshop,
    // mspaint). View transforms are restored after the decode attempt so reload does not
    // surprise the user by resetting their current orientation.
    private void ReloadCurrent()
    {
        if (ReloadCurrentPreservingViewState(resetPreload: false))
            Toast("Reloaded");
    }

    // V15-02: Set current image as the desktop wallpaper. Delegates to WallpaperService which
    // copies to %LOCALAPPDATA%\Images\wallpaper\current.<ext> before calling SystemParametersInfo,
    // so a later rename / move of the source file doesn't break the desktop.
    private void SetAsWallpaper()
    {
        if (CurrentPath is null) return;
        try
        {
            var dest = WallpaperService.SetFromFile(CurrentPath);
            Toast($"Set as wallpaper: {Path.GetFileName(dest)}");
        }
        catch (Exception ex)
        {
            Toast($"Wallpaper failed: {ex.Message}");
        }
    }

    private void RevealInExplorer() => RevealPathInExplorer(CurrentPath);

    private void RevealPathInExplorer(string? path)
    {
        if (path is null) return;
        string full;
        try { full = System.IO.Path.GetFullPath(path); }
        catch (Exception ex) { Toast($"Could not open Explorer: {ex.Message}"); return; }

        if (!File.Exists(full))
        {
            Toast("File no longer exists");
            return;
        }

        try
        {
            ShellIntegration.RevealPathInExplorer(full);
        }
        catch (Exception ex) { Toast($"Could not open Explorer: {ex.Message}"); }
    }

    private void CopyPath() => CopyPath(CurrentPath);

    private void CopyPath(string? path)
    {
        if (path is null) return;
        try { ClipboardService.SetText(path); Toast("Copied path"); }
        catch (Exception ex) { Toast($"Copy failed: {ex.Message}"); }
    }

    // P-04: checks GitHub Releases API for a newer tag. userInitiated=true fires regardless of
    // the 24-h throttle (manual check from the About dialog); userInitiated=false obeys the
    // throttle (silent startup check).
    public async Task CheckForUpdatesAsync(bool userInitiated)
    {
        if (!userInitiated && !UpdateCheckService.IsDueForBackgroundCheck())
            return;

        var result = await UpdateCheckService.CheckAsync().ConfigureAwait(true);
        UpdateCheckService.RecordLastCheckedIfAppropriate(result);

        if (result.Error is not null)
        {
            if (userInitiated) Toast($"Update check failed: {result.Error}");
            return;
        }

        if (result.NewerAvailable)
        {
            Toast($"New version {result.LatestTag} available");
            LatestUpdateTag = result.LatestTag;
            LatestUpdateUrl = result.LatestHtmlUrl;
        }
        else if (userInitiated)
        {
            Toast("You're on the latest version");
        }
    }

    private string? _latestUpdateTag;
    public string? LatestUpdateTag
    {
        get => _latestUpdateTag;
        private set
        {
            if (Set(ref _latestUpdateTag, value))
            {
                Raise(nameof(HasUpdateAvailable));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string? LatestUpdateUrl { get; private set; }

    public bool HasUpdateAvailable => !string.IsNullOrEmpty(LatestUpdateTag);

    private void OpenLatestUpdate()
    {
        if (string.IsNullOrWhiteSpace(LatestUpdateUrl)) return;
        try
        {
            ShellIntegration.OpenShellTarget(LatestUpdateUrl);
        }
        catch (Exception ex)
        {
            Toast($"Could not open release page: {ex.Message}");
        }
    }

    // E6: save a copy of the decoded first frame to a user-chosen path. Rotation and flip are
    // not baked in, matching Windows Photos: temporary viewing transforms stay temporary.
    private void SaveAsCopy()
    {
        if (CurrentImage is not System.Windows.Media.Imaging.BitmapSource bs || CurrentPath is null) return;

        var sourceExtension = ImageExportService.NormalizeExportExtension(Path.GetExtension(CurrentPath));

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save a copy",
            FileName = Path.GetFileNameWithoutExtension(CurrentPath) + "_copy" + sourceExtension,
            Filter = ImageExportService.ExportFilter,
            DefaultExt = sourceExtension.TrimStart('.'),
            AddExtension = true,
            InitialDirectory = Path.GetDirectoryName(CurrentPath),
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var targetPath = ImageExportService.ResolveWritablePath(dlg.FileName);
            if (PathsReferToSameFile(targetPath, CurrentPath))
            {
                Toast("Choose a different filename for the copy");
                return;
            }

            var savedPath = ImageExportService.Save(bs, targetPath);
            Toast($"Saved copy → {Path.GetFileName(savedPath)}");
        }
        catch (Exception ex)
        {
            Toast($"Save failed: {ex.Message}");
        }
    }

    private static bool PathsReferToSameFile(string left, string right)
    {
        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    // V15-10: print current image via PrintDialog. Prints the CurrentImage exactly as decoded —
    // rotation + flip aren't applied to the printed output (same convention as Preview / Photos).
    // If users want the rotated version printed, they save-as-copy first (E6) then print that.
    private void PrintCurrent()
    {
        if (CurrentImage is not System.Windows.Media.Imaging.BitmapSource bs || CurrentPath is null) return;
        try
        {
            var title = System.IO.Path.GetFileName(CurrentPath);
            if (PrintService.Print(bs, title))
                Toast("Sent to printer");
        }
        catch (Exception ex)
        {
            Toast($"Print failed: {ex.Message}");
        }
    }

    // V15-06: open the About dialog. Owner is resolved via the active main window so the dialog
    // centers on-owner rather than on-screen and inherits taskbar semantics.
    private void ShowAboutWindow()
    {
        var about = new Images.AboutWindow
        {
            Owner = Application.Current?.MainWindow
        };
        about.ShowDialog();
    }

    // Item 2: Settings window — opens modal, then re-reads persistent prefs so the viewer
    // immediately reflects any toggle the user flipped (e.g. filmstrip, metadata HUD).
    private void ShowSettingsWindow()
    {
        var settings = new Images.SettingsWindow
        {
            Owner = Application.Current?.MainWindow
        };
        settings.ShowDialog();
        RefreshSettingsFromStore();
    }

    private void RefreshSettingsFromStore()
    {
        var filmstrip = _settings.GetBool(Keys.FilmstripVisible, true);
        if (_isFilmstripVisible != filmstrip)
        {
            _isFilmstripVisible = filmstrip;
            Raise(nameof(IsFilmstripVisible));
            RaiseFolderPreviewState();
        }

        var hud = _settings.GetBool(Keys.MetadataHudVisible, false);
        if (_isMetadataHudVisible != hud)
        {
            _isMetadataHudVisible = hud;
            Raise(nameof(IsMetadataHudVisible));
            Raise(nameof(ShowMetadataHud));
        }
    }

    // P-01: Strip GPS location data from the current file using Magick.NET.
    // Writes atomically (temp-file swap) and reloads so the metadata HUD updates.
    private async Task StripLocationAsync()
    {
        if (CurrentPath is null) return;
        var path = CurrentPath;

        try
        {
            var removed = await Task.Run(() => MetadataEditService.StripGpsMetadata(path));
            if (removed == 0)
            {
                Toast("No GPS data found in this file");
            }
            else
            {
                _preload.Reset();
                LoadCurrent();
                Toast($"GPS location removed ({removed} {(removed == 1 ? "field" : "fields")})");
            }
        }
        catch (Exception ex)
        {
            Toast($"Could not strip GPS data: {ex.Message}");
        }
    }

    // OCR text extraction using Windows.Media.Ocr API. Toggles overlay display on success;
    // if already in OCR mode, toggles off. Handles all error cases with user-facing toasts.
    private async Task ExtractTextAsync()
    {
        if (CurrentPath == null || !HasImage)
        {
            Toast("No image loaded");
            return;
        }

        // Toggle off if already in OCR mode or cancel an extraction in progress.
        if (IsOcrMode || _ocrCts is not null)
        {
            var wasBusy = IsOcrBusy || _ocrCts is not null;
            ClearOcrOverlay(cancelExtraction: true);
            Toast(wasBusy ? "Text extraction canceled" : "Text overlay hidden");
            return;
        }

        var path = CurrentPath;
        var generation = ++_ocrGeneration;
        var cts = new CancellationTokenSource();
        _ocrCts = cts;
        IsOcrBusy = true;
        Toast("Extracting text locally...");

        try
        {
            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var result = await _ocr.ExtractTextAsync(fileStream, cts.Token);
            if (cts.IsCancellationRequested || generation != _ocrGeneration ||
                !string.Equals(path, CurrentPath, StringComparison.OrdinalIgnoreCase))
                return;

            if (result == null)
            {
                Toast("OCR unavailable — no Windows OCR language pack installed");
                IsOcrMode = false;
                return;
            }

            if (result.Lines.Count == 0)
            {
                Toast("No text found");
                IsOcrMode = false;
                return;
            }

            // Convert OcrResult to overlay-renderable lines
            var lines = new ObservableCollection<OcrTextLine>();
            foreach (var line in result.Lines)
            {
                if (line.Words.Count == 0 || string.IsNullOrWhiteSpace(line.Text))
                    continue;

                var left = line.Words.Min(word => word.BoundingRect.Left);
                var top = line.Words.Min(word => word.BoundingRect.Top);
                var right = line.Words.Max(word => word.BoundingRect.Right);
                var bottom = line.Words.Max(word => word.BoundingRect.Bottom);
                lines.Add(new OcrTextLine
                {
                    Text = line.Text,
                    BoundingBox = new Windows.Foundation.Rect(
                        left,
                        top,
                        Math.Max(1, right - left),
                        Math.Max(1, bottom - top)
                    )
                });
            }

            if (lines.Count == 0)
            {
                Toast("No text found");
                IsOcrMode = false;
                return;
            }

            OcrOverlayLines = lines;
            IsOcrMode = true;
            Toast($"{lines.Count} text region{(lines.Count == 1 ? "" : "s")} found");
        }
        catch (OperationCanceledException)
        {
            IsOcrMode = false;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "OCR extraction failed: {Message}", ex.Message);
            Toast("Text extraction failed");
            IsOcrMode = false;
        }
        finally
        {
            if (ReferenceEquals(_ocrCts, cts))
            {
                _ocrCts = null;
            }

            IsOcrBusy = false;
            cts.Dispose();
        }
    }

    private void ClearOcrOverlay(bool cancelExtraction)
    {
        if (cancelExtraction)
        {
            _ocrGeneration++;
            _ocrCts?.Cancel();
            _ocrCts = null;
        }

        OcrOverlayLines = null;
        IsOcrMode = false;
        IsOcrBusy = false;
    }

    // Item 61: silent reload used by the external-edit debounce so the position/rotation state
    // is preserved but no "Reloaded" toast is emitted. The caller decides whether to show a
    // success message based on the returned decode result.
    private bool ReloadCurrentSilent()
    {
        return ReloadCurrentPreservingViewState(resetPreload: true);
    }

    private bool ReloadCurrentPreservingViewState(bool resetPreload)
    {
        var savedRotation = Rotation;
        var savedFlipH = FlipHorizontal;
        var savedFlipV = FlipVertical;
        if (resetPreload)
            _preload.Reset();
        var loaded = LoadCurrent();
        Rotation = savedRotation;
        FlipHorizontal = savedFlipH;
        FlipVertical = savedFlipV;
        return loaded;
    }

    // Item 61: arm a FileSystemWatcher on the specific file so external edits (Photoshop,
    // Paint.NET, etc.) trigger an automatic reload after an 800 ms quiet period.
    private void ArmFileWatcher(string path)
    {
        DisarmFileWatcher();

        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

        try
        {
            _externalEditWatcher = new System.IO.FileSystemWatcher(dir, Path.GetFileName(path))
            {
                NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            _externalEditWatcher.Changed += OnExternalEdit;
        }
        catch
        {
            // FSW can fail on network drives, locked volumes, etc. — degrade silently.
            _externalEditWatcher = null;
        }
    }

    private void DisarmFileWatcher()
    {
        if (_externalEditWatcher is null) return;
        _externalEditWatcher.EnableRaisingEvents = false;
        _externalEditWatcher.Changed -= OnExternalEdit;
        _externalEditWatcher.Dispose();
        _externalEditWatcher = null;
    }

    private void OnExternalEdit(object sender, System.IO.FileSystemEventArgs e)
    {
        // Marshal to the UI thread; reset the debounce on every write event so rapid saves
        // (incremental writes from editors) coalesce into a single reload.
        _uiDispatcher.BeginInvoke(() =>
        {
            if (_isDisposed) return;
            _externalEditDebounce.Stop();
            _externalEditDebounce.Start();
        });
    }

    private void RefreshFolder()
    {
        _nav.Refresh();
        RefreshFromNav();
        Toast("Folder refreshed");
    }

    private bool RefreshFromNav() => LoadCurrent();

    private void ClearCurrentState()
    {
        _renameTimer.Stop();
        _externalEditDebounce.Stop();
        DisarmFileWatcher();
        CurrentImage = null;
        CurrentAnimation = null;
        CurrentPath = null;
        PixelWidth = PixelHeight = 0;
        _fileSize = 0;
        Rotation = 0;
        FlipHorizontal = false;
        FlipVertical = false;
        ClearLoadError();
        DecoderUsed = null;
        ResetPageState();
        ClearPhotoMetadata();
        _folderPreview.Clear();
        RenameStatus = RenameStatusKind.Idle;
        SyncRenameEditorFromDisk();
        Raise(nameof(FileSizeText));
        Raise(nameof(DimensionsText));
        CommandManager.InvalidateRequerySuggested();
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double v = bytes;
        int i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return $"{v:0.##} {units[i]}";
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        FlushPendingRename();
        _isDisposed = true;

        _renameTimer.Stop();
        _toastTimer.Stop();
        _hintTimer.Stop();
        _externalEditDebounce.Stop();
        DisarmFileWatcher();

        _nav.ListChanged -= OnDirectoryListChanged;
        ClearOcrOverlay(cancelExtraction: true);
        _metadataGeneration++;
        _folderPreview.Dispose();
        _preload.Dispose();
        _nav.Dispose();

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Represents one line of OCR text with an image-pixel bounding box for overlay rendering.
/// </summary>
public class OcrTextLine : ObservableObject
{
    public string Text { get; set; } = string.Empty;
    public Windows.Foundation.Rect BoundingBox { get; set; }
}
