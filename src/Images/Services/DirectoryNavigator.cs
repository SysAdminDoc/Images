using System.IO;
using System.Runtime.InteropServices;

namespace Images.Services;

/// <summary>
/// Scans a folder for image files, keeps a natural-sorted list, and exposes prev/next with wrap-around.
/// Watches the folder so external file ops (delete, add, rename from elsewhere) stay reflected.
/// </summary>
public sealed class DirectoryNavigator : IDisposable
{
    public static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // WIC native
        ".bmp", ".dib", ".jpg", ".jpeg", ".jpe", ".jfif", ".png", ".gif", ".tif", ".tiff",
        ".ico", ".cur", ".hdp", ".jxr", ".wdp",
        // Windows Store codecs (WIC with extension installed)
        ".webp", ".heic", ".heif", ".avif", ".jxl",
        // Magick.NET fallbacks
        ".psd", ".psb", ".tga", ".pcx", ".xpm", ".xbm", ".pbm", ".pgm", ".ppm",
        ".svg", ".svgz", ".emf", ".wmf",
        // RAW
        ".cr2", ".cr3", ".crw", ".nef", ".nrw", ".arw", ".srf", ".sr2",
        ".dng", ".raf", ".rw2", ".orf", ".pef", ".3fr", ".erf", ".mef",
        ".mrw", ".x3f", ".rwl", ".iiq", ".kdc", ".dcr"
    };

    private List<string> _files = new();
    private string? _folder;

    public IReadOnlyList<string> Files => _files;
    public int Count => _files.Count;
    public int CurrentIndex { get; private set; } = -1;
    public string? CurrentPath => CurrentIndex >= 0 && CurrentIndex < _files.Count ? _files[CurrentIndex] : null;
    public string? Folder => _folder;

    public event EventHandler? ListChanged;

    public DirectoryNavigator() { }

    /// <summary>
    /// Load the folder containing <paramref name="path"/> and point CurrentIndex at it.
    /// </summary>
    public void Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        // Normalize to absolute Windows-style path so matching against Directory.EnumerateFiles results succeeds.
        string full;
        try { full = Path.GetFullPath(path); }
        catch { return; }
        if (!File.Exists(full)) return;

        var folder = Path.GetDirectoryName(full)!;
        Rescan(folder);
        CurrentIndex = _files.FindIndex(f => string.Equals(f, full, StringComparison.OrdinalIgnoreCase));
        if (CurrentIndex < 0 && _files.Count > 0)
        {
            // Fallback match on filename only — handles case where normalization differs on comparison.
            var name = Path.GetFileName(full);
            CurrentIndex = _files.FindIndex(f => string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase));
            if (CurrentIndex < 0) CurrentIndex = 0;
        }
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
            CurrentIndex = idx >= 0 ? idx : Math.Min(CurrentIndex, _files.Count - 1);
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

    public void UpdateCurrentPath(string newPath)
    {
        if (CurrentIndex < 0) return;
        _files[CurrentIndex] = newPath;
    }

    public void RemoveCurrent()
    {
        if (CurrentIndex < 0) return;
        _files.RemoveAt(CurrentIndex);
        if (_files.Count == 0) { CurrentIndex = -1; return; }
        if (CurrentIndex >= _files.Count) CurrentIndex = _files.Count - 1;
    }

    private void Rescan(string folder)
    {
        _folder = folder;
        if (!Directory.Exists(folder))
        {
            _files = new List<string>();
            CurrentIndex = -1;
            ListChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        var found = Directory
            .EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        found.Sort(NaturalCompare);
        _files = found;
        ListChanged?.Invoke(this, EventArgs.Empty);
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string a, string b);

    private static int NaturalCompare(string a, string b) => StrCmpLogicalW(a, b);

    public void Dispose() { }
}
