using System.IO;
using System.Security;
using System.Text;

namespace Images.Services;

public enum LocalDataSensitivity
{
    PreferencesAndHistory,
    DerivedImageData,
    Diagnostics,
    UserContent,
    LocalModel,
    ServiceMetadata,
}

public enum LocalDataClearAction
{
    Preserve,
    ClearOnPrivacyReset,
    UserManaged,
}

public sealed record LocalDataStoreDefinition(
    string Id,
    string DisplayName,
    string RelativePath,
    bool IsDirectory,
    LocalDataSensitivity Sensitivity,
    bool Rebuildable,
    LocalDataClearAction ClearAction,
    string Purpose);

public sealed record LocalDataStoreSnapshot(
    LocalDataStoreDefinition Definition,
    string? FullPath,
    bool Exists,
    long SizeBytes);

public sealed record LocalDataClearResult(
    int ClearedStores,
    long ClearedBytes,
    IReadOnlyList<string> FailedStoreIds)
{
    public bool Succeeded => FailedStoreIds.Count == 0;
}

/// <summary>
/// Authoritative inventory of every file or directory Images owns beneath its app-data root.
/// Settings, CLI diagnostics, support bundles, privacy docs, and reset behavior all consume this
/// list so new persistence cannot be added to one surface and omitted from the others.
/// </summary>
public sealed class LocalDataStoreRegistry
{
    public static IReadOnlyList<LocalDataStoreDefinition> Definitions { get; } =
    [
        File("settings", "Settings and history", "settings.db", LocalDataSensitivity.PreferencesAndHistory, false, LocalDataClearAction.Preserve, "Preferences, window state, recent paths, shortcuts, and archive progress."),
        File("catalog", "Catalog", "catalog.db", LocalDataSensitivity.DerivedImageData, true, LocalDataClearAction.ClearOnPrivacyReset, "Rebuildable folder catalog and extracted image metadata."),
        File("semantic-index", "Semantic index", "semantic-index.db", LocalDataSensitivity.DerivedImageData, true, LocalDataClearAction.ClearOnPrivacyReset, "Rebuildable local CLIP embeddings and source-path index."),
        File("tag-graph", "Tag graph", "tag-graph.json", LocalDataSensitivity.DerivedImageData, true, LocalDataClearAction.ClearOnPrivacyReset, "Rebuildable relationships derived from image tags."),
        File("smart-collections", "Smart collections", "smart-collections.json", LocalDataSensitivity.UserContent, false, LocalDataClearAction.Preserve, "User-authored saved collection rules."),
        File("keyword-sets", "Keyword sets", "keyword-sets.json", LocalDataSensitivity.UserContent, false, LocalDataClearAction.Preserve, "User-authored reusable keyword sets."),
        File("update-check", "Update-check state", "update-check.json", LocalDataSensitivity.ServiceMetadata, true, LocalDataClearAction.ClearOnPrivacyReset, "Last update check time and latest known release tag."),
        File("network-activity", "Network activity", "network-egress.jsonl", LocalDataSensitivity.Diagnostics, true, LocalDataClearAction.ClearOnPrivacyReset, "Local audit log of update-check requests."),
        File("crash-log", "Crash log", "crash.log", LocalDataSensitivity.Diagnostics, true, LocalDataClearAction.ClearOnPrivacyReset, "Plain-text fatal exception details."),
        Directory("thumbnails", "Thumbnail cache", "thumbs", LocalDataSensitivity.DerivedImageData, true, LocalDataClearAction.ClearOnPrivacyReset, "Rebuildable image thumbnails."),
        Directory("tiles", "Tile cache", "tiles", LocalDataSensitivity.DerivedImageData, true, LocalDataClearAction.ClearOnPrivacyReset, "Rebuildable pyramids for very large images."),
        Directory("models", "Local models", "models", LocalDataSensitivity.LocalModel, false, LocalDataClearAction.UserManaged, "Models explicitly imported by the user and their manifest."),
        Directory("diagnostics", "Support bundles", "diagnostics", LocalDataSensitivity.Diagnostics, false, LocalDataClearAction.ClearOnPrivacyReset, "User-requested support bundle ZIP files."),
        Directory("logs", "Logs and crash dumps", "Logs", LocalDataSensitivity.Diagnostics, true, LocalDataClearAction.ClearOnPrivacyReset, "Rolling structured logs and local crash dumps."),
        Directory("recovery", "Recovery records", "recovery", LocalDataSensitivity.PreferencesAndHistory, false, LocalDataClearAction.ClearOnPrivacyReset, "Recovery Center operation history."),
        Directory("wallpaper", "Wallpaper copy", "wallpaper", LocalDataSensitivity.UserContent, false, LocalDataClearAction.Preserve, "Stable copy selected for the Windows wallpaper."),
        Directory("email-drafts", "Email drafts", "email-drafts", LocalDataSensitivity.UserContent, false, LocalDataClearAction.Preserve, "User-requested unsent MIME drafts with attachments."),
        Directory("writeback-backups", "Writeback backups", "writeback-backups", LocalDataSensitivity.UserContent, false, LocalDataClearAction.Preserve, "Safety copies created before metadata writeback."),
        Directory("quarantine", "Quarantine", "quarantine", LocalDataSensitivity.UserContent, false, LocalDataClearAction.Preserve, "Original files moved by duplicate cleanup or health repair."),
        Directory("clipboard", "Clipboard imports", "clipboard", LocalDataSensitivity.DerivedImageData, true, LocalDataClearAction.ClearOnPrivacyReset, "Temporary images materialized from the clipboard."),
        Directory("animation-frames", "Animation frame previews", "animation-frames", LocalDataSensitivity.DerivedImageData, true, LocalDataClearAction.ClearOnPrivacyReset, "Temporary exported animation frames."),
        Directory("motion-video", "Motion-photo video", "motion-video", LocalDataSensitivity.DerivedImageData, true, LocalDataClearAction.ClearOnPrivacyReset, "Temporary extracted embedded videos."),
        Directory("c2patool", "C2PA runtime settings", "c2patool", LocalDataSensitivity.ServiceMetadata, true, LocalDataClearAction.ClearOnPrivacyReset, "Generated no-network settings for the optional C2PA tool."),
    ];

    private readonly Func<string?> _getRoot;

    public LocalDataStoreRegistry(Func<string?>? getRoot = null)
    {
        _getRoot = getRoot ?? (() => AppStorage.TryGetAppDirectory());
    }

    public IReadOnlyList<LocalDataStoreSnapshot> GetSnapshots()
    {
        var root = _getRoot();
        return Definitions.Select(definition => Snapshot(root, definition)).ToArray();
    }

    public string BuildReport(bool includePaths)
    {
        var sb = new StringBuilder();
        foreach (var snapshot in GetSnapshots())
        {
            var definition = snapshot.Definition;
            sb.Append(definition.DisplayName)
                .Append(": ")
                .Append(FormatBytes(snapshot.SizeBytes))
                .Append(" · ")
                .Append(definition.Sensitivity)
                .Append(" · ")
                .Append(definition.Rebuildable ? "rebuildable" : "preserved");
            if (includePaths && snapshot.FullPath is not null)
                sb.Append(" · ").Append(snapshot.FullPath);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public LocalDataClearResult ClearPrivacyResetStores()
    {
        var snapshots = GetSnapshots();
        var failures = new List<string>();
        var cleared = 0;
        long clearedBytes = 0;

        foreach (var snapshot in snapshots.Where(item =>
                     item.Definition.ClearAction == LocalDataClearAction.ClearOnPrivacyReset))
        {
            if (!snapshot.Exists || snapshot.FullPath is null)
                continue;

            try
            {
                if (snapshot.Definition.IsDirectory)
                {
                    System.IO.Directory.Delete(snapshot.FullPath, recursive: true);
                }
                else
                {
                    System.IO.File.Delete(snapshot.FullPath);
                    TryDelete(snapshot.FullPath + "-wal");
                    TryDelete(snapshot.FullPath + "-shm");
                }

                cleared++;
                clearedBytes += snapshot.SizeBytes;
            }
            catch (Exception ex) when (IsStorageFailure(ex))
            {
                failures.Add(snapshot.Definition.Id);
            }
        }

        return new LocalDataClearResult(cleared, clearedBytes, failures);
    }

    public static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
        _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
    };

    private static LocalDataStoreSnapshot Snapshot(string? root, LocalDataStoreDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(root))
            return new LocalDataStoreSnapshot(definition, null, false, 0);

        var fullPath = Path.Combine(root, definition.RelativePath);
        try
        {
            var exists = definition.IsDirectory
                ? System.IO.Directory.Exists(fullPath)
                : System.IO.File.Exists(fullPath) ||
                  System.IO.File.Exists(fullPath + "-wal") ||
                  System.IO.File.Exists(fullPath + "-shm");
            if (!exists)
                return new LocalDataStoreSnapshot(definition, fullPath, false, 0);

            var size = definition.IsDirectory
                ? System.IO.Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories)
                    .Sum(path => TryGetFileSize(path))
                : TryGetFileSize(fullPath) + TryGetFileSize(fullPath + "-wal") + TryGetFileSize(fullPath + "-shm");
            return new LocalDataStoreSnapshot(definition, fullPath, true, size);
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            return new LocalDataStoreSnapshot(definition, fullPath, true, 0);
        }
    }

    private static long TryGetFileSize(string path)
    {
        try { return System.IO.File.Exists(path) ? new FileInfo(path).Length : 0; }
        catch (Exception ex) when (IsStorageFailure(ex)) { return 0; }
    }

    private static void TryDelete(string path)
    {
        try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
        catch (Exception ex) when (IsStorageFailure(ex)) { }
    }

    private static bool IsStorageFailure(Exception ex)
        => ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException;

    private static LocalDataStoreDefinition File(
        string id,
        string displayName,
        string relativePath,
        LocalDataSensitivity sensitivity,
        bool rebuildable,
        LocalDataClearAction clearAction,
        string purpose)
        => new(id, displayName, relativePath, false, sensitivity, rebuildable, clearAction, purpose);

    private static LocalDataStoreDefinition Directory(
        string id,
        string displayName,
        string relativePath,
        LocalDataSensitivity sensitivity,
        bool rebuildable,
        LocalDataClearAction clearAction,
        string purpose)
        => new(id, displayName, relativePath, true, sensitivity, rebuildable, clearAction, purpose);
}
