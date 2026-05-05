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

    private List<string> _files = new();
    private string? _folder;
    private FileSystemWatcher? _watcher;
    private DispatcherTimer? _watchDebounce;
    private readonly Dispatcher _dispatcher;
    private readonly Func<string, IEnumerable<string>> _enumerateFiles;

    public IReadOnlyList<string> Files => _files;
    public int Count => _files.Count;
    public int CurrentIndex { get; private set; } = -1;
    public string? CurrentPath => CurrentIndex >= 0 && CurrentIndex < _files.Count ? _files[CurrentIndex] : null;
    public string? Folder => _folder;
    public DirectorySortMode SortMode { get; private set; } = DirectorySortMode.NaturalName;

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

        // Short-circuit: if we're already watching this folder, move to the exact file. If the
        // file was created after the last scan, rescan once before declaring the open failed.
        if (_folder is not null && string.Equals(_folder, folder, StringComparison.OrdinalIgnoreCase))
        {
            if (TrySetCurrentIndex(full)) return true;
            if (!Rescan(folder)) return false;
            return TrySetCurrentIndex(full);
        }

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

        AttachWatcher(folder);
        return true;
    }

    private bool TrySetCurrentIndex(string full)
    {
        var idx = _files.FindIndex(f => string.Equals(f, full, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
        {
            // Fallback match on filename only — handles case where normalization differs on comparison.
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
        CurrentIndex = (CurrentIndex + 1) % _files.Count;
        return true;
    }

    public bool MovePrevious()
    {
        if (_files.Count == 0) return false;
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
        if (CurrentIndex < 0) return;
        if (CurrentIndex >= _files.Count)
        {
            CurrentIndex = ClampIndex(CurrentIndex);
            if (CurrentIndex < 0) return;
        }

        var fullPath = Path.GetFullPath(newPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The renamed file no longer exists.", fullPath);
        if (!SupportedExtensions.Contains(Path.GetExtension(fullPath)))
            throw new InvalidOperationException("The renamed file extension is not supported by Images.");

        _files[CurrentIndex] = fullPath;
    }

    public void RemoveCurrent()
    {
        if (CurrentIndex < 0) return;
        _files.RemoveAt(CurrentIndex);
        if (_files.Count == 0) { CurrentIndex = -1; return; }
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
        var ext = Path.GetExtension(e.Name ?? string.Empty);
        if (!string.IsNullOrEmpty(ext) && !SupportedExtensions.Contains(ext)) return;

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
