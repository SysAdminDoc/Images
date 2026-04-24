using System.IO;

namespace Images.Services;

/// <summary>
/// Commits a stem+extension rename to disk with conflict resolution and a bounded undo stack.
/// No threading — callers invoke from the UI thread after debounce.
/// </summary>
public sealed class RenameService
{
    public sealed record UndoEntry(string FromPath, string ToPath, DateTime At);

    private static readonly char[] _invalid = Path.GetInvalidFileNameChars();
    private readonly LinkedList<UndoEntry> _undo = new();
    private const int MaxUndo = 10;

    public IReadOnlyCollection<UndoEntry> UndoHistory => _undo;

    public event EventHandler<UndoEntry>? Renamed;
    public event EventHandler<UndoEntry>? Undone;

    /// <summary>
    /// Strip invalid Windows filename characters, collapse whitespace, trim trailing dots/spaces.
    /// Returns an empty string if the result would be unusable.
    /// </summary>
    public static string Sanitize(string stem)
    {
        if (string.IsNullOrWhiteSpace(stem)) return string.Empty;
        var filtered = new string(stem.Where(c => Array.IndexOf(_invalid, c) < 0).ToArray());
        filtered = filtered.Trim().TrimEnd('.');
        return filtered;
    }

    /// <summary>
    /// Resolve the final on-disk path given a desired stem + extension.
    /// If the target exists (and isn't the same file), append " (2)", " (3)", etc.
    /// </summary>
    public static string ResolveTargetPath(string folder, string desiredStem, string extension, string? currentPath)
    {
        var ext = string.IsNullOrEmpty(extension) ? "" :
                  extension.StartsWith('.') ? extension : "." + extension;

        var baseName = desiredStem;
        var candidate = Path.Combine(folder, baseName + ext);

        if (IsSame(candidate, currentPath)) return candidate;

        var counter = 2;
        while (File.Exists(candidate) && !IsSame(candidate, currentPath))
        {
            candidate = Path.Combine(folder, $"{baseName} ({counter}){ext}");
            counter++;
        }
        return candidate;
    }

    private static bool IsSame(string a, string? b)
        => b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Move the file from <paramref name="currentPath"/> to the resolved target.
    /// Returns the final path (which may differ from <paramref name="desiredStem"/>+ext if a collision
    /// forced a " (2)" suffix). Returns <paramref name="currentPath"/> unchanged on no-op or bad input.
    /// </summary>
    public string Commit(string currentPath, string desiredStem, string extension)
    {
        if (!File.Exists(currentPath)) return currentPath;

        var clean = Sanitize(desiredStem);
        if (string.IsNullOrEmpty(clean)) return currentPath;

        var folder = Path.GetDirectoryName(currentPath)!;
        var target = ResolveTargetPath(folder, clean, extension, currentPath);

        if (IsSame(target, currentPath)) return currentPath;

        File.Move(currentPath, target);

        var entry = new UndoEntry(currentPath, target, DateTime.Now);
        _undo.AddFirst(entry);
        while (_undo.Count > MaxUndo) _undo.RemoveLast();
        Renamed?.Invoke(this, entry);

        return target;
    }

    /// <summary>
    /// Revert a specific undo entry. Tries to put the file back at FromPath; if that path is now occupied
    /// by something else, it picks a safe alternative and records the actual restored path in the returned entry.
    /// </summary>
    public UndoEntry? Revert(UndoEntry entry)
    {
        if (!File.Exists(entry.ToPath)) return null;

        var folder = Path.GetDirectoryName(entry.FromPath)!;
        var originalStem = Path.GetFileNameWithoutExtension(entry.FromPath);
        var originalExt = Path.GetExtension(entry.FromPath);

        var restoreTo = ResolveTargetPath(folder, originalStem, originalExt, entry.ToPath);
        File.Move(entry.ToPath, restoreTo);

        _undo.Remove(entry);
        var reverted = entry with { FromPath = entry.ToPath, ToPath = restoreTo, At = DateTime.Now };
        Undone?.Invoke(this, reverted);
        return reverted;
    }
}
