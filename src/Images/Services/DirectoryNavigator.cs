using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Images.Services;

/// <summary>
/// Scans a folder for image files, keeps a sorted list, and exposes prev/next with wrap-around.
/// Watches the folder so external file ops (delete, add, rename from elsewhere) stay reflected.
/// </summary>
public sealed class DirectoryNavigator : IDisposable
{
    public static readonly HashSet<string> SupportedExtensions = new(
        SupportedImageFormats.Extensions,
        StringComparer.OrdinalIgnoreCase);

    private const int MaxHistoryDepth = 50;

    private List<string> _files = new();
    private string? _folder;
    private FileSystemWatcher? _watcher;
    private DispatcherTimer? _watchDebounce;
    private readonly Dispatcher _dispatcher;
    private readonly Func<string, IEnumerable<string>> _enumerateFiles;
    private readonly LinkedList<string> _backStack = new();
    private readonly LinkedList<string> _forwardStack = new();
    private bool _navigatingHistory;

    public IReadOnlyList<string> Files => _files;
    public int Count => _files.Count;
    public int CurrentIndex { get; private set; } = -1;
    public string? CurrentPath => CurrentIndex >= 0 && CurrentIndex < _files.Count ? _files[CurrentIndex] : null;
    public string? Folder => _folder;
    public DirectorySortMode SortMode { get; private set; } = DirectorySortMode.NaturalName;
    public bool SiblingFolderAutoSwitch { get; set; }
    public bool CanGoBack => _backStack.Count > 0;
    public bool CanGoForward => _forwardStack.Count > 0;

    public event EventHandler? ListChanged;

    public DirectoryNavigator()
        : this(null)
    {
    }

    internal DirectoryNavigator(Func<string, IEnumerable<string>>? enumerateFiles)
    {
        // Captured so FSW event callbacks (raised on a ThreadPool thread) can marshal back.
        _dispatcher = Dispatcher.CurrentDispatcher;
        _enumerateFiles = enumerateFiles ?? DefaultEnumerateFiles;
    }

    public static string? FirstSupportedImageInFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return null;

        try
        {
            var files = Directory
                .EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
                .ToList();
            if (files.Count == 0) return null;

            files.Sort(CompareNaturalName);
            return files[0];
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException or System.Security.SecurityException)
        {
            return null;
        }
    }

    public bool OpenExplicitList(IReadOnlyList<string> paths, int startIndex = 0)
    {
        if (paths is null || paths.Count == 0) return false;

        var resolved = new List<string>(paths.Count);
        foreach (var p in paths)
        {
            try
            {
                var full = Path.GetFullPath(p);
                if (File.Exists(full) && SupportedExtensions.Contains(Path.GetExtension(full)))
                    resolved.Add(full);
            }
            catch
            {
            }
        }

        if (resolved.Count == 0) return false;

        DetachWatcher();
        _folder = null;
        _files = resolved;
        CurrentIndex = Math.Clamp(startIndex, 0, _files.Count - 1);
        _backStack.Clear();
        _forwardStack.Clear();
        ListChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Load the folder containing <paramref name="path"/> and point CurrentIndex at it.
    /// </summary>
    public bool Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        string full;
        try { full = Path.GetFullPath(path); }
        catch { return false; }
        if (!File.Exists(full)) return false;
        if (!SupportedExtensions.Contains(Path.GetExtension(full))) return false;

        var folder = Path.GetDirectoryName(full);
        if (string.IsNullOrEmpty(folder)) return false;

        // Explicit-list mode: the file list was set via OpenExplicitList (e.g., recursive folder
        // browse). If the target is already in the list, just move the index — never rescan a
        // single folder and destroy the cross-directory list. Exact-path only: the filename
        // fallback would match a same-named file from a different directory (camera names like
        // IMG_0001.jpg collide across folders) and silently show the wrong image.
        if (_folder is null && _files.Count > 0 && TrySetCurrentIndex(full, allowFileNameFallback: false))
            return true;

        // Short-circuit: if we're already watching this folder, move to the exact file. If the
        // file was created after the last scan, rescan once before declaring the open failed.
        if (_folder is not null && string.Equals(_folder, folder, StringComparison.OrdinalIgnoreCase))
        {
            if (TrySetCurrentIndex(full)) return true;
            if (!Rescan(folder)) return false;
            return TrySetCurrentIndex(full);
        }

        var previousPath = CurrentPath;
        var previousFolder = _folder;
        var previousFiles = _files;
        var previousIndex = CurrentIndex;

        if (!Rescan(folder) || !TrySetCurrentIndex(full))
        {
            _folder = previousFolder;
            _files = previousFiles;
            CurrentIndex = previousIndex;
            return false;
        }

        if (previousPath is not null && !_navigatingHistory)
        {
            PushBack(previousPath);
            _forwardStack.Clear();
        }

        AttachWatcher(folder);
        return true;
    }

    private bool TrySetCurrentIndex(string full, bool allowFileNameFallback = true)
    {
        var idx = _files.FindIndex(f => string.Equals(f, full, StringComparison.OrdinalIgnoreCase));
        if (idx < 0 && allowFileNameFallback)
        {
            // Fallback match on filename only — handles case where normalization differs on
            // comparison. Safe only for a single watched folder, where filenames are unique;
            // never in a cross-directory explicit list.
            var name = Path.GetFileName(full);
            idx = _files.FindIndex(f => string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase));
        }

        if (idx < 0) return false;
        CurrentIndex = idx;
        return true;
    }

    /// <summary>
    /// Force a rescan of the current folder; tries to preserve CurrentPath.
    /// </summary>
    public void Refresh()
    {
        if (_folder is null) return;
        var keep = CurrentPath;
        Rescan(_folder);
        if (keep is not null)
        {
            var idx = _files.FindIndex(f => string.Equals(f, keep, StringComparison.OrdinalIgnoreCase));
            CurrentIndex = idx >= 0 ? idx : ClampIndex(CurrentIndex);
        }
        else
        {
            CurrentIndex = ClampIndex(CurrentIndex);
        }
    }

    public bool MoveNext()
    {
        if (_files.Count == 0) return false;

        if (SiblingFolderAutoSwitch && CurrentIndex == _files.Count - 1)
        {
            var next = FindSiblingFolder(forward: true);
            if (next is not null)
                return Open(next);
        }

        CurrentIndex = (CurrentIndex + 1) % _files.Count;
        return true;
    }

    public bool MovePrevious()
    {
        if (_files.Count == 0) return false;

        if (SiblingFolderAutoSwitch && CurrentIndex == 0)
        {
            var prev = FindSiblingFolder(forward: false);
            if (prev is not null)
            {
                if (!Open(prev)) return false;
                CurrentIndex = _files.Count - 1;
                return true;
            }
        }

        CurrentIndex = (CurrentIndex - 1 + _files.Count) % _files.Count;
        return true;
    }

    public bool MoveFirst()
    {
        if (_files.Count == 0) return false;
        CurrentIndex = 0;
        return true;
    }

    public bool MoveLast()
    {
        if (_files.Count == 0) return false;
        CurrentIndex = _files.Count - 1;
        return true;
    }

    public bool GoBack()
    {
        return NavigateHistory(_backStack, _forwardStack);
    }

    public bool GoForward()
    {
        return NavigateHistory(_forwardStack, _backStack);
    }

    public IReadOnlyList<string> GetBackHistory() => [.. _backStack];
    public IReadOnlyList<string> GetForwardHistory() => [.. _forwardStack];

    private static void PushBounded(LinkedList<string> list, string path)
    {
        list.AddFirst(path);
        if (list.Count > MaxHistoryDepth)
            list.RemoveLast();
    }

    private void PushBack(string path) => PushBounded(_backStack, path);
    private void PushForward(string path) => PushBounded(_forwardStack, path);

    private bool NavigateHistory(LinkedList<string> source, LinkedList<string> destination)
    {
        var previous = CurrentPath;

        while (source.Count > 0)
        {
            var target = source.First!.Value;
            source.RemoveFirst();

            _navigatingHistory = true;
            try
            {
                if (!Open(target))
                    continue;
            }
            finally { _navigatingHistory = false; }

            if (previous is not null)
                PushBounded(destination, previous);
            return true;
        }

        return false;
    }

    public bool SetSortMode(DirectorySortMode mode)
    {
        if (SortMode == mode) return false;

        SortMode = mode;
        var keep = CurrentPath;
        SortFiles(_files);
        RestoreCurrentPathOrClamp(keep);
        ListChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void UpdateCurrentPath(string newPath)
    {
        var currentPath = CurrentPath;
        if (currentPath is null) return;

        UpdateCurrentPath(currentPath, newPath);
    }

    public void UpdateCurrentPath(string oldPath, string newPath)
    {
        if (_files.Count == 0) return;
        if (CurrentIndex >= _files.Count)
        {
            CurrentIndex = ClampIndex(CurrentIndex);
        }

        var fullPath = Path.GetFullPath(newPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The renamed file no longer exists.", fullPath);
        if (!SupportedExtensions.Contains(Path.GetExtension(fullPath)))
            throw new InvalidOperationException("The renamed file extension is not supported by Images.");

        var oldFullPath = Path.GetFullPath(oldPath);
        var index = _files.FindIndex(path => string.Equals(path, oldFullPath, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            throw new FileNotFoundException("The renamed source is no longer in the navigator list.", oldFullPath);

        _files[index] = fullPath;
    }

    public void RemoveCurrent()
    {
        if (CurrentIndex < 0) return;
        RemoveAt(CurrentIndex);
    }

    public bool RemovePath(string path)
    {
        if (_files.Count == 0 || string.IsNullOrWhiteSpace(path))
            return false;

        var fullPath = Path.GetFullPath(path);
        var index = _files.FindIndex(candidate => string.Equals(candidate, fullPath, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return false;

        RemoveAt(index);
        return true;
    }

    private void RemoveAt(int index)
    {
        if (index < 0 || index >= _files.Count) return;
        _files.RemoveAt(index);
        if (_files.Count == 0) { CurrentIndex = -1; return; }
        if (CurrentIndex > index) CurrentIndex--;
        if (CurrentIndex >= _files.Count) CurrentIndex = _files.Count - 1;
    }

    private bool Rescan(string folder)
    {
        _folder = folder;
        if (!Directory.Exists(folder))
        {
            _files = new List<string>();
            CurrentIndex = -1;
            ListChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        List<string> found;
        try
        {
            found = _enumerateFiles(folder)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
                .ToList();
        }
        // IO/permission races: disk disconnect mid-enumeration, ACL denial, or path going away
        // between the Exists() check and the enumerator. Leave the prior list intact and still
        // signal ListChanged so the UI can re-evaluate (it may show "no image").
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException or System.Security.SecurityException)
        {
            ListChanged?.Invoke(this, EventArgs.Empty);
            return false;
        }

        SortFiles(found);
        _files = found;
        CurrentIndex = ClampIndex(CurrentIndex);
        ListChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private string? FindSiblingFolder(bool forward)
    {
        if (_folder is null) return null;

        try
        {
            var parent = Path.GetDirectoryName(_folder);
            if (string.IsNullOrEmpty(parent)) return null;

            var siblings = Directory
                .EnumerateDirectories(parent, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var idx = siblings.FindIndex(d => string.Equals(d, _folder, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) return null;

            for (var i = 1; i < siblings.Count; i++)
            {
                var candidateIdx = forward
                    ? (idx + i) % siblings.Count
                    : (idx - i + siblings.Count) % siblings.Count;

                var first = FirstSupportedImageInFolder(siblings[candidateIdx]);
                if (first is not null) return first;
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException or System.Security.SecurityException)
        {
        }

        return null;
    }

    private void SortFiles(List<string> files) => files.Sort(CompareByActiveMode);

    private static IEnumerable<string> DefaultEnumerateFiles(string folder)
        => Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly);

    private int CompareByActiveMode(string a, string b) => CompareByMode(a, b, SortMode);

    private static int CompareByMode(string a, string b, DirectorySortMode mode)
    {
        return mode switch
        {
            DirectorySortMode.NameDescending => -CompareNaturalName(a, b),
            DirectorySortMode.ModifiedNewest => ThenByName(CompareDescending(GetLastWriteUtc(a), GetLastWriteUtc(b)), a, b),
            DirectorySortMode.ModifiedOldest => ThenByName(CompareAscending(GetLastWriteUtc(a), GetLastWriteUtc(b)), a, b),
            DirectorySortMode.CreatedNewest => ThenByName(CompareDescending(GetCreationUtc(a), GetCreationUtc(b)), a, b),
            DirectorySortMode.CreatedOldest => ThenByName(CompareAscending(GetCreationUtc(a), GetCreationUtc(b)), a, b),
            DirectorySortMode.SizeLargest => ThenByName(CompareDescending(GetLength(a), GetLength(b)), a, b),
            DirectorySortMode.SizeSmallest => ThenByName(CompareAscending(GetLength(a), GetLength(b)), a, b),
            DirectorySortMode.ExtensionThenName => ThenByName(
                StringComparer.OrdinalIgnoreCase.Compare(Path.GetExtension(a), Path.GetExtension(b)), a, b),
            _ => CompareNaturalName(a, b)
        };
    }

    private static int ThenByName(int primary, string a, string b)
    {
        return primary != 0 ? primary : CompareNaturalName(a, b);
    }

    private static int CompareAscending<T>(T a, T b)
        where T : IComparable<T>
    {
        return a.CompareTo(b);
    }

    private static int CompareDescending<T>(T a, T b)
        where T : IComparable<T>
    {
        return b.CompareTo(a);
    }

    private static DateTime GetLastWriteUtc(string path)
    {
        try { return File.GetLastWriteTimeUtc(path); }
        catch { return DateTime.MinValue; }
    }

    private static DateTime GetCreationUtc(string path)
    {
        try { return File.GetCreationTimeUtc(path); }
        catch { return DateTime.MinValue; }
    }

    private static long GetLength(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return -1; }
    }

    private void RestoreCurrentPathOrClamp(string? keep)
    {
        if (keep is not null)
        {
            var idx = _files.FindIndex(f => string.Equals(f, keep, StringComparison.OrdinalIgnoreCase));
            CurrentIndex = idx >= 0 ? idx : ClampIndex(CurrentIndex);
            return;
        }

        CurrentIndex = ClampIndex(CurrentIndex);
    }

    private int ClampIndex(int index)
    {
        if (_files.Count == 0) return -1;
        return Math.Clamp(index, 0, _files.Count - 1);
    }

    private void AttachWatcher(string folder)
    {
        DetachWatcher();
        try
        {
            _watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            _watcher.Created += OnFsEvent;
            _watcher.Deleted += OnFsEvent;
            _watcher.Renamed += OnFsEvent;
        }
        // Network path vanished, path too long, permission denied — degrade to "no watcher,
        // user can still F5". Never let an FSW failure crash the viewer.
        catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or System.Security.SecurityException or IOException)
        {
            _watcher = null;
        }
    }

    private void DetachWatcher()
    {
        if (_watcher is null) return;
        try
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnFsEvent;
            _watcher.Deleted -= OnFsEvent;
            _watcher.Renamed -= OnFsEvent;
            _watcher.Dispose();
        }
        catch { /* disposal best-effort */ }
        _watcher = null;
    }

    private void OnFsEvent(object sender, FileSystemEventArgs e)
    {
        // Filter early on the background thread — ignore non-image changes.
        if (!ShouldHandleFileSystemEvent(e)) return;

        // Debounce on the UI thread: FSW often fires a burst (create + several write events)
        // for a single file copy. One Refresh per quiet period is enough.
        try
        {
            _dispatcher.BeginInvoke(new Action(() =>
            {
                _watchDebounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
                _watchDebounce.Tick -= WatchDebounce_Tick;
                _watchDebounce.Tick += WatchDebounce_Tick;
                _watchDebounce.Stop();
                _watchDebounce.Start();
            }));
        }
        catch (InvalidOperationException)
        {
            // Dispatcher is shutting down; ignore late file-system notifications.
        }
    }

    internal static bool ShouldHandleFileSystemEvent(FileSystemEventArgs e)
    {
        if (HasSupportedOrEmptyExtension(e.Name))
            return true;

        return e is RenamedEventArgs renamed && HasSupportedOrEmptyExtension(renamed.OldName);
    }

    private static bool HasSupportedOrEmptyExtension(string? name)
    {
        var ext = Path.GetExtension(name ?? string.Empty);
        return string.IsNullOrEmpty(ext) || SupportedExtensions.Contains(ext);
    }

    private void WatchDebounce_Tick(object? sender, EventArgs e)
    {
        _watchDebounce?.Stop();
        Refresh();
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string a, string b);

    private static int CompareNaturalName(string a, string b)
    {
        var result = StrCmpLogicalW(Path.GetFileName(a), Path.GetFileName(b));
        return result != 0
            ? result
            : StringComparer.OrdinalIgnoreCase.Compare(a, b);
    }

    public void Dispose()
    {
        DetachWatcher();
        if (_watchDebounce is not null)
        {
            _watchDebounce.Stop();
            _watchDebounce.Tick -= WatchDebounce_Tick;
            _watchDebounce = null;
        }
    }
}
