using System.Globalization;
using System.IO;
using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Images.Services;

public enum RecoveryOperationKind
{
    Move,
    Quarantine,
    Rename,
    Writeback,
    RecycleBin
}

public enum RecoveryRestoreStatus
{
    Restored,
    AlreadyRestored,
    MissingCurrentPath,
    NotRestorable,
    NotFound,
    Failed
}

public sealed record RecoverySidecarMove(string OriginalPath, string CurrentPath);

public sealed record RecoveryActionRecord(
    string Id,
    RecoveryOperationKind Kind,
    string Title,
    string Description,
    string OriginalPath,
    string CurrentPath,
    IReadOnlyList<RecoverySidecarMove> Sidecars,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? ExpiresUtc,
    bool IsRestorable,
    string RestoreHint,
    string Status,
    DateTimeOffset? RestoredUtc = null,
    string? RestoredPath = null)
{
    [JsonIgnore]
    public string KindText => Kind switch
    {
        RecoveryOperationKind.Move => "Move",
        RecoveryOperationKind.Quarantine => "Quarantine",
        RecoveryOperationKind.Rename => "Rename",
        RecoveryOperationKind.Writeback => "Writeback",
        RecoveryOperationKind.RecycleBin => "Recycle Bin",
        _ => "Operation"
    };

    [JsonIgnore]
    public string CreatedText => CreatedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    [JsonIgnore]
    public string PathText => !string.IsNullOrWhiteSpace(CurrentPath)
        ? CurrentPath
        : OriginalPath;

    [JsonIgnore]
    public string RestoreStateText
    {
        get
        {
            if (Status.Equals(StatusRestored, StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(RestoredPath)
                    ? "Restored"
                    : "Restored to " + RestoredPath;

            return IsRestorable ? "Restore available" : RestoreHint;
        }
    }

    [JsonIgnore]
    public bool CanRestoreNow =>
        IsRestorable &&
        !Status.Equals(StatusRestored, StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public string ExpirationText => ExpiresUtc is null
        ? "No automatic expiry. Files remain until restored, manually deleted, or app data is cleared."
        : "Expires " + ExpiresUtc.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    public const string StatusOpen = "Open";
    public const string StatusRestored = "Restored";
    public const string StatusMissing = "Missing";
}

public sealed record RecoveryRestoreResult(
    RecoveryRestoreStatus Status,
    RecoveryActionRecord? Record,
    string Message,
    string? RestoredPath);

public sealed class RecoveryCenterService
{
    private const string LogFileName = "recovery-log.jsonl";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Func<string?> _getStorageRoot;
    private readonly Func<DateTimeOffset> _clock;

    public RecoveryCenterService(
        Func<string?>? getStorageRoot = null,
        Func<DateTimeOffset>? clock = null)
    {
        _getStorageRoot = getStorageRoot ?? (() => AppStorage.TryGetAppDirectory("recovery"));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public string CleanupRulesText =>
        "Recovery records are stored in app-local JSONL. Quarantined files are kept in app-local quarantine storage; Images does not auto-purge them. Writebacks and Recycle Bin actions are recorded for review, but only moves, renames, and quarantines can be restored automatically.";

    public RecoveryActionRecord RecordMove(
        string sourcePath,
        string destinationPath,
        string title,
        string description,
        IEnumerable<RecoverySidecarMove>? sidecars = null)
        => AppendNew(
            RecoveryOperationKind.Move,
            sourcePath,
            destinationPath,
            title,
            description,
            isRestorable: true,
            restoreHint: "Moves can be restored while the destination file still exists.",
            sidecars);

    public RecoveryActionRecord RecordRename(
        string sourcePath,
        string destinationPath,
        string title,
        string description,
        IEnumerable<RecoverySidecarMove>? sidecars = null)
        => AppendNew(
            RecoveryOperationKind.Rename,
            sourcePath,
            destinationPath,
            title,
            description,
            isRestorable: true,
            restoreHint: "Renames can be restored while the renamed file still exists.",
            sidecars);

    public RecoveryActionRecord RecordQuarantine(
        string sourcePath,
        string destinationPath,
        string title,
        string description,
        IEnumerable<RecoverySidecarMove>? sidecars = null)
        => AppendNew(
            RecoveryOperationKind.Quarantine,
            sourcePath,
            destinationPath,
            title,
            description,
            isRestorable: true,
            restoreHint: "Quarantined files can be restored while the quarantine copy still exists.",
            sidecars);

    public RecoveryActionRecord RecordWriteback(
        string path,
        string title,
        string description)
        => AppendNew(
            RecoveryOperationKind.Writeback,
            path,
            path,
            title,
            description,
            isRestorable: false,
            restoreHint: "No automatic restore copy was created. Restore from file history, backups, or source control if needed.",
            sidecars: null);

    public RecoveryActionRecord RecordRecycleBin(
        string path,
        string title,
        string description)
        => AppendNew(
            RecoveryOperationKind.RecycleBin,
            path,
            path,
            title,
            description,
            isRestorable: false,
            restoreHint: "Restore this item from the Windows Recycle Bin.",
            sidecars: null);

    public IReadOnlyList<RecoveryActionRecord> ListRecent(int maxCount = 200)
    {
        if (maxCount <= 0)
            return [];

        var path = TryGetLogPath();
        if (path is null || !File.Exists(path))
            return [];

        var records = new Dictionary<string, RecoveryActionRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var record = JsonSerializer.Deserialize<RecoveryActionRecord>(line, JsonOptions);
                if (record is not null && !string.IsNullOrWhiteSpace(record.Id))
                    records[record.Id] = record;
            }
            catch (JsonException)
            {
                // Keep the recovery center usable if one record is corrupt.
            }
        }

        return records.Values
            .OrderByDescending(record => record.CreatedUtc)
            .ThenByDescending(record => record.Id, StringComparer.OrdinalIgnoreCase)
            .Take(maxCount)
            .ToArray();
    }

    public string? ResolveRevealPath(string id)
    {
        var record = ListRecent().FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (record is null)
            return null;

        foreach (var candidate in new[] { record.CurrentPath, record.RestoredPath, record.OriginalPath })
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            if (File.Exists(candidate) || Directory.Exists(candidate))
                return candidate;

            var folder = Path.GetDirectoryName(candidate);
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                return folder;
        }

        return null;
    }

    public RecoveryRestoreResult Restore(string id)
    {
        var record = ListRecent().FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (record is null)
            return new RecoveryRestoreResult(RecoveryRestoreStatus.NotFound, null, "Recovery record was not found.", null);

        if (!record.IsRestorable)
            return new RecoveryRestoreResult(RecoveryRestoreStatus.NotRestorable, record, record.RestoreHint, null);

        if (record.Status.Equals(RecoveryActionRecord.StatusRestored, StringComparison.OrdinalIgnoreCase))
        {
            return new RecoveryRestoreResult(
                RecoveryRestoreStatus.AlreadyRestored,
                record,
                "This record was already restored.",
                record.RestoredPath);
        }

        if (string.IsNullOrWhiteSpace(record.CurrentPath) || !File.Exists(record.CurrentPath))
        {
            var missing = record with { Status = RecoveryActionRecord.StatusMissing };
            AppendRecord(missing);
            return new RecoveryRestoreResult(
                RecoveryRestoreStatus.MissingCurrentPath,
                missing,
                "The recovery source file no longer exists.",
                null);
        }

        try
        {
            var targetPath = ResolveRestorePath(record.OriginalPath);
            var targetFolder = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrWhiteSpace(targetFolder))
                throw new IOException("Original destination folder is not available.");

            Directory.CreateDirectory(targetFolder);
            File.Move(record.CurrentPath, targetPath);

            var restoredSidecars = RestoreSidecars(record, targetPath);
            var restored = record with
            {
                Status = RecoveryActionRecord.StatusRestored,
                RestoredUtc = _clock(),
                RestoredPath = targetPath
            };
            AppendRecord(restored);

            var sidecarText = restoredSidecars == 0
                ? string.Empty
                : $" Restored {restoredSidecars} sidecar file{(restoredSidecars == 1 ? "" : "s")}.";
            return new RecoveryRestoreResult(
                RecoveryRestoreStatus.Restored,
                restored,
                "Restored to " + targetPath + "." + sidecarText,
                targetPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
        {
            return new RecoveryRestoreResult(
                RecoveryRestoreStatus.Failed,
                record,
                "Restore failed: " + ex.Message,
                null);
        }
    }

    private RecoveryActionRecord AppendNew(
        RecoveryOperationKind kind,
        string sourcePath,
        string destinationPath,
        string title,
        string description,
        bool isRestorable,
        string restoreHint,
        IEnumerable<RecoverySidecarMove>? sidecars)
    {
        var created = _clock();
        var record = new RecoveryActionRecord(
            Guid.NewGuid().ToString("N"),
            kind,
            title,
            description,
            NormalizePathForStorage(sourcePath),
            NormalizePathForStorage(destinationPath),
            sidecars?.Select(NormalizeSidecar).ToArray() ?? [],
            created,
            ExpiresUtc: null,
            isRestorable,
            restoreHint,
            RecoveryActionRecord.StatusOpen);
        AppendRecord(record);
        return record;
    }

    private void AppendRecord(RecoveryActionRecord record)
    {
        var path = TryGetLogPath();
        if (path is null)
            return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, JsonSerializer.Serialize(record, JsonOptions) + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
        {
            // Recovery logging should never block the destructive operation that already succeeded.
        }
    }

    private int RestoreSidecars(RecoveryActionRecord record, string restoredImagePath)
    {
        var restored = 0;
        foreach (var sidecar in record.Sidecars)
        {
            if (string.IsNullOrWhiteSpace(sidecar.CurrentPath) || !File.Exists(sidecar.CurrentPath))
                continue;

            try
            {
                var sidecarTarget = ResolveRestorePath(TargetSidecarPath(sidecar.OriginalPath, record.OriginalPath, restoredImagePath));
                var folder = Path.GetDirectoryName(sidecarTarget);
                if (string.IsNullOrWhiteSpace(folder))
                    continue;

                Directory.CreateDirectory(folder);
                File.Move(sidecar.CurrentPath, sidecarTarget);
                restored++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
            {
                // Best-effort sidecar recovery; the primary file restore has already succeeded.
            }
        }

        return restored;
    }

    private static string TargetSidecarPath(string originalSidecarPath, string originalImagePath, string restoredImagePath)
    {
        if (originalSidecarPath.Equals(originalImagePath + ".xmp", StringComparison.OrdinalIgnoreCase))
            return restoredImagePath + ".xmp";

        var originalFolder = Path.GetDirectoryName(originalImagePath);
        var originalStem = Path.GetFileNameWithoutExtension(originalImagePath);
        var sidecarFolder = Path.GetDirectoryName(originalSidecarPath);
        var sidecarName = Path.GetFileName(originalSidecarPath);
        if (!string.IsNullOrWhiteSpace(originalFolder) &&
            !string.IsNullOrWhiteSpace(sidecarFolder) &&
            !string.IsNullOrWhiteSpace(originalStem) &&
            sidecarFolder.Equals(originalFolder, StringComparison.OrdinalIgnoreCase) &&
            sidecarName.Equals(originalStem + ".xmp", StringComparison.OrdinalIgnoreCase))
        {
            var restoredFolder = Path.GetDirectoryName(restoredImagePath) ?? sidecarFolder;
            var restoredStem = Path.GetFileNameWithoutExtension(restoredImagePath);
            return Path.Combine(restoredFolder, restoredStem + ".xmp");
        }

        return originalSidecarPath;
    }

    private static RecoverySidecarMove NormalizeSidecar(RecoverySidecarMove sidecar)
        => new(NormalizePathForStorage(sidecar.OriginalPath), NormalizePathForStorage(sidecar.CurrentPath));

    private static string NormalizePathForStorage(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
        {
            return path;
        }
    }

    private static string ResolveRestorePath(string preferredPath)
    {
        var normalized = NormalizePathForStorage(preferredPath);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new IOException("Restore target path is not available.");

        if (normalized.StartsWith("\\\\?\\", StringComparison.Ordinal) ||
            normalized.StartsWith("\\\\.\\", StringComparison.Ordinal))
            throw new IOException("Restore target uses a device-namespace path.");

        if (!Path.IsPathFullyQualified(normalized))
            throw new IOException("Restore target path is not fully qualified.");

        if (!File.Exists(normalized) && !Directory.Exists(normalized))
            return normalized;

        var folder = Path.GetDirectoryName(normalized);
        if (string.IsNullOrWhiteSpace(folder))
            throw new IOException("Restore target folder is not available.");

        var stem = Path.GetFileNameWithoutExtension(normalized);
        var extension = Path.GetExtension(normalized);
        for (var i = 1; i < 10_000; i++)
        {
            var suffix = i == 1 ? " (restored)" : $" (restored {i})";
            var candidate = Path.Combine(folder, stem + suffix + extension);
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                return candidate;
        }

        return Path.Combine(folder, $"{stem}-restored-{Guid.NewGuid():N}{extension}");
    }

    private string? TryGetLogPath()
    {
        var root = _getStorageRoot();
        return string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, LogFileName);
    }

    private static IEnumerable<string> ReadLines(string path)
    {
        try
        {
            return File.ReadLines(path).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return [];
        }
    }
}
