using System.IO;
using System.Text;

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
        var collapsed = new StringBuilder(stem.Length);
        var previousWasWhitespace = false;

        foreach (var c in stem.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                if (!previousWasWhitespace)
                    collapsed.Append(' ');
                previousWasWhitespace = true;
                continue;
            }

            if (Array.IndexOf(_invalid, c) >= 0)
                continue;

            collapsed.Append(c);
            previousWasWhitespace = false;
        }

        return collapsed.ToString().TrimEnd('.', ' ');
    }

    /// <summary>
    /// Normalize a user-editable extension. Extensions are rename metadata, not paths:
    /// keep one leading dot, remove whitespace, remove invalid filename characters, and
    /// collapse accidental compound/path-like input into a single safe suffix.
    /// </summary>
    public static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return string.Empty;

        var raw = extension.Trim().TrimStart('.');
        var filtered = new string(raw
            .Where(c => !char.IsWhiteSpace(c) && c != '.' && Array.IndexOf(_invalid, c) < 0)
            .ToArray());

        return filtered.Length == 0 ? string.Empty : "." + filtered;
    }

    /// <summary>
    /// Resolve the final on-disk path given a desired stem + extension.
    /// If the target exists (and isn't the same file), append " (2)", " (3)", etc.
    /// </summary>
    public static string ResolveTargetPath(string folder, string desiredStem, string extension, string? currentPath)
    {
        if (string.IsNullOrWhiteSpace(folder))
            throw new ArgumentException("Target folder is required.", nameof(folder));

        folder = Path.GetFullPath(folder);
        var ext = NormalizeExtension(extension);

        var baseName = Sanitize(desiredStem);
        if (string.IsNullOrEmpty(baseName))
            throw new ArgumentException("Filename must contain at least one valid character.", nameof(desiredStem));

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
    /// forced a " (2)" suffix). Returns <paramref name="currentPath"/> unchanged on no-op.
    /// Throws for invalid filename input so the UI can surface a clear validation error.
    /// </summary>
    public string Commit(string currentPath, string desiredStem, string extension)
    {
        if (!File.Exists(currentPath)) return currentPath;

        var clean = Sanitize(desiredStem);
        if (string.IsNullOrEmpty(clean))
            throw new ArgumentException("Filename must contain at least one valid character.", nameof(desiredStem));

        var folder = Path.GetDirectoryName(currentPath)!;
        string target = currentPath;
        var moved = false;

        for (var attempt = 0; attempt < 100; attempt++)
        {
            target = ResolveTargetPath(folder, clean, extension, currentPath);

            if (IsSame(target, currentPath)) return currentPath;

            try
            {
                File.Move(currentPath, target);
                moved = true;
                break;
            }
            catch (IOException) when (File.Exists(target) && !IsSame(target, currentPath))
            {
                clean = $"{Sanitize(desiredStem)} ({attempt + 2})";
            }
        }

        if (!moved || !File.Exists(target))
            throw new IOException("Rename did not produce the expected target file.");

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

        string restoreTo = entry.ToPath;
        var moved = false;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            restoreTo = ResolveTargetPath(folder, originalStem, originalExt, entry.ToPath);
            try
            {
                File.Move(entry.ToPath, restoreTo);
                moved = true;
                break;
            }
            catch (IOException) when (File.Exists(restoreTo) && !IsSame(restoreTo, entry.ToPath))
            {
                originalStem = $"{Path.GetFileNameWithoutExtension(entry.FromPath)} ({attempt + 2})";
            }
        }

        if (!moved || !File.Exists(restoreTo))
            throw new IOException("Undo did not produce the expected target file.");

        _undo.Remove(entry);
        var reverted = entry with { FromPath = entry.ToPath, ToPath = restoreTo, At = DateTime.Now };
        Undone?.Invoke(this, reverted);
        return reverted;
    }
}
