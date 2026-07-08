using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public static class SupportBundleService
{
    private static readonly ILogger _log = Log.Get(nameof(SupportBundleService));
    private static readonly Regex FileUriProfileRegex = new(
        @"\bfile:///[A-Z]:/Users/[^/\s""'<>]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex WindowsProfileRegex = new(
        @"\b[A-Z]:[\\/]+Users[\\/]+[^\\/\s""'<>]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string Build()
    {
        var dir = AppStorage.TryGetAppDirectory("diagnostics") ?? Path.GetTempPath();
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
        var zipPath = Path.Combine(dir, $"images-support-{stamp}-{Guid.NewGuid():N}.zip");

        using var stream = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);

        var manifest = new StringBuilder();
        manifest.AppendLine($"Images support bundle — {DateTime.UtcNow:O}");
        manifest.AppendLine($"Version: {AppInfo.Current.DisplayVersion}");
        manifest.AppendLine($"Runtime: {AppInfo.Current.RuntimeDescription}");
        manifest.AppendLine($"OS: {AppInfo.Current.OsDescription}");
        manifest.AppendLine();

        AddText(archive, manifest, "system-info.txt",
            () => CliReport.BuildSystemInfo());

        AddText(archive, manifest, "codec-report.txt",
            () => CodecCapabilityService.BuildClipboardReport());

        AddText(archive, manifest, "network-activity.txt",
            () => NetworkEgressService.BuildClipboardText());

        AddDiagnosticsStatus(archive, manifest);
        AddCacheHealth(archive, manifest);
        AddRecoveryRecords(archive, manifest);
        AddRedactedSettings(archive, manifest);
        AddRecentLogs(archive, manifest);
        AddCrashLog(archive, manifest);

        manifest.AppendLine();
        manifest.AppendLine("No image bytes, no file contents. User-profile paths are redacted to %USERPROFILE% throughout, including log lines.");

        var manifestEntry = archive.CreateEntry("bundle-info.txt", CompressionLevel.Optimal);
        using (var writer = new StreamWriter(manifestEntry.Open(), Encoding.UTF8))
            writer.Write(manifest.ToString());

        return zipPath;
    }

    private static void AddText(ZipArchive archive, StringBuilder manifest, string name, Func<string> builder)
    {
        try
        {
            var text = builder();
            if (string.IsNullOrWhiteSpace(text)) return;

            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(text);
            manifest.AppendLine($"  {name} — {text.Length:N0} chars");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to add {Name} to support bundle", name);
            manifest.AppendLine($"  {name} — FAILED: {ex.Message}");
        }
    }

    private static void AddDiagnosticsStatus(ZipArchive archive, StringBuilder manifest)
    {
        try
        {
            var items = DiagnosticsStatusService.BuildStatusItems();
            var sb = new StringBuilder();
            foreach (var item in items)
                sb.AppendLine($"[{item.Tone}] {item.Title}: {item.Status} — {item.Detail}");

            var entry = archive.CreateEntry("diagnostics-status.txt", CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(sb.ToString());
            manifest.AppendLine($"  diagnostics-status.txt — {items.Count} items");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to add diagnostics status to support bundle");
            manifest.AppendLine($"  diagnostics-status.txt — FAILED: {ex.Message}");
        }
    }

    private static void AddCacheHealth(ZipArchive archive, StringBuilder manifest)
    {
        try
        {
            var health = ThumbnailCache.Instance.GetHealth();
            var sb = new StringBuilder();
            sb.AppendLine($"Available: {health.IsAvailable}");
            sb.AppendLine($"Root: {RedactPath(health.Root)}");
            sb.AppendLine($"Bytes: {health.Bytes:N0}");
            sb.AppendLine($"Files: {health.FileCount:N0}");
            sb.AppendLine($"Temp files: {health.TempFileCount:N0}");
            sb.AppendLine($"Cap: {health.CapBytes:N0}");
            sb.AppendLine($"Last eviction: {health.LastEvictionSweepUtc:O}");

            var entry = archive.CreateEntry("cache-health.txt", CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(sb.ToString());
            manifest.AppendLine("  cache-health.txt");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to add cache health to support bundle");
            manifest.AppendLine($"  cache-health.txt — FAILED: {ex.Message}");
        }
    }

    private static void AddRecoveryRecords(ZipArchive archive, StringBuilder manifest)
    {
        try
        {
            var service = new RecoveryCenterService();
            var records = service.ListRecent(200);
            if (records.Count == 0)
            {
                manifest.AppendLine("  recovery-log.txt — empty");
                return;
            }

            var sb = new StringBuilder();
            foreach (var r in records)
            {
                sb.AppendLine($"[{r.CreatedUtc:O}] {r.Kind} — {r.Title}");
                sb.AppendLine($"  Status: {r.Status}, Restorable: {r.IsRestorable}");
                sb.AppendLine($"  Original: {RedactPath(r.OriginalPath)}");
                sb.AppendLine($"  Current: {RedactPath(r.CurrentPath)}");
                sb.AppendLine();
            }

            var entry = archive.CreateEntry("recovery-log.txt", CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(sb.ToString());
            manifest.AppendLine($"  recovery-log.txt — {records.Count} records");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to add recovery records to support bundle");
            manifest.AppendLine($"  recovery-log.txt — FAILED: {ex.Message}");
        }
    }

    private static void AddRedactedSettings(ZipArchive archive, StringBuilder manifest)
    {
        try
        {
            var settings = SettingsService.Instance;
            var sb = new StringBuilder();
            string[] safeKeys =
            [
                "update_check_enabled", "window_maximized", "remember_window_placement",
                "filmstrip_visible", "metadata_hud_visible", "confirm_recycle_bin_delete",
                "accessibility_reduce_motion", "accessibility_high_contrast",
                "archive_right_to_left", "archive_old_scan_filter", "archive_spread_mode",
                "locale", "viewer_sort_mode", "writeback_backup_policy",
                "writeback_confirm_first", "theme_mode"
            ];

            foreach (var key in safeKeys)
            {
                var value = settings.GetString(key);
                sb.AppendLine($"{key} = {value ?? "(default)"}");
            }

            sb.AppendLine($"recent_folders_count = {settings.GetRecentFolders().Count}");
            sb.AppendLine($"hotkey_overrides_count = {settings.GetHotkeys().Count}");

            var entry = archive.CreateEntry("settings-redacted.txt", CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(sb.ToString());
            manifest.AppendLine("  settings-redacted.txt — keys only, no paths");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to add redacted settings to support bundle");
            manifest.AppendLine($"  settings-redacted.txt — FAILED: {ex.Message}");
        }
    }

    private static void AddRecentLogs(ZipArchive archive, StringBuilder manifest)
    {
        try
        {
            var logsDir = AppStorage.TryGetAppDirectory("Logs");
            if (logsDir is null || !Directory.Exists(logsDir))
            {
                manifest.AppendLine("  logs/ — directory not found");
                return;
            }

            var cutoff = DateTime.UtcNow.AddDays(-3);
            var logFiles = Directory.GetFiles(logsDir, "images-*.log")
                .Where(f => File.GetLastWriteTimeUtc(f) >= cutoff)
                .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
                .Take(5)
                .ToArray();

            var count = 0;
            foreach (var logFile in logFiles)
            {
                var name = $"logs/{Path.GetFileName(logFile)}";
                try
                {
                    using var source = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                    using var dest = entry.Open();
                    CopyRedacted(source, dest);
                    count++;
                }
                catch (IOException)
                {
                }
            }

            manifest.AppendLine($"  logs/ — {count} recent log file(s)");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to add recent logs to support bundle");
            manifest.AppendLine($"  logs/ — FAILED: {ex.Message}");
        }
    }

    private static void AddCrashLog(ZipArchive archive, StringBuilder manifest)
    {
        try
        {
            var crashPath = CrashLog.LogPath;
            if (!File.Exists(crashPath))
            {
                manifest.AppendLine("  crash.log — not present");
                return;
            }

            using var source = new FileStream(crashPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var entry = archive.CreateEntry("crash.log", CompressionLevel.Optimal);
            using var dest = entry.Open();
            CopyRedacted(source, dest);

            var size = new FileInfo(crashPath).Length;
            manifest.AppendLine($"  crash.log — {size:N0} bytes");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to add crash log to support bundle");
            manifest.AppendLine($"  crash.log — FAILED: {ex.Message}");
        }
    }

    /// <summary>
    /// Streams a log file into the bundle with user-profile paths replaced by
    /// %USERPROFILE%. Raw logs contain full image paths (loader, listen-mode,
    /// egress lines), and the bundle manifest promises they are redacted.
    /// </summary>
    private static void CopyRedacted(Stream source, Stream destination)
    {
        using var reader = new StreamReader(source, Encoding.UTF8);
        using var writer = new StreamWriter(destination, Encoding.UTF8, leaveOpen: true);
        while (reader.ReadLine() is { } line)
        {
            writer.WriteLine(RedactProfilePaths(line));
        }
    }

    private static string? RedactPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        return RedactProfilePaths(path);
    }

    internal static string RedactProfilePaths(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            value = value.Replace(userProfile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
            value = value.Replace(userProfile.Replace('\\', '/'), "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        }

        value = FileUriProfileRegex.Replace(value, "file:///%USERPROFILE%");
        return WindowsProfileRegex.Replace(value, "%USERPROFILE%");
    }
}
