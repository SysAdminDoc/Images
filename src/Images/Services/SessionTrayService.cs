using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public sealed class SessionTrayService
{
    private static readonly ILogger _log = Log.Get(nameof(SessionTrayService));

    public ObservableCollection<string> Entries { get; } = new();

    public int Count => Entries.Count;

    public bool Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        string normalized;
        try { normalized = Path.GetFullPath(path); }
        catch { return false; }

        if (Entries.Contains(normalized, StringComparer.OrdinalIgnoreCase)) return false;
        Entries.Add(normalized);
        return true;
    }

    public void AddRange(IEnumerable<string> paths)
    {
        foreach (var path in paths)
            Add(path);
    }

    public bool Remove(string path)
    {
        var index = IndexOf(path);
        if (index < 0) return false;
        Entries.RemoveAt(index);
        return true;
    }

    public void MoveUp(int index)
    {
        if (index <= 0 || index >= Entries.Count) return;
        Entries.Move(index, index - 1);
    }

    public void MoveDown(int index)
    {
        if (index < 0 || index >= Entries.Count - 1) return;
        Entries.Move(index, index + 1);
    }

    public void Clear() => Entries.Clear();

    public IReadOnlyList<string> GetValidEntries()
        => Entries.Where(File.Exists).ToList();

    public void SaveToFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var fileName = Path.GetFileName(path);
        var tempPath = Path.Combine(dir ?? string.Empty, $".{fileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllLines(tempPath, Entries, Encoding.UTF8);
            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    public SessionTrayLoadResult LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return new SessionTrayLoadResult(false, 0, 0, "File not found.");

        try
        {
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            var added = 0;
            var missing = 0;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                    continue;

                string fullPath;
                try { fullPath = Path.GetFullPath(trimmed); }
                catch { missing++; continue; }

                if (File.Exists(fullPath))
                {
                    if (Add(fullPath))
                        added++;
                }
                else
                {
                    if (Add(fullPath))
                        added++;
                    missing++;
                }
            }

            return new SessionTrayLoadResult(true, added, missing,
                $"Loaded {added} entries ({missing} missing on disk).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _log.LogWarning(ex, "Could not load session tray from {Path}", path);
            return new SessionTrayLoadResult(false, 0, 0, $"Could not read file: {ex.Message}");
        }
    }

    private int IndexOf(string path)
    {
        for (var i = 0; i < Entries.Count; i++)
        {
            if (string.Equals(Entries[i], path, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }
}

public sealed record SessionTrayLoadResult(
    bool Success,
    int EntriesLoaded,
    int MissingOnDisk,
    string Message);
