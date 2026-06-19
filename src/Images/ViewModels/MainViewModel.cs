using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Images.Localization;
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
    private readonly ColorAnalysisController _colorAnalysis;
    private readonly C2paInspectionController _c2paInspection;
    private readonly OcrWorkflowController _ocrWorkflow;
    private readonly ExternalEditReloadController _externalEditReload;
    private readonly UpdateCheckController _updateCheck;
    private readonly CommandShortcutService _commandShortcuts;
    private readonly RecycleBinDeleteService _recycleBinDelete;
    private readonly NonDestructiveEditService _editStack = new();
    private readonly ImageFileTransferService _fileTransfer = new();
    private readonly RecoveryCenterService _recoveryCenter = new();
    private readonly ReviewLabelService _reviewLabels = new();
    private readonly Func<LosslessJpegTrimConfirmation, LosslessJpegTrimChoice> _confirmLosslessJpegTrim;
    private readonly Func<string, string?> _pickFolder;
    private readonly Func<string, WallpaperLayout, string> _setWallpaper;
    private readonly Action<BitmapSource> _copyImageToClipboard;
    private readonly Action<BitmapSource, string> _copyImageAndPathToClipboard;
    private readonly Func<string, string> _createEmailDraft;
    private readonly Action<string> _openShellTarget;
    private readonly Action<BitmapSource, string> _printDefault;
    private readonly Func<string?> _pickCompareFile;
    private readonly DispatcherTimer _renameTimer;
    private readonly DispatcherTimer _toastTimer;
    private readonly DispatcherTimer _animationTimer;

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
        UpdateCheckController? updateCheck,
        Func<LosslessJpegTrimConfirmation, LosslessJpegTrimChoice>? confirmLosslessJpegTrim = null,
        Func<string, string?>? pickFolder = null,
        Func<string, WallpaperLayout, string>? setWallpaper = null,
        Action<BitmapSource>? copyImageToClipboard = null,
        Action<BitmapSource, string>? copyImageAndPathToClipboard = null,
        Func<string, string>? createEmailDraft = null,
        Action<string>? openShellTarget = null,
        Action<BitmapSource, string>? printDefault = null,
        Func<string?>? pickCompareFile = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _commandShortcuts = new CommandShortcutService(_settings);
        _clipboardImport = clipboardImport ?? new ClipboardImportService();
        _nav = navigator ?? new DirectoryNavigator();
        _recycleBinDelete = recycleBinDelete ?? new RecycleBinDeleteService(_settings);
        _folderPreview = folderPreview ?? new FolderPreviewController(_uiDispatcher, () => _isDisposed);
        _folderPreview.StateChanged += (_, _) => RaiseFolderPreviewState();
        _photoMetadata = photoMetadata ?? new PhotoMetadataController(_uiDispatcher, () => _isDisposed, () => CurrentPath);
        _photoMetadata.StateChanged += (_, _) => RaisePhotoMetadataState();
        _colorAnalysis = new ColorAnalysisController(_uiDispatcher, () => _isDisposed, () => CurrentPath);
        _colorAnalysis.StateChanged += (_, _) => RaiseColorAnalysisState();
        _c2paInspection = new C2paInspectionController(_uiDispatcher, () => _isDisposed, () => CurrentPath);
        _c2paInspection.StateChanged += (_, _) => RaiseC2paInspectionState();
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
        _confirmLosslessJpegTrim = confirmLosslessJpegTrim ?? ShowLosslessJpegTrimConfirmation;
        _pickFolder = pickFolder ?? PickFolder;
        _setWallpaper = setWallpaper ?? WallpaperService.SetFromFile;
        _copyImageToClipboard = copyImageToClipboard ?? ClipboardService.SetImage;
        _copyImageAndPathToClipboard = copyImageAndPathToClipboard ?? ClipboardService.SetImageAndPath;
        _createEmailDraft = createEmailDraft ?? (path => EmailShareService.CreateDraftWithAttachment(path).DraftPath);
        _openShellTarget = openShellTarget ?? ShellIntegration.OpenShellTarget;
        _printDefault = printDefault ?? PrintService.PrintDefault;
        _pickCompareFile = pickCompareFile ?? PickCompareFile;
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

        _animationTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(100) };
        _animationTimer.Tick += (_, _) => AdvanceAnimationFromTimer();

        _hintTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2400) };
        _hintTimer.Tick += (_, _) => { _hintTimer.Stop(); ShowGestureHint = false; };

        _isFilmstripVisible = _settings.GetBool(Keys.FilmstripVisible, true);
        _isMetadataHudVisible = _settings.GetBool(Keys.MetadataHudVisible, false);
        _archiveRightToLeft = _settings.GetBool(Keys.ArchiveRightToLeft, false);
        _archiveOldScanFilterEnabled = _settings.GetBool(Keys.ArchiveOldScanFilter, false);
        _archiveSpreadModeEnabled = _settings.GetBool(Keys.ArchiveSpreadMode, false);
        RestorePersistedSortMode();

        OpenCommand = new RelayCommand(async () => await OpenFileDialogAsync(), () => !IsOperationBusy);
        NextCommand = new RelayCommand(async () => await NextAsync(), () => CanUseImageCommands);
        PrevCommand = new RelayCommand(async () => await PrevAsync(), () => CanUseImageCommands);
        FirstCommand = new RelayCommand(async () => await FirstAsync(), () => CanUseImageCommands);
        LastCommand = new RelayCommand(async () => await LastAsync(), () => CanUseImageCommands);
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
        ApplyRotationToFileCommand = new RelayCommand(ApplyRotationToFile, () => CanApplyRotationToFile);
        ToggleInspectorCommand = new RelayCommand(() => IsInspectorMode = !IsInspectorMode, () => CanUseInspector);
        ToggleSelectionModeCommand = new RelayCommand(() => IsSelectionMode = !IsSelectionMode, () => CanUseSelection);
        CopySelectionCommand = new RelayCommand(CopyCanvasSelection, () => CanCopySelection);
        ClearSelectionCommand = new RelayCommand(ClearCanvasSelection, () => HasCanvasSelection);
        CancelSelectionCommand = new RelayCommand(CancelSelectionMode, () => IsSelectionMode || HasCanvasSelection);
        ToggleCropModeCommand = new RelayCommand(() => IsCropMode = !IsCropMode, () => CanUseCrop);
        ApplyCropCommand = new RelayCommand(ApplyCropSelection, () => CanApplyCrop);
        CancelCropCommand = new RelayCommand(CancelCropMode, () => IsCropMode || HasCropSelection);
        SetCropAspectPresetCommand = new RelayCommand(SetCropAspectPreset);
        OpenResizeDialogCommand = new RelayCommand(OpenResizeDialog, () => CanUseResize);
        OpenAdjustmentsCommand = new RelayCommand(OpenAdjustments, () => CanUseAdjustments);
        OpenEffectsCommand = new RelayCommand(OpenEffects, () => CanUseEffects);
        AutoEnhanceCommand = new RelayCommand(ApplyAutoEnhance, () => CanUseAutoEnhance);
        OpenAnnotationsCommand = new RelayCommand(OpenAnnotations, () => CanUseAnnotations);
        OpenPerspectiveCommand = new RelayCommand(OpenPerspective, () => CanUsePerspective);
        ToggleExposureBrushModeCommand = new RelayCommand(() => IsExposureBrushMode = !IsExposureBrushMode, () => CanUseExposureBrush);
        ApplyExposureBrushCommand = new RelayCommand(ApplyExposureBrush, () => CanApplyExposureBrush);
        CancelExposureBrushCommand = new RelayCommand(CancelExposureBrushMode, () => IsExposureBrushMode || HasExposureBrushStrokes);
        ClearExposureBrushCommand = new RelayCommand(ClearExposureBrushStrokes, () => HasExposureBrushStrokes);
        SetExposureBrushModeCommand = new RelayCommand(SetExposureBrushMode, parameter => parameter is string);
        ToggleRedEyeModeCommand = new RelayCommand(() => IsRedEyeCorrectionMode = !IsRedEyeCorrectionMode, () => CanUseRedEyeCorrection);
        ApplyRedEyeCorrectionCommand = new RelayCommand(ApplyRedEyeCorrection, () => CanApplyRedEyeCorrection);
        CancelRedEyeCorrectionCommand = new RelayCommand(CancelRedEyeCorrectionMode, () => IsRedEyeCorrectionMode || HasRedEyeCorrectionMarks);
        ClearRedEyeCorrectionCommand = new RelayCommand(ClearRedEyeCorrectionMarks, () => HasRedEyeCorrectionMarks);
        ToggleRetouchModeCommand = new RelayCommand(() => IsRetouchMode = !IsRetouchMode, () => CanUseRetouch);
        ApplyRetouchCommand = new RelayCommand(ApplyRetouch, () => CanApplyRetouch);
        CancelRetouchCommand = new RelayCommand(CancelRetouchMode, () => IsRetouchMode || HasRetouchStrokes || HasRetouchSource);
        ClearRetouchCommand = new RelayCommand(ClearRetouchStrokes, () => HasRetouchStrokes);
        SetRetouchModeCommand = new RelayCommand(SetRetouchMode, parameter => parameter is string);
        ClearRetouchSourceCommand = new RelayCommand(ClearRetouchSource, () => HasRetouchSource);
        ToggleInpaintModeCommand = new RelayCommand(ToggleInpaintMode, () => CanUseInpaint);
        ApplyInpaintCommand = new RelayCommand(ApplyInpaint, () => HasInpaintMaskRegions);
        CancelInpaintCommand = new RelayCommand(CancelInpaintMode, () => IsInpaintMode || HasInpaintMaskRegions);
        ClearInpaintMaskCommand = new RelayCommand(ClearInpaintMask, () => HasInpaintMaskRegions);
        CopyInspectorHexCommand = new RelayCommand(() => CopyInspectorValue(s => s.Hex, "HEX"), () => HasInspectorSample);
        CopyInspectorRgbCommand = new RelayCommand(() => CopyInspectorValue(s => s.Rgb, "RGB"), () => HasInspectorSample);
        CopyInspectorHsvCommand = new RelayCommand(() => CopyInspectorValue(s => s.Hsv, "HSV"), () => HasInspectorSample);
        CopyInspectorSummaryCommand = new RelayCommand(() => CopyInspectorValue(s => s.Summary, "pixel sample"), () => HasInspectorSample);
        ClearInspectorSelectionCommand = new RelayCommand(ClearInspectorSelection, () => HasInspectorSelection);
        ToggleAnimationPlaybackCommand = new RelayCommand(ToggleAnimationPlayback, () => IsAnimated);
        PreviousAnimationFrameCommand = new RelayCommand(() => StepAnimationFrame(-1), () => CanStepAnimationFrame);
        NextAnimationFrameCommand = new RelayCommand(() => StepAnimationFrame(1), () => CanStepAnimationFrame);
        FirstAnimationFrameCommand = new RelayCommand(() => SelectAnimationFrame(0, pause: true), () => IsAnimated);
        LastAnimationFrameCommand = new RelayCommand(() => SelectAnimationFrame((CurrentAnimation?.Frames.Count ?? 1) - 1, pause: true), () => IsAnimated);
        SetAnimationFrameCommand = new RelayCommand(SetAnimationFrameFromParameter, CanSetAnimationFrameFromParameter);
        CopyAnimationFrameCommand = new RelayCommand(CopyAnimationFrame, () => IsAnimated);
        ExportAnimationFrameCommand = new RelayCommand(async () => await ExportAnimationFrameAsync(), () => IsAnimated);
        FlipHorizontalCommand = new RelayCommand(() => { FlipHorizontal = !FlipHorizontal; }, () => CanUseDisplayImageCommands);
        FlipVerticalCommand = new RelayCommand(() => { FlipVertical = !FlipVertical; }, () => CanUseDisplayImageCommands);
        RevealCommand = new RelayCommand(RevealInExplorer, () => HasImage);
        CopyPathCommand = new RelayCommand(CopyPath, () => HasImage);
        CopyImageCommand = new RelayCommand(CopyCurrentImage, () => CanUseDisplayImageCommands);
        CopyImageAndPathCommand = new RelayCommand(CopyCurrentImageAndPath, () => CanUseDisplayImageCommands && HasImage);
        CopyToFolderCommand = new RelayCommand(p => TransferCurrentImage(ImageFileTransferMode.Copy, p as string), _ => CanUseImageCommands);
        MoveToFolderCommand = new RelayCommand(p => TransferCurrentImage(ImageFileTransferMode.Move, p as string), _ => CanUseImageCommands);
        SetAsWallpaperCommand = new RelayCommand(SetAsWallpaper, _ => CanUseImageCommands);
        ReloadCommand = new RelayCommand(async () => await ReloadCurrentAsync(), () => CanUseImageCommands);
        PrintCommand = new RelayCommand(PrintCurrent, () => CanUseDisplayImageCommands);
        PrintDefaultCommand = new RelayCommand(PrintCurrentToDefault, () => CanUseDisplayImageCommands);
        SendToEmailCommand = new RelayCommand(SendCurrentToEmail, () => CanUseImageCommands);
        SaveAsCopyCommand = new RelayCommand(async () => await SaveAsCopyAsync(), () => CanUseDisplayImageCommands);
        OpenExportWorkbenchCommand = new RelayCommand(OpenExportWorkbench, () => CanUseDisplayImageCommands);
        CheckForUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync(userInitiated: true), () => !IsCheckingForUpdates);
        OpenLatestUpdateCommand = new RelayCommand(_updateCheck.OpenLatestUpdate, () => HasUpdateAvailable && !IsCheckingForUpdates);
        RefreshCommand = new RelayCommand(RefreshFolder, () => CanRefreshFolder);
        CommitRenameCommand = new RelayCommand(() => { _renameTimer.Stop(); FlushPendingRename(); });
        CancelRenameCommand = new RelayCommand(CancelRenameEdit);
        UnlockExtensionCommand = new RelayCommand(() => IsExtensionUnlocked = !IsExtensionUnlocked);
        UndoRenameCommand = new RelayCommand(p => UndoOne(p as RenameService.UndoEntry), p => p is RenameService.UndoEntry);
        AboutCommand = new RelayCommand(ShowAboutWindow);
        StartCompareCommand = new RelayCommand(StartCompareWithNext, () => CanStartCompareWithNext);
        CompareWithCommand = new RelayCommand(StartCompareWithPickedFile, () => CanUseCompareMode);
        ExitCompareCommand = new RelayCommand(() => ClearCompareMode(showToast: true), () => IsCompareMode);
        SwapCompareCommand = new RelayCommand(SwapComparePair, () => CanSwapComparePair);
        ToggleCompareOverlayCommand = new RelayCommand(() => IsCompareOverlayMode = !IsCompareOverlayMode, () => IsCompareMode);
        IncreaseCompareOpacityCommand = new RelayCommand(() => AdjustCompareOverlayOpacity(0.05), () => IsCompareMode);
        DecreaseCompareOpacityCommand = new RelayCommand(() => AdjustCompareOverlayOpacity(-0.05), () => IsCompareMode);
        ToggleOverlayModeCommand = new RelayCommand(() => IsPinnedOverlayMode = !IsPinnedOverlayMode, () => CanUseOverlayMode || IsPinnedOverlayMode);
        ExitOverlayModeCommand = new RelayCommand(ExitOverlayMode, () => IsPinnedOverlayMode);
        OpenReferenceBoardCommand = new RelayCommand(OpenReferenceBoard);
        OpenDuplicateCleanupCommand = new RelayCommand(OpenDuplicateCleanup);
        OpenFileHealthScanCommand = new RelayCommand(OpenFileHealthScan);
        OpenRecoveryCenterCommand = new RelayCommand(OpenRecoveryCenter);
        OpenModelManagerCommand = new RelayCommand(OpenModelManager);
        OpenSemanticSearchCommand = new RelayCommand(OpenSemanticSearch);
        OpenTagGraphCommand = new RelayCommand(OpenTagGraph);
        OpenImportInboxCommand = new RelayCommand(OpenImportInbox);
        ImportXmpSidecarsCommand = new RelayCommand(ImportXmpSidecars);
        OpenMacroActionsCommand = new RelayCommand(OpenMacroActions);
        OpenBatchProcessorCommand = new RelayCommand(OpenBatchProcessor);
        OpenEditStackCommand = new RelayCommand(OpenEditStack, () => HasImage);
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
        ApplyGallerySmartFilterCommand = new RelayCommand(ApplyGallerySmartFilter, p => p is string);
        ClearGalleryFilterCommand = new RelayCommand(() => GalleryFilterText = "", () => HasGalleryFilter);
        ToggleReviewModeCommand = new RelayCommand(() => IsReviewMode = !IsReviewMode, () => CanUseReviewLabels || IsReviewMode);
        SetReviewRatingCommand = new RelayCommand(SetReviewRating, _ => CanUseReviewLabels);
        MarkReviewPickCommand = new RelayCommand(() => SetReviewLabel(ReviewLabelKind.Pick), () => CanUseReviewLabels);
        MarkReviewRejectCommand = new RelayCommand(() => SetReviewLabel(ReviewLabelKind.Reject), () => CanUseReviewLabels);
        ClearReviewLabelCommand = new RelayCommand(() => SetReviewLabel(ReviewLabelKind.None), () => CanUseReviewLabels);
        UndoReviewLabelCommand = new RelayCommand(UndoReviewLabel, () => CanUndoReviewLabel);
        ToggleFilmstripCommand = new RelayCommand(ToggleFilmstrip, () => CanToggleFilmstrip);
        ToggleMetadataHudCommand = new RelayCommand(ToggleMetadataHud, () => CanToggleMetadataHud);
        PasteFromClipboardCommand = new RelayCommand(PasteFromClipboard, () => !IsOperationBusy);
        OpenInDefaultAppCommand = new RelayCommand(OpenInDefaultApp, () => HasImage);
        StripLocationCommand = new RelayCommand(async () => await StripLocationAsync(), () => CanUseImageCommands);
        StripDeviceInfoCommand = new RelayCommand(async () => await StripMetadataAsync(MetadataStripCategory.DeviceInfo), () => CanUseImageCommands);
        StripTimestampsCommand = new RelayCommand(async () => await StripMetadataAsync(MetadataStripCategory.Timestamps), () => CanUseImageCommands);
        StripSoftwareCommand = new RelayCommand(async () => await StripMetadataAsync(MetadataStripCategory.Software), () => CanUseImageCommands);
        StripAllMetadataCommand = new RelayCommand(async () => await StripMetadataAsync(MetadataStripCategory.All), () => CanUseImageCommands);
        ExtractMotionVideoCommand = new RelayCommand(async () => await ExtractMotionVideoAsync(), () => IsMotionPhoto || CompanionVideoPath is not null);
        SettingsCommand = new RelayCommand(ShowSettingsWindow);
        ExtractTextCommand = new RelayCommand(async () => await _ocrWorkflow.ToggleAsync(), () => HasImage && !IsOperationBusy && !IsCompareMode);
        ToggleCommandPaletteCommand = new RelayCommand(() => ShowCommandPalette = !ShowCommandPalette);
        InstallStoreExtensionCommand = new RelayCommand(OpenStoreExtensionPage, () => _loadErrorStoreExtension is not null);
        CycleChannelModeCommand = new RelayCommand(CycleChannelMode, () => HasDisplayImage);
        SetChannelModeCommand = new RelayCommand(p =>
        {
            if (p is ChannelMode mode) ChannelMode = mode;
            else if (p is string s && Enum.TryParse<ChannelMode>(s, true, out var parsed)) ChannelMode = parsed;
        }, _ => HasDisplayImage);

        // V30-33: slideshow commands
        ToggleSlideshowCommand = new RelayCommand(ToggleSlideshow, () => HasImage);
        StartSlideshowCommand = new RelayCommand(StartSlideshow, () => HasImage && !IsSlideshowActive);
        StopSlideshowCommand = new RelayCommand(StopSlideshow, () => IsSlideshowActive);
        PauseSlideshowCommand = new RelayCommand(PauseResumeSlideshow, () => IsSlideshowActive);
        IncreaseSlideshowIntervalCommand = new RelayCommand(() => SlideshowIntervalSeconds++, () => IsSlideshowActive && SlideshowIntervalSeconds < 60);
        DecreaseSlideshowIntervalCommand = new RelayCommand(() => SlideshowIntervalSeconds--, () => IsSlideshowActive && SlideshowIntervalSeconds > 1);
        ToggleSlideshowShuffleCommand = new RelayCommand(ToggleSlideshowShuffle, () => IsSlideshowActive);

        RefreshCommandPalette();

        // V20-02 UI consumer: seed RecentFolders from SettingsService at startup so the side
        // panel renders prior-session folders before the user opens anything.
        RefreshRecentFolders();
        RefreshRecentTransferFolders();
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
                Raise(nameof(CanUseSelection));
                Raise(nameof(CanUseCrop));
                Raise(nameof(CanApplyRotationToFile));
                Raise(nameof(CanUseExposureBrush));
                Raise(nameof(CanUseRedEyeCorrection));
                Raise(nameof(CanUseRetouch));
                Raise(nameof(CanUseOverlayMode));
                RaiseCompareState();
                Raise(nameof(CanToggleMetadataHud));
                Raise(nameof(ShowMetadataHud));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    // -------------------- Channel isolation (V20-28) --------------------
    // Stores the unfiltered image so channel mode changes can reapply without reloading.
    private ImageSource? _rawImage;

    private ChannelMode _channelMode = ChannelMode.Normal;
    public ChannelMode ChannelMode
    {
        get => _channelMode;
        set
        {
            if (Set(ref _channelMode, value))
            {
                Raise(nameof(ChannelModeLabel));
                Raise(nameof(IsChannelIsolated));
                ReapplyChannelIsolation();
            }
        }
    }

    public string ChannelModeLabel => _channelMode switch
    {
        ChannelMode.Red => "R",
        ChannelMode.Green => "G",
        ChannelMode.Blue => "B",
        ChannelMode.Alpha => "A",
        _ => ""
    };

    public bool IsChannelIsolated => _channelMode != ChannelMode.Normal;

    public ICommand CycleChannelModeCommand { get; }
    public ICommand SetChannelModeCommand { get; }

    private void CycleChannelMode()
    {
        ChannelMode = _channelMode switch
        {
            ChannelMode.Normal => ChannelMode.Red,
            ChannelMode.Red => ChannelMode.Green,
            ChannelMode.Green => ChannelMode.Blue,
            ChannelMode.Blue => ChannelMode.Alpha,
            ChannelMode.Alpha => ChannelMode.Normal,
            _ => ChannelMode.Normal
        };
    }

    /// <summary>
    /// Sets the raw image and applies the current channel isolation filter.
    /// All code paths that load or clear the displayed image should call this
    /// instead of assigning <see cref="CurrentImage"/> directly.
    /// </summary>
    private void SetCurrentImageWithChannel(ImageSource? image)
    {
        _rawImage = image;
        if (image is BitmapSource bitmap && _channelMode != ChannelMode.Normal)
            CurrentImage = ChannelIsolationService.Isolate(bitmap, _channelMode) ?? image;
        else
            CurrentImage = image;
    }

    /// <summary>
    /// Re-applies channel isolation to the cached raw image when the user
    /// changes <see cref="ChannelMode"/> without navigating to a new file.
    /// </summary>
    private void ReapplyChannelIsolation()
    {
        if (_rawImage is null)
            return;

        if (_rawImage is BitmapSource bitmap && _channelMode != ChannelMode.Normal)
            CurrentImage = ChannelIsolationService.Isolate(bitmap, _channelMode) ?? _rawImage;
        else
            CurrentImage = _rawImage;
    }

    private TilePyramidInfo? _currentTilePyramid;
    public TilePyramidInfo? CurrentTilePyramid
    {
        get => _currentTilePyramid;
        private set
        {
            if (Set(ref _currentTilePyramid, value))
            {
                Raise(nameof(IsTilePyramidActive));
                Raise(nameof(HasDisplayImage));
                Raise(nameof(CanUseInspector));
                Raise(nameof(CanUseSelection));
                Raise(nameof(CanUseCrop));
                Raise(nameof(CanApplyRotationToFile));
                Raise(nameof(CanUseExposureBrush));
                Raise(nameof(CanUseRedEyeCorrection));
                Raise(nameof(CanUseRetouch));
                Raise(nameof(CanUseOverlayMode));
                RaiseCompareState();
                Raise(nameof(CanToggleMetadataHud));
                Raise(nameof(ShowMetadataHud));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsTilePyramidActive => CurrentTilePyramid is not null;

    // Animated-frame payload. Non-null when the current file is a multi-frame GIF / APNG /
    // animated WebP. ZoomPanImage renders the selected frame through view-model state so the
    // side-panel timeline, shortcuts, and copy/export actions share one source of truth.
    private AnimationSequence? _currentAnimation;
    public AnimationSequence? CurrentAnimation
    {
        get => _currentAnimation;
        private set
        {
            if (Set(ref _currentAnimation, value))
            {
                RefreshAnimationWorkbench(value);
                Raise(nameof(IsAnimated));
                Raise(nameof(AnimationFrameCountText));
                RaiseAnimationWorkbenchState();
            }
        }
    }

    // IsAnimated mirrors ZoomPanImage.OnAnimationChanged's `Frames.Count < 2` early-return so
    // a defensive 1-frame sequence never claims animation in the chip when the canvas isn't
    // actually playing. ImageLoader.TryLoadAnimated already returns null for <2 frames; this
    // guards future code paths that bypass the loader.
    public bool IsAnimated => CurrentAnimation is { Frames.Count: >= 2 };

    private MotionPhotoInfo? _motionPhoto;
    public bool IsMotionPhoto => _motionPhoto is not null;
    public string? CompanionVideoPath { get; private set; }

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

    public ObservableCollection<AnimationFrameItem> AnimationFrames { get; } = new();

    private int _currentAnimationFrameIndex;
    public int CurrentAnimationFrameIndex
    {
        get => _currentAnimationFrameIndex;
        set => SelectAnimationFrame(value, pause: false);
    }

    public double AnimationScrubberValue
    {
        get => CurrentAnimationFrameIndex;
        set => SelectAnimationFrame((int)Math.Round(value), pause: true);
    }

    public double AnimationFrameSliderMaximum
        => IsAnimated ? Math.Max(0, CurrentAnimation!.Frames.Count - 1) : 0;

    private bool _isAnimationPlaying;
    public bool IsAnimationPlaying
    {
        get => _isAnimationPlaying;
        set
        {
            if (!IsAnimated && value)
                value = false;

            if (!Set(ref _isAnimationPlaying, value))
                return;

            if (value)
            {
                _animationCompletedLoops = 0;
                RestartAnimationTimer();
            }
            else
            {
                _animationTimer.Stop();
            }

            Raise(nameof(AnimationPlaybackText));
            Raise(nameof(AnimationWorkbenchStatusText));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private double _animationPlaybackSpeed = 1.0;
    public double AnimationPlaybackSpeed
    {
        get => _animationPlaybackSpeed;
        set
        {
            var clamped = AnimationWorkbenchService.ClampPlaybackSpeed(value);
            if (!Set(ref _animationPlaybackSpeed, clamped))
                return;

            Raise(nameof(AnimationPlaybackSpeedText));
            Raise(nameof(AnimationWorkbenchStatusText));
            if (IsAnimationPlaying)
                RestartAnimationTimer();
        }
    }

    private int _animationCompletedLoops;

    public bool CanStepAnimationFrame => IsAnimated;
    public string AnimationPlaybackText => IsAnimationPlaying ? Strings.MainAnimPause : Strings.MainAnimPlay;
    public string AnimationPlaybackSpeedText => AnimationWorkbenchService.FormatSpeed(AnimationPlaybackSpeed);
    public string SelectedAnimationFrameText => AnimationWorkbenchService.FormatFramePosition(
        CurrentAnimationFrameIndex,
        CurrentAnimation?.Frames.Count ?? 0);
    public string SelectedAnimationFrameDelayText => IsAnimated
        ? AnimationWorkbenchService.FormatDelay(CurrentAnimation!.Delays[CurrentAnimationFrameIndex])
        : "";
    public string SelectedAnimationTimestampText => IsAnimated
        ? AnimationWorkbenchService.FormatTimestamp(CurrentAnimation!.Delays, CurrentAnimationFrameIndex)
        : "";
    public string AnimationWorkbenchStatusText
    {
        get
        {
            if (!IsAnimated)
                return Strings.MainAnimWorkbenchEmpty;

            var state = IsAnimationPlaying ? Strings.MainAnimPlay : Strings.MainAnimPause;
            return $"{state} at {AnimationPlaybackSpeedText} · {SelectedAnimationFrameText} · {SelectedAnimationFrameDelayText}";
        }
    }

    public BitmapSource? SelectedAnimationFrame
        => IsAnimated ? CurrentAnimation!.Frames[CurrentAnimationFrameIndex] : null;

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

    private string _pageLabel = Strings.MainPageLabel;
    public string PageLabel
    {
        get => _pageLabel;
        private set
        {
            if (Set(ref _pageLabel, string.IsNullOrWhiteSpace(value) ? Strings.MainPageLabel : value))
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
    public string LeftBookPageTurnTooltip => ArchiveRightToLeft ? Strings.MainArchiveNextPage : Strings.MainArchivePrevPage;
    public string RightBookPageTurnTooltip => ArchiveRightToLeft ? Strings.MainArchivePrevPage : Strings.MainArchiveNextPage;
    public string ArchivePageTurnModeText => ArchiveRightToLeft ? Strings.MainArchiveRtlTurns : Strings.MainArchiveLtrTurns;
    public string ArchivePageTurnModeHint => ArchiveRightToLeft
        ? Strings.MainArchiveRtlHint
        : Strings.MainArchiveLtrHint;
    public string ArchiveOldScanFilterText => ArchiveOldScanFilterEnabled ? Strings.MainArchiveCleanScansOn : Strings.MainArchiveCleanScans;
    public string ArchiveOldScanFilterHint => Strings.MainArchiveCleanScansHint;
    public string ArchiveSpreadModeText => ArchiveSpreadModeEnabled ? Strings.MainArchiveSpreadsOn : Strings.MainArchiveSpreads;
    public string ArchiveSpreadModeHint => ArchiveRightToLeft
        ? Strings.MainArchiveSpreadHintRtl
        : Strings.MainArchiveSpreadHintLtr;
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
            _ = GoToPageAsync(target, Strings.MainOpLoadingPage);
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
        Raise(nameof(CanUseCrop));
        Raise(nameof(CanApplyRotationToFile));
        Raise(nameof(CanUseExposureBrush));
        Raise(nameof(CanUseRedEyeCorrection));
        Raise(nameof(CanUseRetouch));
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
                Raise(nameof(CanUseCrop));
                Raise(nameof(CurrentFormatSupportsCrop));
                Raise(nameof(CanApplyRotationToFile));
                Raise(nameof(CanUseExposureBrush));
                Raise(nameof(CanUseRedEyeCorrection));
                Raise(nameof(CanUseRetouch));
                RaiseCompareState();
                RefreshReviewState(value);
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool HasImage => !string.IsNullOrEmpty(CurrentPath) && File.Exists(CurrentPath);
    public bool HasDisplayImage => CurrentImage is not null || IsTilePyramidActive;
    public bool CanUseInspector => CurrentImage is not null && !IsTilePyramidActive && !IsOperationBusy && !IsCompareMode;
    public bool CanUseSelection => CurrentImage is not null && !IsTilePyramidActive && !IsOperationBusy && !IsCompareMode;
    public bool CurrentFormatSupportsCrop => CurrentPath is not null && SupportedImageFormats.IsCropWritableRaster(CurrentPath);
    private bool CanUsePixelEditTools => HasImage && CurrentImage is not null && !IsTilePyramidActive && !IsOperationBusy && !IsArchiveBook && !IsPeekMode && !IsCompareMode;
    public bool CanUseCrop => CanUsePixelEditTools && CurrentFormatSupportsCrop;
    public bool CanApplyRotationToFile =>
        CanUsePixelEditTools &&
        CurrentPath is not null &&
        SupportedImageFormats.IsCropWritableRaster(CurrentPath) &&
        NormalizeRotationForWriteback(Rotation) != 0;
    private bool CanUseResize => CanUsePixelEditTools;
    private bool CanUseAdjustments => CanUsePixelEditTools;
    private bool CanUseEffects => CanUsePixelEditTools;
    private bool CanUseAutoEnhance => CanUsePixelEditTools;
    private bool CanUseAnnotations => CanUsePixelEditTools;
    private bool CanUsePerspective => CanUsePixelEditTools;
    public bool CanUseExposureBrush => CanUsePixelEditTools;
    public bool CanUseRedEyeCorrection => CanUsePixelEditTools;
    public bool CanUseRetouch => CanUsePixelEditTools;
    public bool IsViewerEmpty => CurrentPath is null;
    public bool CanRefreshFolder => (CurrentPath is not null || _nav.Count > 0) && !IsOperationBusy;

    private bool CanUseImageCommands => HasImage && !IsOperationBusy;
    private bool CanUseDisplayImageCommands => CurrentImage is not null && !IsTilePyramidActive && !IsOperationBusy;

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
                Raise(nameof(CanUseSelection));
                Raise(nameof(CanUseCrop));
                Raise(nameof(CurrentFormatSupportsCrop));
                Raise(nameof(CanApplyRotationToFile));
                Raise(nameof(CanUseExposureBrush));
                Raise(nameof(CanUseRedEyeCorrection));
                Raise(nameof(CanUseRetouch));
                Raise(nameof(CanUseOverlayMode));
                RaiseCompareState();
                RaiseReviewState();
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
        ? Strings.MainFirstRunPrivacyOn
        : Strings.MainFirstRunPrivacyOff;

    public string FirstRunFormatStatusText => CodecCapabilityService.BuildOverviewText();
    public string FirstRunOcrStatusText => OcrCapabilityService.BuildOverviewText();
    public string FirstRunDocumentStatusText => CodecCapabilityService.BuildDocumentStatusText();
    public string FirstRunRecoveryText => Strings.MainFirstRunRecovery;

    // First-run gesture hint. Flipped true exactly once — the first time an image successfully
    // lands in the viewport. The view animates the hint in, then fades it out after 2.4 s.
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

    // V20-29: command palette overlay — Ctrl+Shift+P opens, Escape/Enter/click dismisses.
    private IReadOnlyDictionary<string, string> _shortcutTexts = new Dictionary<string, string>(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, string> ShortcutTexts
    {
        get => _shortcutTexts;
        private set => Set(ref _shortcutTexts, value);
    }

    public string PreviousNextShortcutText => ShortcutPairText(CommandIds.Previous, CommandIds.Next);
    public string FirstLastShortcutText => ShortcutPairText(CommandIds.First, CommandIds.Last);

    private List<CommandPaletteItem> _commandPaletteRegistry = new();

    private bool _showCommandPalette;
    public bool ShowCommandPalette
    {
        get => _showCommandPalette;
        set
        {
            if (Set(ref _showCommandPalette, value))
            {
                if (value)
                {
                    CommandPaletteFilterText = "";
                    SelectedCommandPaletteIndex = 0;
                    RefreshCommandPaletteItems();
                }
            }
        }
    }

    private string _commandPaletteFilterText = "";
    public string CommandPaletteFilterText
    {
        get => _commandPaletteFilterText;
        set
        {
            if (Set(ref _commandPaletteFilterText, value))
            {
                RefreshCommandPaletteItems();
                SelectedCommandPaletteIndex = 0;
            }
        }
    }

    private int _selectedCommandPaletteIndex;
    public int SelectedCommandPaletteIndex
    {
        get => _selectedCommandPaletteIndex;
        set => Set(ref _selectedCommandPaletteIndex, value);
    }

    private List<CommandPaletteItem> _filteredCommandPaletteItems = new();
    public List<CommandPaletteItem> FilteredCommandPaletteItems
    {
        get => _filteredCommandPaletteItems;
        private set => Set(ref _filteredCommandPaletteItems, value);
    }

    public ICommand ToggleCommandPaletteCommand { get; }

    private void RefreshCommandPaletteItems()
    {
        var filter = _commandPaletteFilterText?.Trim() ?? "";
        if (string.IsNullOrEmpty(filter))
        {
            FilteredCommandPaletteItems = _commandPaletteRegistry
                .Where(c => c.Command?.CanExecute(null) != false)
                .ToList();
            return;
        }

        var words = filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        FilteredCommandPaletteItems = _commandPaletteRegistry
            .Where(c => c.Command?.CanExecute(null) != false)
            .Where(c => words.All(w =>
                c.Name.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                c.Category.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                c.Shortcut.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public void ExecuteSelectedPaletteCommand()
    {
        if (SelectedCommandPaletteIndex >= 0 && SelectedCommandPaletteIndex < FilteredCommandPaletteItems.Count)
        {
            var item = FilteredCommandPaletteItems[SelectedCommandPaletteIndex];
            ShowCommandPalette = false;
            item.Command?.Execute(null);
        }
    }

    public bool IsCommandShortcut(string id, Key key, ModifierKeys modifiers)
        => _commandShortcuts.IsShortcut(id, key, modifiers);

    public bool TryExecuteCommandShortcut(Key key, ModifierKeys modifiers)
    {
        if (!_commandShortcuts.TryMatch(key, modifiers, out var definition)
            || definition.Id == CommandIds.CommandPalette)
        {
            return false;
        }

        return ExecuteCommandShortcut(definition.Id);
    }

    private bool ExecuteCommandShortcut(string id)
    {
        switch (id)
        {
            case CommandIds.Open:
                OpenCommand.Execute(null);
                return true;
            case CommandIds.Previous:
                if (IsArchiveBook) LeftBookPageTurnCommand.Execute(null);
                else PrevCommand.Execute(null);
                return true;
            case CommandIds.Next:
                if (IsArchiveBook) RightBookPageTurnCommand.Execute(null);
                else NextCommand.Execute(null);
                return true;
            case CommandIds.First:
                if (IsArchiveBook) FirstPageCommand.Execute(null);
                else FirstCommand.Execute(null);
                return true;
            case CommandIds.Last:
                if (IsArchiveBook) LastPageCommand.Execute(null);
                else LastCommand.Execute(null);
                return true;
            case CommandIds.Refresh:
                RefreshCommand.Execute(null);
                return true;
            case CommandIds.Filmstrip:
                ToggleFilmstripCommand.Execute(null);
                return true;
            case CommandIds.MetadataHud:
                ToggleMetadataHudCommand.Execute(null);
                return true;
            case CommandIds.Gallery:
                ToggleGalleryCommand.Execute(null);
                return true;
            case CommandIds.ExtractText:
                ExtractTextCommand.Execute(null);
                return true;
            case CommandIds.CropMode:
                ToggleCropModeCommand.Execute(null);
                return true;
            case CommandIds.SelectionMode:
                ToggleSelectionModeCommand.Execute(null);
                return true;
            case CommandIds.Resize:
                OpenResizeDialogCommand.Execute(null);
                return true;
            case CommandIds.Adjustments:
                OpenAdjustmentsCommand.Execute(null);
                return true;
            case CommandIds.Effects:
                OpenEffectsCommand.Execute(null);
                return true;
            case CommandIds.AutoEnhance:
                AutoEnhanceCommand.Execute(null);
                return true;
            case CommandIds.Perspective:
                OpenPerspectiveCommand.Execute(null);
                return true;
            case CommandIds.ExposureBrush:
                ToggleExposureBrushModeCommand.Execute(null);
                return true;
            case CommandIds.RedEye:
                ToggleRedEyeModeCommand.Execute(null);
                return true;
            case CommandIds.Retouch:
                ToggleRetouchModeCommand.Execute(null);
                return true;
            case CommandIds.ExportWorkbench:
                OpenExportWorkbenchCommand.Execute(null);
                return true;
            case CommandIds.Delete:
                DeleteCommand.Execute(null);
                return true;
            case CommandIds.Reload:
                ReloadCommand.Execute(null);
                return true;
            case CommandIds.Print:
                PrintCommand.Execute(null);
                return true;
            case CommandIds.SaveCopy:
                SaveAsCopyCommand.Execute(null);
                return true;
            case CommandIds.Paste:
                PasteFromClipboardCommand.Execute(null);
                return true;
            case CommandIds.ReferenceBoard:
                OpenReferenceBoardCommand.Execute(null);
                return true;
            case CommandIds.DuplicateCleanup:
                OpenDuplicateCleanupCommand.Execute(null);
                return true;
            case CommandIds.FileHealthScan:
                OpenFileHealthScanCommand.Execute(null);
                return true;
            case CommandIds.TagGraph:
                OpenTagGraphCommand.Execute(null);
                return true;
            case CommandIds.ImportInbox:
                OpenImportInboxCommand.Execute(null);
                return true;
            case CommandIds.MacroActions:
                OpenMacroActionsCommand.Execute(null);
                return true;
            case CommandIds.BatchProcessor:
                OpenBatchProcessorCommand.Execute(null);
                return true;
            case CommandIds.EditStack:
                OpenEditStackCommand.Execute(null);
                return true;
            case CommandIds.ReviewMode:
                ToggleReviewModeCommand.Execute(null);
                return true;
            case CommandIds.Compare:
                StartCompareCommand.Execute(null);
                return true;
            case CommandIds.CompareWith:
                CompareWithCommand.Execute(null);
                return true;
            case CommandIds.Settings:
                SettingsCommand.Execute(null);
                return true;
            default:
                return false;
        }
    }

    private CommandPaletteItem PaletteCommand(string id, ICommand command)
    {
        var definition = _commandShortcuts.GetDefinition(id);
        return new CommandPaletteItem
        {
            CommandId = id,
            Name = definition.Name,
            Shortcut = _commandShortcuts.GetShortcutText(id),
            Category = definition.Category,
            Command = command,
        };
    }

    private List<CommandPaletteItem> BuildCommandPaletteRegistry()
    {
        var view = Strings.CommandPalette_Category_View;
        var edit = Strings.CommandPalette_Category_Edit;
        var file = Strings.CommandPalette_Category_File;
        var tools = Strings.CommandPalette_Category_Tools;
        var sort = Strings.CommandPalette_Category_Sort;
        var help = Strings.CommandPalette_Category_Help;

        var items = new List<CommandPaletteItem>
        {
            // Navigation
            PaletteCommand(CommandIds.Open, OpenCommand),
            PaletteCommand(CommandIds.Next, NextCommand),
            PaletteCommand(CommandIds.Previous, PrevCommand),
            PaletteCommand(CommandIds.First, FirstCommand),
            PaletteCommand(CommandIds.Last, LastCommand),
            PaletteCommand(CommandIds.Refresh, RefreshCommand),

            // View
            PaletteCommand(CommandIds.Filmstrip, ToggleFilmstripCommand),
            PaletteCommand(CommandIds.MetadataHud, ToggleMetadataHudCommand),
            PaletteCommand(CommandIds.Gallery, ToggleGalleryCommand),
            new() { Name = Strings.CommandPalette_Inspector, Category = view, Command = ToggleInspectorCommand },
            PaletteCommand(CommandIds.ExtractText, ExtractTextCommand),
            new() { Name = Strings.CommandPalette_ChannelNormal, Category = view, Command = new RelayCommand(() => ChannelMode = ChannelMode.Normal) },
            new() { Name = Strings.CommandPalette_ChannelRed, Category = view, Command = new RelayCommand(() => ChannelMode = ChannelMode.Red) },
            new() { Name = Strings.CommandPalette_ChannelGreen, Category = view, Command = new RelayCommand(() => ChannelMode = ChannelMode.Green) },
            new() { Name = Strings.CommandPalette_ChannelBlue, Category = view, Command = new RelayCommand(() => ChannelMode = ChannelMode.Blue) },
            new() { Name = Strings.CommandPalette_ChannelAlpha, Category = view, Command = new RelayCommand(() => ChannelMode = ChannelMode.Alpha) },

            // Edit
            new() { Name = Strings.CommandPalette_RotateCw, Category = edit, Command = RotateCwCommand },
            new() { Name = Strings.CommandPalette_RotateCcw, Category = edit, Command = RotateCcwCommand },
            new() { Name = Strings.CommandPalette_Rotate180, Category = edit, Command = Rotate180Command },
            new() { Name = Strings.CommandPalette_FlipH, Category = edit, Command = FlipHorizontalCommand },
            new() { Name = Strings.CommandPalette_FlipV, Category = edit, Command = FlipVerticalCommand },
            PaletteCommand(CommandIds.CropMode, ToggleCropModeCommand),
            PaletteCommand(CommandIds.SelectionMode, ToggleSelectionModeCommand),
            PaletteCommand(CommandIds.Resize, OpenResizeDialogCommand),
            PaletteCommand(CommandIds.Adjustments, OpenAdjustmentsCommand),
            PaletteCommand(CommandIds.Effects, OpenEffectsCommand),
            PaletteCommand(CommandIds.AutoEnhance, AutoEnhanceCommand),
            PaletteCommand(CommandIds.Perspective, OpenPerspectiveCommand),
            PaletteCommand(CommandIds.ExposureBrush, ToggleExposureBrushModeCommand),
            PaletteCommand(CommandIds.RedEye, ToggleRedEyeModeCommand),
            PaletteCommand(CommandIds.Retouch, ToggleRetouchModeCommand),
            new() { Name = Strings.CommandPalette_Annotations, Category = edit, Command = OpenAnnotationsCommand },
            PaletteCommand(CommandIds.ExportWorkbench, OpenExportWorkbenchCommand),

            // File
            PaletteCommand(CommandIds.Delete, DeleteCommand),
            PaletteCommand(CommandIds.Reload, ReloadCommand),
            PaletteCommand(CommandIds.Print, PrintCommand),
            PaletteCommand(CommandIds.SaveCopy, SaveAsCopyCommand),
            new() { Name = Strings.CommandPalette_CopyPath, Category = file, Command = CopyPathCommand },
            new() { Name = Strings.CommandPalette_CopyImage, Category = file, Command = CopyImageCommand },
            new() { Name = Strings.CommandPalette_Reveal, Category = file, Command = RevealCommand },
            new() { Name = Strings.CommandPalette_OpenDefault, Category = file, Command = OpenInDefaultAppCommand },
            new() { Name = Strings.CommandPalette_StripGps, Category = file, Command = StripLocationCommand },
            new() { Name = Strings.CommandPalette_StripDeviceInfo, Category = file, Command = StripDeviceInfoCommand },
            new() { Name = Strings.CommandPalette_StripTimestamps, Category = file, Command = StripTimestampsCommand },
            new() { Name = Strings.CommandPalette_StripSoftware, Category = file, Command = StripSoftwareCommand },
            new() { Name = Strings.CommandPalette_StripAll, Category = file, Command = StripAllMetadataCommand },
            new() { Name = Strings.CommandPalette_ExtractMotionVideo, Category = file, Command = ExtractMotionVideoCommand },
            new() { Name = Strings.CommandPalette_Wallpaper, Category = file, Command = SetAsWallpaperCommand },
            new() { Name = Strings.CommandPalette_CopyToFolder, Category = file, Command = CopyToFolderCommand },
            new() { Name = Strings.CommandPalette_MoveToFolder, Category = file, Command = MoveToFolderCommand },

            // Tools
            PaletteCommand(CommandIds.ReferenceBoard, OpenReferenceBoardCommand),
            PaletteCommand(CommandIds.DuplicateCleanup, OpenDuplicateCleanupCommand),
            PaletteCommand(CommandIds.FileHealthScan, OpenFileHealthScanCommand),
            new() { Name = Strings.CommandPalette_RecoveryCenter, Category = tools, Command = OpenRecoveryCenterCommand },
            new() { Name = Strings.CommandPalette_ModelManager, Category = tools, Command = OpenModelManagerCommand },
            new() { Name = Strings.CommandPalette_SemanticSearch, Category = tools, Command = OpenSemanticSearchCommand },
            PaletteCommand(CommandIds.TagGraph, OpenTagGraphCommand),
            PaletteCommand(CommandIds.ImportInbox, OpenImportInboxCommand),
            new() { Name = Strings.CommandPalette_ImportXmpSidecars, Category = tools, Command = ImportXmpSidecarsCommand },
            PaletteCommand(CommandIds.MacroActions, OpenMacroActionsCommand),
            PaletteCommand(CommandIds.BatchProcessor, OpenBatchProcessorCommand),
            PaletteCommand(CommandIds.EditStack, OpenEditStackCommand),

            // Review
            PaletteCommand(CommandIds.ReviewMode, ToggleReviewModeCommand),

            // Compare
            PaletteCommand(CommandIds.Compare, StartCompareCommand),
            PaletteCommand(CommandIds.CompareWith, CompareWithCommand),

            // Slideshow
            new() { Name = Strings.CommandPalette_ToggleSlideshow, Category = view, Command = ToggleSlideshowCommand },
            new() { Name = Strings.CommandPalette_PauseSlideshow, Category = view, Command = PauseSlideshowCommand },
            new() { Name = Strings.CommandPalette_StopSlideshow, Category = view, Command = StopSlideshowCommand },
            new() { Name = Strings.CommandPalette_SlideshowShuffle, Category = view, Command = ToggleSlideshowShuffleCommand },
            new() { Name = Strings.CommandPalette_SlideshowFaster, Category = view, Command = IncreaseSlideshowIntervalCommand },
            new() { Name = Strings.CommandPalette_SlideshowSlower, Category = view, Command = DecreaseSlideshowIntervalCommand },

            // Sort
            new() { Name = Strings.CommandPalette_SortName, Category = sort, Command = new RelayCommand(() => SetFolderSort(DirectorySortMode.NaturalName)) },
            new() { Name = Strings.CommandPalette_SortNameDesc, Category = sort, Command = new RelayCommand(() => SetFolderSort(DirectorySortMode.NameDescending)) },
            new() { Name = Strings.CommandPalette_SortModifiedNewest, Category = sort, Command = new RelayCommand(() => SetFolderSort(DirectorySortMode.ModifiedNewest)) },
            new() { Name = Strings.CommandPalette_SortModifiedOldest, Category = sort, Command = new RelayCommand(() => SetFolderSort(DirectorySortMode.ModifiedOldest)) },
            new() { Name = Strings.CommandPalette_SortCreatedNewest, Category = sort, Command = new RelayCommand(() => SetFolderSort(DirectorySortMode.CreatedNewest)) },
            new() { Name = Strings.CommandPalette_SortCreatedOldest, Category = sort, Command = new RelayCommand(() => SetFolderSort(DirectorySortMode.CreatedOldest)) },
            new() { Name = Strings.CommandPalette_SortSizeLargest, Category = sort, Command = new RelayCommand(() => SetFolderSort(DirectorySortMode.SizeLargest)) },
            new() { Name = Strings.CommandPalette_SortSizeSmallest, Category = sort, Command = new RelayCommand(() => SetFolderSort(DirectorySortMode.SizeSmallest)) },
            new() { Name = Strings.CommandPalette_SortType, Category = sort, Command = new RelayCommand(() => SetFolderSort(DirectorySortMode.ExtensionThenName)) },

            // Help
            PaletteCommand(CommandIds.Settings, SettingsCommand),
            PaletteCommand(CommandIds.CommandPalette, ToggleCommandPaletteCommand),
            new() { Name = Strings.CommandPalette_About, Category = help, Command = AboutCommand },
            new() { Name = Strings.CommandPalette_CheckUpdates, Category = help, Command = CheckForUpdatesCommand },
            PaletteCommand(CommandIds.Paste, PasteFromClipboardCommand),
        };

        // V20-27: append dynamic "Send to monitor N" entries for each connected display.
        var monitors = MonitorService.GetAllMonitors();
        if (monitors.Count > 1)
        {
            var window = Strings.CommandPalette_Category_Window;
            for (var i = 0; i < monitors.Count; i++)
            {
                var idx = i;
                var m = monitors[i];
                var label = Strings.Format(
                    nameof(Strings.CommandPalette_SendToMonitorFormat),
                    m.DisplayNumber);
                items.Add(new CommandPaletteItem
                {
                    Name = label,
                    Category = window,
                    Command = new RelayCommand(
                        () => RequestSendToMonitor?.Invoke(idx),
                        () => RequestSendToMonitor is not null),
                });
            }
        }

        return items;
    }

    // V20-27: delegate set by MainWindow after construction so the VM can request a window move
    // to a specific monitor index. Null until the window wires it up.
    public Action<int>? RequestSendToMonitor { get; set; }
    public Func<string, string?>? RequestArchivePassword { get; set; }

    /// <summary>
    /// V20-27: rebuilds the palette registry, e.g. after the window wires up the send-to-monitor
    /// delegate so the dynamic monitor entries get a live callback.
    /// </summary>
    public void RefreshCommandPalette()
    {
        ShortcutTexts = _commandShortcuts.GetShortcutTextMap();
        Raise(nameof(PreviousNextShortcutText));
        Raise(nameof(FirstLastShortcutText));
        _commandPaletteRegistry = BuildCommandPaletteRegistry();
        RefreshCommandPaletteItems();
    }

    private string ShortcutPairText(string firstId, string secondId)
        => string.Format(CultureInfo.CurrentCulture, "{0} / {1}", ShortcutText(firstId), ShortcutText(secondId));

    private string ShortcutText(string id)
        => ShortcutTexts.TryGetValue(id, out var shortcut) ? shortcut : string.Empty;

    // V15-07: fullscreen toggled by F11. The view collapses the side panel + floats the toolbar
    // when fullscreen, restores everything on exit.
    private bool _isFullscreen;
    public bool IsFullscreen
    {
        get => _isFullscreen;
        set => Set(ref _isFullscreen, value);
    }

    // V20-31: listen-mode — TCP listener for piped workflows (--listen <port>).
    private ListenService? _listenService;

    private bool _isListening;
    public bool IsListening
    {
        get => _isListening;
        private set
        {
            if (Set(ref _isListening, value))
            {
                Raise(nameof(ListenPortLabel));
                Raise(nameof(ListenPortTooltip));
            }
        }
    }

    private int _listenPort;
    public int ListenPort
    {
        get => _listenPort;
        private set
        {
            if (Set(ref _listenPort, value))
            {
                Raise(nameof(ListenPortLabel));
                Raise(nameof(ListenPortTooltip));
            }
        }
    }

    public string ListenPortLabel => IsListening
        ? Strings.Format(nameof(Strings.ListenMode_PortLabel), ListenPort)
        : "";

    public string ListenPortTooltip => IsListening
        ? Strings.Format(nameof(Strings.ListenMode_Tooltip), ListenPort, _listenService?.SessionToken ?? "")
        : "";

    /// <summary>
    /// V20-31: starts the local TCP listen service. The <paramref name="port"/> is bound
    /// to loopback only (127.0.0.1). Received paths dispatch to the UI thread and open
    /// through the normal <see cref="OpenFile"/> path.
    /// </summary>
    public void StartListenMode(int port)
    {
        if (_listenService is not null) return;

        _listenService = new ListenService(
            path => _uiDispatcher.Invoke(() => OpenFile(path)));
        _listenService.Start(port);
        ListenPort = _listenService.Port;
        IsListening = true;

        _log.LogInformation("V20-31 listen mode started on port {Port}", ListenPort);
        Toast(Strings.Format(nameof(Strings.ListenMode_StartedToast), ListenPort));
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
                Raise(nameof(CanUseOverlayMode));
                Raise(nameof(CanUseCrop));
                Raise(nameof(CanApplyRotationToFile));
                Raise(nameof(CanUseExposureBrush));
                Raise(nameof(CanUseRedEyeCorrection));
                Raise(nameof(CanUseRetouch));
                Raise(nameof(ShowMetadataHud));
                RaiseCompareState();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private bool _isPinnedOverlayMode;
    public bool IsPinnedOverlayMode
    {
        get => _isPinnedOverlayMode;
        set
        {
            if (value && !CanUseOverlayMode)
                value = false;

            if (!Set(ref _isPinnedOverlayMode, value))
                return;

            if (!value && _isOverlayClickThrough)
            {
                _isOverlayClickThrough = false;
                Raise(nameof(IsOverlayClickThrough));
                Raise(nameof(OverlayClickThroughText));
            }

            RaiseOverlayState();
            Toast(value ? Strings.MainToastOverlayOn : Strings.MainToastOverlayOff);
        }
    }

    private bool _isOverlayClickThrough;
    public bool IsOverlayClickThrough
    {
        get => _isOverlayClickThrough;
        set
        {
            if (value && (!IsPinnedOverlayMode || !_overlayExitHotKeyAvailable))
                value = false;

            if (!Set(ref _isOverlayClickThrough, value))
                return;

            Raise(nameof(OverlayClickThroughText));
            Raise(nameof(OverlayStatusText));
            Toast(value ? Strings.MainToastClickThroughOn : Strings.MainToastClickThroughOff);
        }
    }

    private double _overlayOpacity = 0.68;
    public double OverlayOpacity
    {
        get => _overlayOpacity;
        set
        {
            var clamped = Math.Clamp(double.IsNaN(value) ? 0.68 : value, 0.25, 1.0);
            if (!Set(ref _overlayOpacity, clamped))
                return;

            Raise(nameof(OverlayOpacityText));
            Raise(nameof(OverlayStatusText));
        }
    }

    private bool _overlayExitHotKeyAvailable = true;
    public bool OverlayExitHotKeyAvailable => _overlayExitHotKeyAvailable;
    public bool CanUseOverlayMode => CurrentImage is not null && !IsTilePyramidActive && !IsOperationBusy && !IsPeekMode && !IsCompareMode;
    public bool ShowOverlayBanner => IsPinnedOverlayMode;
    public string OverlayModeText => IsPinnedOverlayMode ? Strings.MainOverlayOn : Strings.MainOverlayOff;
    public string OverlayToggleText => IsPinnedOverlayMode ? Strings.MainOverlayTurnOff : Strings.MainOverlayTurnOn;
    public string OverlayClickThroughText => IsOverlayClickThrough ? Strings.MainOverlayClickThroughOn : Strings.MainOverlayClickThroughOff;
    public string OverlayOpacityText => $"{OverlayOpacity:P0}";
    public string OverlayExitText => OverlayExitHotKeyAvailable
        ? $"{OverlayWindowService.ExitHotKeyText} exits overlay"
        : Strings.MainOverlayExitNoHotkey;
    public string OverlayStatusText
    {
        get
        {
            if (!IsPinnedOverlayMode)
                return Strings.MainOverlayStatusIdle;

            if (!OverlayExitHotKeyAvailable)
                return Strings.MainOverlayStatusNoHotkey;

            return IsOverlayClickThrough
                ? $"Click-through is active. Use {OverlayWindowService.ExitHotKeyText} or the taskbar close command to exit."
                : $"Pinned above other windows at {OverlayOpacityText}. Use {OverlayWindowService.ExitHotKeyText}, Exit overlay, or the context menu to exit.";
        }
    }

    private ImageSource? _compareImage;
    public ImageSource? CompareImage
    {
        get => _compareImage;
        private set
        {
            if (Set(ref _compareImage, value))
                RaiseCompareState();
        }
    }

    private string? _comparePath;
    public string? ComparePath
    {
        get => _comparePath;
        private set
        {
            if (Set(ref _comparePath, value))
                RaiseCompareState();
        }
    }

    private bool _isCompareMode;
    public bool IsCompareMode
    {
        get => _isCompareMode;
        private set
        {
            if (Set(ref _isCompareMode, value))
            {
                RaiseCompareState();
                RaiseImageToolState();
            }
        }
    }

    private bool _isCompareOverlayMode;
    public bool IsCompareOverlayMode
    {
        get => _isCompareOverlayMode;
        set
        {
            if (!IsCompareMode && value)
                value = false;

            if (!Set(ref _isCompareOverlayMode, value))
                return;

            RaiseCompareState();
            if (IsCompareMode)
                Toast(value ? Strings.MainToastCompareOverlayOn : Strings.MainToastCompare2UpOn);
        }
    }

    private double _compareOverlayOpacity = 0.5;
    public double CompareOverlayOpacity
    {
        get => _compareOverlayOpacity;
        set
        {
            var clamped = Math.Clamp(double.IsNaN(value) ? 0.5 : value, 0.05, 1.0);
            if (Set(ref _compareOverlayOpacity, clamped))
                RaiseCompareState();
        }
    }

    public bool CanUseCompareMode => HasImage && CurrentImage is not null && !IsTilePyramidActive && !IsOperationBusy && !IsPeekMode;
    public bool CanStartCompareWithNext => CanUseCompareMode && TryGetNextComparePath() is not null;
    public bool CanSwapComparePair => IsCompareMode && CurrentPath is not null && ComparePath is not null && File.Exists(ComparePath);
    public bool ShowCompareMode => IsCompareMode && CurrentImage is not null && !IsTilePyramidActive && CompareImage is not null && !IsPeekMode;
    public string CompareModeText => IsCompareMode ? Strings.MainCompareOn : Strings.MainCompareWithNext;
    public string CompareLayoutText => IsCompareOverlayMode ? Strings.MainCompareOverlay : Strings.MainCompare2Up;
    public string CompareLayoutToggleText => IsCompareOverlayMode ? Strings.MainCompare2Up : Strings.MainCompareOverlay;
    public string CompareOverlayOpacityText => $"{CompareOverlayOpacity:P0}";
    public string ComparePrimaryFileName => CurrentPath is null ? Strings.MainComparePrimaryDefault : Path.GetFileName(CurrentPath);
    public string CompareSecondaryFileName => ComparePath is null ? Strings.MainCompareSecondaryDefault : Path.GetFileName(ComparePath);
    public string CompareStatusText
    {
        get
        {
            if (!IsCompareMode)
                return CanStartCompareWithNext
                    ? Strings.MainCompareStatusCanStart
                    : Strings.MainCompareStatusNeedTwo;

            return $"{ComparePrimaryFileName} vs {CompareSecondaryFileName} - {CompareLayoutText} - B opacity {CompareOverlayOpacityText}";
        }
    }

    public string CurrentFileName => CurrentPath is null ? "" : Path.GetFileName(CurrentPath);
    public string CurrentFolder => CurrentPath is null ? "" : Path.GetDirectoryName(CurrentPath) ?? "";

    // Window title — filename first (Windows convention), app name second, em-dash separator.
    // Falls back to bare "Images" when no file is open.
    public string WindowTitle =>
        CurrentPath is null ? Strings.MainWindowTitleDefault : $"{Path.GetFileName(CurrentPath)} \u2014 {Strings.MainWindowTitleDefault}";

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

    private string _loadErrorTitle = Strings.MainLoadErrorDefault;
    public string LoadErrorTitle { get => _loadErrorTitle; private set => Set(ref _loadErrorTitle, value); }

    private string _loadErrorHelpText = "";
    public string LoadErrorHelpText { get => _loadErrorHelpText; private set => Set(ref _loadErrorHelpText, value); }

    private bool _loadErrorShowsCodecDetails;
    public bool LoadErrorShowsCodecDetails
    {
        get => _loadErrorShowsCodecDetails;
        private set => Set(ref _loadErrorShowsCodecDetails, value);
    }

    private string? _loadErrorStoreActionLabel;
    public string? LoadErrorStoreActionLabel
    {
        get => _loadErrorStoreActionLabel;
        private set
        {
            if (Set(ref _loadErrorStoreActionLabel, value))
                Raise(nameof(HasLoadErrorStoreAction));
        }
    }

    public bool HasLoadErrorStoreAction => !string.IsNullOrWhiteSpace(LoadErrorStoreActionLabel);

    private StoreExtensionService.StoreExtensionInfo? _loadErrorStoreExtension;

    public ICommand InstallStoreExtensionCommand { get; }

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

    public string DropOverlayTitle => IsDropAccepted ? Strings.MainDropAcceptedTitle : Strings.MainDropRejectedTitle;
    public string DropOverlayMessage => IsDropAccepted
        ? Strings.MainDropAcceptedMessage
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
    public double Rotation
    {
        get => _rotation;
        private set
        {
            if (Set(ref _rotation, value))
            {
                Raise(nameof(CanApplyRotationToFile));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

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
            {
                IsSelectionMode = false;
                IsCropMode = false;
                IsExposureBrushMode = false;
                IsRedEyeCorrectionMode = false;
                IsRetouchMode = false;
                ClearCanvasSelection();
                ClearExposureBrushStrokes(showToast: false);
                ClearRedEyeCorrectionMarks(showToast: false);
                ClearRetouchState(showToast: false);
                InspectorStatusText = Strings.MainInspectorReady;
            }

            Raise(nameof(InspectorModeText));
            Raise(nameof(InspectorModeHelpText));
            Raise(nameof(IsCanvasSelectionMode));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string InspectorModeText => IsInspectorMode ? Strings.MainInspectorOn : Strings.MainInspectorOff;
    public string InspectorModeHelpText => IsInspectorMode
        ? Strings.MainInspectorActiveHelp
        : Strings.MainInspectorInactiveHelp;

    private bool _inspectorNearestNeighborPreview;
    public bool InspectorNearestNeighborPreview
    {
        get => _inspectorNearestNeighborPreview;
        set
        {
            if (Set(ref _inspectorNearestNeighborPreview, value))
                Toast(value ? Strings.MainToastNearestNeighborOn : Strings.MainToastHighQualityOn);
        }
    }

    private PixelSample? _inspectorSample;
    private PixelSelection? _inspectorSelection;

    private string _inspectorStatusText = Strings.MainInspectorDefault;
    public string InspectorStatusText
    {
        get => _inspectorStatusText;
        private set => Set(ref _inspectorStatusText, value);
    }

    public bool HasInspectorSample => _inspectorSample is not null;
    public bool HasInspectorSelection => _inspectorSelection is not null;
    public string InspectorCoordinateText => _inspectorSample?.CoordinateText ?? Strings.MainInspectorNoPixel;
    public string InspectorHexText => _inspectorSample?.Hex ?? "#------";
    public string InspectorRgbText => _inspectorSample?.Rgb ?? "RGB --, --, --";
    public string InspectorHsvText => _inspectorSample?.Hsv ?? "HSV --, --%, --%";
    public string InspectorAlphaText => _inspectorSample?.Alpha ?? "A --";
    public string InspectorSelectionText => _inspectorSelection.HasValue
        ? _inspectorSelection.Value.DisplayText
        : Strings.MainInspectorShiftDrag;

    private bool _isSelectionMode;
    public bool IsSelectionMode
    {
        get => _isSelectionMode;
        set
        {
            if (!Set(ref _isSelectionMode, value))
                return;

            if (value)
            {
                IsInspectorMode = false;
                IsCropMode = false;
                IsOcrMode = false;
                IsExposureBrushMode = false;
                IsRedEyeCorrectionMode = false;
                IsRetouchMode = false;
                ClearCropSelection();
                ClearExposureBrushStrokes(showToast: false);
                ClearRedEyeCorrectionMarks(showToast: false);
                ClearRetouchState(showToast: false);
                SelectionStatusText = Strings.MainSelectReady;
            }
            else if (!HasCanvasSelection)
            {
                SelectionStatusText = Strings.MainSelectPaused;
            }

            RaiseSelectionModeState();
        }
    }

    private PixelSelection? _canvasSelection;
    public PixelSelection? CanvasSelection
    {
        get => _canvasSelection;
        private set
        {
            if (Set(ref _canvasSelection, value))
                RaiseSelectionState();
        }
    }

    private string _selectionStatusText = Strings.MainSelectOpen;
    public string SelectionStatusText
    {
        get => _selectionStatusText;
        private set => Set(ref _selectionStatusText, value);
    }

    public bool HasCanvasSelection => CanvasSelection is { Width: > 0, Height: > 0 };
    public bool CanCopySelection => HasCanvasSelection && CurrentImage is BitmapSource && CanUseSelection;
    public bool ShowSelectionOverlay => IsSelectionMode || HasCanvasSelection;
    public string SelectionModeText => IsSelectionMode ? Strings.MainSelectOn : Strings.MainSelectOff;
    public string SelectionModeHelpText => IsSelectionMode
        ? Strings.MainSelectActiveHelp
        : Strings.MainSelectInactiveHelp;
    public string CanvasSelectionText => CanvasSelection?.DisplayText ?? Strings.MainSelectNoSelection;

    private bool _isCropMode;
    public bool IsCropMode
    {
        get => _isCropMode;
        set
        {
            if (!Set(ref _isCropMode, value))
                return;

            if (value)
            {
                IsInspectorMode = false;
                IsSelectionMode = false;
                IsOcrMode = false;
                IsExposureBrushMode = false;
                IsRedEyeCorrectionMode = false;
                IsRetouchMode = false;
                ClearCanvasSelection();
                ClearExposureBrushStrokes(showToast: false);
                ClearRedEyeCorrectionMarks(showToast: false);
                ClearRetouchState(showToast: false);
                CropStatusText = Strings.MainCropReady;
            }
            else if (!HasCropSelection)
            {
                CropStatusText = Strings.MainCropPaused;
            }

            RaiseCropModeState();
        }
    }

    private PixelSelection? _cropSelection;
    public PixelSelection? CropSelection
    {
        get => _cropSelection;
        private set
        {
            if (Set(ref _cropSelection, value))
                RaiseCropSelectionState();
        }
    }

    private string _cropStatusText = Strings.MainCropOpen;
    public string CropStatusText
    {
        get => _cropStatusText;
        private set => Set(ref _cropStatusText, value);
    }

    private static string CropUnavailableStatusText =>
        Strings.MainCropUnavailable;

    public IReadOnlyList<CropAspectPreset> CropAspectPresets { get; } = CropSelectionService.AspectPresets;

    private CropAspectPreset _selectedCropAspectPreset = CropSelectionService.FreeAspectPreset;
    public CropAspectPreset SelectedCropAspectPreset
    {
        get => _selectedCropAspectPreset;
        private set
        {
            if (!Set(ref _selectedCropAspectPreset, value))
                return;

            if (HasCropSelection)
                CropStatusText = $"Aspect set to {CropAspectText}. Drag again to update the crop.";
            else if (IsCropMode)
                CropStatusText = $"Aspect set to {CropAspectText}. Drag on the image to choose a crop.";

            RaiseCropAspectState();
        }
    }

    private string _customCropAspectWidth = "1";
    public string CustomCropAspectWidth
    {
        get => _customCropAspectWidth;
        set
        {
            if (Set(ref _customCropAspectWidth, value))
                RaiseCropAspectState();
        }
    }

    private string _customCropAspectHeight = "1";
    public string CustomCropAspectHeight
    {
        get => _customCropAspectHeight;
        set
        {
            if (Set(ref _customCropAspectHeight, value))
                RaiseCropAspectState();
        }
    }

    public bool HasCropSelection => CropSelection is { Width: > 0, Height: > 0 };
    public bool CanApplyCrop => IsCropMode && HasCropSelection && CanUseCrop;
    public bool ShowCropOverlay => IsCropMode || HasCropSelection;
    public bool IsCanvasSelectionMode => IsInspectorMode || IsSelectionMode || IsCropMode || IsExposureBrushMode || IsRedEyeCorrectionMode || IsRetouchMode;
    public string CropModeText => IsCropMode ? Strings.MainCropOn : Strings.MainCropOff;
    public string CropModeHelpText => IsCropMode
        ? Strings.MainCropActiveHelp
        : Strings.MainCropInactiveHelp;
    public string CropSelectionText => CropSelection?.DisplayText ?? Strings.MainCropNoSelection;
    public string CropAspectText => EffectiveCropAspectPreset?.Label ?? Strings.MainCropCustomRatio;
    public string CropAspectHelpText => EffectiveCropAspectPreset?.Description ?? Strings.MainCropCustomHelp;
    public bool ShowCustomCropAspect => SelectedCropAspectPreset.Id.Equals(CropSelectionService.CustomAspectPresetId, StringComparison.OrdinalIgnoreCase);

    private bool _isExposureBrushMode;
    public bool IsExposureBrushMode
    {
        get => _isExposureBrushMode;
        set
        {
            if (!Set(ref _isExposureBrushMode, value))
                return;

            if (value)
            {
                IsInspectorMode = false;
                IsSelectionMode = false;
                IsCropMode = false;
                IsOcrMode = false;
                IsRedEyeCorrectionMode = false;
                IsRetouchMode = false;
                ClearCanvasSelection();
                ClearRedEyeCorrectionMarks(showToast: false);
                ClearRetouchState(showToast: false);
                ClearCropSelection();
                ExposureBrushStatusText = Strings.MainExposureActiveStatus;
            }
            else if (!HasExposureBrushStrokes)
            {
                ExposureBrushStatusText = "Turn on exposure brush, then paint dodge or burn strokes without changing the source file.";
            }

            RaiseExposureBrushModeState();
        }
    }

    public ObservableCollection<LocalExposureBrushStroke> ExposureBrushStrokes { get; } = [];

    private double _exposureBrushRadius = 48;
    public double ExposureBrushRadius
    {
        get => _exposureBrushRadius;
        set
        {
            var radius = Math.Clamp(double.IsFinite(value) ? value : 48, LocalExposureBrushService.MinRadius, LocalExposureBrushService.MaxRadius);
            if (Set(ref _exposureBrushRadius, radius))
                RaiseExposureBrushSettingsState();
        }
    }

    private double _exposureBrushStrength = 28;
    public double ExposureBrushStrength
    {
        get => _exposureBrushStrength;
        set
        {
            var strength = Math.Clamp(double.IsFinite(value) ? value : 28, 1, 100);
            if (Set(ref _exposureBrushStrength, strength))
                RaiseExposureBrushSettingsState();
        }
    }

    private bool _isExposureBrushBurn;
    public bool IsExposureBrushBurn
    {
        get => _isExposureBrushBurn;
        private set
        {
            if (Set(ref _isExposureBrushBurn, value))
                RaiseExposureBrushSettingsState();
        }
    }

    private string _exposureBrushStatusText = Strings.MainExposureInactiveStatus;
    public string ExposureBrushStatusText
    {
        get => _exposureBrushStatusText;
        private set => Set(ref _exposureBrushStatusText, value);
    }

    public string ExposureBrushModeText => IsExposureBrushMode ? Strings.MainExposureOn : Strings.MainExposureOff;
    public string ExposureBrushModeHelpText => IsExposureBrushMode
        ? Strings.MainExposureActiveHelp
        : Strings.MainExposureInactiveHelp;
    public string ExposureBrushToneText => IsExposureBrushBurn ? Strings.MainExposureBurnText : Strings.MainExposureDodgeText;
    public string ExposureBrushRadiusText => string.Create(CultureInfo.InvariantCulture, $"{ExposureBrushRadius:0} px");
    public string ExposureBrushStrengthText => string.Create(CultureInfo.InvariantCulture, $"{ExposureBrushStrength:0}%");
    public string ExposureBrushStrokeText => HasExposureBrushStrokes
        ? LocalExposureBrushService.CreateSummary(ExposureBrushStrokes.ToList())
        : Strings.MainExposureNoStrokes;
    public bool HasExposureBrushStrokes => ExposureBrushStrokes.Count > 0;
    public bool CanApplyExposureBrush => IsExposureBrushMode && HasExposureBrushStrokes && CanUseExposureBrush;
    public bool ShowExposureBrushOverlay => IsExposureBrushMode || HasExposureBrushStrokes;
    private double ExposureBrushSignedStrength => (IsExposureBrushBurn ? -1 : 1) * Math.Clamp(ExposureBrushStrength / 100, 0.01, 1);

    private bool _isRedEyeCorrectionMode;
    public bool IsRedEyeCorrectionMode
    {
        get => _isRedEyeCorrectionMode;
        set
        {
            if (!Set(ref _isRedEyeCorrectionMode, value))
                return;

            if (value)
            {
                IsInspectorMode = false;
                IsSelectionMode = false;
                IsCropMode = false;
                IsExposureBrushMode = false;
                IsRetouchMode = false;
                IsOcrMode = false;
                ClearCanvasSelection();
                ClearCropSelection();
                ClearExposureBrushStrokes(showToast: false);
                ClearRetouchState(showToast: false);
                RedEyeCorrectionStatusText = Strings.MainRedEyeActiveStatus;
            }
            else if (!HasRedEyeCorrectionMarks)
            {
                RedEyeCorrectionStatusText = Strings.MainRedEyeInactiveStatus;
            }

            RaiseRedEyeCorrectionModeState();
        }
    }

    public ObservableCollection<RedEyeCorrectionMark> RedEyeCorrectionMarks { get; } = [];

    private double _redEyeCorrectionRadius = 24;
    public double RedEyeCorrectionRadius
    {
        get => _redEyeCorrectionRadius;
        set
        {
            var radius = Math.Clamp(double.IsFinite(value) ? value : 24, RedEyeCorrectionService.MinRadius, RedEyeCorrectionService.MaxRadius);
            if (Set(ref _redEyeCorrectionRadius, radius))
                RaiseRedEyeCorrectionSettingsState();
        }
    }

    private double _redEyeCorrectionStrength = 85;
    public double RedEyeCorrectionStrength
    {
        get => _redEyeCorrectionStrength;
        set
        {
            var strength = Math.Clamp(double.IsFinite(value) ? value : 85, 5, 100);
            if (Set(ref _redEyeCorrectionStrength, strength))
                RaiseRedEyeCorrectionSettingsState();
        }
    }

    private double _redEyeCorrectionThreshold = 35;
    public double RedEyeCorrectionThreshold
    {
        get => _redEyeCorrectionThreshold;
        set
        {
            var threshold = Math.Clamp(double.IsFinite(value) ? value : 35, 0, 100);
            if (Set(ref _redEyeCorrectionThreshold, threshold))
                RaiseRedEyeCorrectionSettingsState();
        }
    }

    private string _redEyeCorrectionStatusText = Strings.MainRedEyeInactiveStatus;
    public string RedEyeCorrectionStatusText
    {
        get => _redEyeCorrectionStatusText;
        private set => Set(ref _redEyeCorrectionStatusText, value);
    }

    public string RedEyeCorrectionModeText => IsRedEyeCorrectionMode ? Strings.MainRedEyeOn : Strings.MainRedEyeOff;
    public string RedEyeCorrectionModeHelpText => IsRedEyeCorrectionMode
        ? Strings.MainRedEyeActiveHelp
        : Strings.MainRedEyeInactiveHelp;
    public string RedEyeCorrectionRadiusText => string.Create(CultureInfo.InvariantCulture, $"{RedEyeCorrectionRadius:0} px");
    public string RedEyeCorrectionStrengthText => string.Create(CultureInfo.InvariantCulture, $"{RedEyeCorrectionStrength:0}%");
    public string RedEyeCorrectionThresholdText => string.Create(CultureInfo.InvariantCulture, $"{RedEyeCorrectionThreshold:0}%");
    public string RedEyeCorrectionMarkText => HasRedEyeCorrectionMarks
        ? RedEyeCorrectionService.CreateSummary(RedEyeCorrectionMarks.ToList())
        : Strings.MainRedEyeNoMarks;
    public bool HasRedEyeCorrectionMarks => RedEyeCorrectionMarks.Count > 0;
    public bool CanApplyRedEyeCorrection => IsRedEyeCorrectionMode && HasRedEyeCorrectionMarks && CanUseRedEyeCorrection;
    public bool ShowRedEyeCorrectionOverlay => IsRedEyeCorrectionMode || HasRedEyeCorrectionMarks;
    private double RedEyeCorrectionNormalizedStrength => Math.Clamp(RedEyeCorrectionStrength / 100, RedEyeCorrectionService.MinStrength, RedEyeCorrectionService.MaxStrength);
    private double RedEyeCorrectionNormalizedThreshold => Math.Clamp(RedEyeCorrectionThreshold / 100, RedEyeCorrectionService.MinThreshold, RedEyeCorrectionService.MaxThreshold);

    private bool _isRetouchMode;
    public bool IsRetouchMode
    {
        get => _isRetouchMode;
        set
        {
            if (!Set(ref _isRetouchMode, value))
                return;

            if (value)
            {
                IsInspectorMode = false;
                IsSelectionMode = false;
                IsCropMode = false;
                IsExposureBrushMode = false;
                IsRedEyeCorrectionMode = false;
                IsOcrMode = false;
                ClearCanvasSelection();
                ClearCropSelection();
                ClearExposureBrushStrokes(showToast: false);
                ClearRedEyeCorrectionMarks(showToast: false);
                RetouchStatusText = HasRetouchSource
                    ? "Paint over the target area. Alt-click picks a new source; Enter applies."
                    : "Alt-click or click once to pick a source, then paint the target area.";
            }
            else if (!HasRetouchStrokes)
            {
                RetouchStatusText = Strings.MainRetouchInactiveStatus;
            }

            RaiseRetouchModeState();
        }
    }

    public ObservableCollection<RetouchBrushStroke> RetouchStrokes { get; } = [];

    private PixelCoordinate? _retouchSource;
    private PixelCoordinate? _retouchStrokeSourceAnchor;
    private PixelCoordinate? _retouchStrokeTargetAnchor;
    public bool HasRetouchSource => _retouchSource is not null;
    public string RetouchSourceText => _retouchSource is { } source
        ? $"Source {source.X}, {source.Y}"
        : Strings.MainRetouchNoSource;

    private double _retouchRadius = 28;
    public double RetouchRadius
    {
        get => _retouchRadius;
        set
        {
            var radius = Math.Clamp(double.IsFinite(value) ? value : 28, RetouchBrushService.MinRadius, RetouchBrushService.MaxRadius);
            if (Set(ref _retouchRadius, radius))
                RaiseRetouchSettingsState();
        }
    }

    private double _retouchStrength = 85;
    public double RetouchStrength
    {
        get => _retouchStrength;
        set
        {
            var strength = Math.Clamp(double.IsFinite(value) ? value : 85, 5, 100);
            if (Set(ref _retouchStrength, strength))
                RaiseRetouchSettingsState();
        }
    }

    private bool _isRetouchHealing;
    public bool IsRetouchHealing
    {
        get => _isRetouchHealing;
        private set
        {
            if (Set(ref _isRetouchHealing, value))
                RaiseRetouchSettingsState();
        }
    }

    private string _retouchStatusText = Strings.MainRetouchInactiveStatus;
    public string RetouchStatusText
    {
        get => _retouchStatusText;
        private set => Set(ref _retouchStatusText, value);
    }

    public string RetouchModeText => IsRetouchMode ? Strings.MainRetouchOn : Strings.MainRetouchOff;
    public string RetouchModeHelpText => IsRetouchMode
        ? Strings.MainRetouchActiveHelp
        : Strings.MainRetouchInactiveHelp;
    public string RetouchBrushModeText => IsRetouchHealing ? Strings.MainRetouchHealingBrush : Strings.MainRetouchCloneStamp;
    public string RetouchRadiusText => string.Create(CultureInfo.InvariantCulture, $"{RetouchRadius:0} px");
    public string RetouchStrengthText => string.Create(CultureInfo.InvariantCulture, $"{RetouchStrength:0}%");
    public string RetouchStrokeText => HasRetouchStrokes
        ? RetouchBrushService.CreateSummary(RetouchStrokes.ToList())
        : Strings.MainRetouchNoStrokes;
    public bool HasRetouchStrokes => RetouchStrokes.Count > 0;
    public bool CanApplyRetouch => IsRetouchMode && HasRetouchStrokes && CanUseRetouch;
    public bool ShowRetouchOverlay => IsRetouchMode || HasRetouchStrokes;
    private double RetouchNormalizedStrength => Math.Clamp(RetouchStrength / 100, RetouchBrushService.MinStrength, RetouchBrushService.MaxStrength);

    // -------------------- Slideshow (V30-33) --------------------

    private DispatcherTimer? _slideshowTimer;
    private Random? _slideshowRandom;
    private bool _slideshowHoverPaused;

    private bool _isSlideshowActive;
    public bool IsSlideshowActive
    {
        get => _isSlideshowActive;
        private set
        {
            if (Set(ref _isSlideshowActive, value))
            {
                Raise(nameof(SlideshowStatusText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private bool _isSlideshowPaused;
    public bool IsSlideshowPaused
    {
        get => _isSlideshowPaused;
        private set
        {
            if (Set(ref _isSlideshowPaused, value))
                Raise(nameof(SlideshowStatusText));
        }
    }

    private int _slideshowIntervalSeconds = 5;
    public int SlideshowIntervalSeconds
    {
        get => _slideshowIntervalSeconds;
        set
        {
            if (value < 1) value = 1;
            if (value > 60) value = 60;
            if (Set(ref _slideshowIntervalSeconds, value))
            {
                Raise(nameof(SlideshowStatusText));
                if (IsSlideshowActive && !IsSlideshowPaused)
                    RestartSlideshowTimer();
            }
        }
    }

    private bool _slideshowShuffle;
    public bool SlideshowShuffle
    {
        get => _slideshowShuffle;
        set => Set(ref _slideshowShuffle, value);
    }

    private bool _slideshowLoop = true;
    public bool SlideshowLoop
    {
        get => _slideshowLoop;
        set => Set(ref _slideshowLoop, value);
    }

    public string SlideshowStatusText => IsSlideshowActive
        ? (IsSlideshowPaused
            ? Strings.Slideshow_Paused
            : string.Format(Strings.Slideshow_Playing, SlideshowIntervalSeconds))
        : "";

    public ICommand ToggleSlideshowCommand { get; }
    public ICommand StartSlideshowCommand { get; }
    public ICommand StopSlideshowCommand { get; }
    public ICommand PauseSlideshowCommand { get; }
    public ICommand IncreaseSlideshowIntervalCommand { get; }
    public ICommand DecreaseSlideshowIntervalCommand { get; }
    public ICommand ToggleSlideshowShuffleCommand { get; }

    private void StartSlideshow()
    {
        if (!HasImage) return;
        IsSlideshowActive = true;
        IsSlideshowPaused = false;
        _slideshowHoverPaused = false;
        _slideshowRandom = SlideshowShuffle ? new Random() : null;
        RestartSlideshowTimer();
        Toast(Strings.MainToastSlideshowStarted);
    }

    public void StopSlideshow()
    {
        if (!IsSlideshowActive) return;
        _slideshowTimer?.Stop();
        _slideshowTimer = null;
        IsSlideshowActive = false;
        IsSlideshowPaused = false;
        _slideshowHoverPaused = false;
        Toast(Strings.MainToastSlideshowStopped);
    }

    private void ToggleSlideshow()
    {
        if (IsSlideshowActive) StopSlideshow();
        else StartSlideshow();
    }

    public void PauseResumeSlideshow()
    {
        if (!IsSlideshowActive) return;
        IsSlideshowPaused = !IsSlideshowPaused;
        if (IsSlideshowPaused)
        {
            _slideshowTimer?.Stop();
            Toast(Strings.MainToastSlideshowPaused);
        }
        else
        {
            RestartSlideshowTimer();
            Toast(Strings.MainToastSlideshowResumed);
        }
    }

    /// <summary>
    /// V30-33: temporarily pauses the slideshow timer when the mouse hovers over the
    /// slideshow indicator chip. Resumes on mouse leave only if the pause was hover-initiated.
    /// </summary>
    public void SlideshowHoverPause()
    {
        if (!IsSlideshowActive || IsSlideshowPaused) return;
        _slideshowHoverPaused = true;
        _slideshowTimer?.Stop();
    }

    public void SlideshowHoverResume()
    {
        if (!IsSlideshowActive || !_slideshowHoverPaused) return;
        _slideshowHoverPaused = false;
        if (!IsSlideshowPaused)
            RestartSlideshowTimer();
    }

    private void ToggleSlideshowShuffle()
    {
        SlideshowShuffle = !SlideshowShuffle;
        _slideshowRandom = SlideshowShuffle ? new Random() : null;
        Toast(SlideshowShuffle ? Strings.MainToastSlideshowShuffleOn : Strings.MainToastSlideshowShuffleOff);
    }

    private void RestartSlideshowTimer()
    {
        _slideshowTimer?.Stop();
        _slideshowTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_slideshowIntervalSeconds)
        };
        _slideshowTimer.Tick += SlideshowTimer_Tick;
        _slideshowTimer.Start();
    }

    private async void SlideshowTimer_Tick(object? sender, EventArgs e)
    {
        if (_isDisposed || !IsSlideshowActive || IsSlideshowPaused)
            return;

        if (_slideshowShuffle && _slideshowRandom is not null)
        {
            var count = _nav.Files.Count;
            if (count > 1)
            {
                var nextIndex = _slideshowRandom.Next(count);
                while (nextIndex == _nav.CurrentIndex && count > 1)
                    nextIndex = _slideshowRandom.Next(count);

                // Navigate by finding the path at the target index and opening it
                var targetPath = _nav.Files[nextIndex];
                _nav.Open(targetPath);
                ResetPageState();
                await LoadCurrentWithOperationStatusAsync(
                    Strings.MainOpLoadingNext,
                    BuildDecodeOperationDetail(targetPath));
            }
        }
        else
        {
            if (_nav.CurrentIndex >= _nav.Files.Count - 1)
            {
                if (_slideshowLoop)
                {
                    if (!_nav.MoveFirst()) return;
                }
                else
                {
                    StopSlideshow();
                    return;
                }
            }
            else
            {
                if (!_nav.MoveNext()) return;
            }

            ResetPageState();
            await LoadCurrentWithOperationStatusAsync(
                Strings.MainOpLoadingNext,
                BuildDecodeOperationDetail(_nav.CurrentPath ?? ""));
        }
    }

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
        ? Strings.MainExtensionUnlockedHelp
        : Strings.MainExtensionLockedHelp;

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
                return Strings.MainRenameUnsupportedExt;

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
        RenameStatusKind.Pending => Strings.MainRenameUnsaved,
        RenameStatusKind.Saved => Strings.MainRenameSaved,
        RenameStatusKind.Conflict => Strings.MainRenameConflict,
        RenameStatusKind.Error => Strings.MainRenameError,
        _ when HasImage => Strings.MainRenameCurrent,
        _ => Strings.MainRenameOpenImage
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

    public ObservableCollection<string> RecentTransferFolders { get; } = new();

    private void RefreshRecentTransferFolders()
    {
        var fresh = _settings.GetRecentTransferFolders();
        RecentTransferFolders.Clear();
        foreach (var f in fresh) RecentTransferFolders.Add(f);
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
                Toast(value ? Strings.MainToastCleanScanOn : Strings.MainToastCleanScanOff);
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
                Toast(value ? Strings.MainToastTwoPageOn : Strings.MainToastTwoPageOff);
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
        ? Strings.MainMetadataHudHide
        : Strings.MainMetadataHudShow;

    private void ToggleMetadataHud()
    {
        if (!CanToggleMetadataHud) return;

        IsMetadataHudVisible = !IsMetadataHudVisible;
        Toast(IsMetadataHudVisible ? Strings.MainToastMetadataHudOn : Strings.MainToastMetadataHudOff);
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

    public ObservableCollection<MetadataFact> ColorAnalysisRows => _colorAnalysis.Rows;

    public bool IsColorAnalysisLoading => _colorAnalysis.IsLoading;

    public string ColorAnalysisStatusText => _colorAnalysis.StatusText;

    public string ColorAnalysisWarningText => _colorAnalysis.WarningText;

    public bool ShowColorAnalysisPanel =>
        ColorAnalysisRows.Count > 0 ||
        !string.IsNullOrWhiteSpace(ColorAnalysisStatusText) ||
        !string.IsNullOrWhiteSpace(ColorAnalysisWarningText);

    private void RefreshColorAnalysis(string path)
    {
        _colorAnalysis.Refresh(path);
    }

    private void ClearColorAnalysis()
    {
        _colorAnalysis.Clear();
    }

    private void RaiseColorAnalysisState()
    {
        Raise(nameof(IsColorAnalysisLoading));
        Raise(nameof(ColorAnalysisStatusText));
        Raise(nameof(ColorAnalysisWarningText));
        Raise(nameof(ShowColorAnalysisPanel));
    }

    // -------------------- C2PA content credentials --------------------

    public C2paInspectionResult? C2paResult => _c2paInspection.Result;

    public bool IsC2paLoading => _c2paInspection.IsLoading;

    public bool HasC2paCredentials => _c2paInspection.HasCredentials;

    public string C2paTrustBadgeText => _c2paInspection.TrustBadgeText;

    public string C2paTrustBadgeTooltip => _c2paInspection.TrustBadgeTooltip;

    public string C2paStatusText => _c2paInspection.StatusText;

    public bool ShowC2paPanel => HasC2paCredentials || IsC2paLoading;

    public string C2paClaimGeneratorText
    {
        get
        {
            if (C2paResult?.Claims is not { Count: > 0 } claims) return "";
            var active = C2paResult.ActiveManifestLabel is not null
                ? claims.FirstOrDefault(c => c.Label == C2paResult.ActiveManifestLabel) ?? claims[^1]
                : claims[^1];
            return active.ClaimGenerator ?? "";
        }
    }

    public string C2paSignatureDateText
    {
        get
        {
            if (C2paResult?.Claims is not { Count: > 0 } claims) return "";
            var active = C2paResult.ActiveManifestLabel is not null
                ? claims.FirstOrDefault(c => c.Label == C2paResult.ActiveManifestLabel) ?? claims[^1]
                : claims[^1];
            return active.SignatureDate ?? "";
        }
    }

    public string C2paAssertionsSummaryText
    {
        get
        {
            if (C2paResult?.Claims is not { Count: > 0 } claims) return "";
            var active = C2paResult.ActiveManifestLabel is not null
                ? claims.FirstOrDefault(c => c.Label == C2paResult.ActiveManifestLabel) ?? claims[^1]
                : claims[^1];
            if (active.Assertions.Count == 0) return "";
            return string.Join("; ", active.Assertions.Select(a => a.Summary).Distinct());
        }
    }

    public string C2paIngredientsSummaryText
    {
        get
        {
            if (C2paResult?.Claims is not { Count: > 0 } claims) return "";
            var active = C2paResult.ActiveManifestLabel is not null
                ? claims.FirstOrDefault(c => c.Label == C2paResult.ActiveManifestLabel) ?? claims[^1]
                : claims[^1];
            if (active.Ingredients.Count == 0) return "";
            return string.Join(", ", active.Ingredients.Select(i =>
                $"{i.Title} ({i.Relationship})"));
        }
    }

    private void RefreshC2paInspection(string path) => _c2paInspection.Refresh(path);

    private void ClearC2paInspection() => _c2paInspection.Clear();

    private void RaiseC2paInspectionState()
    {
        Raise(nameof(IsC2paLoading));
        Raise(nameof(HasC2paCredentials));
        Raise(nameof(C2paTrustBadgeText));
        Raise(nameof(C2paTrustBadgeTooltip));
        Raise(nameof(C2paStatusText));
        Raise(nameof(ShowC2paPanel));
        Raise(nameof(C2paResult));
        Raise(nameof(C2paClaimGeneratorText));
        Raise(nameof(C2paSignatureDateText));
        Raise(nameof(C2paAssertionsSummaryText));
        Raise(nameof(C2paIngredientsSummaryText));
    }

    // -------------------- Folder preview strip --------------------

    public ObservableCollection<FolderPreviewItem> FolderPreviewItems => _folderPreview.Items;
    public ObservableCollection<FolderPreviewItem> GalleryItems { get; } = [];
    private IReadOnlyList<AssetSmartFilterItem> _gallerySmartFilterIndex = [];
    private string _gallerySmartFilterSignature = "";
    private string _galleryFilterSummaryText = "";
    private readonly Stack<ReviewLabelUndoEntry> _reviewUndo = new();
    private ReviewLabelState _currentReviewState = new(null, ReviewLabelKind.None, "");

    private bool _isGalleryOpen;
    public bool IsGalleryOpen
    {
        get => _isGalleryOpen;
        set
        {
            if (!Set(ref _isGalleryOpen, value)) return;

            if (value)
            {
                RefreshGalleryItems();
                SelectedGalleryItem = CurrentFolderPreviewItem ?? GalleryItems.FirstOrDefault() ?? FolderPreviewItems.FirstOrDefault();
            }

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
    public string GalleryToggleTooltip => IsGalleryOpen ? Strings.MainGalleryCloseTooltip : Strings.MainGalleryOpenTooltip;
    public string GalleryStatusText
    {
        get
        {
            var total = FolderPreviewItems.Count;
            var smartSummary = string.IsNullOrWhiteSpace(GalleryFilterSummaryText)
                ? ""
                : $" · {GalleryFilterSummaryText}";

            if (HasGalleryFilter)
                return $"{GalleryItems.Count} of {total} {(total == 1 ? "item" : "items")} · {FolderSortLabel}{smartSummary}";

            return $"{total} {(total == 1 ? "item" : "items")} · {FolderSortLabel}";
        }
    }
    public bool HasGalleryFilter => !string.IsNullOrWhiteSpace(GalleryFilterText);
    public bool ShowGalleryFilterEmpty => ShowGallery && HasGalleryFilter && GalleryItems.Count == 0;
    public string GalleryFilterSummaryText => _galleryFilterSummaryText;
    public string GalleryFilterTooltip => Strings.MainGalleryFilterTooltip;

    private bool _isReviewMode;
    public bool IsReviewMode
    {
        get => _isReviewMode;
        set
        {
            if (!Set(ref _isReviewMode, value)) return;
            RaiseReviewState();
            Toast(value ? Strings.MainToastReviewModeOn : Strings.MainToastReviewModeOff);
        }
    }

    public bool CanUseReviewLabels => HasImage && !IsArchiveBook && !IsPeekMode && !IsOperationBusy;
    public bool CanUndoReviewLabel => _reviewUndo.Count > 0;
    public string ReviewModeText => IsReviewMode ? Strings.MainReviewOn : Strings.MainReviewOff;
    public string ReviewRatingText => _currentReviewState.RatingText;
    public string ReviewLabelText => _currentReviewState.LabelText;
    public string ReviewStatusText => $"{ReviewRatingText} · {ReviewLabelText}";
    public bool IsReviewPick => _currentReviewState.Label == ReviewLabelKind.Pick;
    public bool IsReviewReject => _currentReviewState.Label == ReviewLabelKind.Reject;

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
            Raise(nameof(GalleryFilterSummaryText));
            CommandManager.InvalidateRequerySuggested();
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

    public string FilmstripToggleTooltip => IsFilmstripVisible ? Strings.MainFilmstripHideTooltip : Strings.MainFilmstripShowTooltip;

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
        Toast(IsFilmstripVisible ? Strings.MainToastFilmstripShown : Strings.MainToastFilmstripHidden);
    }

    private void SetFolderSort(object? parameter)
    {
        if (!DirectorySortModeInfo.TryParseCommandParameter(parameter, out var mode)) return;
        if (!_nav.SetSortMode(mode)) return;

        _settings.SetString(Keys.ViewerSortMode, mode.ToString());
        RaiseFolderPreviewState();
        Toast($"Sorted by {DirectorySortModeInfo.DisplayName(mode)}");
    }

    private void RestorePersistedSortMode()
    {
        var raw = _settings.GetString(Keys.ViewerSortMode);
        if (raw is not null && Enum.TryParse<DirectorySortMode>(raw, ignoreCase: true, out var mode))
            _nav.SetSortMode(mode);
    }

    private void ToggleGallery()
    {
        if (IsGalleryOpen)
        {
            IsGalleryOpen = false;
            Toast(Strings.MainToastGalleryClosed);
            return;
        }

        if (FolderPreviewItems.Count == 0)
        {
            Toast(Strings.MainToastGalleryOpenFolder);
            return;
        }

        IsGalleryOpen = true;
        Toast(Strings.MainToastGalleryOpen);
    }

    private void OpenSelectedGalleryItem()
    {
        OpenPreviewItem(SelectedGalleryItem);
    }

    private void ApplyGallerySmartFilter(object? parameter)
    {
        if (parameter is not string token || string.IsNullOrWhiteSpace(token))
            return;

        var parts = GalleryFilterText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var existingIndex = parts.FindIndex(part => part.Equals(token, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
            parts.RemoveAt(existingIndex);
        else
            parts.Add(token);

        GalleryFilterText = string.Join(' ', parts);
    }

    private void SetReviewRating(object? parameter)
    {
        if (!CanUseReviewLabels || CurrentPath is null)
            return;

        int? rating = null;
        if (parameter is int numeric)
            rating = Math.Clamp(numeric, 0, 5);
        else if (parameter is string raw && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            rating = Math.Clamp(parsed, 0, 5);

        ApplyReviewMutation(CurrentPath, _reviewLabels.SetRating(CurrentPath, rating));
    }

    private void SetReviewLabel(ReviewLabelKind label)
    {
        if (!CanUseReviewLabels || CurrentPath is null)
            return;

        ApplyReviewMutation(CurrentPath, _reviewLabels.SetLabel(CurrentPath, label));
    }

    private void ApplyReviewMutation(string path, ReviewLabelMutationResult result)
    {
        if (!result.Success)
        {
            Toast(result.Message);
            return;
        }

        _reviewUndo.Push(new ReviewLabelUndoEntry(path, result.Previous));
        _currentReviewState = result.Current;
        RaiseReviewState();
        InvalidateGallerySmartFilterIndex();
        Toast(result.Message);
    }

    private void UndoReviewLabel()
    {
        if (_reviewUndo.Count == 0)
            return;

        var entry = _reviewUndo.Pop();
        var result = _reviewLabels.Restore(entry.Path, entry.State);
        if (!result.Success)
        {
            Toast(result.Message);
            return;
        }

        if (CurrentPath is not null && string.Equals(Path.GetFullPath(CurrentPath), Path.GetFullPath(entry.Path), StringComparison.OrdinalIgnoreCase))
            _currentReviewState = result.Current;
        RaiseReviewState();
        InvalidateGallerySmartFilterIndex();
        Toast(Strings.MainToastReviewUndone);
    }

    private void RefreshReviewState(string? path)
    {
        _currentReviewState = path is not null && File.Exists(path)
            ? _reviewLabels.ReadState(path)
            : new ReviewLabelState(null, ReviewLabelKind.None, "");
        RaiseReviewState();
    }

    private void RaiseReviewState()
    {
        Raise(nameof(IsReviewMode));
        Raise(nameof(CanUseReviewLabels));
        Raise(nameof(CanUndoReviewLabel));
        Raise(nameof(ReviewModeText));
        Raise(nameof(ReviewRatingText));
        Raise(nameof(ReviewLabelText));
        Raise(nameof(ReviewStatusText));
        Raise(nameof(IsReviewPick));
        Raise(nameof(IsReviewReject));
        CommandManager.InvalidateRequerySuggested();
    }

    private void InvalidateGallerySmartFilterIndex()
    {
        _gallerySmartFilterSignature = "";
        if (IsGalleryOpen || HasGalleryFilter)
            RefreshGalleryItems();
        Raise(nameof(GalleryStatusText));
        Raise(nameof(GalleryFilterSummaryText));
    }

    private void RaiseFolderPreviewState()
    {
        if (IsGalleryOpen || HasGalleryFilter)
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
        EnsureGallerySmartFilterIndex();

        IEnumerable<FolderPreviewItem> visible;
        if (string.IsNullOrEmpty(filter))
        {
            visible = FolderPreviewItems;
            SetGallerySmartFilterSummary("");
        }
        else
        {
            var result = AssetSmartFilterService.Filter(_gallerySmartFilterIndex, filter);
            var visiblePaths = result.Items
                .Select(item => item.Path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            visible = FolderPreviewItems.Where(item => visiblePaths.Contains(item.Path));
            SetGallerySmartFilterSummary(result.Summary);
        }

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

    private void EnsureGallerySmartFilterIndex()
    {
        var paths = FolderPreviewItems.Select(item => item.Path).ToArray();
        var signature = string.Join('\u001f', paths);
        if (signature.Equals(_gallerySmartFilterSignature, StringComparison.Ordinal))
            return;

        _gallerySmartFilterIndex = AssetSmartFilterService.BuildIndex(paths);
        _gallerySmartFilterSignature = signature;
    }

    private void SetGallerySmartFilterSummary(string summary)
    {
        if (_galleryFilterSummaryText.Equals(summary, StringComparison.Ordinal))
            return;

        _galleryFilterSummaryText = summary;
        Raise(nameof(GalleryFilterSummaryText));
        Raise(nameof(GalleryStatusText));
    }

    private void SyncFolderPreviewSecondaryStatus()
    {
        if (_folderPreview.HasThumbnailFailures)
        {
            ShowSecondaryStatus(
                Strings.MainSecondaryThumbnailFailure,
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
                Toast(Strings.MainRenameUndoFailed);
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
    public ICommand ApplyRotationToFileCommand { get; }
    public ICommand ToggleInspectorCommand { get; }
    public ICommand ToggleSelectionModeCommand { get; }
    public ICommand CopySelectionCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand CancelSelectionCommand { get; }
    public ICommand ToggleCropModeCommand { get; }
    public ICommand ApplyCropCommand { get; }
    public ICommand CancelCropCommand { get; }
    public ICommand SetCropAspectPresetCommand { get; }
    public ICommand OpenResizeDialogCommand { get; }
    public ICommand OpenAdjustmentsCommand { get; }
    public ICommand OpenEffectsCommand { get; }
    public ICommand AutoEnhanceCommand { get; }
    public ICommand OpenAnnotationsCommand { get; }
    public ICommand OpenPerspectiveCommand { get; }
    public ICommand ToggleExposureBrushModeCommand { get; }
    public ICommand ApplyExposureBrushCommand { get; }
    public ICommand CancelExposureBrushCommand { get; }
    public ICommand ClearExposureBrushCommand { get; }
    public ICommand SetExposureBrushModeCommand { get; }
    public ICommand ToggleRedEyeModeCommand { get; }
    public ICommand ApplyRedEyeCorrectionCommand { get; }
    public ICommand CancelRedEyeCorrectionCommand { get; }
    public ICommand ClearRedEyeCorrectionCommand { get; }
    public ICommand ToggleRetouchModeCommand { get; }
    public ICommand ApplyRetouchCommand { get; }
    public ICommand CancelRetouchCommand { get; }
    public ICommand ClearRetouchCommand { get; }
    public ICommand SetRetouchModeCommand { get; }
    public ICommand ClearRetouchSourceCommand { get; }
    public ICommand ToggleInpaintModeCommand { get; }
    public ICommand ApplyInpaintCommand { get; }
    public ICommand CancelInpaintCommand { get; }
    public ICommand ClearInpaintMaskCommand { get; }
    public ICommand CopyInspectorHexCommand { get; }
    public ICommand CopyInspectorRgbCommand { get; }
    public ICommand CopyInspectorHsvCommand { get; }
    public ICommand CopyInspectorSummaryCommand { get; }
    public ICommand ClearInspectorSelectionCommand { get; }
    public ICommand ToggleAnimationPlaybackCommand { get; }
    public ICommand PreviousAnimationFrameCommand { get; }
    public ICommand NextAnimationFrameCommand { get; }
    public ICommand FirstAnimationFrameCommand { get; }
    public ICommand LastAnimationFrameCommand { get; }
    public ICommand SetAnimationFrameCommand { get; }
    public ICommand CopyAnimationFrameCommand { get; }
    public ICommand ExportAnimationFrameCommand { get; }
    public ICommand FlipHorizontalCommand { get; }
    public ICommand FlipVerticalCommand { get; }
    public ICommand RevealCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand CopyImageCommand { get; }
    public ICommand CopyImageAndPathCommand { get; }
    public ICommand CopyToFolderCommand { get; }
    public ICommand MoveToFolderCommand { get; }
    public ICommand SetAsWallpaperCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand PrintDefaultCommand { get; }
    public ICommand SendToEmailCommand { get; }
    public ICommand SaveAsCopyCommand { get; }
    public ICommand OpenExportWorkbenchCommand { get; }
    public ICommand CheckForUpdatesCommand { get; }
    public ICommand OpenLatestUpdateCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand CommitRenameCommand { get; }
    public ICommand CancelRenameCommand { get; }
    public ICommand UnlockExtensionCommand { get; }
    public ICommand UndoRenameCommand { get; }
    public ICommand AboutCommand { get; }
    public ICommand StartCompareCommand { get; }
    public ICommand CompareWithCommand { get; }
    public ICommand ExitCompareCommand { get; }
    public ICommand SwapCompareCommand { get; }
    public ICommand ToggleCompareOverlayCommand { get; }
    public ICommand IncreaseCompareOpacityCommand { get; }
    public ICommand DecreaseCompareOpacityCommand { get; }
    public ICommand ToggleOverlayModeCommand { get; }
    public ICommand ExitOverlayModeCommand { get; }
    public ICommand OpenReferenceBoardCommand { get; }
    public ICommand OpenDuplicateCleanupCommand { get; }
    public ICommand OpenFileHealthScanCommand { get; }
    public ICommand OpenRecoveryCenterCommand { get; }
    public ICommand OpenModelManagerCommand { get; }
    public ICommand OpenSemanticSearchCommand { get; }
    public ICommand OpenTagGraphCommand { get; }
    public ICommand OpenImportInboxCommand { get; }
    public ICommand ImportXmpSidecarsCommand { get; }
    public ICommand OpenMacroActionsCommand { get; }
    public ICommand OpenBatchProcessorCommand { get; }
    public ICommand OpenEditStackCommand { get; }
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
    public ICommand ApplyGallerySmartFilterCommand { get; }
    public ICommand ClearGalleryFilterCommand { get; }
    public ICommand ToggleReviewModeCommand { get; }
    public ICommand SetReviewRatingCommand { get; }
    public ICommand MarkReviewPickCommand { get; }
    public ICommand MarkReviewRejectCommand { get; }
    public ICommand ClearReviewLabelCommand { get; }
    public ICommand UndoReviewLabelCommand { get; }
    public ICommand ToggleFilmstripCommand { get; }
    public ICommand ToggleMetadataHudCommand { get; }
    public ICommand PasteFromClipboardCommand { get; }
    public ICommand OpenInDefaultAppCommand { get; }
    public ICommand StripLocationCommand { get; }
    public ICommand StripDeviceInfoCommand { get; }
    public ICommand StripTimestampsCommand { get; }
    public ICommand StripSoftwareCommand { get; }
    public ICommand StripAllMetadataCommand { get; }
    public ICommand ExtractMotionVideoCommand { get; }
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
        if (!TryPrepareOpenFile(path, out var resumedArchivePage))
            return;

        var loaded = LoadCurrent();
        CompletePreparedOpenFile(path, resumedArchivePage, loaded);
    }

    public void OpenFileList(IReadOnlyList<string> paths)
    {
        if (paths is null || paths.Count == 0) return;

        FlushPendingRename();
        if (!_nav.OpenExplicitList(paths))
        {
            ShowToast("Could not open the provided files.");
            return;
        }

        _preload.Reset();
        ResetPageState();
        var loaded = LoadCurrent();
        CompletePreparedOpenFile(paths[0], 0, loaded);
    }

    private async Task OpenFileAsync(string path)
    {
        if (!TryPrepareOpenFile(path, out var resumedArchivePage))
            return;

        var loaded = await LoadCurrentAsync();
        CompletePreparedOpenFile(path, resumedArchivePage, loaded);
    }

    private bool TryPrepareOpenFile(string path, out int resumedArchivePage)
    {
        resumedArchivePage = 0;
        FlushPendingRename();
        if (!SupportedImageFormats.IsArchive(path))
            _currentArchivePassword = null;

        if (!SupportedImageFormats.IsSupported(path))
        {
            // V20-37 / item 86: human-readable suggestion when a recognized non-image type is
            // dropped (video, document, etc.) so the user understands why nothing happened.
            var ext = Path.GetExtension(path);
            var suggestion = SupportedImageFormats.SuggestionForUnsupported(ext);
            ShowSecondaryStatus(
                Strings.MainSecondaryFileTypeNotSupported,
                suggestion is null
                    ? $"Images does not recognize {FormatExtensionForMessage(ext)} as a supported image or document preview format."
                    : suggestion,
                SecondaryStatusToneKind.Warning,
                "\uE783");
            Toast(suggestion is null
                ? $"Unsupported file type: {ext}"
                : $"Unsupported {ext}. {suggestion}");
            return false;
        }

        var previousFolder = _nav.Folder;
        if (!_nav.Open(path))
        {
            ShowSecondaryStatus(
                File.Exists(path) ? Strings.MainSecondaryCouldNotOpen : Strings.MainSecondaryFileGone,
                File.Exists(path)
                    ? $"{Path.GetFileName(path)} could not be opened from this location."
                    : Strings.MainSecondaryFileGoneDetail,
                SecondaryStatusToneKind.Warning,
                "\uE783");
            Toast(File.Exists(path)
                ? $"Could not open {Path.GetFileName(path)}"
                : Strings.MainSecondaryFileGone);
            return false;
        }

        if (!string.Equals(previousFolder, _nav.Folder, StringComparison.OrdinalIgnoreCase))
            _preload.Reset();

        ResetPageState();
        resumedArchivePage = ArchiveReadPositionService.GetLastPageIndex(_settings, path);
        if (resumedArchivePage > 0)
            PageIndex = resumedArchivePage;
        ClearSecondaryStatus();
        return true;
    }

    private void CompletePreparedOpenFile(string path, int resumedArchivePage, bool loaded)
    {
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
            await OpenFileAsync(path);
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
            await LoadCurrentAsync();
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
                Strings.MainSecondaryRecentFolderRemoved,
                Strings.MainSecondaryRecentFolderRemovedDetail,
                SecondaryStatusToneKind.Warning,
                "\uE8B7");
            Toast(Strings.MainToastFolderNoLongerExists);
            RefreshRecentFolders(); // GetRecentFolders filters missing folders — re-pull.
            return;
        }

        try
        {
            var first = DirectoryNavigator.FirstSupportedImageInFolder(folder);
            if (first is null)
            {
                ShowSecondaryStatus(
                    Strings.MainSecondaryNoSupportedImages,
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
                Strings.MainSecondaryFolderUnreachable,
                Strings.MainSecondaryFolderUnreachableDetail,
                SecondaryStatusToneKind.Warning,
                "\uE783");
            Toast(Strings.MainToastFolderUnreachable);
        }
    }

    private async Task OpenRecentArchiveAsync(ArchiveReadPositionService.ArchiveReadHistoryItem? archive)
    {
        if (archive is null) return;

        if (!File.Exists(archive.Path))
        {
            ShowSecondaryStatus(
                Strings.MainSecondaryArchiveGone,
                Strings.MainSecondaryArchiveGoneDetail,
                SecondaryStatusToneKind.Warning,
                "\uE783");
            Toast(Strings.MainToastArchiveNoLongerExists);
            RefreshArchiveReadHistory();
            return;
        }

        await OpenFileWithOperationStatusAsync(archive.Path, Strings.MainOpOpeningArchive);
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
                    Strings.MainSecondaryClipboardNotSupported,
                    Strings.MainSecondaryClipboardNotSupportedDetail,
                    SecondaryStatusToneKind.Warning,
                    "\uE783");
                break;
            case ClipboardImportStatus.NothingImageLike:
                ShowSecondaryStatus(
                    Strings.MainSecondaryClipboardNoImage,
                    Strings.MainSecondaryClipboardNoImageDetail,
                    SecondaryStatusToneKind.Info,
                    "\uE946");
                break;
            case ClipboardImportStatus.ImageUnavailable:
                ShowSecondaryStatus(
                    Strings.MainSecondaryClipboardUnreadable,
                    Strings.MainSecondaryClipboardUnreadableDetail,
                    SecondaryStatusToneKind.Warning,
                    "\uE783");
                break;
            case ClipboardImportStatus.StorageUnavailable:
            case ClipboardImportStatus.SaveFailed:
                ShowSecondaryStatus(
                    Strings.MainSecondaryClipboardSaveFailed,
                    Strings.MainSecondaryClipboardSaveFailedDetail,
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

    private void SendCurrentToEmail()
    {
        if (CurrentPath is null) return;

        try
        {
            var draftPath = _createEmailDraft(CurrentPath);
            _openShellTarget(draftPath);
            Toast(Strings.MainToastEmailOpened);
        }
        catch (Exception ex)
        {
            ShowSecondaryStatus(
                Strings.MainSecondaryEmailFailed,
                FirstLine(ex.Message),
                SecondaryStatusToneKind.Warning,
                "\uE783");
            Toast($"Email failed: {FirstLine(ex.Message)}");
        }
    }

    private async Task OpenFileDialogAsync()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = Strings.MainDialogOpenImage,
            Filter = SupportedImageFormats.OpenDialogFilter,
            FilterIndex = 1
        };
        if (dlg.ShowDialog() == true)
            await OpenFileWithOperationStatusAsync(dlg.FileName, Strings.MainOpOpeningFile);
    }

    private string? PickCompareFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = Strings.MainDialogCompareWith,
            Filter = SupportedImageFormats.OpenDialogFilter,
            CheckFileExists = true,
            Multiselect = false
        };
        if (Directory.Exists(CurrentFolder))
            dlg.InitialDirectory = CurrentFolder;

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private async Task NextAsync()
    {
        await NavigateImageAsync(_nav.MoveNext, Strings.MainOpLoadingNext);
    }

    private async Task PrevAsync()
    {
        await NavigateImageAsync(_nav.MovePrevious, Strings.MainOpLoadingPrev);
    }

    private async Task FirstAsync()
    {
        await NavigateImageAsync(_nav.MoveFirst, Strings.MainOpLoadingFirst);
    }

    private async Task LastAsync()
    {
        await NavigateImageAsync(_nav.MoveLast, Strings.MainOpLoadingLast);
    }

    private async Task NavigateImageAsync(Func<bool> move, string title)
    {
        if (IsOperationBusy)
            return;

        FlushPendingRename();
        if (!move())
            return;

        // V30-33: manual navigation during slideshow resets the timer countdown rather
        // than stopping playback, so the user can nudge forward and the show continues.
        if (IsSlideshowActive && !IsSlideshowPaused)
            RestartSlideshowTimer();

        ResetPageState();
        await LoadCurrentWithOperationStatusAsync(title, BuildDecodeOperationDetail(_nav.CurrentPath ?? ""));
    }

    private int PageStep => ArchiveSpreadModeEnabled && IsArchiveBook ? Math.Max(1, PageSpan) : 1;

    private async Task NextPageAsync()
    {
        await GoToPageAsync(PageIndex + PageStep, Strings.MainOpLoadingNextPage);
    }

    private async Task PrevPageAsync()
    {
        await GoToPageAsync(PageIndex - PageStep, Strings.MainOpLoadingPrevPage);
    }

    private async Task FirstPageAsync()
    {
        await GoToPageAsync(0, Strings.MainOpLoadingFirstPage);
    }

    private async Task LastPageAsync()
    {
        await GoToPageAsync(PageCount - 1, Strings.MainOpLoadingLastPage);
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

    private bool LoadCurrent(bool startCropMode = true)
    {
        var path = _nav.CurrentPath;
        if (!PrepareCurrentLoad(path))
            return false;

        try
        {
            return CompleteCurrentLoad(path!, DecodeCurrentPath(path!), startCropMode: startCropMode);
        }
        catch (Exception ex)
        {
            return CompleteCurrentLoad(path!, error: ex, startCropMode: startCropMode);
        }
    }

    private async Task<bool> LoadCurrentAsync(bool startCropMode = true)
    {
        var path = _nav.CurrentPath;
        if (!PrepareCurrentLoad(path))
            return false;

        try
        {
            var result = await DecodeCurrentPathAsync(path!);
            if (!string.Equals(_nav.CurrentPath, path, StringComparison.OrdinalIgnoreCase))
            {
                IsImageLoading = false;
                return false;
            }

            return CompleteCurrentLoad(path!, result, startCropMode: startCropMode);
        }
        catch (Exception ex)
        {
            if (!string.Equals(_nav.CurrentPath, path, StringComparison.OrdinalIgnoreCase))
            {
                IsImageLoading = false;
                return false;
            }

            return CompleteCurrentLoad(path!, error: ex, startCropMode: startCropMode);
        }
    }

    private bool _isImageLoading;
    public bool IsImageLoading
    {
        get => _isImageLoading;
        private set => Set(ref _isImageLoading, value);
    }

    private bool PrepareCurrentLoad(string? path)
    {
        if (path is null)
        {
            ClearCurrentState();
            return false;
        }

        IsImageLoading = true;
        SetCurrentImageWithChannel(null);
        _ocrWorkflow.Clear(cancelExtraction: true);
        return true;
    }

    private string? _currentArchivePassword;

    private ImageLoader.LoadResult DecodeCurrentPath(string path)
    {
        var isArchiveBookPage = SupportedImageFormats.IsArchive(path);
        var usePreload = ShouldUsePreloadForCurrentPage(isArchiveBookPage);
        try
        {
            return usePreload
                ? _preload.TryGet(path) ?? ImageLoader.Load(path, PageIndex, ArchiveSpreadModeEnabled, ArchiveRightToLeft, _currentArchivePassword)
                : ImageLoader.Load(path, PageIndex, ArchiveSpreadModeEnabled, ArchiveRightToLeft, _currentArchivePassword);
        }
        catch (ArchiveBookService.ArchivePasswordRequiredException)
        {
            var password = RequestArchivePassword?.Invoke(Path.GetFileName(path));
            if (string.IsNullOrEmpty(password))
                throw;
            _currentArchivePassword = password;
            return ImageLoader.Load(path, PageIndex, ArchiveSpreadModeEnabled, ArchiveRightToLeft, password);
        }
    }

    private async Task<ImageLoader.LoadResult> DecodeCurrentPathAsync(string path)
    {
        var pageIndex = PageIndex;
        var archiveSpreadMode = ArchiveSpreadModeEnabled;
        var archiveRightToLeft = ArchiveRightToLeft;
        var isArchiveBookPage = SupportedImageFormats.IsArchive(path);
        var usePreload = pageIndex == 0 && !(isArchiveBookPage && archiveSpreadMode);
        if (usePreload)
        {
            if (_preload.TryGet(path) is { } finished)
                return finished;

            if (_preload.TryGetInFlight(path) is { } inFlight)
            {
                var preloaded = await inFlight;
                if (preloaded is not null)
                    return preloaded;
            }
        }

        var taskName = $"foreground-decode:{Path.GetFileName(path)}";
        try
        {
            return await BackgroundTaskTracker.Run(
                taskName,
                () => ImageLoader.Load(path, pageIndex, archiveSpreadMode, archiveRightToLeft, _currentArchivePassword));
        }
        catch (ArchiveBookService.ArchivePasswordRequiredException)
        {
            var password = RequestArchivePassword?.Invoke(Path.GetFileName(path));
            if (string.IsNullOrEmpty(password))
                throw;
            _currentArchivePassword = password;
            return await BackgroundTaskTracker.Run(
                $"{taskName}-retry",
                () => ImageLoader.Load(path, pageIndex, archiveSpreadMode, archiveRightToLeft, password));
        }
    }

    private bool ShouldUsePreloadForCurrentPage(bool isArchiveBookPage)
        => PageIndex == 0 && !(isArchiveBookPage && ArchiveSpreadModeEnabled);

    private bool CompleteCurrentLoad(
        string path,
        ImageLoader.LoadResult? result = null,
        Exception? error = null,
        bool startCropMode = true)
    {
        var loaded = false;

        if (error is null && result is not null)
        {
            try
            {
                var isArchiveBookPage = SupportedImageFormats.IsArchive(path);
                var tilePyramid = result.TilePyramid;
                ImageSource? image = tilePyramid is null
                    ? ApplyArchiveDisplayFilters(result.Image, isArchiveBookPage, ArchiveOldScanFilterEnabled)
                    : null;
                var animation = tilePyramid is null && !(isArchiveBookPage && ArchiveOldScanFilterEnabled) ? result.Animation : null;
                var decoderUsed = isArchiveBookPage && ArchiveOldScanFilterEnabled
                    ? $"{result.DecoderUsed} + clean scan filter"
                    : result.DecoderUsed;
                var editOperations = GetEnabledDisplayEditOperations(path, isArchiveBookPage);
                if (tilePyramid is null && editOperations.Count > 0 && image is BitmapSource bitmap)
                {
                    image = ImageExportService.RenderPreview(bitmap, editOperations);
                    animation = null;
                    decoderUsed = $"{decoderUsed} + {editOperations.Count} edit preview";
                }

                // Order matters: CurrentImage first so ZoomPanImage.OnSourceChanged runs and clears
                // any animation from the previous file; then CurrentAnimation, which either applies
                // new keyframes or stays null for a static image.
                CurrentTilePyramid = tilePyramid;
                SetCurrentImageWithChannel(image);
                CurrentAnimation = animation;
                PixelWidth = tilePyramid?.SourceWidth ?? (image is BitmapSource displayedBitmap ? displayedBitmap.PixelWidth : result.PixelWidth);
                PixelHeight = tilePyramid?.SourceHeight ?? (image is BitmapSource displayedBitmapHeight ? displayedBitmapHeight.PixelHeight : result.PixelHeight);
                DecoderUsed = decoderUsed;
                ClearInspectorState();
                IsSelectionMode = false;
                ClearCanvasSelection();
                IsCropMode = false;
                ClearCropSelection();
                IsExposureBrushMode = false;
                ClearExposureBrushStrokes(showToast: false);
                IsRedEyeCorrectionMode = false;
                ClearRedEyeCorrectionMarks(showToast: false);
                IsRetouchMode = false;
                ClearRetouchState(showToast: false);
                ApplyPageSequence(result.Pages);
                ArchiveReadPositionService.SaveLastPageIndex(_settings, path, PageIndex, PageCount);
                if (isArchiveBookPage)
                    RefreshArchiveReadHistory();
                Rotation = 0;
                FlipHorizontal = false;
                FlipVertical = false;
                ClearLoadError();
                _motionPhoto = result.MotionPhoto;
                CompanionVideoPath = MotionPhotoService.FindCompanionVideo(path);
                Raise(nameof(IsMotionPhoto));
                Raise(nameof(CompanionVideoPath));

                loaded = true;

                if (result.FormatMismatch is { } fm)
                    Toast(string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        Strings.MainFormatMismatch,
                        fm.DetectedFormat,
                        fm.FileExtension,
                        fm.SuggestedExtension));

                // First-run only — surface the gesture hint the first time an image lands.
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
                error = ex;
            }
        }

        if (error is not null)
            ApplyLoadFailure(error);

        CurrentPath = path;
        try { _fileSize = new FileInfo(path).Length; } catch { _fileSize = 0; }
        Raise(nameof(FileSizeText));
        RefreshFolderPreview();
        if (!HasDisplayImage)
        {
            ClearPhotoMetadata();
            ClearColorAnalysis();
            ClearC2paInspection();
        }
        else
        {
            RefreshPhotoMetadata(path);
            if (IsTilePyramidActive)
                ClearColorAnalysis();
            else
                RefreshColorAnalysis(path);
            RefreshC2paInspection(path);
        }

        if (loaded && (IsTilePyramidActive || !CurrentFormatSupportsCrop))
            CropStatusText = CropUnavailableStatusText;

        if (loaded && startCropMode && !IsTilePyramidActive)
            StartFreehandCropModeForCurrentImage();

        SyncRenameEditorFromDisk();

        // V20-03: after loading, enqueue neighbours so the next arrow-press is instant.
        // The preload itself runs off the UI thread.
        IsImageLoading = false;
        EnqueueNeighbours();
        return loaded;
    }

    private IReadOnlyList<EditOperation> GetEnabledDisplayEditOperations(string path, bool isArchiveBookPage)
    {
        if (isArchiveBookPage)
            return [];

        try
        {
            return NonDestructiveEditService
                .GetOperations(_editStack.LoadSnapshot(path))
                .Where(operation => operation.Enabled)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            _log.LogWarning(ex, "Could not load edit preview operations for {Path}", path);
            return [];
        }
    }

    private void ApplyLoadFailure(Exception ex)
    {
        IsPinnedOverlayMode = false;
        ClearCompareMode(showToast: false);
        CurrentTilePyramid = null;
        SetCurrentImageWithChannel(null);
        CurrentAnimation = null;
        PixelWidth = PixelHeight = 0;
        DecoderUsed = Strings.MainDecoderUnavailable;
        ClearInspectorState();
        IsSelectionMode = false;
        ClearCanvasSelection();
        IsCropMode = false;
        ClearCropSelection();
        IsExposureBrushMode = false;
        ClearExposureBrushStrokes(showToast: false);
        IsRedEyeCorrectionMode = false;
        ClearRedEyeCorrectionMarks(showToast: false);
        IsRetouchMode = false;
        ClearRetouchState(showToast: false);
        ResetPageState();
        SetLoadError(ex);
    }

    private void StartFreehandCropModeForCurrentImage()
    {
        if (!CanAutoStartCropMode)
            return;

        SelectedCropAspectPreset = CropSelectionService.FreeAspectPreset;
        ClearCropSelection();
        IsCropMode = true;
    }

    private bool CanAutoStartCropMode =>
        CanUseCrop;

    private void SetLoadError(Exception ex)
    {
        var path = _nav.CurrentPath;
        var ext = path is null ? "" : Path.GetExtension(path);

        // Ghostscript dependency — special case with codec-details link.
        if (ex.Message.Contains("requires Ghostscript", StringComparison.OrdinalIgnoreCase))
        {
            LoadErrorTitle = Strings.MainLoadErrorGhostscriptTitle;
            LoadErrorMessage = Strings.MainLoadErrorGhostscriptMessage;
            LoadErrorHelpText = Strings.MainLoadErrorGhostscriptHelp;
            LoadErrorShowsCodecDetails = true;
            Toast(Strings.MainToastGhostscriptNeeded);
            return;
        }

        // Specific exception types carry their own actionable message.
        if (ex is FileNotFoundException)
        {
            LoadErrorTitle = Strings.MainLoadErrorNotFoundTitle;
            LoadErrorMessage = Strings.MainLoadErrorNotFoundMessage;
            LoadErrorHelpText = Strings.MainLoadErrorNotFoundHelp;
            LoadErrorShowsCodecDetails = false;
            Toast(Strings.MainToastFileNotFound);
            return;
        }

        if (ex is UnauthorizedAccessException)
        {
            LoadErrorTitle = Strings.MainLoadErrorAccessTitle;
            LoadErrorMessage = Strings.MainLoadErrorAccessMessage;
            LoadErrorHelpText = Strings.MainLoadErrorAccessHelp;
            LoadErrorShowsCodecDetails = false;
            Toast(Strings.MainToastAccessDenied);
            return;
        }

        if (ex is OutOfMemoryException)
        {
            LoadErrorTitle = Strings.MainLoadErrorOomTitle;
            LoadErrorMessage = Strings.MainLoadErrorOomMessage;
            LoadErrorHelpText = Strings.MainLoadErrorOomHelp;
            LoadErrorShowsCodecDetails = false;
            Toast(Strings.MainToastImageTooLarge);
            return;
        }

        // V20-18: Store extension detection for HEIC/AVIF/JXL.
        if (!string.IsNullOrEmpty(ext))
        {
            var storeExt = StoreExtensionService.GetMissingExtension(ext);
            if (storeExt is not null)
            {
                _loadErrorStoreExtension = storeExt;
                LoadErrorStoreActionLabel = string.Format(
                    Strings.MainLoadErrorStoreActionLabel, storeExt.DisplayName);
                LoadErrorTitle = Strings.MainLoadErrorStoreExtensionTitle;
                LoadErrorMessage = string.Format(
                    Strings.MainLoadErrorStoreExtensionMessage,
                    ext.TrimStart('.').ToUpperInvariant(),
                    storeExt.DisplayName);
                LoadErrorHelpText = Strings.MainLoadErrorStoreExtensionHelp;
                LoadErrorShowsCodecDetails = false;
                Toast(string.Format(Strings.MainToastStoreExtensionNeeded,
                    ext.TrimStart('.').ToUpperInvariant()));
                return;
            }

            if (StoreExtensionService.IsJxlFormat(ext))
            {
                _loadErrorStoreExtension = null;
                LoadErrorStoreActionLabel = null;
                LoadErrorTitle = Strings.MainLoadErrorStoreExtensionTitle;
                LoadErrorMessage = Strings.MainLoadErrorJxlMessage;
                LoadErrorHelpText = Strings.MainLoadErrorJxlHelp;
                LoadErrorShowsCodecDetails = false;
                Toast(Strings.MainToastJxlNeedsUpdate);
                return;
            }
        }

        // Item 86 enhancement: format-specific decode hints for supported-but-failing types.
        var decodeHint = string.IsNullOrEmpty(ext)
            ? null
            : SupportedImageFormats.SuggestionForDecodeFailure(ext);

        LoadErrorTitle = Strings.MainLoadErrorDefault;
        LoadErrorMessage = $"This file could not be decoded. {ex.Message}";
        LoadErrorHelpText = decodeHint
            ?? Strings.MainLoadErrorDecodeHelp;
        LoadErrorShowsCodecDetails = false;
        Toast(Strings.MainToastCouldNotDecode);
    }

    private void OpenStoreExtensionPage()
    {
        if (_loadErrorStoreExtension is null) return;
        _loadErrorStoreExtension.OpenStorePage();
        Toast(string.Format(Strings.MainToastStoreExtensionOpened, _loadErrorStoreExtension.DisplayName));
    }

    private void ClearLoadError()
    {
        LoadErrorTitle = "This image couldn't be displayed";
        LoadErrorHelpText = "";
        LoadErrorShowsCodecDetails = false;
        _loadErrorStoreExtension = null;
        LoadErrorStoreActionLabel = null;
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
        PageLabel = Strings.MainPageLabel;
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
                Toast(Strings.MainToastFilenameInvalid);
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
            return Strings.MainUnexpectedError;

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
                Toast(Strings.MainToastDeleteCanceled);
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
        _recoveryCenter.RecordRecycleBin(
            toDelete,
            Strings.MainSentToRecycleBin,
            $"Sent {Path.GetFileName(toDelete)} to the Windows Recycle Bin from the main viewer.");
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

    private void ApplyRotationToFile()
    {
        if (!CanApplyRotationToFile || CurrentPath is null)
            return;

        var degrees = NormalizeRotationForWriteback(Rotation);
        if (degrees == 0)
            return;

        var path = CurrentPath;
        var existingOperations = GetEnabledDisplayEditOperations(path, isArchiveBookPage: false);
        var allowLosslessJpegTrim = false;
        if (TryReadLosslessJpegRotation(degrees, out var losslessRotation) &&
            ImageExportService.TryPlanLosslessJpegRotationTrimConfirmation(
                path,
                losslessRotation,
                existingOperations) is { } trimPlan)
        {
            var choice = _confirmLosslessJpegTrim(LosslessJpegTrimConfirmation.ForRotation(trimPlan));
            if (choice == LosslessJpegTrimChoice.Cancel)
            {
                Toast(Strings.MainToastRotationCanceled);
                return;
            }

            allowLosslessJpegTrim = choice == LosslessJpegTrimChoice.ApplyTrimmedLossless;
        }

        var operations = existingOperations
            .Append(new EditOperation(
                Guid.NewGuid().ToString("N"),
                "rotate",
                DateTimeOffset.UtcNow,
                Enabled: true,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["degrees"] = degrees.ToString(CultureInfo.InvariantCulture)
                },
                $"Rotate {degrees} degrees"))
            .ToList();

        WritebackGuardService.CreateBackup(path, WritebackGuardService.GetBackupMode(_settings));

        BeginOperationStatus(Strings.MainOpApplyingRotation, $"Overwriting {Path.GetFileName(path)}.");
        try
        {
            ImageExportService.Overwrite(path, operations, allowLosslessJpegTrim);
            _recoveryCenter.RecordWriteback(
                path,
                Strings.MainRotationWriteback,
                $"Overwrote {Path.GetFileName(path)} with a {degrees}-degree rotation.");
            if (existingOperations.Count > 0)
            {
                var clearResult = _editStack.ClearMasterOperations(path);
                if (!clearResult.Success)
                    throw new IOException(clearResult.Message);
            }

            _preload.Reset();
            ShellChangeNotificationService.NotifyFileUpdated(path);
            Rotation = 0;
            var fileUpdated = LoadCurrent(startCropMode: false);
            Toast(allowLosslessJpegTrim
                ? "Rotation applied losslessly with JPEG trim"
                : fileUpdated ? "Rotation applied to file" : "Rotation saved to file");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or InvalidOperationException or NotSupportedException or ImageMagick.MagickException)
        {
            Toast("Rotation failed: " + ex.Message);
        }
        finally
        {
            EndOperationStatus();
        }
    }

    private static int NormalizeRotationForWriteback(double rotation)
    {
        var degrees = ((int)Math.Round(rotation) % 360 + 360) % 360;
        return degrees is 90 or 180 or 270 ? degrees : 0;
    }

    private static bool TryReadLosslessJpegRotation(int degrees, out LosslessJpegRotation rotation)
    {
        rotation = degrees switch
        {
            90 => LosslessJpegRotation.Rotate90,
            180 => LosslessJpegRotation.Rotate180,
            270 => LosslessJpegRotation.Rotate270,
            _ => default
        };
        return degrees is 90 or 180 or 270;
    }

    private static LosslessJpegTrimChoice ShowLosslessJpegTrimConfirmation(LosslessJpegTrimConfirmation confirmation)
    {
        var result = MessageBox.Show(
            confirmation.Message +
            Environment.NewLine +
            Environment.NewLine +
            $"Yes: {confirmation.TrimActionText}" +
            Environment.NewLine +
            $"No: {confirmation.ExactActionText}" +
            Environment.NewLine +
            Strings.MainCancelLeaveUnchanged,
            confirmation.Title,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        return result switch
        {
            MessageBoxResult.Yes => LosslessJpegTrimChoice.ApplyTrimmedLossless,
            MessageBoxResult.No => LosslessJpegTrimChoice.ReencodeExact,
            _ => LosslessJpegTrimChoice.Cancel
        };
    }

    public void StartCompareWithPath(string? secondaryPath)
    {
        if (!CanUseCompareMode || string.IsNullOrWhiteSpace(secondaryPath))
        {
            Toast(Strings.MainToastOpenBeforeCompare);
            return;
        }

        if (!TryLoadCompareImage(secondaryPath, out var image, out var normalizedPath, out var message))
        {
            ShowSecondaryStatus(
                Strings.MainSecondaryCompareUnavailable,
                message,
                SecondaryStatusToneKind.Warning,
                "\uE783");
            Toast(Strings.MainToastCompareUnavailable);
            return;
        }

        EnterCompareMode(image, normalizedPath);
    }

    public void StartCompareWithPair(string? primaryPath, string? secondaryPath)
    {
        if (string.IsNullOrWhiteSpace(primaryPath) || string.IsNullOrWhiteSpace(secondaryPath))
        {
            Toast(Strings.MainToastChooseTwoImages);
            return;
        }

        if (!string.Equals(CurrentPath, primaryPath, StringComparison.OrdinalIgnoreCase) || !HasDisplayImage)
        {
            if (!OpenPrimaryForCompare(primaryPath))
                return;
        }

        StartCompareWithPath(secondaryPath);
    }

    private void StartCompareWithNext()
    {
        var nextPath = TryGetNextComparePath();
        if (nextPath is null)
        {
            Toast(Strings.MainToastNoNextCompare);
            return;
        }

        StartCompareWithPath(nextPath);
    }

    private void StartCompareWithPickedFile()
    {
        var path = _pickCompareFile();
        if (!string.IsNullOrWhiteSpace(path))
            StartCompareWithPath(path);
    }

    private string? TryGetNextComparePath()
    {
        if (_nav.Count < 2 || _nav.CurrentIndex < 0)
            return null;

        var nextIndex = (_nav.CurrentIndex + 1) % _nav.Count;
        var nextPath = _nav.Files[nextIndex];
        return string.Equals(nextPath, CurrentPath, StringComparison.OrdinalIgnoreCase)
            ? null
            : nextPath;
    }

    private bool OpenPrimaryForCompare(string primaryPath)
    {
        if (!TryPrepareOpenFile(primaryPath, out var resumedArchivePage))
            return false;

        var loaded = LoadCurrent(startCropMode: false);
        CompletePreparedOpenFile(_nav.CurrentPath ?? primaryPath, resumedArchivePage, loaded);
        if (!loaded || !HasDisplayImage)
        {
            Toast(Strings.MainToastCompareOpenFailed);
            return false;
        }

        return true;
    }

    private void EnterCompareMode(ImageSource image, string path)
    {
        IsPinnedOverlayMode = false;
        IsGalleryOpen = false;
        IsInspectorMode = false;
        IsSelectionMode = false;
        IsCropMode = false;
        IsOcrMode = false;
        IsExposureBrushMode = false;
        IsRedEyeCorrectionMode = false;
        IsRetouchMode = false;
        ClearInspectorState();
        ClearCanvasSelection();
        ClearCropSelection();
        ClearExposureBrushStrokes(showToast: false);
        ClearRedEyeCorrectionMarks(showToast: false);
        ClearRetouchState(showToast: false);

        CompareImage = image;
        ComparePath = path;
        IsCompareMode = true;
        ClearSecondaryStatus();
        Toast(Strings.MainToastCompareModeOn);
    }

    private bool TryLoadCompareImage(
        string path,
        out ImageSource image,
        out string normalizedPath,
        out string message)
    {
        image = null!;
        normalizedPath = string.Empty;
        message = string.Empty;

        try
        {
            normalizedPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            message = Strings.MainComparePathInvalid;
            return false;
        }

        if (!File.Exists(normalizedPath))
        {
            message = Strings.MainCompareFileGone;
            return false;
        }

        if (!SupportedImageFormats.IsSupported(normalizedPath))
        {
            message = $"{FormatExtensionForMessage(Path.GetExtension(normalizedPath))} is not a supported compare format.";
            return false;
        }

        if (string.Equals(normalizedPath, CurrentPath, StringComparison.OrdinalIgnoreCase))
        {
            message = Strings.MainCompareSameFile;
            return false;
        }

        try
        {
            var result = ImageLoader.Load(normalizedPath);
            var isArchiveBookPage = SupportedImageFormats.IsArchive(normalizedPath);
            var display = ApplyArchiveDisplayFilters(result.Image, isArchiveBookPage, ArchiveOldScanFilterEnabled);
            var editOperations = GetEnabledDisplayEditOperations(normalizedPath, isArchiveBookPage);
            if (editOperations.Count > 0 && display is BitmapSource bitmap)
                display = ImageExportService.RenderPreview(bitmap, editOperations);

            image = display;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or InvalidOperationException or NotSupportedException or ImageMagick.MagickException)
        {
            message = $"Images could not decode {Path.GetFileName(normalizedPath)} for compare: {ex.Message}";
            return false;
        }
    }

    private void SwapComparePair()
    {
        if (!CanSwapComparePair || CurrentPath is null || ComparePath is null)
            return;

        var oldPrimary = CurrentPath;
        var oldSecondary = ComparePath;
        var keepOverlay = IsCompareOverlayMode;
        if (!OpenPrimaryForCompare(oldSecondary))
            return;

        if (!TryLoadCompareImage(oldPrimary, out var image, out var normalizedPath, out var message))
        {
            ShowSecondaryStatus("Compare swap failed", message, SecondaryStatusToneKind.Warning, "\uE783");
            ClearCompareMode(showToast: false);
            Toast(Strings.MainToastCompareSwapFailed);
            return;
        }

        CompareImage = image;
        ComparePath = normalizedPath;
        IsCompareMode = true;
        IsCompareOverlayMode = keepOverlay;
        Toast(Strings.MainToastCompareSwapped);
    }

    private void AdjustCompareOverlayOpacity(double delta)
    {
        CompareOverlayOpacity += delta;
        Toast($"Compare opacity {CompareOverlayOpacityText}");
    }

    private void ClearCompareMode(bool showToast)
    {
        var wasActive = IsCompareMode || CompareImage is not null || ComparePath is not null;
        IsCompareMode = false;
        CompareImage = null;
        ComparePath = null;
        IsCompareOverlayMode = false;
        if (showToast && wasActive)
            Toast(Strings.MainToastCompareModeOff);
    }

    private void RaiseCompareState()
    {
        Raise(nameof(CanUseCompareMode));
        Raise(nameof(CanStartCompareWithNext));
        Raise(nameof(CanSwapComparePair));
        Raise(nameof(ShowCompareMode));
        Raise(nameof(CompareModeText));
        Raise(nameof(CompareLayoutText));
        Raise(nameof(CompareLayoutToggleText));
        Raise(nameof(CompareOverlayOpacityText));
        Raise(nameof(ComparePrimaryFileName));
        Raise(nameof(CompareSecondaryFileName));
        Raise(nameof(CompareStatusText));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RaiseImageToolState()
    {
        Raise(nameof(CanUseInspector));
        Raise(nameof(CanUseSelection));
        Raise(nameof(CanUseCrop));
        Raise(nameof(CurrentFormatSupportsCrop));
        Raise(nameof(CanApplyRotationToFile));
        Raise(nameof(CanUseExposureBrush));
        Raise(nameof(CanUseRedEyeCorrection));
        Raise(nameof(CanUseRetouch));
        Raise(nameof(CanUseOverlayMode));
        CommandManager.InvalidateRequerySuggested();
    }

    public void SetOverlayExitHotKeyAvailable(bool available)
    {
        if (!Set(ref _overlayExitHotKeyAvailable, available))
            return;

        Raise(nameof(OverlayExitText));
        Raise(nameof(OverlayStatusText));
        if (!available && IsOverlayClickThrough)
        {
            _isOverlayClickThrough = false;
            Raise(nameof(IsOverlayClickThrough));
            Raise(nameof(OverlayClickThroughText));
            Toast(Strings.MainToastClickThroughDisabled);
        }
    }

    private void ExitOverlayMode()
    {
        IsPinnedOverlayMode = false;
    }

    private void RaiseOverlayState()
    {
        Raise(nameof(IsPinnedOverlayMode));
        Raise(nameof(CanUseOverlayMode));
        Raise(nameof(ShowOverlayBanner));
        Raise(nameof(OverlayModeText));
        Raise(nameof(OverlayToggleText));
        Raise(nameof(OverlayClickThroughText));
        Raise(nameof(OverlayOpacityText));
        Raise(nameof(OverlayExitText));
        Raise(nameof(OverlayStatusText));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RefreshAnimationWorkbench(AnimationSequence? sequence)
    {
        _animationTimer.Stop();
        _animationCompletedLoops = 0;
        if (_isAnimationPlaying)
        {
            _isAnimationPlaying = false;
            Raise(nameof(IsAnimationPlaying));
            Raise(nameof(AnimationPlaybackText));
        }

        AnimationFrames.Clear();
        _currentAnimationFrameIndex = 0;

        if (sequence is { Frames.Count: >= 2 })
        {
            for (var i = 0; i < sequence.Frames.Count; i++)
            {
                AnimationFrames.Add(new AnimationFrameItem(
                    i,
                    sequence.Frames[i],
                    AnimationWorkbenchService.FormatDelay(sequence.Delays[i]),
                    AnimationWorkbenchService.FormatTimestamp(sequence.Delays, i)));
            }
        }

        UpdateAnimationTimelineSelection();
        RaiseAnimationFrameState();
        IsAnimationPlaying = sequence is { Frames.Count: >= 2 };
        CommandManager.InvalidateRequerySuggested();
    }

    private void SelectAnimationFrame(int index, bool pause)
    {
        if (!IsAnimated)
        {
            if (_currentAnimationFrameIndex != 0)
            {
                _currentAnimationFrameIndex = 0;
                RaiseAnimationFrameState();
            }
            return;
        }

        var clamped = AnimationWorkbenchService.ClampFrameIndex(CurrentAnimation, index);
        if (pause)
            IsAnimationPlaying = false;

        if (!Set(ref _currentAnimationFrameIndex, clamped))
        {
            UpdateAnimationTimelineSelection();
            return;
        }

        _animationCompletedLoops = 0;
        UpdateAnimationTimelineSelection();
        RaiseAnimationFrameState();
        if (IsAnimationPlaying)
            RestartAnimationTimer();
    }

    private void ToggleAnimationPlayback()
    {
        IsAnimationPlaying = !IsAnimationPlaying;
    }

    private void StepAnimationFrame(int delta)
    {
        if (!IsAnimated)
            return;

        IsAnimationPlaying = false;
        var count = CurrentAnimation!.Frames.Count;
        SelectAnimationFrame((CurrentAnimationFrameIndex + delta + count) % count, pause: false);
    }

    private void AdvanceAnimationFromTimer()
    {
        if (!IsAnimationPlaying || CurrentAnimation is not { Frames.Count: >= 2 } sequence)
        {
            IsAnimationPlaying = false;
            return;
        }

        var next = CurrentAnimationFrameIndex + 1;
        if (next >= sequence.Frames.Count)
        {
            if (sequence.LoopCount > 0 && _animationCompletedLoops + 1 >= sequence.LoopCount)
            {
                SelectAnimationFrame(sequence.Frames.Count - 1, pause: false);
                IsAnimationPlaying = false;
                return;
            }

            _animationCompletedLoops++;
            next = 0;
        }

        SelectAnimationFrame(next, pause: false);
    }

    private void RestartAnimationTimer()
    {
        _animationTimer.Stop();
        if (!IsAnimationPlaying || CurrentAnimation is not { Frames.Count: >= 2 } sequence)
            return;

        var index = AnimationWorkbenchService.ClampFrameIndex(sequence, CurrentAnimationFrameIndex);
        _animationTimer.Interval = AnimationWorkbenchService.DelayForSpeed(sequence.Delays[index], AnimationPlaybackSpeed);
        _animationTimer.Start();
    }

    private void SetAnimationFrameFromParameter(object? parameter)
    {
        if (TryGetAnimationFrameIndex(parameter, out var index))
            SelectAnimationFrame(index, pause: true);
    }

    private bool CanSetAnimationFrameFromParameter(object? parameter)
        => IsAnimated && TryGetAnimationFrameIndex(parameter, out var index)
           && index >= 0
           && index < CurrentAnimation!.Frames.Count;

    private static bool TryGetAnimationFrameIndex(object? parameter, out int index)
    {
        switch (parameter)
        {
            case int i:
                index = i;
                return true;
            case double d:
                index = (int)Math.Round(d);
                return true;
            case AnimationFrameItem item:
                index = item.Index;
                return true;
            case string text when int.TryParse(text, out var parsed):
                index = parsed;
                return true;
            default:
                index = -1;
                return false;
        }
    }

    private void CopyAnimationFrame()
    {
        if (SelectedAnimationFrame is not { } frame)
            return;

        try
        {
            ClipboardService.SetImage(frame);
            Toast($"Copied {SelectedAnimationFrameText}");
        }
        catch (Exception ex)
        {
            Toast($"Copy failed: {ex.Message}");
        }
    }

    private async Task ExportAnimationFrameAsync()
    {
        if (SelectedAnimationFrame is not { } frame || CurrentPath is null)
            return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = Strings.MainDialogExportFrame,
            FileName = AnimationWorkbenchService.CreateDefaultFrameExportFileName(CurrentPath, CurrentAnimationFrameIndex),
            Filter = "PNG image|*.png",
            DefaultExt = "png",
            AddExtension = true,
            InitialDirectory = Path.GetDirectoryName(CurrentPath),
        };
        if (dialog.ShowDialog() != true)
            return;

        BeginOperationStatus(Strings.MainOpExportingFrame, $"{SelectedAnimationFrameText} to {Path.GetFileName(dialog.FileName)}.");
        try
        {
            await YieldForOperationStatusAsync();
            AnimationWorkbenchService.SaveFramePng(frame, dialog.FileName);
            Toast($"Exported {SelectedAnimationFrameText}");
        }
        catch (Exception ex)
        {
            Toast($"Export failed: {ex.Message}");
        }
        finally
        {
            EndOperationStatus();
        }
    }

    public bool TryCreateAnimationFrameDragFile(out string path)
    {
        path = "";
        if (SelectedAnimationFrame is not { } frame)
            return false;

        try
        {
            path = AnimationWorkbenchService.SaveFrameToTemp(frame, CurrentPath, CurrentAnimationFrameIndex);
            return true;
        }
        catch (Exception ex)
        {
            Toast($"Frame drag failed: {ex.Message}");
            return false;
        }
    }

    private void UpdateAnimationTimelineSelection()
    {
        for (var i = 0; i < AnimationFrames.Count; i++)
            AnimationFrames[i].IsSelected = i == CurrentAnimationFrameIndex;
    }

    private void RaiseAnimationFrameState()
    {
        Raise(nameof(CurrentAnimationFrameIndex));
        Raise(nameof(AnimationScrubberValue));
        Raise(nameof(AnimationFrameSliderMaximum));
        Raise(nameof(CanStepAnimationFrame));
        Raise(nameof(SelectedAnimationFrame));
        Raise(nameof(SelectedAnimationFrameText));
        Raise(nameof(SelectedAnimationFrameDelayText));
        Raise(nameof(SelectedAnimationTimestampText));
        Raise(nameof(AnimationWorkbenchStatusText));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RaiseAnimationWorkbenchState()
    {
        Raise(nameof(AnimationFrameSliderMaximum));
        Raise(nameof(CanStepAnimationFrame));
        Raise(nameof(AnimationPlaybackText));
        Raise(nameof(AnimationPlaybackSpeedText));
        Raise(nameof(AnimationWorkbenchStatusText));
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

    public void UpdateCanvasSelection(PixelSelection? selection)
    {
        CanvasSelection = ImageSelectionService.Normalize(selection, PixelWidth, PixelHeight);
        SelectionStatusText = CanvasSelection is { } value
            ? $"Selection ready: {value.DisplayText}. Copy places the selected pixels on the clipboard."
            : "Pointer is outside the image.";
    }

    public void UpdateCanvasSelection(PixelCoordinate anchor, PixelCoordinate current)
    {
        CanvasSelection = ImageSelectionService.CreateSelection(anchor, current, PixelWidth, PixelHeight);
        SelectionStatusText = CanvasSelection is { } value
            ? $"Selection ready: {value.DisplayText}. Copy places the selected pixels on the clipboard."
            : "Pointer is outside the image.";
    }

    public void ClearCanvasSelection()
    {
        CanvasSelection = null;
        SelectionStatusText = IsSelectionMode
            ? "Selection mode is ready. Drag on the image to choose pixels to copy."
            : "Selection is paused. Toggle it on to drag a pixel selection.";
    }

    public void UpdateCropSelection(PixelSelection? selection)
    {
        CropSelection = CropSelectionService.Normalize(selection, PixelWidth, PixelHeight);
        CropStatusText = CropSelection is { } crop
            ? $"Crop selected: {crop.DisplayText}. Aspect: {CropAspectText}. Press Enter or Apply to overwrite the file."
            : "Pointer is outside the image.";
    }

    public void UpdateCropSelection(PixelCoordinate anchor, PixelCoordinate current)
    {
        if (EffectiveCropAspectPreset is not { } aspect)
        {
            CropSelection = null;
            CropStatusText = Strings.MainCropCustomAspectError;
            return;
        }

        CropSelection = CropSelectionService.CreateSelection(anchor, current, aspect, PixelWidth, PixelHeight);
        CropStatusText = CropSelection is { } crop
            ? $"Crop selected: {crop.DisplayText}. Aspect: {CropAspectText}. Press Enter or Apply to overwrite the file."
            : "Pointer is outside the image.";
    }

    public void ClearCropSelection()
    {
        CropSelection = null;
        CropStatusText = IsCropMode
            ? "Freehand crop is ready. Drag on the image to choose a crop. Enter or Apply overwrites the file."
            : "Crop is paused. Toggle it on to drag a crop that overwrites the file.";
    }

    public void AddExposureBrushStroke(PixelCoordinate coordinate)
    {
        if (!IsExposureBrushMode || !CanUseExposureBrush || PixelWidth <= 0 || PixelHeight <= 0)
            return;

        var stroke = LocalExposureBrushService.NormalizeStroke(
            coordinate,
            ExposureBrushRadius,
            ExposureBrushSignedStrength,
            PixelWidth,
            PixelHeight);

        if (ExposureBrushStrokes.Count > 0)
        {
            var previous = ExposureBrushStrokes[ExposureBrushStrokes.Count - 1];
            var dx = previous.X - stroke.X;
            var dy = previous.Y - stroke.Y;
            var minimumSpacing = Math.Max(2, stroke.Radius * 0.18);
            if ((dx * dx) + (dy * dy) < minimumSpacing * minimumSpacing)
                return;
        }

        ExposureBrushStrokes.Add(stroke);
        ExposureBrushStatusText = string.Create(
            CultureInfo.InvariantCulture,
            $"{stroke.ModeLabel} stroke at {stroke.X:0}, {stroke.Y:0}. Enter applies the brush stack.");
        RaiseExposureBrushStrokeState();
    }

    public void AddRedEyeCorrectionMark(PixelCoordinate coordinate)
    {
        if (!IsRedEyeCorrectionMode || !CanUseRedEyeCorrection || PixelWidth <= 0 || PixelHeight <= 0)
            return;

        var mark = RedEyeCorrectionService.NormalizeMark(
            coordinate,
            RedEyeCorrectionRadius,
            RedEyeCorrectionNormalizedStrength,
            RedEyeCorrectionNormalizedThreshold,
            PixelWidth,
            PixelHeight);

        if (RedEyeCorrectionMarks.Count > 0)
        {
            var previous = RedEyeCorrectionMarks[RedEyeCorrectionMarks.Count - 1];
            var dx = previous.X - mark.X;
            var dy = previous.Y - mark.Y;
            var minimumSpacing = Math.Max(2, mark.Radius * 0.3);
            if ((dx * dx) + (dy * dy) < minimumSpacing * minimumSpacing)
                return;
        }

        RedEyeCorrectionMarks.Add(mark);
        RedEyeCorrectionStatusText = string.Create(
            CultureInfo.InvariantCulture,
            $"Red-eye mark at {mark.X:0}, {mark.Y:0}. Enter applies {RedEyeCorrectionMarks.Count} mark{(RedEyeCorrectionMarks.Count == 1 ? "" : "s")}.");
        RaiseRedEyeCorrectionMarkState();
    }

    public void BeginRetouchStroke(PixelCoordinate coordinate, bool pickSource)
    {
        if (!IsRetouchMode || !CanUseRetouch || PixelWidth <= 0 || PixelHeight <= 0)
            return;

        if (pickSource || _retouchSource is null)
        {
            _retouchSource = coordinate;
            _retouchStrokeSourceAnchor = null;
            _retouchStrokeTargetAnchor = null;
            RetouchStatusText = $"Source picked at {coordinate.X}, {coordinate.Y}. Paint the target area.";
            RaiseRetouchSourceState();
            return;
        }

        _retouchStrokeSourceAnchor = _retouchSource;
        _retouchStrokeTargetAnchor = coordinate;
        AddRetouchStroke(coordinate);
    }

    public void UpdateRetouchStroke(PixelCoordinate coordinate)
    {
        if (!IsRetouchMode || _retouchStrokeSourceAnchor is null || _retouchStrokeTargetAnchor is null)
            return;

        AddRetouchStroke(coordinate);
    }

    public void EndRetouchStroke(PixelCoordinate coordinate)
    {
        if (_retouchStrokeSourceAnchor is not null && _retouchStrokeTargetAnchor is not null)
            AddRetouchStroke(coordinate);

        _retouchStrokeSourceAnchor = null;
        _retouchStrokeTargetAnchor = null;
    }

    private void AddRetouchStroke(PixelCoordinate target)
    {
        if (_retouchStrokeSourceAnchor is not { } sourceAnchor || _retouchStrokeTargetAnchor is not { } targetAnchor)
            return;

        var source = new PixelCoordinate(
            sourceAnchor.X + target.X - targetAnchor.X,
            sourceAnchor.Y + target.Y - targetAnchor.Y);
        var stroke = RetouchBrushService.NormalizeStroke(
            source,
            target,
            RetouchRadius,
            RetouchNormalizedStrength,
            IsRetouchHealing,
            PixelWidth,
            PixelHeight);

        if (RetouchStrokes.Count > 0)
        {
            var previous = RetouchStrokes[RetouchStrokes.Count - 1];
            var dx = previous.TargetX - stroke.TargetX;
            var dy = previous.TargetY - stroke.TargetY;
            var minimumSpacing = Math.Max(2, stroke.Radius * 0.28);
            if ((dx * dx) + (dy * dy) < minimumSpacing * minimumSpacing)
                return;
        }

        RetouchStrokes.Add(stroke);
        RetouchStatusText = $"{stroke.ModeLabel} stroke added. Alt-click picks a new source; Enter applies.";
        RaiseRetouchStrokeState();
    }

    private CropAspectPreset? EffectiveCropAspectPreset
    {
        get
        {
            if (!ShowCustomCropAspect)
                return SelectedCropAspectPreset;

            if (!int.TryParse(CustomCropAspectWidth, out var width) ||
                !int.TryParse(CustomCropAspectHeight, out var height) ||
                width <= 0 ||
                height <= 0)
            {
                return null;
            }

            return new CropAspectPreset(
                CropSelectionService.CustomAspectPresetId,
                $"{width}:{height}",
                $"Custom {width}:{height} crop.",
                width,
                height);
        }
    }

    private void SetCropAspectPreset(object? parameter)
    {
        var id = parameter switch
        {
            CropAspectPreset preset => preset.Id,
            string value => value,
            _ => null
        };

        if (CropSelectionService.FindAspectPreset(id) is { } selectedPreset)
            SelectedCropAspectPreset = selectedPreset;
    }

    private void ApplyCropSelection()
    {
        if (!CanApplyCrop || CurrentPath is null || CropSelection is not { } crop)
            return;

        var path = CurrentPath;
        var existingOperations = GetEnabledDisplayEditOperations(path, isArchiveBookPage: false);
        var allowLosslessJpegTrim = false;
        if (ImageExportService.TryPlanLosslessJpegCropTrimConfirmation(
                path,
                crop,
                PixelWidth,
                PixelHeight,
                existingOperations) is { } trimPlan)
        {
            var choice = _confirmLosslessJpegTrim(LosslessJpegTrimConfirmation.ForCrop(trimPlan));
            if (choice == LosslessJpegTrimChoice.Cancel)
            {
                CropStatusText = Strings.MainCropCanceledStatus;
                Toast(Strings.MainToastCropCanceled);
                return;
            }

            allowLosslessJpegTrim = choice == LosslessJpegTrimChoice.ApplyTrimmedLossless;
        }

        var operations = existingOperations
            .Append(new EditOperation(
                Guid.NewGuid().ToString("N"),
                "crop",
                DateTimeOffset.UtcNow,
                Enabled: true,
                CropSelectionService.ToEditParameters(crop),
                $"Crop {crop.Width}x{crop.Height} at {crop.X},{crop.Y}"))
            .ToList();

        var backupMode = WritebackGuardService.GetBackupMode(_settings);
        var backupPath = WritebackGuardService.CreateBackup(path, backupMode);

        BeginOperationStatus(Strings.MainOpApplyingCrop, $"Overwriting {Path.GetFileName(path)}.");
        try
        {
            ImageExportService.Overwrite(path, operations, allowLosslessJpegTrim);
            _recoveryCenter.RecordWriteback(
                path,
                Strings.MainCropWriteback,
                $"Overwrote {Path.GetFileName(path)} with crop {crop.Width}x{crop.Height} at {crop.X},{crop.Y}.");
            if (existingOperations.Count > 0)
            {
                var clearResult = _editStack.ClearMasterOperations(path);
                if (!clearResult.Success)
                    throw new IOException(clearResult.Message);
            }

            _preload.Reset();
            ShellChangeNotificationService.NotifyFileUpdated(path);
            var fileUpdated = LoadCurrent(startCropMode: false);
            IsCropMode = false;
            ClearCropSelection();

            Toast(allowLosslessJpegTrim
                ? "Crop applied losslessly with JPEG trim"
                : fileUpdated ? "Crop applied to file" : "Crop saved to file");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or InvalidOperationException or NotSupportedException or ImageMagick.MagickException)
        {
            CropStatusText = "Crop failed: " + ex.Message;
            Toast(Strings.MainToastCropFailed);
        }
        finally
        {
            EndOperationStatus();
        }
    }

    private void OpenResizeDialog()
    {
        if (!CanUseResize || CurrentPath is null || PixelWidth <= 0 || PixelHeight <= 0)
            return;

        var dialog = new Images.ResizeDialogWindow(PixelWidth, PixelHeight)
        {
            Owner = Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() != true || dialog.Result is not { } plan)
            return;

        var result = _editStack.AppendOperation(CurrentPath, "resize", plan.ToEditParameters(), plan.Label);
        if (result.Success)
        {
            Toast($"Resize added: {plan.OutputWidth}x{plan.OutputHeight}");
        }
        else
        {
            Toast(result.Message);
        }
    }

    private void OpenAdjustments()
    {
        if (!CanUseAdjustments || CurrentPath is null)
            return;

        var imagePath = CurrentPath;
        var window = new Images.AdjustmentsWindow(
            imagePath,
            plan =>
            {
                if (!File.Exists(imagePath))
                {
                    Toast(Strings.MainToastAdjustmentUnavailable);
                    return;
                }

                var result = _editStack.AppendOperation(imagePath, "adjust", plan.ToEditParameters(), plan.Label);
                Toast(result.Success ? Strings.MainToastAdjustmentAdded : result.Message);
            })
        {
            Owner = Application.Current?.MainWindow
        };

        window.Show();
    }

    private void OpenEffects()
    {
        if (!CanUseEffects || CurrentPath is null)
            return;

        var imagePath = CurrentPath;
        var window = new Images.EffectsWindow(
            imagePath,
            plan =>
            {
                if (!File.Exists(imagePath))
                {
                    Toast(Strings.MainToastEffectsUnavailable);
                    return;
                }

                var result = _editStack.AppendOperation(imagePath, "effects", plan.ToEditParameters(), plan.Label);
                Toast(result.Success ? Strings.MainToastEffectsAdded : result.Message);
            })
        {
            Owner = Application.Current?.MainWindow
        };

        window.Show();
    }

    private void ApplyAutoEnhance()
    {
        if (!CanUseAutoEnhance || CurrentPath is null)
            return;

        var result = _editStack.AppendOperation(
            CurrentPath,
            "auto-enhance",
            ImageAutoEnhancePlan.Balanced.ToEditParameters(),
            ImageAutoEnhancePlan.Balanced.Label);

        Toast(result.Success ? Strings.MainToastAutoEnhanceAdded : result.Message);
    }

    private void OpenAnnotations()
    {
        if (!CanUseAnnotations || CurrentPath is null)
            return;

        var imagePath = CurrentPath;
        var window = new Images.AnnotationsWindow(
            imagePath,
            plan =>
            {
                if (!File.Exists(imagePath))
                {
                    Toast(Strings.MainToastAnnotationsUnavailable);
                    return;
                }

                var result = _editStack.AppendOperation(imagePath, "annotation", plan.ToEditParameters(), plan.Label);
                Toast(result.Success ? Strings.MainToastAnnotationsAdded : result.Message);
            })
        {
            Owner = Application.Current?.MainWindow
        };

        window.Show();
    }

    private void OpenPerspective()
    {
        if (!CanUsePerspective || CurrentPath is null)
            return;

        var imagePath = CurrentPath;
        var window = new Images.PerspectiveCorrectionWindow(
            imagePath,
            plan =>
            {
                if (!File.Exists(imagePath))
                {
                    Toast(Strings.MainToastPerspectiveUnavailable);
                    return;
                }

                var result = _editStack.AppendOperation(imagePath, "perspective", plan.ToEditParameters(), plan.Label);
                Toast(result.Success ? Strings.MainToastPerspectiveAdded : result.Message);
            })
        {
            Owner = Application.Current?.MainWindow
        };

        window.Show();
    }

    private void SetExposureBrushMode(object? parameter)
    {
        if (parameter is not string mode)
            return;

        if (mode.Equals("burn", StringComparison.OrdinalIgnoreCase))
        {
            IsExposureBrushBurn = true;
            ExposureBrushStatusText = IsExposureBrushMode
                ? "Burn mode selected. Paint to darken local areas."
                : "Burn mode selected. Turn on exposure brush to paint.";
        }
        else if (mode.Equals("dodge", StringComparison.OrdinalIgnoreCase))
        {
            IsExposureBrushBurn = false;
            ExposureBrushStatusText = IsExposureBrushMode
                ? "Dodge mode selected. Paint to brighten local areas."
                : "Dodge mode selected. Turn on exposure brush to paint.";
        }
    }

    private void ApplyExposureBrush()
    {
        if (!CanApplyExposureBrush || CurrentPath is null)
            return;

        var strokes = ExposureBrushStrokes.ToList();
        var result = _editStack.AppendOperation(
            CurrentPath,
            "local-exposure",
            LocalExposureBrushService.ToEditParameters(strokes),
            LocalExposureBrushService.CreateLabel(strokes));

        if (result.Success)
        {
            IsExposureBrushMode = false;
            ClearExposureBrushStrokes(showToast: false);
            Toast(Strings.MainToastExposureBrushAdded);
        }
        else
        {
            ExposureBrushStatusText = "Exposure brush failed: " + result.Message;
            Toast(Strings.MainToastExposureBrushFailed);
        }
    }

    private void CancelExposureBrushMode()
    {
        IsExposureBrushMode = false;
        ClearExposureBrushStrokes(showToast: false);
        Toast(Strings.MainToastExposureBrushCanceled);
    }

    private void ClearExposureBrushStrokes()
    {
        ClearExposureBrushStrokes(showToast: true);
    }

    private void ClearExposureBrushStrokes(bool showToast)
    {
        if (ExposureBrushStrokes.Count > 0)
            ExposureBrushStrokes.Clear();

        ExposureBrushStatusText = IsExposureBrushMode
            ? "Paint on the image. Enter adds strokes to edit history; Esc cancels."
            : "Turn on exposure brush, then paint dodge or burn strokes without changing the source file.";
        RaiseExposureBrushStrokeState();

        if (showToast)
            Toast(Strings.MainToastExposureBrushCleared);
    }

    private void ApplyRedEyeCorrection()
    {
        if (!CanApplyRedEyeCorrection || CurrentPath is null)
            return;

        var marks = RedEyeCorrectionMarks.ToList();
        var result = _editStack.AppendOperation(
            CurrentPath,
            "red-eye",
            RedEyeCorrectionService.ToEditParameters(marks),
            RedEyeCorrectionService.CreateLabel(marks));

        if (result.Success)
        {
            IsRedEyeCorrectionMode = false;
            ClearRedEyeCorrectionMarks(showToast: false);
            Toast(Strings.MainToastRedEyeAdded);
        }
        else
        {
            RedEyeCorrectionStatusText = "Red-eye correction failed: " + result.Message;
            Toast(Strings.MainToastRedEyeFailed);
        }
    }

    private void CancelRedEyeCorrectionMode()
    {
        IsRedEyeCorrectionMode = false;
        ClearRedEyeCorrectionMarks(showToast: false);
        Toast(Strings.MainToastRedEyeCanceled);
    }

    private void ClearRedEyeCorrectionMarks()
    {
        ClearRedEyeCorrectionMarks(showToast: true);
    }

    private void ClearRedEyeCorrectionMarks(bool showToast)
    {
        if (RedEyeCorrectionMarks.Count > 0)
            RedEyeCorrectionMarks.Clear();

        RedEyeCorrectionStatusText = IsRedEyeCorrectionMode
            ? "Click or drag over red pupils. Enter adds corrections to edit history; Esc cancels."
            : "Turn on red-eye correction, then mark red pupils without changing the source file.";
        RaiseRedEyeCorrectionMarkState();

        if (showToast)
            Toast(Strings.MainToastRedEyeCleared);
    }

    private void SetRetouchMode(object? parameter)
    {
        if (parameter is not string mode)
            return;

        if (mode.Equals("heal", StringComparison.OrdinalIgnoreCase))
        {
            IsRetouchHealing = true;
            RetouchStatusText = IsRetouchMode
                ? "Healing brush selected. Alt-click a clean source, then paint the target."
                : "Healing brush selected. Turn on retouch to paint.";
        }
        else if (mode.Equals("clone", StringComparison.OrdinalIgnoreCase))
        {
            IsRetouchHealing = false;
            RetouchStatusText = IsRetouchMode
                ? "Clone stamp selected. Alt-click a source, then paint the target."
                : "Clone stamp selected. Turn on retouch to paint.";
        }
    }

    private void ApplyRetouch()
    {
        if (!CanApplyRetouch || CurrentPath is null)
            return;

        var strokes = RetouchStrokes.ToList();
        var result = _editStack.AppendOperation(
            CurrentPath,
            "retouch",
            RetouchBrushService.ToEditParameters(strokes),
            RetouchBrushService.CreateLabel(strokes));

        if (result.Success)
        {
            IsRetouchMode = false;
            ClearRetouchState(showToast: false);
            Toast(Strings.MainToastRetouchAdded);
        }
        else
        {
            RetouchStatusText = "Retouch failed: " + result.Message;
            Toast(Strings.MainToastRetouchFailed);
        }
    }

    private void CancelRetouchMode()
    {
        IsRetouchMode = false;
        ClearRetouchState(showToast: false);
        Toast(Strings.MainToastRetouchCanceled);
    }

    private void ClearRetouchStrokes()
    {
        if (RetouchStrokes.Count > 0)
            RetouchStrokes.Clear();

        RetouchStatusText = HasRetouchSource
            ? "Paint over the target area. Alt-click picks a new source; Enter applies."
            : "Alt-click or click once to pick a source, then paint the target area.";
        RaiseRetouchStrokeState();
        Toast(Strings.MainToastRetouchStrokesCleared);
    }

    private void ClearRetouchSource()
    {
        _retouchSource = null;
        _retouchStrokeSourceAnchor = null;
        _retouchStrokeTargetAnchor = null;
        RetouchStatusText = Strings.MainRetouchSourceClearedStatus;
        RaiseRetouchSourceState();
        Toast(Strings.MainToastRetouchSourceCleared);
    }

    private void ClearRetouchState(bool showToast)
    {
        if (RetouchStrokes.Count > 0)
            RetouchStrokes.Clear();

        _retouchSource = null;
        _retouchStrokeSourceAnchor = null;
        _retouchStrokeTargetAnchor = null;
        RetouchStatusText = IsRetouchMode
            ? "Alt-click or click once to pick a source, then paint the target area."
            : "Turn on retouch, pick a source, then paint clone or healing strokes.";
        RaiseRetouchSourceState();
        RaiseRetouchStrokeState();

        if (showToast)
            Toast(Strings.MainToastRetouchCleared);
    }

    // -------------------- AI content-aware repair (LaMa inpaint) --------------------

    private readonly System.Collections.ObjectModel.ObservableCollection<InpaintMaskRegion> _inpaintMaskRegions = new();
    private bool _isInpaintMode;
    private double _inpaintBrushRadius = 30;

    public bool IsInpaintMode
    {
        get => _isInpaintMode;
        set { _isInpaintMode = value; Raise(); Raise(nameof(InpaintStatusText)); }
    }

    public double InpaintBrushRadius
    {
        get => _inpaintBrushRadius;
        set { _inpaintBrushRadius = Math.Clamp(value, LaMaInpaintService.MinBrushRadius, LaMaInpaintService.MaxBrushRadius); Raise(); }
    }

    public System.Collections.ObjectModel.ObservableCollection<InpaintMaskRegion> InpaintMaskRegions => _inpaintMaskRegions;

    public bool HasInpaintMaskRegions => _inpaintMaskRegions.Count > 0;

    public bool CanUseInpaint => HasImage && !IsArchiveBook && CurrentFormatSupportsCrop;

    public string InpaintStatusText => IsInpaintMode
        ? HasInpaintMaskRegions
            ? Strings.MainInpaintActiveWithMask
            : Strings.MainInpaintActiveNoMask
        : "";

    public void AddInpaintMaskRegion(double x, double y)
    {
        _inpaintMaskRegions.Add(new InpaintMaskRegion(x, y, InpaintBrushRadius));
        Raise(nameof(HasInpaintMaskRegions));
    }

    private void ToggleInpaintMode()
    {
        if (IsInpaintMode)
        {
            CancelInpaintMode();
            return;
        }

        if (!LaMaInpaintService.IsAvailable())
        {
            Toast(Strings.MainToastNoLamaModel);
            return;
        }

        IsCropMode = false;
        IsSelectionMode = false;
        IsInspectorMode = false;
        IsExposureBrushMode = false;
        IsRedEyeCorrectionMode = false;
        IsRetouchMode = false;
        ClearCropSelection();

        IsInpaintMode = true;
        Toast(Strings.MainToastAiRepaintMode);
    }

    private async void ApplyInpaint()
    {
        if (!HasInpaintMaskRegions || CurrentPath is null) return;

        var path = CurrentPath;
        var regions = _inpaintMaskRegions.ToList();
        var width = PixelWidth;
        var height = PixelHeight;
        if (width <= 0 || height <= 0) return;

        Toast(Strings.MainToastAiRepairRunning);

        InpaintResult result;
        try
        {
            result = await Task.Run(() =>
                LaMaInpaintService.Inpaint(path, regions, width, height));
        }
        catch (Exception ex)
        {
            Toast($"Repair failed: {ex.Message}");
            return;
        }

        if (!result.Success || result.RepairedImage is null)
        {
            Toast(result.ErrorMessage ?? Strings.MainToastRepairFailed);
            return;
        }

        try
        {
            using var repaired = result.RepairedImage;
            repaired.Write(path);
            ShellChangeNotificationService.NotifyFileUpdated(path);
            await ReloadCurrentAsync();
            ClearInpaintMask();
            Toast(Strings.MainToastAiRepairApplied);
        }
        catch (Exception ex)
        {
            Toast($"Failed to save repair: {ex.Message}");
        }
    }

    private void CancelInpaintMode()
    {
        IsInpaintMode = false;
        ClearInpaintMask();
        Toast(Strings.MainToastAiRepairCanceled);
    }

    private void ClearInpaintMask()
    {
        _inpaintMaskRegions.Clear();
        Raise(nameof(HasInpaintMaskRegions));
    }

    private void CancelCropMode()
    {
        IsCropMode = false;
        ClearCropSelection();
        Toast(Strings.MainToastCropCanceled);
    }

    private void CancelSelectionMode()
    {
        IsSelectionMode = false;
        ClearCanvasSelection();
        Toast(Strings.MainToastSelectionCanceled);
    }

    private void CopyCanvasSelection()
    {
        if (!CanCopySelection || CurrentImage is not BitmapSource bitmap || CanvasSelection is not { } selection)
            return;

        try
        {
            ClipboardService.SetImage(ImageSelectionService.ExtractSelection(bitmap, selection));
            Toast($"Copied selection {selection.Width}x{selection.Height}");
        }
        catch (Exception ex)
        {
            SelectionStatusText = "Selection copy failed: " + ex.Message;
            Toast(Strings.MainToastSelectionCopyFailed);
        }
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

    private void RaiseSelectionModeState()
    {
        Raise(nameof(IsSelectionMode));
        Raise(nameof(ShowSelectionOverlay));
        Raise(nameof(IsCanvasSelectionMode));
        Raise(nameof(SelectionModeText));
        Raise(nameof(SelectionModeHelpText));
        Raise(nameof(CanCopySelection));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RaiseSelectionState()
    {
        Raise(nameof(CanvasSelection));
        Raise(nameof(HasCanvasSelection));
        Raise(nameof(ShowSelectionOverlay));
        Raise(nameof(CanvasSelectionText));
        Raise(nameof(CanCopySelection));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RaiseCropModeState()
    {
        Raise(nameof(IsCropMode));
        Raise(nameof(ShowCropOverlay));
        Raise(nameof(IsCanvasSelectionMode));
        Raise(nameof(CropModeText));
        Raise(nameof(CropModeHelpText));
        Raise(nameof(CanApplyCrop));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RaiseCropSelectionState()
    {
        Raise(nameof(CropSelection));
        Raise(nameof(HasCropSelection));
        Raise(nameof(ShowCropOverlay));
        Raise(nameof(CropSelectionText));
        Raise(nameof(CanApplyCrop));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RaiseCropAspectState()
    {
        Raise(nameof(SelectedCropAspectPreset));
        Raise(nameof(CropAspectText));
        Raise(nameof(CropAspectHelpText));
        Raise(nameof(ShowCustomCropAspect));
    }

    private void RaiseExposureBrushModeState()
    {
        Raise(nameof(IsExposureBrushMode));
        Raise(nameof(ShowExposureBrushOverlay));
        Raise(nameof(IsCanvasSelectionMode));
        Raise(nameof(ExposureBrushModeText));
        Raise(nameof(ExposureBrushModeHelpText));
        Raise(nameof(CanApplyExposureBrush));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RaiseExposureBrushSettingsState()
    {
        Raise(nameof(IsExposureBrushBurn));
        Raise(nameof(ExposureBrushToneText));
        Raise(nameof(ExposureBrushRadiusText));
        Raise(nameof(ExposureBrushStrengthText));
    }

    private void RaiseExposureBrushStrokeState()
    {
        Raise(nameof(ExposureBrushStrokes));
        Raise(nameof(HasExposureBrushStrokes));
        Raise(nameof(ShowExposureBrushOverlay));
        Raise(nameof(ExposureBrushStrokeText));
        Raise(nameof(CanApplyExposureBrush));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RaiseRedEyeCorrectionModeState()
    {
        Raise(nameof(IsRedEyeCorrectionMode));
        Raise(nameof(ShowRedEyeCorrectionOverlay));
        Raise(nameof(IsCanvasSelectionMode));
        Raise(nameof(RedEyeCorrectionModeText));
        Raise(nameof(RedEyeCorrectionModeHelpText));
        Raise(nameof(CanApplyRedEyeCorrection));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RaiseRedEyeCorrectionSettingsState()
    {
        Raise(nameof(RedEyeCorrectionRadiusText));
        Raise(nameof(RedEyeCorrectionStrengthText));
        Raise(nameof(RedEyeCorrectionThresholdText));
    }

    private void RaiseRedEyeCorrectionMarkState()
    {
        Raise(nameof(RedEyeCorrectionMarks));
        Raise(nameof(HasRedEyeCorrectionMarks));
        Raise(nameof(ShowRedEyeCorrectionOverlay));
        Raise(nameof(RedEyeCorrectionMarkText));
        Raise(nameof(CanApplyRedEyeCorrection));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RaiseRetouchModeState()
    {
        Raise(nameof(IsRetouchMode));
        Raise(nameof(ShowRetouchOverlay));
        Raise(nameof(IsCanvasSelectionMode));
        Raise(nameof(RetouchModeText));
        Raise(nameof(RetouchModeHelpText));
        Raise(nameof(CanApplyRetouch));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RaiseRetouchSettingsState()
    {
        Raise(nameof(IsRetouchHealing));
        Raise(nameof(RetouchBrushModeText));
        Raise(nameof(RetouchRadiusText));
        Raise(nameof(RetouchStrengthText));
    }

    private void RaiseRetouchSourceState()
    {
        Raise(nameof(HasRetouchSource));
        Raise(nameof(RetouchSourceText));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RaiseRetouchStrokeState()
    {
        Raise(nameof(RetouchStrokes));
        Raise(nameof(HasRetouchStrokes));
        Raise(nameof(ShowRetouchOverlay));
        Raise(nameof(RetouchStrokeText));
        Raise(nameof(CanApplyRetouch));
        CommandManager.InvalidateRequerySuggested();
    }

    // V15-04: Reload re-enumerates the current file through the loader, re-applying WIC /
    // Magick / animated-GIF path as appropriate. Useful after external edit (Photoshop,
    // mspaint). View transforms are restored after the decode attempt so reload does not
    // surprise the user by resetting their current orientation.
    private async Task ReloadCurrentAsync()
    {
        if (!HasImage || IsOperationBusy) return;

        BeginOperationStatus(Strings.MainOpReloadingImage, Strings.MainOpReloadingDetail);
        try
        {
            await YieldForOperationStatusAsync();
            if (ReloadCurrentPreservingViewState(resetPreload: false))
                Toast(Strings.MainToastReloaded);
        }
        finally
        {
            EndOperationStatus();
        }
    }

    // V15-02 / V30-21: Set current image as the desktop wallpaper. Delegates to
    // WallpaperService which copies to %LOCALAPPDATA%\Images\wallpaper\current.<ext> before
    // calling SystemParametersInfo, so a later rename / move of the source file doesn't break
    // the desktop. V30-21 adds explicit Windows layout modes.
    private void SetAsWallpaper(object? parameter)
    {
        if (CurrentPath is null) return;
        var layout = WallpaperService.TryParseLayout(parameter as string, out var parsed)
            ? parsed
            : WallpaperLayout.Fill;

        try
        {
            var dest = _setWallpaper(CurrentPath, layout);
            Toast($"Set as {WallpaperService.DisplayName(layout).ToLowerInvariant()} wallpaper: {Path.GetFileName(dest)}");
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
            Toast(Strings.MainToastFileNoLongerExists);
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
        try { ClipboardService.SetText(path); Toast(Strings.MainToastCopiedPath); }
        catch (Exception ex) { Toast($"Copy failed: {ex.Message}"); }
    }

    private void CopyCurrentImage()
    {
        if (CurrentImage is not BitmapSource bitmap) return;
        try
        {
            _copyImageToClipboard(bitmap);
            Toast(Strings.MainToastCopiedImage);
        }
        catch (Exception ex)
        {
            Toast($"Copy failed: {ex.Message}");
        }
    }

    private void CopyCurrentImageAndPath()
    {
        if (CurrentImage is not BitmapSource bitmap || CurrentPath is null) return;
        try
        {
            _copyImageAndPathToClipboard(bitmap, CurrentPath);
            Toast(Strings.MainToastCopiedImageAndPath);
        }
        catch (Exception ex)
        {
            Toast($"Copy failed: {ex.Message}");
        }
    }

    private void TransferCurrentImage(ImageFileTransferMode mode, string? destinationFolder)
    {
        if (CurrentPath is null || IsOperationBusy) return;

        var sourcePath = CurrentPath;
        if (string.IsNullOrWhiteSpace(destinationFolder))
        {
            try
            {
                destinationFolder = _pickFolder(mode == ImageFileTransferMode.Copy
                    ? Strings.MainDialogCopyToFolder
                    : Strings.MainDialogMoveToFolder);
            }
            catch (Exception ex)
            {
                Toast($"Folder picker failed: {ex.Message}");
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(destinationFolder))
            return;

        var verb = mode == ImageFileTransferMode.Copy ? "Copying" : "Moving";
        BeginOperationStatus($"{verb} image", $"{verb} {Path.GetFileName(sourcePath)} to {DisplayFolderName(destinationFolder)}.");
        try
        {
            var result = _fileTransfer.Transfer(sourcePath, destinationFolder, mode);
            HandleTransferResult(result, destinationFolder);
        }
        finally
        {
            EndOperationStatus();
        }
    }

    private void HandleTransferResult(ImageFileTransferResult result, string requestedDestinationFolder)
    {
        switch (result.Status)
        {
            case ImageFileTransferStatus.Succeeded:
                HandleSuccessfulTransfer(result, requestedDestinationFolder);
                break;
            case ImageFileTransferStatus.AlreadyInDestination:
                Toast($"Already in {DisplayFolderName(requestedDestinationFolder)}");
                break;
            case ImageFileTransferStatus.SourceMissing:
                ShowSecondaryStatus(
                    Strings.MainSecondaryFileGone,
                    Strings.MainSecondaryTransferSourceDetail,
                    SecondaryStatusToneKind.Warning,
                    "\uE783");
                Toast(Strings.MainToastFileNoLongerExists);
                break;
            case ImageFileTransferStatus.DestinationMissing:
                ShowSecondaryStatus(
                    Strings.MainSecondaryTransferFolderGone,
                    Strings.MainSecondaryTransferFolderGoneDetail,
                    SecondaryStatusToneKind.Warning,
                    "\uE8B7");
                RefreshRecentTransferFolders();
                Toast(Strings.MainToastTransferFolderGone);
                break;
            case ImageFileTransferStatus.UnsupportedSource:
                ShowSecondaryStatus(
                    "File type not supported",
                    Strings.MainSecondaryTransferNotSupportedDetail,
                    SecondaryStatusToneKind.Warning,
                    "\uE783");
                Toast(Strings.MainToastTransferUnsupported);
                break;
            default:
                ShowSecondaryStatus(
                    result.Mode == ImageFileTransferMode.Copy ? Strings.MainSecondaryCopyFailed : Strings.MainSecondaryMoveFailed,
                    FirstLine(result.Message),
                    SecondaryStatusToneKind.Error,
                    "\uE783");
                Toast($"{(result.Mode == ImageFileTransferMode.Copy ? "Copy" : "Move")} failed: {FirstLine(result.Message)}");
                break;
        }
    }

    private void HandleSuccessfulTransfer(ImageFileTransferResult result, string requestedDestinationFolder)
    {
        var destinationFolder = Path.GetDirectoryName(result.DestinationPath) ?? requestedDestinationFolder;
        _settings.TouchRecentTransferFolder(destinationFolder);
        RefreshRecentTransferFolders();

        ShellChangeNotificationService.NotifyFileUpdated(result.DestinationPath);
        ShellChangeNotificationService.NotifyFileUpdated(result.SourcePath);

        ClearSecondaryStatus();
        var destinationLabel = $"{DisplayFolderName(destinationFolder)}\\{Path.GetFileName(result.DestinationPath)}";
        if (result.Mode == ImageFileTransferMode.Move)
        {
            _recoveryCenter.RecordMove(
                result.SourcePath,
                result.DestinationPath,
                Strings.MainMovedImage,
                $"Moved {Path.GetFileName(result.SourcePath)} to {DisplayFolderName(destinationFolder)}.",
                BuildRecoverySidecarMoves(result));
            OpenFile(result.DestinationPath);
            Toast($"Moved to {destinationLabel}");
            return;
        }

        Toast($"Copied to {destinationLabel}");
    }

    private static string? PickFolder(string title)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = title
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
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

    // E6/V100-01: save a copy of the decoded frame, applying persisted edit-stack operations
    // when they exist. Temporary viewing transforms stay temporary.
    private async Task SaveAsCopyAsync()
    {
        if (CurrentImage is not System.Windows.Media.Imaging.BitmapSource bs || CurrentPath is null) return;

        var sourceExtension = ImageExportService.NormalizeExportExtension(Path.GetExtension(CurrentPath));

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = Strings.MainDialogSaveCopy,
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
                Toast(Strings.MainChooseDifferentFilename);
                return;
            }
        }
        catch (Exception ex)
        {
            Toast($"Save failed: {ex.Message}");
            return;
        }

        BeginOperationStatus(Strings.MainOpSavingCopy, $"Exporting {Path.GetFileName(targetPath)}.");
        try
        {
            await YieldForOperationStatusAsync();
            if (_editStack.HasEnabledOperations(CurrentPath))
            {
                var result = await Task.Run(() => _editStack.Export(CurrentPath, targetPath));
                if (result.Success)
                    Toast($"Saved edited copy → {Path.GetFileName(result.OutputPath)}");
                else
                    Toast($"Save failed: {result.Message}");
            }
            else
            {
                var savedPath = ImageExportService.Save(bs, targetPath);
                Toast($"Saved copy → {Path.GetFileName(savedPath)}");
            }
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
                Toast(Strings.MainToastSentToPrinter);
        }
        catch (Exception ex)
        {
            Toast($"Print failed: {ex.Message}");
        }
    }

    private void PrintCurrentToDefault()
    {
        if (CurrentImage is not System.Windows.Media.Imaging.BitmapSource bs || CurrentPath is null) return;
        try
        {
            _printDefault(bs, System.IO.Path.GetFileName(CurrentPath));
            Toast(Strings.MainToastSentToDefaultPrinter);
        }
        catch (Exception ex)
        {
            Toast($"Default print failed: {ex.Message}");
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

    private void OpenDuplicateCleanup()
    {
        var cleanup = new Images.DuplicateCleanupWindow(_recoveryCenter)
        {
            Owner = Application.Current?.MainWindow
        };
        cleanup.CompareRequested += (_, e) => StartCompareWithPair(e.PrimaryPath, e.SecondaryPath);

        if (!string.IsNullOrWhiteSpace(CurrentFolder) && Directory.Exists(CurrentFolder))
            cleanup.AddScanFolder(CurrentFolder);

        cleanup.Show();
    }

    private void OpenFileHealthScan()
    {
        var scan = new Images.FileHealthScanWindow(_recoveryCenter)
        {
            Owner = Application.Current?.MainWindow
        };

        if (!string.IsNullOrWhiteSpace(CurrentFolder) && Directory.Exists(CurrentFolder))
            scan.AddScanFolder(CurrentFolder);

        scan.Show();
    }

    private void OpenRecoveryCenter()
    {
        var recovery = new Images.RecoveryCenterWindow(_recoveryCenter)
        {
            Owner = Application.Current?.MainWindow
        };

        recovery.Show();
    }

    private void OpenModelManager()
    {
        var manager = new Images.ModelManagerWindow
        {
            Owner = Application.Current?.MainWindow
        };

        manager.Show();
    }

    private void OpenSemanticSearch()
    {
        var semanticSearch = new Images.SemanticSearchWindow
        {
            Owner = Application.Current?.MainWindow
        };

        if (Directory.Exists(CurrentFolder))
            semanticSearch.AddSearchRoot(CurrentFolder);

        semanticSearch.OpenRequested += (_, args) => OpenFile(args.Path);
        semanticSearch.Show();
    }

    private void OpenTagGraph()
    {
        var tagGraph = new Images.TagGraphWindow
        {
            Owner = Application.Current?.MainWindow
        };

        tagGraph.SetCurrentImage(CurrentPath);
        tagGraph.Show();
    }

    private void OpenImportInbox()
    {
        var inbox = new Images.ImportInboxWindow
        {
            Owner = Application.Current?.MainWindow
        };

        if (!string.IsNullOrWhiteSpace(CurrentPath) && File.Exists(CurrentPath))
            inbox.AddSource(CurrentPath);

        inbox.Show();
        if (!string.IsNullOrWhiteSpace(CurrentPath) && File.Exists(CurrentPath))
            _ = inbox.ReloadAsync();
    }

    private void ImportXmpSidecars()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select folder containing XMP sidecars",
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
            return;

        try
        {
            var importer = new XmpSidecarImportService();
            var result = importer.ScanFolder(dialog.FolderName);

            if (result.TotalScanned == 0)
            {
                Toast(Strings.MainToastXmpNoSidecars);
                return;
            }

            var summary = XmpSidecarImportService.ApplyFolderRatings(
                result,
                XmpSidecarImportService.FindImageForSidecar,
                (imagePath, rating) => _reviewLabels.SetRating(imagePath, rating));

            Toast(Strings.Format(
                nameof(Strings.MainToastXmpImportResult),
                summary.RatingsApplied,
                summary.SkippedWithoutRating,
                summary.UnmatchedImages,
                summary.FailedSidecars));
        }
        catch (Exception ex)
        {
            Toast(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                Strings.MainToastXmpImportFailed, ex.Message));
        }
    }

    private void OpenMacroActions()
    {
        var macros = new Images.MacroActionWindow
        {
            Owner = Application.Current?.MainWindow
        };

        if (!string.IsNullOrWhiteSpace(CurrentPath) && File.Exists(CurrentPath))
            macros.AddSource(CurrentPath);

        macros.Show();
    }

    private void OpenBatchProcessor()
    {
        var batch = new Images.BatchProcessorWindow
        {
            Owner = Application.Current?.MainWindow
        };

        if (!string.IsNullOrWhiteSpace(CurrentPath) && File.Exists(CurrentPath))
            batch.AddSource(CurrentPath);

        batch.Show();
    }

    private void OpenExportWorkbench()
    {
        if (CurrentImage is not BitmapSource bitmap)
            return;

        var extension = CurrentPath is null
            ? ".jpg"
            : ImageExportService.NormalizeExportExtension(Path.GetExtension(CurrentPath));
        var window = new Images.ExportPreviewWindow(bitmap, CurrentPath, extension)
        {
            Owner = Application.Current?.MainWindow
        };

        window.Show();
    }

    private void OpenEditStack()
    {
        var edits = new Images.EditStackWindow
        {
            Owner = Application.Current?.MainWindow
        };

        edits.SetCurrentImage(CurrentPath);
        edits.Show();
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
        RefreshCommandPalette();
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

        WritebackGuardService.CreateBackup(path, WritebackGuardService.GetBackupMode(_settings));

        BeginOperationStatus(Strings.MainOpRemovingLocation, $"Updating {Path.GetFileName(path)}.");
        try
        {
            var removed = await Task.Run(() => MetadataEditService.StripGpsMetadata(path));
            if (removed == 0)
            {
                Toast(Strings.MainToastNoGpsData);
            }
            else
            {
                _recoveryCenter.RecordWriteback(
                    path,
                    Strings.MainGpsWriteback,
                    $"Removed {removed} GPS metadata field{(removed == 1 ? "" : "s")} from {Path.GetFileName(path)}.");
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

    private async Task StripMetadataAsync(MetadataStripCategory categories)
    {
        if (CurrentPath is null || IsOperationBusy) return;
        var path = CurrentPath;
        var label = MetadataEditService.CategoryLabel(categories);

        WritebackGuardService.CreateBackup(path, WritebackGuardService.GetBackupMode(_settings));

        BeginOperationStatus(
            string.Format(System.Globalization.CultureInfo.InvariantCulture, Strings.MainOpStrippingMetadata, label),
            $"Updating {Path.GetFileName(path)}.");
        try
        {
            var result = await Task.Run(() => MetadataEditService.StripMetadata(path, categories));
            if (result.RemovedCount == 0)
            {
                Toast(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    Strings.MainToastNoMetadataFound, label));
            }
            else
            {
                _recoveryCenter.RecordWriteback(
                    path,
                    string.Format(System.Globalization.CultureInfo.InvariantCulture, Strings.MainMetadataWriteback, label),
                    $"Removed {result.RemovedCount} {label} field{(result.RemovedCount == 1 ? "" : "s")} from {Path.GetFileName(path)}.");
                _preload.Reset();
                LoadCurrent();
                Toast(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    Strings.MainToastMetadataStripped, label, result.RemovedCount,
                    result.RemovedCount == 1 ? "field" : "fields"));
            }
        }
        catch (Exception ex)
        {
            Toast(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                Strings.MainToastMetadataStripFailed, label, ex.Message));
        }
        finally
        {
            EndOperationStatus();
        }
    }

    private async Task ExtractMotionVideoAsync()
    {
        if (CurrentPath is null) return;
        var path = CurrentPath;

        if (_motionPhoto is not null)
        {
            var output = await Task.Run(() => MotionPhotoService.ExtractEmbeddedVideo(path, _motionPhoto));
            if (output is not null)
                Toast(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    Strings.MainToastMotionVideoExtracted, Path.GetFileName(output)));
            else
                Toast(Strings.MainToastMotionVideoFailed);
        }
        else if (CompanionVideoPath is not null)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = CompanionVideoPath,
                    UseShellExecute = true
                });
                Toast(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    Strings.MainToastCompanionVideoOpened, Path.GetFileName(CompanionVideoPath)));
            }
            catch
            {
                Toast(Strings.MainToastCompanionVideoFailed);
            }
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
        Toast(Strings.MainToastFolderRefreshed);
    }

    private bool RefreshFromNav() => LoadCurrent();

    private void ClearCurrentState()
    {
        _renameTimer.Stop();
        _externalEditReload.Disarm();
        IsPinnedOverlayMode = false;
        ClearCompareMode(showToast: false);
        CurrentTilePyramid = null;
        SetCurrentImageWithChannel(null);
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
        IsSelectionMode = false;
        ClearCanvasSelection();
        IsCropMode = false;
        ClearCropSelection();
        IsExposureBrushMode = false;
        ClearExposureBrushStrokes(showToast: false);
        IsRedEyeCorrectionMode = false;
        ClearRedEyeCorrectionMarks(showToast: false);
        IsRetouchMode = false;
        ClearRetouchState(showToast: false);
        ResetPageState();
        ClearPhotoMetadata();
        ClearColorAnalysis();
        ClearC2paInspection();
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

    private sealed record ReviewLabelUndoEntry(string Path, ReviewLabelState State);

    private static IReadOnlyList<RecoverySidecarMove> BuildRecoverySidecarMoves(ImageFileTransferResult result)
    {
        if (result.SidecarDestinationPaths.Count == 0)
            return [];

        var moves = new List<RecoverySidecarMove>();
        var sourceDirectory = Path.GetDirectoryName(result.SourcePath);
        var sourceStem = Path.GetFileNameWithoutExtension(result.SourcePath);
        foreach (var sidecarDestination in result.SidecarDestinationPaths)
        {
            if (string.IsNullOrWhiteSpace(sidecarDestination))
                continue;

            if (sidecarDestination.Equals(result.DestinationPath + ".xmp", StringComparison.OrdinalIgnoreCase))
            {
                moves.Add(new RecoverySidecarMove(result.SourcePath + ".xmp", sidecarDestination));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(sourceDirectory) &&
                !string.IsNullOrWhiteSpace(sourceStem) &&
                Path.GetFileName(sidecarDestination).Equals(
                    Path.GetFileNameWithoutExtension(result.DestinationPath) + ".xmp",
                    StringComparison.OrdinalIgnoreCase))
            {
                moves.Add(new RecoverySidecarMove(Path.Combine(sourceDirectory, sourceStem + ".xmp"), sidecarDestination));
            }
        }

        return moves;
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        FlushPendingRename();
        _isDisposed = true;

        _renameTimer.Stop();
        _toastTimer.Stop();
        _animationTimer.Stop();
        _hintTimer.Stop();
        _slideshowTimer?.Stop();
        _slideshowTimer = null;
        _listenService?.Dispose();
        _externalEditReload.Dispose();

        _nav.ListChanged -= OnDirectoryListChanged;
        _ocrWorkflow.Dispose();
        _photoMetadata.Dispose();
        _colorAnalysis.Dispose();
        _c2paInspection.Dispose();
        _folderPreview.Dispose();
        _preload.Dispose();
        _nav.Dispose();

        GC.SuppressFinalize(this);
    }
}

internal enum LosslessJpegTrimChoice
{
    ApplyTrimmedLossless,
    ReencodeExact,
    Cancel,
}

internal sealed record LosslessJpegTrimConfirmation(
    string Title,
    string Message,
    string TrimActionText,
    string ExactActionText)
{
    public static LosslessJpegTrimConfirmation ForCrop(LosslessJpegCropPlan plan)
    {
        var aligned = plan.AlignedSelection;
        var trimmedSize = aligned is null ? "the aligned JPEG MCU area" : $"{aligned.Value.Width} x {aligned.Value.Height} px";
        return new LosslessJpegTrimConfirmation(
            "Lossless JPEG crop needs trimming",
            $"{plan.UserMessage} The requested crop is {plan.RequestedSelection.Width} x {plan.RequestedSelection.Height} px; the lossless JPEG result will be {trimmedSize}.",
            "apply the trimmed crop losslessly with jpegtran",
            "keep the exact crop by re-encoding the JPEG");
    }

    public static LosslessJpegTrimConfirmation ForRotation(LosslessJpegRotationPlan plan)
        => new(
            "Lossless JPEG rotation needs trimming",
            $"{plan.UserMessage} The trimmed edge pixels are required only for the lossless jpegtran path.",
            "apply the rotation losslessly after trimming edge pixels",
            "keep the full image by re-encoding the JPEG");
}
