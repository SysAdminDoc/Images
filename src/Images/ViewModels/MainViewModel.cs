using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Images.Services;

namespace Images.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly DirectoryNavigator _nav = new();
    private readonly RenameService _rename = new();
    private readonly PreloadService _preload = new();
    private readonly DispatcherTimer _renameTimer;
    private readonly DispatcherTimer _toastTimer;

    private bool _suppressStemChange;
    private string _committedStemOnDisk = string.Empty;

    private readonly DispatcherTimer _hintTimer;

    public MainViewModel()
    {
        _renameTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _renameTimer.Tick += (_, _) => { _renameTimer.Stop(); FlushPendingRename(); };

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); ToastMessage = null; };

        _hintTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2400) };
        _hintTimer.Tick += (_, _) => { _hintTimer.Stop(); ShowGestureHint = false; };

        OpenCommand = new RelayCommand(OpenFileDialog);
        NextCommand = new RelayCommand(Next, () => HasImage);
        PrevCommand = new RelayCommand(Prev, () => HasImage);
        FirstCommand = new RelayCommand(First, () => HasImage);
        LastCommand = new RelayCommand(Last, () => HasImage);
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
        Raise(nameof(PositionText));
        if (CurrentPath is not null && !File.Exists(CurrentPath))
        {
            // Current file was deleted externally — pick whatever slot the navigator landed on.
            if (_nav.CurrentPath is not null) LoadCurrent();
            else ClearCurrentState();
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
        set => Set(ref _isPeekMode, value);
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
            // Normalize user-typed extension: ensure leading dot, no spaces/invalid chars.
            var normalized = value?.Trim() ?? string.Empty;
            if (normalized.Length > 0 && !normalized.StartsWith('.'))
                normalized = "." + normalized;
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
        var fresh = SettingsService.Instance.GetRecentFolders();
        RecentFolders.Clear();
        foreach (var f in fresh) RecentFolders.Add(f);
    }

    // -------------------- Recent renames --------------------

    public ObservableCollection<RenameService.UndoEntry> RecentRenames { get; } = new();

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

    // -------------------- Navigation --------------------

    public void OpenFile(string path)
    {
        FlushPendingRename();
        _nav.Open(path);
        LoadCurrent();

        // V20-02: persist containing folder to recent-folders MRU. Silent on any failure —
        // recent-folders is a convenience, not critical.
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
        {
            SettingsService.Instance.TouchRecentFolder(folder);
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

    private void Next() { FlushPendingRename(); if (_nav.MoveNext()) LoadCurrent(); }
    private void Prev() { FlushPendingRename(); if (_nav.MovePrevious()) LoadCurrent(); }
    private void First() { FlushPendingRename(); if (_nav.MoveFirst()) LoadCurrent(); }
    private void Last() { FlushPendingRename(); if (_nav.MoveLast()) LoadCurrent(); }

    private void LoadCurrent()
    {
        var path = _nav.CurrentPath;
        if (path is null)
        {
            ClearCurrentState();
            return;
        }

        try
        {
            // V20-03: try the preload ring first — a hit is instant, a miss falls through to
            // the direct load. Either way we re-enqueue the new neighbors.
            var res = _preload.TryGet(path) ?? ImageLoader.Load(path);
            // Order matters: CurrentImage first so ZoomPanImage.OnSourceChanged runs and clears
            // any animation from the previous file; then CurrentAnimation, which either applies
            // new keyframes or stays null for a static image.
            CurrentImage = res.Image;
            CurrentAnimation = res.Animation;
            PixelWidth = res.PixelWidth;
            PixelHeight = res.PixelHeight;
            DecoderUsed = res.DecoderUsed;
            Rotation = 0;
            FlipHorizontal = false;
            FlipVertical = false;
            LoadErrorMessage = null;

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
            LoadErrorMessage = $"This file could not be decoded. {ex.Message}";
            Toast(ex.Message.Contains("requires Ghostscript", StringComparison.OrdinalIgnoreCase)
                ? "Document preview needs Ghostscript"
                : "Could not decode this file");
        }

        CurrentPath = path;
        try { _fileSize = new FileInfo(path).Length; } catch { _fileSize = 0; }
        Raise(nameof(FileSizeText));

        SyncRenameEditorFromDisk();

        // V20-03: after loading, enqueue neighbours so the next arrow-press is instant.
        // The preload itself runs off the UI thread.
        EnqueueNeighbours();
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
        try
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                toDelete,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,
                Microsoft.VisualBasic.FileIO.UICancelOption.DoNothing);

            _nav.RemoveCurrent();
            Toast($"Sent to Recycle Bin: {Path.GetFileName(toDelete)}");
            if (_nav.CurrentPath is null) { ClearCurrentState(); return; }
            LoadCurrent();
        }
        catch (Exception ex)
        {
            Toast($"Delete failed: {ex.Message}");
        }
    }

    private void Rotate(double delta)
    {
        Rotation = (Rotation + delta) % 360;
    }

    // V15-04: Reload re-enumerates the current file through the loader, re-applying WIC /
    // Magick / animated-GIF path as appropriate. Useful after external edit (Photoshop,
    // mspaint). Rotation + flip state survive because LoadCurrent only reassigns them when
    // a NEW path is loaded; here the path is identical so we keep whatever the user had.
    private void ReloadCurrent()
    {
        var savedRotation = Rotation;
        var savedFlipH = FlipHorizontal;
        var savedFlipV = FlipVertical;
        LoadCurrent();
        Rotation = savedRotation;
        FlipHorizontal = savedFlipH;
        FlipVertical = savedFlipV;
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

    private void RevealInExplorer()
    {
        if (CurrentPath is null) return;
        string full;
        try { full = System.IO.Path.GetFullPath(CurrentPath); }
        catch (Exception ex) { Toast($"Could not open Explorer: {ex.Message}"); return; }

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = false,
            };
            // ArgumentList bypasses CommandLineToArgvW quoting rules entirely — the single token
            // "/select,C:\path with space.jpg" goes through verbatim as argv[1] so no injection
            // vector via embedded quotes or commas in the filename.
            psi.ArgumentList.Add("/select," + full);
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex) { Toast($"Could not open Explorer: {ex.Message}"); }
    }

    private void CopyPath()
    {
        if (CurrentPath is null) return;
        try { Clipboard.SetText(CurrentPath); Toast("Copied path"); }
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
        UpdateCheckService.LastCheckedUtc = DateTime.UtcNow;

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
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = LatestUpdateUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Toast($"Could not open release page: {ex.Message}");
        }
    }

    // E6: save a copy of the current image to a user-chosen path. The written bytes are a
    // re-encode of the displayed first-frame via WIC's format-appropriate encoder (JpegBitmapEncoder
    // for .jpg, PngBitmapEncoder for .png, etc.). Rotation and flip are NOT baked in — print
    // keeps the viewer's in-session edits out of the file (same convention as Windows Photos).
    // If the user wants the transformed version, they can screenshot the viewer.
    private void SaveAsCopy()
    {
        if (CurrentImage is not System.Windows.Media.Imaging.BitmapSource bs || CurrentPath is null) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save a copy",
            FileName = Path.GetFileNameWithoutExtension(CurrentPath) + "_copy" + Path.GetExtension(CurrentPath),
            Filter = "JPEG|*.jpg;*.jpeg|PNG|*.png|BMP|*.bmp|TIFF|*.tif;*.tiff|All files|*.*",
            InitialDirectory = Path.GetDirectoryName(CurrentPath),
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
            System.Windows.Media.Imaging.BitmapEncoder encoder = ext switch
            {
                ".jpg" or ".jpeg" or ".jfif" => new System.Windows.Media.Imaging.JpegBitmapEncoder { QualityLevel = 92 },
                ".png" => new System.Windows.Media.Imaging.PngBitmapEncoder(),
                ".bmp" => new System.Windows.Media.Imaging.BmpBitmapEncoder(),
                ".tif" or ".tiff" => new System.Windows.Media.Imaging.TiffBitmapEncoder(),
                ".gif" => new System.Windows.Media.Imaging.GifBitmapEncoder(),
                _ => new System.Windows.Media.Imaging.PngBitmapEncoder(), // default to PNG for lossless
            };
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bs));
            using (var fs = new FileStream(dlg.FileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                encoder.Save(fs);
            }
            Toast($"Saved copy → {Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex)
        {
            Toast($"Save failed: {ex.Message}");
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

    private void RefreshFolder()
    {
        _nav.Refresh();
        RefreshFromNav();
        Toast("Folder refreshed");
    }

    private void RefreshFromNav() => LoadCurrent();

    private void ClearCurrentState()
    {
        _renameTimer.Stop();
        CurrentImage = null;
        CurrentAnimation = null;
        CurrentPath = null;
        PixelWidth = PixelHeight = 0;
        _fileSize = 0;
        Rotation = 0;
        FlipHorizontal = false;
        FlipVertical = false;
        LoadErrorMessage = null;
        DecoderUsed = null;
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
}
