using System.IO;
using System.Security;
using System.Text;

namespace Images.Services;

/// <summary>
/// Commits a stem+extension rename to disk with conflict resolution and a bounded undo stack.
/// No threading — callers invoke from the UI thread after debounce.
/// </summary>
public sealed class RenameService
{
    public sealed record UndoEntry(string FromPath, string ToPath, DateTime At);

    public enum MoveStatus
    {
        NoChange,
        Moved,
        SkippedMissing,
        Failed,
        RolledBack,
        RollbackFailed,
    }

    public sealed record MoveOutcome(
        string SourcePath,
        string DestinationPath,
        MoveStatus Status,
        string? Error = null);

    public sealed record CommitResult(
        string OriginalPath,
        string FinalPath,
        bool Changed,
        MoveOutcome Primary,
        IReadOnlyList<MoveOutcome> Sidecars,
        RecoveryActionRecord? RecoveryRecord);

    public sealed class RenameTransactionException : IOException
    {
        public RenameTransactionException(string message, CommitResult result, bool isPartialState, Exception innerException)
            : base(message, innerException)
        {
            Result = result;
            IsPartialState = isPartialState;
        }

        public CommitResult Result { get; }
        public bool IsPartialState { get; }
    }

    private static readonly char[] _invalid = Path.GetInvalidFileNameChars();
    private readonly LinkedList<UndoEntry> _undo = new();
    private readonly RecoveryCenterService _recoveryCenter;
    private readonly IRenameFileSystem _fileSystem;
    private const int MaxUndo = 10;

    public RenameService(RecoveryCenterService? recoveryCenter = null)
        : this(recoveryCenter ?? new RecoveryCenterService(), SystemRenameFileSystem.Instance)
    {
    }

    internal RenameService(RecoveryCenterService recoveryCenter, IRenameFileSystem fileSystem)
    {
        _recoveryCenter = recoveryCenter ?? throw new ArgumentNullException(nameof(recoveryCenter));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

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

    public static bool IsSupportedTargetExtension(string extension)
        => IsSupportedTargetExtension(extension, currentPath: null);

    public static bool IsSupportedTargetExtension(string extension, string? currentPath)
    {
        var normalized = NormalizeExtension(extension);
        if (string.IsNullOrEmpty(normalized) ||
            !SupportedImageFormats.IsSupportedExtension(normalized))
            return false;

        var targetIsArchive = SupportedImageFormats.IsArchiveExtension(normalized);
        var currentIsArchive = currentPath is not null && SupportedImageFormats.IsArchive(currentPath);
        return currentIsArchive || targetIsArchive
            ? currentIsArchive && targetIsArchive
            : true;
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
        ValidateTargetExtension(ext, currentPath);

        var baseName = Sanitize(desiredStem);
        if (string.IsNullOrEmpty(baseName))
            throw new ArgumentException("Filename must contain at least one valid character.", nameof(desiredStem));

        var candidate = Path.Combine(folder, baseName + ext);

        if (IsSame(candidate, currentPath)) return candidate;

        var counter = 2;
        while (TargetUnavailable(candidate, currentPath))
        {
            candidate = Path.Combine(folder, $"{baseName} ({counter}){ext}");
            counter++;
        }
        return candidate;
    }

    private static void ValidateTargetExtension(string extension, string? currentPath)
    {
        if (string.IsNullOrEmpty(extension))
            throw new ArgumentException("Extension must contain at least one supported format suffix.", nameof(extension));

        if (!IsSupportedTargetExtension(extension, currentPath))
            throw new ArgumentException($"Extension '{extension}' is not supported by Images.", nameof(extension));
    }

    private static bool IsSame(string a, string? b)
        => b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool IsExactSame(string a, string? b)
        => b is not null && string.Equals(NormalizePath(a), NormalizePath(b), StringComparison.Ordinal);

    private static bool TargetUnavailable(string candidate, string? currentPath)
        => (File.Exists(candidate) && !IsSame(candidate, currentPath)) ||
           (currentPath is not null && SidecarCompanionFiles.WouldOverwriteDestination(currentPath, candidate));

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    /// <summary>
    /// Move the file from <paramref name="currentPath"/> to the resolved target.
    /// Returns structured primary, sidecar, and durable recovery outcomes. The final path may differ
    /// from <paramref name="desiredStem"/>+ext if a collision forced a " (2)" suffix.
    /// Throws for invalid filename input so the UI can surface a clear validation error.
    /// </summary>
    public CommitResult Commit(string currentPath, string desiredStem, string extension)
    {
        if (!_fileSystem.FileExists(currentPath))
            return NoChange(currentPath);

        var clean = Sanitize(desiredStem);
        if (string.IsNullOrEmpty(clean))
            throw new ArgumentException("Filename must contain at least one valid character.", nameof(desiredStem));

        var folder = Path.GetDirectoryName(currentPath)!;
        string target = currentPath;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            target = ResolveTargetPath(folder, clean, extension, currentPath);

            if (IsExactSame(target, currentPath)) return NoChange(currentPath);

            try
            {
                var result = MoveTransaction(currentPath, target, "Renamed image");
                var entry = new UndoEntry(currentPath, target, DateTime.Now);
                _undo.AddFirst(entry);
                while (_undo.Count > MaxUndo) _undo.RemoveLast();
                Renamed?.Invoke(this, entry);
                return result;
            }
            catch (IOException ex) when (ex is not RenameTransactionException &&
                                         _fileSystem.FileExists(target) &&
                                         !IsSame(target, currentPath))
            {
                clean = $"{Sanitize(desiredStem)} ({attempt + 2})";
            }
        }

        throw new IOException("Rename could not resolve an available target after 100 attempts.");
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
                MoveTransaction(entry.ToPath, restoreTo, "Reverted rename");
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

    private CommitResult MoveTransaction(string sourcePath, string targetPath, string recoveryTitle)
    {
        var sidecarOutcomes = new List<MoveOutcome>();
        var movedSidecars = new List<SidecarCompanionFiles.SidecarMovePlan>();
        var primary = new MoveOutcome(sourcePath, targetPath, MoveStatus.Moved);
        RecoveryActionRecord? recoveryRecord = null;

        _fileSystem.Move(sourcePath, targetPath);

        try
        {
            foreach (var plan in SidecarCompanionFiles.EnumerateMovePlans(sourcePath, targetPath))
            {
                if (!_fileSystem.FileExists(plan.SourcePath))
                {
                    sidecarOutcomes.Add(new MoveOutcome(
                        plan.SourcePath,
                        plan.DestinationPath,
                        MoveStatus.SkippedMissing));
                    continue;
                }

                try
                {
                    _fileSystem.Move(plan.SourcePath, plan.DestinationPath);
                    movedSidecars.Add(plan);
                    sidecarOutcomes.Add(new MoveOutcome(
                        plan.SourcePath,
                        plan.DestinationPath,
                        MoveStatus.Moved));
                }
                catch (Exception ex) when (IsMoveFailure(ex))
                {
                    sidecarOutcomes.Add(new MoveOutcome(
                        plan.SourcePath,
                        plan.DestinationPath,
                        MoveStatus.Failed,
                        ex.Message));
                    throw;
                }
            }

            var recoverySidecars = movedSidecars
                .Select(plan => new RecoverySidecarMove(plan.SourcePath, plan.DestinationPath))
                .ToArray();
            recoveryRecord = _recoveryCenter.RecordRenameDurable(
                sourcePath,
                targetPath,
                recoveryTitle,
                $"{recoveryTitle} from {Path.GetFileName(sourcePath)} to {Path.GetFileName(targetPath)}.",
                recoverySidecars);
        }
        catch (Exception ex) when (IsMoveFailure(ex))
        {
            var rollbackFailed = false;
            for (var i = movedSidecars.Count - 1; i >= 0; i--)
            {
                var plan = movedSidecars[i];
                var outcomeIndex = sidecarOutcomes.FindIndex(outcome =>
                    outcome.SourcePath.Equals(plan.SourcePath, StringComparison.OrdinalIgnoreCase) &&
                    outcome.DestinationPath.Equals(plan.DestinationPath, StringComparison.OrdinalIgnoreCase));
                try
                {
                    _fileSystem.Move(plan.DestinationPath, plan.SourcePath);
                    sidecarOutcomes[outcomeIndex] = sidecarOutcomes[outcomeIndex] with { Status = MoveStatus.RolledBack };
                }
                catch (Exception rollbackEx) when (IsMoveFailure(rollbackEx))
                {
                    rollbackFailed = true;
                    sidecarOutcomes[outcomeIndex] = sidecarOutcomes[outcomeIndex] with
                    {
                        Status = MoveStatus.RollbackFailed,
                        Error = rollbackEx.Message,
                    };
                }
            }

            try
            {
                _fileSystem.Move(targetPath, sourcePath);
                primary = primary with { Status = MoveStatus.RolledBack };
            }
            catch (Exception rollbackEx) when (IsMoveFailure(rollbackEx))
            {
                rollbackFailed = true;
                primary = primary with { Status = MoveStatus.RollbackFailed, Error = rollbackEx.Message };
            }

            RecoveryActionRecord? partialRecord = null;
            if (rollbackFailed)
            {
                var primaryCurrentPath = _fileSystem.FileExists(sourcePath) ? sourcePath : targetPath;
                var remainingSidecars = movedSidecars
                    .Where(plan => _fileSystem.FileExists(plan.DestinationPath))
                    .Select(plan => new RecoverySidecarMove(plan.SourcePath, plan.DestinationPath))
                    .ToArray();
                partialRecord = _recoveryCenter.RecordPartialRename(
                    sourcePath,
                    primaryCurrentPath,
                    "One or more image or XMP moves could not be rolled back; the recorded paths require manual inspection.",
                    remainingSidecars);
            }

            var result = new CommitResult(
                sourcePath,
                _fileSystem.FileExists(sourcePath) ? sourcePath : targetPath,
                Changed: rollbackFailed,
                primary,
                sidecarOutcomes,
                partialRecord);
            var message = rollbackFailed
                ? "Rename left files in a partial state. Open Recovery Center before making further changes."
                : "Rename transaction could not be completed; all image and XMP moves were restored.";
            throw new RenameTransactionException(message, result, rollbackFailed, ex);
        }

        return new CommitResult(sourcePath, targetPath, Changed: true, primary, sidecarOutcomes, recoveryRecord);
    }

    private static CommitResult NoChange(string path)
        => new(
            path,
            path,
            Changed: false,
            new MoveOutcome(path, path, MoveStatus.NoChange),
            [],
            RecoveryRecord: null);

    private static bool IsMoveFailure(Exception ex)
        => ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException;
}

internal interface IRenameFileSystem
{
    bool FileExists(string path);
    void Move(string sourcePath, string destinationPath);
}

internal sealed class SystemRenameFileSystem : IRenameFileSystem
{
    public static SystemRenameFileSystem Instance { get; } = new();

    private SystemRenameFileSystem()
    {
    }

    public bool FileExists(string path) => File.Exists(path);

    public void Move(string sourcePath, string destinationPath)
        => File.Move(sourcePath, destinationPath, overwrite: false);
}
