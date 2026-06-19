using System.IO;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public enum WritebackBackupMode
{
    None,
    SameFolder,
    AppLocal,
}

public static class WritebackGuardService
{
    private static readonly ILogger _log = Log.Get("Images.WritebackGuard");

    public static WritebackBackupMode GetBackupMode(SettingsService settings)
    {
        var raw = settings.GetString(Keys.WritebackBackupPolicy, "none");
        return raw switch
        {
            "same-folder" => WritebackBackupMode.SameFolder,
            "app-local" => WritebackBackupMode.AppLocal,
            _ => WritebackBackupMode.None,
        };
    }

    public static bool ShouldConfirmFirst(SettingsService settings)
        => settings.GetBool(Keys.WritebackConfirmFirst, true);

    public static string? CreateBackup(string sourcePath, WritebackBackupMode mode)
    {
        if (mode == WritebackBackupMode.None || !File.Exists(sourcePath))
            return null;

        try
        {
            var backupPath = mode switch
            {
                WritebackBackupMode.SameFolder => BuildSameFolderBackupPath(sourcePath),
                WritebackBackupMode.AppLocal => BuildAppLocalBackupPath(sourcePath),
                _ => null,
            };

            if (backupPath is null) return null;

            var dir = Path.GetDirectoryName(backupPath);
            if (dir is not null) Directory.CreateDirectory(dir);

            File.Copy(sourcePath, backupPath, overwrite: false);
            _log.LogInformation("writeback-guard: backed up {Source} to {Backup}", sourcePath, backupPath);
            return backupPath;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "writeback-guard: backup failed for {Source}", sourcePath);
            return null;
        }
    }

    private static string BuildSameFolderBackupPath(string sourcePath)
    {
        var dir = Path.GetDirectoryName(sourcePath) ?? ".";
        var stem = Path.GetFileNameWithoutExtension(sourcePath);
        var ext = Path.GetExtension(sourcePath);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        return Path.Combine(dir, $"{stem}_backup_{timestamp}{ext}");
    }

    private static string? BuildAppLocalBackupPath(string sourcePath)
    {
        var backupRoot = AppStorage.TryGetAppDirectory("writeback-backups");
        if (backupRoot is null) return null;

        var fileName = Path.GetFileName(sourcePath);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        return Path.Combine(backupRoot, $"{stem}_{timestamp}{ext}");
    }
}
