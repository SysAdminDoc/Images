using System.Globalization;
using System.IO;

namespace Images.Services;

public static class DiagnosticsStatusService
{
    public const string ReadyTone = "Ready";
    public const string InfoTone = "Info";
    public const string WarningTone = "Warning";

    public sealed record DiagnosticStatusItem(
        string Title,
        string Status,
        string Detail,
        string Tone,
        string Icon);

    public static IReadOnlyList<DiagnosticStatusItem> BuildStatusItems()
        => BuildStatusItems(
            CodecCapabilityService.BuildProvenance(),
            OcrCapabilityService.GetStatus(),
            UpdateCheckService.OptedIn,
            UpdateCheckService.LastCheckedUtc,
            AppStorage.TryGetAppDirectory(),
            AppStorage.TryGetAppDirectory("Logs"),
            AppStorage.TryGetAppDirectory("thumbs"),
            CrashLog.LogPath,
            ThumbnailCache.Instance.GetHealth(),
            BackgroundTaskTracker.Snapshot);

    internal static IReadOnlyList<DiagnosticStatusItem> BuildStatusItems(
        CodecCapabilityService.RuntimeProvenance provenance,
        OcrCapabilityService.OcrCapabilityStatus ocrStatus,
        bool updateChecksEnabled,
        DateTime? lastUpdateCheckUtc,
        string? appDataRoot,
        string? logsPath,
        string? thumbnailsPath,
        string crashLogPath,
        ThumbnailCacheHealth? thumbnailCache = null,
        BackgroundTaskSnapshot? backgroundTasks = null)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(ocrStatus);

        return
        [
            new(
                "Text extraction",
                ocrStatus.StatusTitle,
                $"{ocrStatus.LanguageSummary}. {ocrStatus.StatusDetail}",
                ocrStatus.IsAvailable ? ReadyTone : WarningTone,
                "\uE8EA"),
            new(
                "Document previews",
                provenance.GhostscriptAvailable ? "Ghostscript ready" : "Ghostscript not available",
                BuildGhostscriptDetail(provenance),
                provenance.GhostscriptAvailable ? ReadyTone : WarningTone,
                provenance.GhostscriptAvailable ? "\uE73E" : "\uE783"),
            new(
                "Image codecs",
                "Magick.NET available",
                BuildMagickDetail(provenance),
                ReadyTone,
                "\uE8B9"),
            new(
                "Logs",
                Directory.Exists(logsPath) ? "Log folder ready" : "Log folder unavailable",
                BuildLogsDetail(logsPath, crashLogPath),
                Directory.Exists(logsPath) ? ReadyTone : WarningTone,
                "\uE838"),
            new(
                "Storage",
                appDataRoot is not null && thumbnailsPath is not null ? "Writable storage ready" : "Storage fallback needed",
                BuildStorageDetail(appDataRoot, thumbnailsPath),
                appDataRoot is not null && thumbnailsPath is not null ? ReadyTone : WarningTone,
                "\uE8DA"),
            new(
                "Thumbnail cache",
                BuildThumbnailCacheStatus(thumbnailCache),
                BuildThumbnailCacheDetail(thumbnailCache, thumbnailsPath),
                BuildThumbnailCacheTone(thumbnailCache),
                "\uE81E"),
            new(
                "Update checks",
                updateChecksEnabled ? "Automatic checks enabled" : "Automatic checks off",
                BuildUpdateCheckDetail(updateChecksEnabled, lastUpdateCheckUtc),
                updateChecksEnabled ? ReadyTone : InfoTone,
                "\uE72C"),
            new(
                "Background work",
                BuildBackgroundWorkStatus(backgroundTasks ?? default),
                BuildBackgroundWorkDetail(backgroundTasks ?? default),
                BuildBackgroundWorkTone(backgroundTasks ?? default),
                "\uE9F5")
        ];
    }

    private static string BuildGhostscriptDetail(CodecCapabilityService.RuntimeProvenance provenance)
    {
        if (!provenance.GhostscriptAvailable)
            return "PDF, EPS, PS, and AI previews need bundled Ghostscript, IMAGES_GHOSTSCRIPT_DIR, or an installed runtime.";

        var version = string.IsNullOrWhiteSpace(provenance.GhostscriptVersion)
            ? provenance.GhostscriptSource
            : provenance.GhostscriptVersion;
        var location = provenance.GhostscriptDirectory ?? provenance.GhostscriptDllPath ?? provenance.GhostscriptSource;
        return $"Using {version} from {location}.";
    }

    private static string BuildMagickDetail(CodecCapabilityService.RuntimeProvenance provenance)
        => string.IsNullOrWhiteSpace(provenance.MagickAssemblyPath)
            ? provenance.MagickVersion
            : $"{provenance.MagickVersion} at {provenance.MagickAssemblyPath}";

    private static string BuildLogsDetail(string? logsPath, string crashLogPath)
    {
        if (string.IsNullOrWhiteSpace(logsPath))
            return $"Crash log path: {crashLogPath}";

        return $"Daily logs and crash dumps are stored in {logsPath}. Crash log: {crashLogPath}";
    }

    private static string BuildStorageDetail(string? appDataRoot, string? thumbnailsPath)
    {
        if (appDataRoot is null)
            return "Images could not create its app data root. It may fall back to temporary storage.";

        return thumbnailsPath is null
            ? $"App data root: {appDataRoot}. Thumbnail cache is unavailable."
            : $"App data root: {appDataRoot}. Thumbnails: {thumbnailsPath}";
    }

    private static string BuildThumbnailCacheStatus(ThumbnailCacheHealth? health)
    {
        if (health is null || !health.IsAvailable)
            return "Thumbnail cache unavailable";

        if (!string.IsNullOrWhiteSpace(health.Error))
            return "Thumbnail cache needs attention";

        if (health.CapBytes > 0 && health.Bytes >= health.CapBytes * 0.9)
            return "Thumbnail cache near limit";

        return "Thumbnail cache ready";
    }

    private static string BuildThumbnailCacheDetail(ThumbnailCacheHealth? health, string? thumbnailsPath)
    {
        if (health is null)
            return string.IsNullOrWhiteSpace(thumbnailsPath)
                ? "Thumbnail cache storage is not available."
                : $"Cache path: {thumbnailsPath}. Size could not be measured.";

        if (!string.IsNullOrWhiteSpace(health.Error))
            return $"Could not scan {health.Root ?? thumbnailsPath ?? "the thumbnail cache"}: {health.Error}";

        if (!health.IsAvailable)
            return "Thumbnail cache storage is not available.";

        var root = health.Root ?? thumbnailsPath ?? "thumbnail cache";
        var cap = health.CapBytes > 0 ? FormatBytes(health.CapBytes) : "uncapped";
        var temp = health.TempFileCount == 0 ? "no temp files" : $"{health.TempFileCount} temp files";
        var lastSweep = health.LastEvictionSweepUtc is null
            ? "No eviction sweep has run yet."
            : $"Last eviction sweep: {health.LastEvictionSweepUtc.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}.";

        return $"{health.FileCount} files, {FormatBytes(health.Bytes)} of {cap}, {temp}. {lastSweep} Path: {root}";
    }

    private static string BuildThumbnailCacheTone(ThumbnailCacheHealth? health)
    {
        if (health is null || !health.IsAvailable || !string.IsNullOrWhiteSpace(health.Error))
            return WarningTone;

        return health.CapBytes > 0 && health.Bytes >= health.CapBytes * 0.9 ? InfoTone : ReadyTone;
    }

    private static string BuildUpdateCheckDetail(bool updateChecksEnabled, DateTime? lastUpdateCheckUtc)
    {
        if (!updateChecksEnabled)
            return "Off by default. Manual checks are still available from About.";

        if (lastUpdateCheckUtc is null)
            return "No successful check has been recorded yet.";

        var local = lastUpdateCheckUtc.Value.Kind == DateTimeKind.Local
            ? lastUpdateCheckUtc.Value
            : lastUpdateCheckUtc.Value.ToLocalTime();
        return $"Last successful check: {local.ToString("g", CultureInfo.CurrentCulture)}";
    }

    private static string BuildBackgroundWorkStatus(BackgroundTaskSnapshot snapshot)
    {
        if (snapshot.Running > 0)
            return "Background work active";

        if (snapshot.Faulted > 0)
            return "Background work needs attention";

        return "Background work idle";
    }

    private static string BuildBackgroundWorkDetail(BackgroundTaskSnapshot snapshot)
    {
        if (snapshot.Started == 0)
            return "No tracked background work has run in this session.";

        return $"{snapshot.Running} running, {snapshot.Completed} completed, {snapshot.Faulted} failed, {snapshot.Canceled} canceled this session.";
    }

    private static string BuildBackgroundWorkTone(BackgroundTaskSnapshot snapshot)
    {
        if (snapshot.Faulted > 0)
            return WarningTone;

        return snapshot.Running > 0 ? InfoTone : ReadyTone;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        var value = Math.Max(0, bytes);
        var unitIndex = 0;
        var displayValue = (double)value;

        while (displayValue >= 1024 && unitIndex < units.Length - 1)
        {
            displayValue /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{value} {units[unitIndex]}"
            : $"{displayValue:0.#} {units[unitIndex]}";
    }
}
