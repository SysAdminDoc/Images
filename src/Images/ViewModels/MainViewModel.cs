using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Images.Services;
using Microsoft.Extensions.Logging;

namespace Images.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private static readonly ILogger _log = Log.For<MainViewModel>();
    private readonly DirectoryNavigator _nav;
    private readonly RenameService _rename = new();
    private readonly PreloadService _preload = new();
    private readonly Dispatcher _uiDispatcher = Dispatcher.CurrentDispatcher;
    private readonly SettingsService _settings;
    private readonly ClipboardImportService _clipboardImport;
    private readonly FolderPreviewController _folderPreview;
    private readonly PhotoMetadataController _photoMetadata;
    private readonly OcrWorkflowController _ocrWorkflow;
    private readonly ExternalEditReloadController _externalEditReload;
    private readonly UpdateCheckController _updateCheck;
    private readonly RecycleBinDeleteService _recycleBinDelete;
    private readonly DispatcherTimer _renameTimer;
    private readonly DispatcherTimer _toastTimer;

    private bool _suppressStemChange;
    private string _committedStemOnDisk = string.Empty;
    private bool _isDisposed;
    private bool _isFilmstripVisible;
    private bool _isMetadataHudVisible;
    private readonly DispatcherTimer _hintTimer;

    public MainViewModel()
        : this(SettingsService.Instance)
    {
    }

    public MainViewModel(
        SettingsService settings,
        ClipboardImportService? clipboardImport = null,
        DirectoryNavigator? navigator = null,
        RecycleBinDeleteService? recycleBinDelete = null)
        : this(
            settings,
            clipboardImport,
            navigator,
            recycleBinDelete,
            folderPreview: null,
            photoMetadata: null,
            ocrWorkflow: null,
            externalEditReload: null,
            updateCheck: null)
    {
    }

    internal MainViewModel(
        SettingsService settings,
        ClipboardImportService? clipboardImport,
        DirectoryNavigator? navigator,
        RecycleBinDeleteService? recycleBinDelete,
        FolderPreviewController? folderPreview,
        PhotoMetadataController? photoMetadata,
        OcrWorkflowController? ocrWorkflow,
        ExternalEditReloadController? externalEditReload,
        UpdateCheckController? updateCheck)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _clipboardImport = clipboardImport ?? new ClipboardImportService();
        _nav = navigator ?? new DirectoryNavigator();
        _recycleBinDelete = recycleBinDelete ?? new RecycleBinDeleteService(_settings);
        _folderPreview = folderPreview ?? new FolderPreviewController(_uiDispatcher, () => _isDisposed);
        _folderPreview.StateChanged += (_, _) => RaiseFolderPreviewState();
        _photoMetadata = photoMetadata ?? new PhotoMetadataController(_uiDispatcher, () => _isDisposed, () => CurrentPath);
        _photoMetadata.StateChanged += (_, _) => RaisePhotoMetadataState();
        _ocrWorkflow = ocrWorkflow ?? new OcrWorkflowController(
            () => CurrentPath,
            () => HasImage,
            Toast,
            logError: ex => _log.LogError(ex, "OCR extraction failed: {Message}", ex.Message));
        _ocrWorkflow.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
                Raise(e.PropertyName);
        };
        _externalEditReload = externalEditReload ?? new ExternalEditReloadController(
            _uiDispatcher,
            () => _isDisposed,
            ReloadCurrentSilent,
            Toast);
        _updateCheck = updateCheck ?? new UpdateCheckController(
            Toast,
            openTarget: ShellIntegration.OpenShellTarget,
            invalidateCommands: CommandManager.InvalidateRequerySuggested);
        _updateCheck.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
                Raise(e.PropertyName);
            if (e.PropertyName is nameof(UpdateCheckController.HasUpdateCheckIssue)
                or nameof(UpdateCheckController.UpdateCheckIssueTitle)
                or nameof(UpdateCheckController.UpdateCheckIssueDetail))
            {
                SyncUpdateCheckSecondaryStatus();
            }
        };

        _renameTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _renameTimer.Tick += (_, _) => { _renameTimer.Stop(); FlushPendingRename(); };

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); ToastMessage = null; };

        _hintTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2400) };
        _hintTimer.Tick += (_, _) => { _hintTimer.Stop(); ShowGestureHint = false; };

        _isFilmstripVisible = _settings.GetBool(Keys.FilmstripVisible, true);
        _isMetadataHudVisible = _settings.GetBool(Keys.MetadataHudVisible, false);
        _archiveRightToLeft = _settings.GetBool(Keys.ArchiveRightToLeft, false);
        _archiveOldScanFilterEnabled = _settings.GetBool(Keys.ArchiveOldScanFilter, false);
        _archiveSpreadModeEnabled = _settings.GetBool(Keys.ArchiveSpreadMode, false);

        OpenCommand = new RelayCommand(async () => await OpenFileDialogAsync(), () => !IsOperationBusy);
        NextCommand = new RelayCommand(Next, () => CanUseImageCommands);
        PrevCommand = new RelayCommand(Prev, () => CanUseImageCommands);
        FirstCommand = new RelayCommand(First, () => CanUseImageCommands);
        LastCommand = new RelayCommand(Last, () => CanUseImageCommands);
        NextPageCommand = new RelayCommand(async () => await NextPageAsync(), () => HasNextPage && !IsOperationBusy);
        PrevPageCommand = new RelayCommand(async () => await PrevPageAsync(), () => HasPreviousPage && !IsOperationBusy);
        FirstPageCommand = new RelayCommand(async () => await FirstPageAsync(), () => HasPreviousPage && !IsOperationBusy);
        LastPageCommand = new RelayCommand(async () => await LastPageAsync(), () => HasNextPage && !IsOperationBusy);
        LeftBookPageTurnCommand = new RelayCommand(async () => await TurnLeftBookPageAsync(), () => CanTurnLeftBookPage);
        RightBookPageTurnCommand = new RelayCommand(async () => await TurnRightBookPageAsync(), () => CanTurnRightBookPage);
        DeleteCommand = new RelayCommand(DeleteCurrent, () => CanUseImageCommands);
        RotateCwCommand = new RelayCommand(() => Rotate(90), () => CanUseDisplayImageCommands);
        RotateCcwCommand = new RelayCommand(() => Rotate(-90), () => CanUseDisplayImageCommands);
        Rotate180Command = new RelayCommand(() => Rotate(180), () => CanUseDisplayImageCommands);
        ToggleInspectorCommand = new RelayCommand(() => IsInspectorMode = !IsInspectorMode, () => CanUseInspector);
        CopyInspectorHexCommand = new RelayCommand(() => CopyInspectorValue(s => s.Hex, "HEX"), () => HasInspectorSample);
        CopyInspectorRgbCommand = new RelayCommand(() => CopyInspectorValue(s => s.Rgb, "RGB"), () => HasInspectorSample);
        CopyInspectorHsvCommand = new RelayCommand(() => CopyInspectorValue(s => s.Hsv, "HSV"), () => HasInspectorSample);
        CopyInspectorSummaryCommand = new RelayCommand(() => CopyInspectorValue(s => s.Summary, "pixel sample"), () => HasInspectorSample);
        ClearInspectorSelectionCommand = new RelayCommand(ClearInspectorSelection, () => HasInspectorSelection);
        FlipHorizontalCommand = new RelayCommand(() => { FlipHorizontal = !FlipHorizontal; }, () => CanUseDisplayImageCommands);
        FlipVerticalCommand = new RelayCommand(() => { FlipVertical = !FlipVertical; }, () => CanUseDisplayImageCommands);
        RevealCommand = new RelayCommand(RevealInExplorer, () => HasImage);
        CopyPathCommand = new RelayCommand(CopyPath, () => HasImage);
        SetAsWallpaperCommand = new RelayCommand(SetAsWallpaper, () => CanUseImageCommands);
        ReloadCommand = new RelayCommand(async () => await ReloadCurrentAsync(), () => CanUseImageCommands);
        PrintCommand = new RelayCommand(PrintCurrent, () => CanUseDisplayImageCommands);
        SaveAsCopyCommand = new RelayCommand(async () => await SaveAsCopyAsync(), () => CanUseDisplayImageCommands);
        CheckForUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync(userInitiated: true), () => !IsCheckingForUpdates);
        OpenLatestUpdateCommand = new RelayCommand(_updateCheck.OpenLatestUpdate, () => HasUpdateAvailable && !IsCheckingForUpdates);
        RefreshCommand = new RelayCommand(RefreshFolder, () => CanRefreshFolder);
        CommitRenameCommand = new RelayCommand(() => { _renameTimer.Stop(); FlushPendingRename(); });
        CancelRenameCommand = new RelayCommand(CancelRenameEdit);
        UnlockExtensionCommand = new RelayCommand(() => IsExtensionUnlocked = !IsExtensionUnlocked);
        UndoRenameCommand = new RelayCommand(p => UndoOne(p as RenameService.UndoEntry), p => p is RenameService.UndoEntry);
        AboutCommand = new RelayCommand(ShowAboutWindow);
        OpenReferenceBoardCommand = new RelayCommand(OpenReferenceBoard);
        OpenRecentFolderCommand = new RelayCommand(p => OpenRecentFolder(p as string), p => p is string);
        OpenRecentArchiveCommand = new RelayCommand(
            async p => await OpenRecentArchiveAsync(p as ArchiveReadPositionService.ArchiveReadHistoryItem),
            p => p is ArchiveReadPositionService.ArchiveReadHistoryItem && !IsOperationBusy);
        OpenPreviewItemCommand = new RelayCommand(p => OpenPreviewItem(p as FolderPreviewItem), p => p is FolderPreviewItem);
        RevealPreviewItemCommand = new RelayCommand(p => RevealPreviewItem(p as FolderPreviewItem), p => p is FolderPreviewItem);
        CopyPreviewPathCommand = new RelayCommand(p => CopyPreviewPath(p as FolderPreviewItem), p => p is FolderPreviewItem);
        EnsurePreviewThumbnailCommand = new RelayCommand(p => EnsurePreviewThumbnail(p as FolderPreviewItem), p => p is FolderPreviewItem);
        SetFolderSortCommand = new RelayCommand(SetFolderSort, p => DirectorySortModeInfo.TryParseCommandParameter(p, out _));
        ToggleGalleryCommand = new RelayCommand(ToggleGallery, () => CanToggleGallery);
        CloseGalleryCommand = new RelayCommand(() => IsGalleryOpen = false, () => IsGalleryOpen);
        OpenSelectedGalleryItemCommand = new RelayCommand(OpenSelectedGalleryItem, () => IsGalleryOpen && SelectedGalleryItem is not null);
        ToggleFilmstripCommand = new RelayCommand(ToggleFilmstrip, () => CanToggleFilmstrip);
        ToggleMetadataHudCommand = new RelayCommand(ToggleMetadataHud, () => CanToggleMetadataHud);
        PasteFromClipboardCommand = new RelayCommand(PasteFromClipboard, () => !IsOperationBusy);
        OpenInDefaultAppCommand = new RelayCommand(OpenInDefaultApp, () => HasImage);
        StripLocationCommand = new RelayCommand(async () => await StripLocationAsync(), () => CanUseImageCommands);
        SettingsCommand = new RelayCommand(ShowSettingsWindow);
        ExtractTextCommand = new RelayCommand(async () => await _ocrWorkflow.ToggleAsync(), () => HasImage && !IsOperationBusy);

        // V20-02 UI consumer: seed RecentFolders from SettingsService at startup so the side
        // panel renders prior-session folders before the user opens anything.
        RefreshRecentFolders();
        RefreshArchiveReadHistory();

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
                Raise(nameof(CanUseInspector));
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

    private int _pageSpan = 1;
    public int PageSpan
    {
        get => _pageSpan;
        private set
        {
            if (Set(ref _pageSpan, Math.Max(1, value)))
            {
                RaisePageState();
            }
        }
    }

    public bool HasMultiplePages => PageCount > 1;
    public bool HasPreviousPage => HasMultiplePages && PageIndex > 0;
    public bool HasNextPage => HasMultiplePages && PageIndex + PageSpan < PageCount;
    public string PagePositionText
    {
        get
        {
            if (!HasMultiplePages) return "";
            if (PageSpan <= 1) return $"{PageLabel} {PageIndex + 1} / {PageCount}";
            var end = Math.Min(PageIndex + PageSpan, PageCount);
            return $"{PageLabel} {PageIndex + 1}-{end} / {PageCount}";
        }
    }
    public bool IsArchiveBook => CurrentPath is not null && SupportedImageFormats.IsArchive(CurrentPath) && HasMultiplePages;
    public bool CanTurnLeftBookPage => IsArchiveBook && !IsOperationBusy && (ArchiveRightToLeft ? HasNextPage : HasPreviousPage);
    public bool CanTurnRightBookPage => IsArchiveBook && !IsOperationBusy && (ArchiveRightToLeft ? HasPreviousPage : HasNextPage);
    public string LeftBookPageTurnTooltip => ArchiveRightToLeft ? "Next book page" : "Previous book page";
    public string RightBookPageTurnTooltip => ArchiveRightToLeft ? "Previous book page" : "Next book page";
    public string ArchivePageTurnModeText => ArchiveRightToLeft ? "Right-to-left page turns" : "Left-to-right page turns";
    public string ArchivePageTurnModeHint => ArchiveRightToLeft
        ? "For manga-style books, the left edge and Left Arrow advance; the right edge goes back."
        : "For western books, the right edge and Right Arrow advance; the left edge goes back.";
    public string ArchiveOldScanFilterText => ArchiveOldScanFilterEnabled ? "Clean old scans on" : "Clean old scans";
    public string ArchiveOldScanFilterHint => "Preview-only: converts archive pages to high-contrast grayscale. The archive file is not changed.";
    public string ArchiveSpreadModeText => ArchiveSpreadModeEnabled ? "Two-page spreads on" : "Two-page spreads";
    public string ArchiveSpreadModeHint => ArchiveRightToLeft
        ? "Pairs pages side by side for reading, with the next page on the left in right-to-left mode."
        : "Pairs pages side by side for reading, keeping explicit covers as single pages.";
    public string CurrentArchiveProgressText => IsArchiveBook
        ? $"Reading {Path.GetFileName(CurrentPath)} · {PagePositionText}"
        : "";
    public int PageNumber
    {
        get => PageIndex + 1;
        set
        {
            if (!HasMultiplePages || IsOperationBusy) return;
            var target = Math.Clamp(value, 1, PageCount) - 1;
            if (target == PageIndex) return;
            _ = GoToPageAsync(target, "Loading page");
        }
    }

    private void RaisePageState()
    {
        Raise(nameof(HasMultiplePages));
        Raise(nameof(HasPreviousPage));
        Raise(nameof(HasNextPage));
            Raise(nameof(PagePositionText));
            Raise(nameof(PageNumber));
            Raise(nameof(PageSpan));
            Raise(nameof(IsArchiveBook));
        Raise(nameof(CanTurnLeftBookPage));
        Raise(nameof(CanTurnRightBookPage));
        Raise(nameof(CurrentArchiveProgressText));
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
                Raise(nameof(CanRefreshFolder));
                Raise(nameof(WindowTitle));
                Raise(nameof(IsArchiveBook));
                Raise(nameof(CanTurnLeftBookPage));
                Raise(nameof(CanTurnRightBookPage));
                Raise(nameof(CurrentArchiveProgressText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool HasImage => !string.IsNullOrEmpty(CurrentPath) && File.Exists(CurrentPath);
    public bool HasDisplayImage => CurrentImage is not null;
    public bool CanUseInspector => HasDisplayImage && !IsOperationBusy;
    public bool IsViewerEmpty => CurrentPath is null;
    public bool CanRefreshFolder => (CurrentPath is not null || _nav.Count > 0) && !IsOperationBusy;

    private bool CanUseImageCommands => HasImage && !IsOperationBusy;
    private bool CanUseDisplayImageCommands => HasDisplayImage && !IsOperationBusy;

    private string _operationStatusTitle = "";
    public string OperationStatusTitle
    {
        get => _operationStatusTitle;
        private set
        {
            if (Set(ref _operationStatusTitle, value))
                Raise(nameof(ShowOperationStatus));
        }
    }

    private string _operationStatusDetail = "";
    public string OperationStatusDetail
    {
        get => _operationStatusDetail;
        private set
        {
            if (Set(ref _operationStatusDetail, value))
                Raise(nameof(ShowOperationStatus));
        }
    }

    private bool _isOperationBusy;
    public bool IsOperationBusy
    {
        get => _isOperationBusy;
        private set
        {
            if (Set(ref _isOperationBusy, value))
            {
                Raise(nameof(ShowOperationStatus));
                Raise(nameof(CanRefreshFolder));
                Raise(nameof(CanUseInspector));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool ShowOperationStatus => IsOperationBusy
        && (!string.IsNullOrWhiteSpace(OperationStatusTitle)
            || !string.IsNullOrWhiteSpace(OperationStatusDetail));

    public enum SecondaryStatusToneKind { Info, Success, Warning, Error }
    private enum SecondaryStatusSource { None, UserFlow, UpdateCheck, FolderPreview }

    private string _secondaryStatusTitle = "";
    private SecondaryStatusSource _secondaryStatusSource;
    public string SecondaryStatusTitle
    {
        get => _secondaryStatusTitle;
        private set
        {
            if (Set(ref _secondaryStatusTitle, value))
                Raise(nameof(HasSecondaryStatus));
        }
    }

    private string _secondaryStatusDetail = "";
    public string SecondaryStatusDetail
    {
        get => _secondaryStatusDetail;
        private set
        {
            if (Set(ref _secondaryStatusDetail, value))
                Raise(nameof(HasSecondaryStatus));
        }
    }

    private string _secondaryStatusIcon = "\uE946";
    public string SecondaryStatusIcon
    {
        get => _secondaryStatusIcon;
        private set => Set(ref _secondaryStatusIcon, value);
    }

    private SecondaryStatusToneKind _secondaryStatusTone = SecondaryStatusToneKind.Info;
    public SecondaryStatusToneKind SecondaryStatusTone
    {
        get => _secondaryStatusTone;
        private set => Set(ref _secondaryStatusTone, value);
    }

    public bool HasSecondaryStatus
        => !string.IsNullOrWhiteSpace(SecondaryStatusTitle)
            || !string.IsNullOrWhiteSpace(SecondaryStatusDetail);

    public string FirstRunPrivacyText => _settings.GetBool(Keys.UpdateCheckEnabled, false)
        ? "Automatic update checks are enabled. Image files stay local; release checks only contact GitHub."
        : "No telemetry and no image uploads. Automatic update checks are off until you enable them.";

    public string FirstRunFormatStatusText => CodecCapabilityService.BuildOverviewText();
    public string FirstRunOcrStatusText => OcrCapabilityService.BuildOverviewText();
    public string FirstRunDocumentStatusText => CodecCapabilityService.BuildDocumentStatusText();
    public string FirstRunRecoveryText => "Settings manages privacy and viewer defaults. Diagnostics shows codec, OCR, log, and storage status.";

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

    private bool _isInspectorMode;
    public bool IsInspectorMode
    {
        get => _isInspectorMode;
        set
        {
            if (!Set(ref _isInspectorMode, value))
                return;

            if (!value)
                ClearInspectorState();
            else
                InspectorStatusText = "Move over the image to sample pixels. Click to hold a sample; Ctrl+click copies it. Shift-drag measures.";

            Raise(nameof(InspectorModeText));
            Raise(nameof(InspectorModeHelpText));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string InspectorModeText => IsInspectorMode ? "Inspector on" : "Inspector off";
    public string InspectorModeHelpText => IsInspectorMode
        ? "Sampling is active. Shift-drag measures a rectangle."
        : "Turn on Inspector to read pixel color and coordinates.";

    private bool _inspectorNearestNeighborPreview;
    public bool InspectorNearestNeighborPreview
    {
        get => _inspectorNearestNeighborPreview;
        set
        {
            if (Set(ref _inspectorNearestNeighborPreview, value))
                Toast(value ? "Nearest-neighbor preview on" : "High-quality preview on");
        }
    }

    private PixelSample? _inspectorSample;
    private PixelSelection? _inspectorSelection;

    private string _inspectorStatusText = "Turn on Inspector to sample pixel color, coordinates, and dimensions.";
    public string InspectorStatusText
    {
        get => _inspectorStatusText;
        private set => Set(ref _inspectorStatusText, value);
    }

    public bool HasInspectorSample => _inspectorSample is not null;
    public bool HasInspectorSelection => _inspectorSelection is not null;
    public string InspectorCoordinateText => _inspectorSample?.CoordinateText ?? "No pixel selected";
    public string InspectorHexText => _inspectorSample?.Hex ?? "#------";
    public string InspectorRgbText => _inspectorSample?.Rgb ?? "RGB --, --, --";
    public string InspectorHsvText => _inspectorSample?.Hsv ?? "HSV --, --%, --%";
    public string InspectorAlphaText => _inspectorSample?.Alpha ?? "A --";
    public string InspectorSelectionText => _inspectorSelection.HasValue
        ? _inspectorSelection.Value.DisplayText
        : "Shift-drag to measure";

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
            if (!RenameService.IsSupportedTargetExtension(Extension, CurrentPath))
                return "Choose a supported Images extension";

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

    public ObservableCollection<ArchiveReadPositionService.ArchiveReadHistoryItem> RecentArchiveBooks { get; } = new();

    private bool _archiveRightToLeft;
    public bool ArchiveRightToLeft
    {
        get => _archiveRightToLeft;
        set
        {
            if (!Set(ref _archiveRightToLeft, value)) return;

            _settings.SetBool(Keys.ArchiveRightToLeft, value);
            Raise(nameof(CanTurnLeftBookPage));
            Raise(nameof(CanTurnRightBookPage));
            Raise(nameof(LeftBookPageTurnTooltip));
            Raise(nameof(RightBookPageTurnTooltip));
            Raise(nameof(ArchivePageTurnModeText));
            Raise(nameof(ArchivePageTurnModeHint));
            Raise(nameof(ArchiveSpreadModeHint));
            CommandManager.InvalidateRequerySuggested();

            if (ArchiveSpreadModeEnabled && IsArchiveBook && HasDisplayImage && !IsOperationBusy)
                ReloadCurrentPreservingViewState(resetPreload: false);
        }
    }

    private bool _archiveOldScanFilterEnabled;
    public bool ArchiveOldScanFilterEnabled
    {
        get => _archiveOldScanFilterEnabled;
        set
        {
            if (!Set(ref _archiveOldScanFilterEnabled, value)) return;

            _settings.SetBool(Keys.ArchiveOldScanFilter, value);
            Raise(nameof(ArchiveOldScanFilterText));
            Raise(nameof(ArchiveOldScanFilterHint));

            if (IsArchiveBook && HasDisplayImage && !IsOperationBusy)
            {
                ReloadCurrentPreservingViewState(resetPreload: false);
                Toast(value ? "Clean scan preview on" : "Clean scan preview off");
            }
        }
    }

    private bool _archiveSpreadModeEnabled;
    public bool ArchiveSpreadModeEnabled
    {
        get => _archiveSpreadModeEnabled;
        set
        {
            if (!Set(ref _archiveSpreadModeEnabled, value)) return;

            _settings.SetBool(Keys.ArchiveSpreadMode, value);
            Raise(nameof(ArchiveSpreadModeText));
            Raise(nameof(ArchiveSpreadModeHint));

            if (IsArchiveBook && HasDisplayImage && !IsOperationBusy)
            {
                ReloadCurrentPreservingViewState(resetPreload: false);
                Toast(value ? "Two-page spreads on" : "Two-page spreads off");
            }
        }
    }

    private void RefreshArchiveReadHistory()
    {
        var fresh = ArchiveReadPositionService.GetRecentArchives(_settings);
        RecentArchiveBooks.Clear();
        foreach (var item in fresh) RecentArchiveBooks.Add(item);
    }

    // -------------------- Recent renames --------------------

    public ObservableCollection<RenameService.UndoEntry> RecentRenames { get; } = new();

    // -------------------- Photo metadata --------------------

    public ObservableCollection<MetadataFact> PhotoMetadataRows => _photoMetadata.Rows;

    public string MetadataStatusText => _photoMetadata.StatusText;

    public bool IsMetadataLoading => _photoMetadata.IsLoading;

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
        _photoMetadata.Refresh(path);
    }

    private void ClearPhotoMetadata()
    {
        _photoMetadata.Clear();
    }

    private void RaisePhotoMetadataState()
    {
        Raise(nameof(IsMetadataLoading));
        Raise(nameof(MetadataStatusText));
    }

    // -------------------- Folder preview strip --------------------

    public ObservableCollection<FolderPreviewItem> FolderPreviewItems => _folderPreview.Items;
    public ObservableCollection<FolderPreviewItem> GalleryItems { get; } = [];

    private bool _isGalleryOpen;
    public bool IsGalleryOpen
    {
        get => _isGalleryOpen;
        set
        {
            if (!Set(ref _isGalleryOpen, value)) return;

            if (value)
                SelectedGalleryItem = CurrentFolderPreviewItem ?? FolderPreviewItems.FirstOrDefault();

            Raise(nameof(CanToggleGallery));
            Raise(nameof(ShowGallery));
            Raise(nameof(ShowGalleryFilterEmpty));
            Raise(nameof(GalleryStatusText));
            Raise(nameof(GalleryToggleTooltip));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private FolderPreviewItem? _selectedGalleryItem;
    public FolderPreviewItem? SelectedGalleryItem
    {
        get => _selectedGalleryItem;
        set
        {
            if (Set(ref _selectedGalleryItem, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool CanToggleGallery => IsGalleryOpen || (HasImage && !IsPeekMode);
    public bool ShowGallery => IsGalleryOpen && HasImage && FolderPreviewItems.Count > 0 && !IsPeekMode;
    public string GalleryToggleTooltip => IsGalleryOpen ? "Close gallery (G)" : "Open gallery (G)";
    public string GalleryStatusText
    {
        get
        {
            var total = FolderPreviewItems.Count;
            if (HasGalleryFilter)
                return $"{GalleryItems.Count} of {total} {(total == 1 ? "item" : "items")} · {FolderSortLabel}";

            return $"{total} {(total == 1 ? "item" : "items")} · {FolderSortLabel}";
        }
    }
    public bool HasGalleryFilter => !string.IsNullOrWhiteSpace(GalleryFilterText);
    public bool ShowGalleryFilterEmpty => ShowGallery && HasGalleryFilter && GalleryItems.Count == 0;

    private string _galleryFilterText = "";
    public string GalleryFilterText
    {
        get => _galleryFilterText;
        set
        {
            var normalized = value ?? "";
            if (!Set(ref _galleryFilterText, normalized)) return;

            RefreshGalleryItems();
            Raise(nameof(HasGalleryFilter));
            Raise(nameof(ShowGalleryFilterEmpty));
            Raise(nameof(GalleryStatusText));
        }
    }

    private FolderPreviewItem? CurrentFolderPreviewItem
        => FolderPreviewItems.FirstOrDefault(item => item.IsCurrent);

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

    public int ThumbnailFailureCount => _folderPreview.ThumbnailFailureCount;
    public bool HasThumbnailFailures => _folderPreview.HasThumbnailFailures;
    public string ThumbnailFailureStatusText => _folderPreview.ThumbnailFailureStatusText;

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
        var closeGallery = IsGalleryOpen;
        OpenFile(item.Path);
        if (closeGallery)
            IsGalleryOpen = false;
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

    private void ToggleGallery()
    {
        if (IsGalleryOpen)
        {
            IsGalleryOpen = false;
            Toast("Gallery closed");
            return;
        }

        if (FolderPreviewItems.Count == 0)
        {
            Toast("Open a folder with images to use Gallery");
            return;
        }

        IsGalleryOpen = true;
        Toast("Gallery open");
    }

    private void OpenSelectedGalleryItem()
    {
        OpenPreviewItem(SelectedGalleryItem);
    }

    private void RaiseFolderPreviewState()
    {
        RefreshGalleryItems();
        Raise(nameof(CanToggleFilmstrip));
        Raise(nameof(ShowFilmstrip));
        Raise(nameof(ShowSideFolderPreview));
        Raise(nameof(CanToggleGallery));
        Raise(nameof(ShowGallery));
        Raise(nameof(HasGalleryFilter));
        Raise(nameof(ShowGalleryFilterEmpty));
        Raise(nameof(GalleryStatusText));
        Raise(nameof(GalleryToggleTooltip));
        Raise(nameof(FilmstripToggleTooltip));
        Raise(nameof(CurrentSortMode));
        Raise(nameof(FolderSortLabel));
        Raise(nameof(FolderSortTooltip));
        Raise(nameof(CanRefreshFolder));
        Raise(nameof(ThumbnailFailureCount));
        Raise(nameof(HasThumbnailFailures));
        Raise(nameof(ThumbnailFailureStatusText));
        SyncFolderPreviewSecondaryStatus();
        CommandManager.InvalidateRequerySuggested();
    }

    private void RefreshGalleryItems()
    {
        var previousSelectedPath = SelectedGalleryItem?.Path;
        var filter = GalleryFilterText.Trim();
        var visible = string.IsNullOrEmpty(filter)
            ? FolderPreviewItems
            : FolderPreviewItems.Where(item =>
                item.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.Path.Contains(filter, StringComparison.OrdinalIgnoreCase));

        GalleryItems.Clear();
        foreach (var item in visible)
            GalleryItems.Add(item);

        var selected =
            GalleryItems.FirstOrDefault(item => string.Equals(item.Path, previousSelectedPath, StringComparison.OrdinalIgnoreCase)) ??
            GalleryItems.FirstOrDefault(item => item.IsCurrent) ??
            GalleryItems.FirstOrDefault();

        if (!ReferenceEquals(SelectedGalleryItem, selected))
            SelectedGalleryItem = selected;
    }

    private void SyncFolderPreviewSecondaryStatus()
    {
        if (_folderPreview.HasThumbnailFailures)
        {
            ShowSecondaryStatus(
                "Some thumbnails could not be shown",
                $"{_folderPreview.ThumbnailFailureStatusText} Refresh the folder or open Diagnostics if this repeats.",
                SecondaryStatusToneKind.Warning,
                "\uE783",
                SecondaryStatusSource.FolderPreview);
            return;
        }

        ClearSecondaryStatus(SecondaryStatusSource.FolderPreview);
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

    private void ShowSecondaryStatus(
        string title,
        string detail,
        SecondaryStatusToneKind tone = SecondaryStatusToneKind.Info,
        string icon = "\uE946",
        SecondaryStatusSource source = SecondaryStatusSource.UserFlow)
    {
        SecondaryStatusTitle = title;
        SecondaryStatusDetail = detail;
        SecondaryStatusTone = tone;
        SecondaryStatusIcon = icon;
        _secondaryStatusSource = source;
    }

    private void ClearSecondaryStatus(SecondaryStatusSource? source = null)
    {
        if (source is not null && _secondaryStatusSource != source.Value)
            return;

        SecondaryStatusTitle = "";
        SecondaryStatusDetail = "";
        SecondaryStatusTone = SecondaryStatusToneKind.Info;
        SecondaryStatusIcon = "\uE946";
        _secondaryStatusSource = SecondaryStatusSource.None;
    }

    private static string FormatExtensionForMessage(string extension)
        => string.IsNullOrWhiteSpace(extension) ? "this file" : extension;

    private static string DisplayFolderName(string folder)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(folder));
        return string.IsNullOrWhiteSpace(name) ? folder : name;
    }

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
    public ICommand LeftBookPageTurnCommand { get; }
    public ICommand RightBookPageTurnCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RotateCwCommand { get; }
    public ICommand RotateCcwCommand { get; }
    public ICommand Rotate180Command { get; }
    public ICommand ToggleInspectorCommand { get; }
    public ICommand CopyInspectorHexCommand { get; }
    public ICommand CopyInspectorRgbCommand { get; }
    public ICommand CopyInspectorHsvCommand { get; }
    public ICommand CopyInspectorSummaryCommand { get; }
    public ICommand ClearInspectorSelectionCommand { get; }
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
    public ICommand OpenReferenceBoardCommand { get; }
    public ICommand OpenRecentFolderCommand { get; }
    public ICommand OpenRecentArchiveCommand { get; }
    public ICommand OpenPreviewItemCommand { get; }
    public ICommand RevealPreviewItemCommand { get; }
    public ICommand CopyPreviewPathCommand { get; }
    public ICommand EnsurePreviewThumbnailCommand { get; }
    public ICommand SetFolderSortCommand { get; }
    public ICommand ToggleGalleryCommand { get; }
    public ICommand CloseGalleryCommand { get; }
    public ICommand OpenSelectedGalleryItemCommand { get; }
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
        get => _ocrWorkflow.IsOcrMode;
        set => _ocrWorkflow.IsOcrMode = value;
    }

    public bool IsOcrBusy => _ocrWorkflow.IsOcrBusy;

    public bool ShowOcrStatusPanel => _ocrWorkflow.ShowOcrStatusPanel;

    public string OcrModeTooltip => _ocrWorkflow.OcrModeTooltip;

    public string OcrStatusTitle => _ocrWorkflow.OcrStatusTitle;

    public string OcrStatusDetail => _ocrWorkflow.OcrStatusDetail;

    public string OcrRegionCountText => _ocrWorkflow.OcrRegionCountText;

    public ObservableCollection<OcrTextLine>? OcrOverlayLines
    {
        get => _ocrWorkflow.OcrOverlayLines;
        set => _ocrWorkflow.OcrOverlayLines = value;
    }

    // -------------------- Navigation --------------------

    public void OpenFile(string path)
    {
        FlushPendingRename();

        if (!SupportedImageFormats.IsSupported(path))
        {
            // V20-37 / item 86: human-readable suggestion when a recognized non-image type is
            // dropped (video, document, etc.) so the user understands why nothing happened.
            var ext = Path.GetExtension(path);
            var suggestion = SupportedImageFormats.SuggestionForUnsupported(ext);
            ShowSecondaryStatus(
                "File type not supported",
                suggestion is null
                    ? $"Images does not recognize {FormatExtensionForMessage(ext)} as a supported image or document preview format."
                    : suggestion,
                SecondaryStatusToneKind.Warning,
                "\uE783");
            Toast(suggestion is null
                ? $"Unsupported file type: {ext}"
                : $"Unsupported {ext}. {suggestion}");
            return;
        }

        var previousFolder = _nav.Folder;
        if (!_nav.Open(path))
        {
            ShowSecondaryStatus(
                File.Exists(path) ? "Could not open file" : "File no longer exists",
                File.Exists(path)
                    ? $"{Path.GetFileName(path)} could not be opened from this location."
                    : "The file may have been moved, renamed, deleted, or disconnected.",
                SecondaryStatusToneKind.Warning,
                "\uE783");
            Toast(File.Exists(path)
                ? $"Could not open {Path.GetFileName(path)}"
                : "File no longer exists");
            return;
        }

        if (!string.Equals(previousFolder, _nav.Folder, StringComparison.OrdinalIgnoreCase))
            _preload.Reset();

        ResetPageState();
        var resumedArchivePage = ArchiveReadPositionService.GetLastPageIndex(_settings, path);
        if (resumedArchivePage > 0)
            PageIndex = resumedArchivePage;
        ClearSecondaryStatus();
        var loaded = LoadCurrent();
        if (loaded && resumedArchivePage > 0 && PageIndex > 0 && HasMultiplePages)
            Toast($"Continued at {PageLabel.ToLowerInvariant()} {PageIndex + 1}");
        _externalEditReload.Arm(path);

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

    private async Task OpenFileWithOperationStatusAsync(string path, string title)
    {
        if (IsOperationBusy) return;

        BeginOperationStatus(title, BuildDecodeOperationDetail(path));
        try
        {
            await YieldForOperationStatusAsync();
            OpenFile(path);
        }
        finally
        {
            EndOperationStatus();
        }
    }

    private async Task LoadCurrentWithOperationStatusAsync(string title, string detail)
    {
        if (IsOperationBusy) return;

        BeginOperationStatus(title, detail);
        try
        {
            await YieldForOperationStatusAsync();
            LoadCurrent();
        }
        finally
        {
            EndOperationStatus();
        }
    }

    private static string BuildDecodeOperationDetail(string path)
    {
        var fileName = Path.GetFileName(path);
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pdf" or ".ps" or ".eps" or ".ai" => $"Rendering document preview for {fileName}.",
            ".tif" or ".tiff" => $"Loading multi-page image {fileName}.",
            ".zip" or ".cbz" => $"Opening archive book {fileName}.",
            _ => $"Decoding {fileName}."
        };
    }

    // V20-02 UI consumer: open the first supported image in <folder>. Click handler for the
    // side-panel "Recent folders" cards. EnumerateFiles avoids materializing a full list when
    // the folder is huge (medium-DAM scenario — we only need the first match).
    private void OpenRecentFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return;
        if (!Directory.Exists(folder))
        {
            ShowSecondaryStatus(
                "Recent folder removed",
                "It was removed from the list because the folder no longer exists.",
                SecondaryStatusToneKind.Warning,
                "\uE8B7");
            Toast("Folder no longer exists");
            RefreshRecentFolders(); // GetRecentFolders filters missing folders — re-pull.
            return;
        }

        try
        {
            var first = DirectoryNavigator.FirstSupportedImageInFolder(folder);
            if (first is null)
            {
                ShowSecondaryStatus(
                    "No supported images in this folder",
                    $"Images did not find supported formats in {DisplayFolderName(folder)}. Choose another folder or paste an image.",
                    SecondaryStatusToneKind.Info,
                    "\uE8B7");
                Toast($"No images in {Path.GetFileName(folder)}");
                return;
            }
            OpenFile(first);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            ShowSecondaryStatus(
                "Folder unreachable",
                "Check permissions or reconnect the drive, then try again.",
                SecondaryStatusToneKind.Warning,
                "\uE783");
            Toast("Folder unreachable");
        }
    }

    private async Task OpenRecentArchiveAsync(ArchiveReadPositionService.ArchiveReadHistoryItem? archive)
    {
        if (archive is null) return;

        if (!File.Exists(archive.Path))
        {
            ShowSecondaryStatus(
                "Archive no longer available",
                "It was removed from book history because the file no longer exists.",
                SecondaryStatusToneKind.Warning,
                "\uE783");
            Toast("Archive no longer exists");
            RefreshArchiveReadHistory();
            return;
        }

        await OpenFileWithOperationStatusAsync(archive.Path, "Opening archive book");
    }

    private void PasteFromClipboard()
    {
        var result = _clipboardImport.Import();
        if (result.Path is not null)
            OpenFile(result.Path);
        else
            ShowClipboardImportStatus(result);

        if (!string.IsNullOrWhiteSpace(result.Message))
            Toast(result.Message);
    }

    private void ShowClipboardImportStatus(ClipboardImportResult result)
    {
        switch (result.Status)
        {
            case ClipboardImportStatus.NoSupportedFile:
                ShowSecondaryStatus(
                    "Clipboard file not supported",
                    "The clipboard contains files, but none are formats Images can open.",
                    SecondaryStatusToneKind.Warning,
                    "\uE783");
                break;
            case ClipboardImportStatus.NothingImageLike:
                ShowSecondaryStatus(
                    "Clipboard has no image",
                    "Copy an image, screenshot, or supported image file, then paste again.",
                    SecondaryStatusToneKind.Info,
                    "\uE946");
                break;
            case ClipboardImportStatus.ImageUnavailable:
                ShowSecondaryStatus(
                    "Clipboard image could not be read",
                    "Try copying the image again from the source app.",
                    SecondaryStatusToneKind.Warning,
                    "\uE783");
                break;
            case ClipboardImportStatus.StorageUnavailable:
            case ClipboardImportStatus.SaveFailed:
                ShowSecondaryStatus(
                    "Clipboard paste could not be saved",
                    "Images could not create a temporary local copy. Open Diagnostics to inspect storage.",
                    SecondaryStatusToneKind.Error,
                    "\uE783");
                break;
            default:
                ClearSecondaryStatus();
                break;
        }
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

    private async Task OpenFileDialogAsync()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open image or preview",
            Filter = SupportedImageFormats.OpenDialogFilter,
            FilterIndex = 1
        };
        if (dlg.ShowDialog() == true)
            await OpenFileWithOperationStatusAsync(dlg.FileName, "Opening file");
    }

    private void Next() { FlushPendingRename(); if (_nav.MoveNext()) { ResetPageState(); LoadCurrent(); } }
    private void Prev() { FlushPendingRename(); if (_nav.MovePrevious()) { ResetPageState(); LoadCurrent(); } }
    private void First() { FlushPendingRename(); if (_nav.MoveFirst()) { ResetPageState(); LoadCurrent(); } }
    private void Last() { FlushPendingRename(); if (_nav.MoveLast()) { ResetPageState(); LoadCurrent(); } }

    private int PageStep => ArchiveSpreadModeEnabled && IsArchiveBook ? Math.Max(1, PageSpan) : 1;

    private async Task NextPageAsync()
    {
        await GoToPageAsync(PageIndex + PageStep, "Loading next page");
    }

    private async Task PrevPageAsync()
    {
        await GoToPageAsync(PageIndex - PageStep, "Loading previous page");
    }

    private async Task FirstPageAsync()
    {
        await GoToPageAsync(0, "Loading first page");
    }

    private async Task LastPageAsync()
    {
        await GoToPageAsync(PageCount - 1, "Loading last page");
    }

    private async Task TurnLeftBookPageAsync()
    {
        if (ArchiveRightToLeft)
            await NextPageAsync();
        else
            await PrevPageAsync();
    }

    private async Task TurnRightBookPageAsync()
    {
        if (ArchiveRightToLeft)
            await PrevPageAsync();
        else
            await NextPageAsync();
    }

    private async Task GoToPageAsync(int targetPageIndex, string title)
    {
        if (IsOperationBusy || !HasMultiplePages)
            return;

        targetPageIndex = Math.Clamp(targetPageIndex, 0, PageCount - 1);
        if (targetPageIndex == PageIndex)
            return;

        FlushPendingRename();
        PageIndex = targetPageIndex;
        await LoadCurrentWithOperationStatusAsync(title, $"{PageLabel} {PageIndex + 1} of {PageCount}.");
    }

    private bool LoadCurrent()
    {
        var path = _nav.CurrentPath;
        if (path is null)
        {
            ClearCurrentState();
            return false;
        }

        _ocrWorkflow.Clear(cancelExtraction: true);
        var loaded = false;

        try
        {
            // V20-03: try the preload ring first — a hit is instant, a miss falls through to
            // the direct load. Either way we re-enqueue the new neighbors.
            var isArchiveBookPage = SupportedImageFormats.IsArchive(path);
            var usePreload = PageIndex == 0 && !(isArchiveBookPage && ArchiveSpreadModeEnabled);
            var res = usePreload
                ? _preload.TryGet(path) ?? ImageLoader.Load(path, PageIndex, ArchiveSpreadModeEnabled, ArchiveRightToLeft)
                : ImageLoader.Load(path, PageIndex, ArchiveSpreadModeEnabled, ArchiveRightToLeft);
            var image = ApplyArchiveDisplayFilters(res.Image, isArchiveBookPage, ArchiveOldScanFilterEnabled);
            var animation = isArchiveBookPage && ArchiveOldScanFilterEnabled ? null : res.Animation;
            var decoderUsed = isArchiveBookPage && ArchiveOldScanFilterEnabled
                ? $"{res.DecoderUsed} + clean scan filter"
                : res.DecoderUsed;
            // Order matters: CurrentImage first so ZoomPanImage.OnSourceChanged runs and clears
            // any animation from the previous file; then CurrentAnimation, which either applies
            // new keyframes or stays null for a static image.
            CurrentImage = image;
            CurrentAnimation = animation;
            PixelWidth = res.PixelWidth;
            PixelHeight = res.PixelHeight;
            DecoderUsed = decoderUsed;
            ClearInspectorState();
            ApplyPageSequence(res.Pages);
            ArchiveReadPositionService.SaveLastPageIndex(_settings, path, PageIndex, PageCount);
            if (isArchiveBookPage)
                RefreshArchiveReadHistory();
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

            LaunchTiming.LogFirstImage(_log, path, IsPeekMode);
        }
        catch (Exception ex)
        {
            CurrentImage = null;
            CurrentAnimation = null;
            PixelWidth = PixelHeight = 0;
            DecoderUsed = "Unavailable";
            ClearInspectorState();
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
        PageSpan = pages.PageSpan;
        PageIndex = pages.PageIndex;
    }

    private void ResetPageState()
    {
        PageLabel = "Page";
        PageCount = 1;
        PageSpan = 1;
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
            Toast($"Rename failed: {FirstLine(ex.Message)}");
        }
    }

    private static ImageSource ApplyArchiveDisplayFilters(ImageSource image, bool isArchiveBookPage, bool oldScanFilterEnabled)
    {
        if (!isArchiveBookPage || !oldScanFilterEnabled)
            return image;

        return image is BitmapSource bitmap
            ? ScanFilterService.ApplyOldScanFilter(bitmap)
            : image;
    }

    private static string FirstLine(string message)
    {
        var line = message
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(line))
            return "Unexpected error";

        var parameterSuffix = line.IndexOf(" (Parameter '", StringComparison.Ordinal);
        return parameterSuffix > 0 ? line[..parameterSuffix] : line;
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
        if (CurrentPath is null) return;
        _renameTimer.Stop();

        var toDelete = CurrentPath;
        var result = _recycleBinDelete.Delete(toDelete, Application.Current?.MainWindow);
        switch (result.Status)
        {
            case RecycleBinDeleteStatus.Canceled:
                Toast("Delete canceled");
                return;
            case RecycleBinDeleteStatus.Missing:
                _nav.RemoveCurrent();
                Toast($"File no longer exists: {Path.GetFileName(toDelete)}");
                AdvanceAfterRemovedCurrent();
                return;
            case RecycleBinDeleteStatus.Failed:
                Toast($"Delete failed: {result.ErrorMessage}");
                return;
        }

        _nav.RemoveCurrent();
        Toast($"Sent to Recycle Bin: {result.FileName}");
        AdvanceAfterRemovedCurrent();
    }

    private void AdvanceAfterRemovedCurrent()
    {
        if (_nav.CurrentPath is null) { ClearCurrentState(); return; }
        ResetPageState();
        LoadCurrent();
    }

    private void Rotate(double delta)
    {
        Rotation = (Rotation + delta) % 360;
    }

    public void UpdateInspectorSample(PixelSample? sample)
    {
        _inspectorSample = sample;
        InspectorStatusText = sample is null
            ? "Pointer is outside the image."
            : sample.Summary;
        RaiseInspectorSampleState();
    }

    public void UpdateInspectorSelection(PixelSelection? selection)
    {
        _inspectorSelection = selection;
        if (selection.HasValue)
            InspectorStatusText = $"Measured {selection.Value.DisplayText}.";
        RaiseInspectorSelectionState();
    }

    public void ClearInspectorSelection()
    {
        _inspectorSelection = null;
        InspectorStatusText = HasInspectorSample
            ? _inspectorSample!.Summary
            : "Move over the image to sample pixels.";
        RaiseInspectorSelectionState();
    }

    private void ClearInspectorState()
    {
        _inspectorSample = null;
        _inspectorSelection = null;
        InspectorStatusText = IsInspectorMode
            ? "Move over the image to sample pixels. Click to hold a sample; Ctrl+click copies it. Shift-drag measures."
            : "Turn on Inspector to sample pixel color, coordinates, and dimensions.";
        RaiseInspectorSampleState();
        RaiseInspectorSelectionState();
    }

    private void CopyInspectorValue(Func<PixelSample, string> valueSelector, string label)
    {
        if (_inspectorSample is not { } sample)
            return;

        try
        {
            ClipboardService.SetText(valueSelector(sample));
            Toast($"Copied {label}");
        }
        catch (Exception ex)
        {
            Toast($"Copy failed: {ex.Message}");
        }
    }

    private void RaiseInspectorSampleState()
    {
        Raise(nameof(HasInspectorSample));
        Raise(nameof(InspectorCoordinateText));
        Raise(nameof(InspectorHexText));
        Raise(nameof(InspectorRgbText));
        Raise(nameof(InspectorHsvText));
        Raise(nameof(InspectorAlphaText));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RaiseInspectorSelectionState()
    {
        Raise(nameof(HasInspectorSelection));
        Raise(nameof(InspectorSelectionText));
        CommandManager.InvalidateRequerySuggested();
    }

    // V15-04: Reload re-enumerates the current file through the loader, re-applying WIC /
    // Magick / animated-GIF path as appropriate. Useful after external edit (Photoshop,
    // mspaint). View transforms are restored after the decode attempt so reload does not
    // surprise the user by resetting their current orientation.
    private async Task ReloadCurrentAsync()
    {
        if (!HasImage || IsOperationBusy) return;

        BeginOperationStatus("Reloading image", "Refreshing decoder output and metadata.");
        try
        {
            await YieldForOperationStatusAsync();
            if (ReloadCurrentPreservingViewState(resetPreload: false))
                Toast("Reloaded");
        }
        finally
        {
            EndOperationStatus();
        }
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
    public Task CheckForUpdatesAsync(bool userInitiated)
        => _updateCheck.CheckAsync(userInitiated);

    public string? LatestUpdateTag => _updateCheck.LatestUpdateTag;

    public string? LatestUpdateUrl => _updateCheck.LatestUpdateUrl;

    public bool HasUpdateAvailable => _updateCheck.HasUpdateAvailable;

    public bool IsCheckingForUpdates => _updateCheck.IsCheckingForUpdates;

    public string UpdateCheckStatusText => _updateCheck.UpdateCheckStatusText;

    public bool HasUpdateCheckIssue => _updateCheck.HasUpdateCheckIssue;
    public string UpdateCheckIssueTitle => _updateCheck.UpdateCheckIssueTitle;
    public string UpdateCheckIssueDetail => _updateCheck.UpdateCheckIssueDetail;

    private void SyncUpdateCheckSecondaryStatus()
    {
        if (_updateCheck.HasUpdateCheckIssue)
        {
            ShowSecondaryStatus(
                _updateCheck.UpdateCheckIssueTitle,
                _updateCheck.UpdateCheckIssueDetail,
                SecondaryStatusToneKind.Warning,
                "\uE783",
                SecondaryStatusSource.UpdateCheck);
            return;
        }

        ClearSecondaryStatus(SecondaryStatusSource.UpdateCheck);
    }

    private void BeginOperationStatus(string title, string detail)
    {
        OperationStatusTitle = title;
        OperationStatusDetail = detail;
        IsOperationBusy = true;
    }

    private Task YieldForOperationStatusAsync()
        => _uiDispatcher.InvokeAsync(static () => { }, DispatcherPriority.ContextIdle).Task;

    private void EndOperationStatus()
    {
        IsOperationBusy = false;
        OperationStatusTitle = "";
        OperationStatusDetail = "";
    }

    // E6: save a copy of the decoded first frame to a user-chosen path. Rotation and flip are
    // not baked in, matching Windows Photos: temporary viewing transforms stay temporary.
    private async Task SaveAsCopyAsync()
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

        string targetPath;
        try
        {
            targetPath = ImageExportService.ResolveWritablePath(dlg.FileName);
            if (PathsReferToSameFile(targetPath, CurrentPath))
            {
                Toast("Choose a different filename for the copy");
                return;
            }
        }
        catch (Exception ex)
        {
            Toast($"Save failed: {ex.Message}");
            return;
        }

        BeginOperationStatus("Saving copy", $"Exporting {Path.GetFileName(targetPath)}.");
        try
        {
            await YieldForOperationStatusAsync();
            var savedPath = ImageExportService.Save(bs, targetPath);
            Toast($"Saved copy → {Path.GetFileName(savedPath)}");
        }
        catch (Exception ex)
        {
            Toast($"Save failed: {ex.Message}");
        }
        finally
        {
            EndOperationStatus();
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

    private void OpenReferenceBoard()
    {
        var board = new Images.ReferenceBoardWindow
        {
            Owner = Application.Current?.MainWindow
        };

        if (!string.IsNullOrWhiteSpace(CurrentPath) && File.Exists(CurrentPath))
            board.AddFiles([CurrentPath]);

        board.Show();
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

        var rightToLeft = _settings.GetBool(Keys.ArchiveRightToLeft, false);
        if (_archiveRightToLeft != rightToLeft)
        {
            _archiveRightToLeft = rightToLeft;
            Raise(nameof(ArchiveRightToLeft));
            Raise(nameof(CanTurnLeftBookPage));
            Raise(nameof(CanTurnRightBookPage));
            Raise(nameof(LeftBookPageTurnTooltip));
            Raise(nameof(RightBookPageTurnTooltip));
            Raise(nameof(ArchivePageTurnModeText));
            Raise(nameof(ArchivePageTurnModeHint));
            Raise(nameof(ArchiveSpreadModeHint));
            CommandManager.InvalidateRequerySuggested();
        }

        var oldScanFilter = _settings.GetBool(Keys.ArchiveOldScanFilter, false);
        if (_archiveOldScanFilterEnabled != oldScanFilter)
        {
            _archiveOldScanFilterEnabled = oldScanFilter;
            Raise(nameof(ArchiveOldScanFilterEnabled));
            Raise(nameof(ArchiveOldScanFilterText));
            Raise(nameof(ArchiveOldScanFilterHint));
            if (IsArchiveBook && HasDisplayImage && !IsOperationBusy)
                ReloadCurrentPreservingViewState(resetPreload: false);
        }

        var spreadMode = _settings.GetBool(Keys.ArchiveSpreadMode, false);
        if (_archiveSpreadModeEnabled != spreadMode)
        {
            _archiveSpreadModeEnabled = spreadMode;
            Raise(nameof(ArchiveSpreadModeEnabled));
            Raise(nameof(ArchiveSpreadModeText));
            Raise(nameof(ArchiveSpreadModeHint));
            if (IsArchiveBook && HasDisplayImage && !IsOperationBusy)
                ReloadCurrentPreservingViewState(resetPreload: false);
        }

        Raise(nameof(FirstRunPrivacyText));
    }

    // P-01: Strip GPS location data from the current file using Magick.NET.
    // Writes atomically (temp-file swap) and reloads so the metadata HUD updates.
    private async Task StripLocationAsync()
    {
        if (CurrentPath is null || IsOperationBusy) return;
        var path = CurrentPath;

        BeginOperationStatus("Removing location data", $"Updating {Path.GetFileName(path)}.");
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
        finally
        {
            EndOperationStatus();
        }
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
        _externalEditReload.Disarm();
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
        ClearInspectorState();
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
        _externalEditReload.Dispose();

        _nav.ListChanged -= OnDirectoryListChanged;
        _ocrWorkflow.Dispose();
        _photoMetadata.Dispose();
        _folderPreview.Dispose();
        _preload.Dispose();
        _nav.Dispose();

        GC.SuppressFinalize(this);
    }
}
