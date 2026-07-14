using System.Globalization;
using System.IO;
using Images.Localization;

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
            BackgroundTaskTracker.Snapshot,
            DisplayColorService.Current,
            SettingsService.Instance.GetBool(Keys.ColorManagement, false));

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
        BackgroundTaskSnapshot? backgroundTasks = null,
        DisplayColorState? displayColor = null,
        bool colorManagementEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(ocrStatus);
        var documentPreviewsReady = provenance.GhostscriptAvailable && provenance.MagickPolicy.DocumentDelegatesEnabled;

        return
        [
            new(
                Strings.DiagnosticsTextExtraction,
                ocrStatus.StatusTitle,
                Strings.Format(nameof(Strings.DiagnosticsOcrDetailFormat), ocrStatus.LanguageSummary, ocrStatus.StatusDetail),
                ocrStatus.IsAvailable ? ReadyTone : WarningTone,
                "\uE8EA"),
            new(
                Strings.DiagnosticsDocumentPreviews,
                documentPreviewsReady ? Strings.DiagnosticsGhostscriptReady : Strings.DiagnosticsDisabledByCodecPolicy,
                BuildGhostscriptDetail(provenance),
                documentPreviewsReady ? ReadyTone : WarningTone,
                documentPreviewsReady ? "\uE73E" : "\uE783"),
            new(
                Strings.DiagnosticsImageCodecs,
                Strings.DiagnosticsMagickAvailable,
                BuildMagickDetail(provenance),
                ReadyTone,
                "\uE8B9"),
            BuildDisplayColorStatus(displayColor ?? DisplayColorState.Unprobed, colorManagementEnabled),
            new(
                Strings.DiagnosticsContentCredentials,
                provenance.C2paToolAvailable ? Strings.DiagnosticsC2paToolReady : Strings.DiagnosticsC2paToolUnavailable,
                BuildC2paToolDetail(provenance),
                provenance.C2paToolAvailable ? ReadyTone : WarningTone,
                provenance.C2paToolAvailable ? "\uE8D7" : "\uE783"),
            new(
                Strings.DiagnosticsLogs,
                Directory.Exists(logsPath) ? Strings.DiagnosticsLogFolderReady : Strings.DiagnosticsLogFolderUnavailable,
                BuildLogsDetail(logsPath, crashLogPath),
                Directory.Exists(logsPath) ? ReadyTone : WarningTone,
                "\uE838"),
            new(
                Strings.DiagnosticsStorage,
                appDataRoot is not null && thumbnailsPath is not null ? Strings.DiagnosticsWritableStorageReady : Strings.DiagnosticsStorageFallbackNeeded,
                BuildStorageDetail(appDataRoot, thumbnailsPath),
                appDataRoot is not null && thumbnailsPath is not null ? ReadyTone : WarningTone,
                "\uE8DA"),
            new(
                Strings.DiagnosticsThumbnailCache,
                BuildThumbnailCacheStatus(thumbnailCache),
                BuildThumbnailCacheDetail(thumbnailCache, thumbnailsPath),
                BuildThumbnailCacheTone(thumbnailCache),
                "\uE81E"),
            new(
                Strings.DiagnosticsUpdateChecks,
                updateChecksEnabled ? Strings.DiagnosticsAutomaticChecksEnabled : Strings.DiagnosticsAutomaticChecksOff,
                BuildUpdateCheckDetail(updateChecksEnabled, lastUpdateCheckUtc),
                updateChecksEnabled ? ReadyTone : InfoTone,
                "\uE72C"),
            new(
                Strings.DiagnosticsBackgroundWork,
                BuildBackgroundWorkStatus(backgroundTasks ?? default),
                BuildBackgroundWorkDetail(backgroundTasks ?? default),
                BuildBackgroundWorkTone(backgroundTasks ?? default),
                "\uE9F5")
        ];
    }

    private static DiagnosticStatusItem BuildDisplayColorStatus(
        DisplayColorState display,
        bool colorManagementEnabled)
    {
        var device = display.DeviceName;
        var signal = display.AdvancedColorKnown
            ? Strings.Format(nameof(Strings.DiagnosticsSignalFormat), display.ColorEncoding, display.BitsPerColorChannel)
            : Strings.DiagnosticsSignalUnavailable;

        if (!colorManagementEnabled)
        {
            return new DiagnosticStatusItem(
                Strings.DiagnosticsDisplayColor,
                Strings.DiagnosticsIccOutputOff,
                Strings.Format(nameof(Strings.DiagnosticsIccOutputOffDetailFormat), device, signal, display.ProbeDetail),
                InfoTone,
                "\uE790");
        }

        if (display.UseLegacyMonitorProfile)
        {
            var profile = display.ProfileFileName ?? display.ProfileDescription ?? Strings.DiagnosticsCustomMonitorProfile;
            return new DiagnosticStatusItem(
                Strings.DiagnosticsDisplayColor,
                Strings.DiagnosticsMonitorIccActive,
                Strings.Format(nameof(Strings.DiagnosticsMonitorIccDetailFormat), profile, device, signal),
                ReadyTone,
                "\uE790");
        }

        if (display.AdvancedColorKnown && display.AdvancedColorEnabled)
        {
            return new DiagnosticStatusItem(
                Strings.DiagnosticsDisplayColor,
                Strings.DiagnosticsAdvancedColorActive,
                Strings.Format(nameof(Strings.DiagnosticsAdvancedColorDetailFormat), device, signal),
                ReadyTone,
                "\uE790");
        }

        var status = display.AdvancedColorKnown ? Strings.DiagnosticsSrgbFallbackActive : Strings.DiagnosticsSrgbSafetyFallback;
        var tone = display.AdvancedColorKnown ? ReadyTone : WarningTone;
        return new DiagnosticStatusItem(
            Strings.DiagnosticsDisplayColor,
            status,
            Strings.Format(nameof(Strings.DiagnosticsSrgbFallbackDetailFormat), device, signal, display.ProbeDetail),
            tone,
            "\uE790");
    }

    private static string BuildGhostscriptDetail(CodecCapabilityService.RuntimeProvenance provenance)
    {
        if (!provenance.MagickPolicy.DocumentDelegatesEnabled)
            return provenance.GhostscriptAvailable
                ? Strings.DiagnosticsGhostscriptPolicyInstalled
                : Strings.DiagnosticsGhostscriptPolicyDisabled;

        if (!provenance.GhostscriptAvailable)
            return Strings.DiagnosticsGhostscriptRequired;

        var version = string.IsNullOrWhiteSpace(provenance.GhostscriptVersion)
            ? provenance.GhostscriptSource
            : provenance.GhostscriptVersion;
        var location = provenance.GhostscriptDirectory ?? provenance.GhostscriptDllPath ?? provenance.GhostscriptSource;
        return Strings.Format(nameof(Strings.DiagnosticsUsingVersionFromFormat), version, location);
    }

    private static string BuildMagickDetail(CodecCapabilityService.RuntimeProvenance provenance)
        => string.IsNullOrWhiteSpace(provenance.MagickAssemblyPath)
            ? provenance.MagickVersion
            : Strings.Format(nameof(Strings.DiagnosticsVersionAtFormat), provenance.MagickVersion, provenance.MagickAssemblyPath);

    private static string BuildC2paToolDetail(CodecCapabilityService.RuntimeProvenance provenance)
    {
        if (!provenance.C2paToolAvailable)
            return Strings.Format(nameof(Strings.DiagnosticsC2paDegradedFormat), provenance.C2paToolStatus);

        var version = string.IsNullOrWhiteSpace(provenance.C2paToolVersion)
            ? Strings.DiagnosticsVersionUnavailable
            : provenance.C2paToolVersion;
        var location = provenance.C2paToolExecutablePath ?? provenance.C2paToolSource;
        return Strings.Format(nameof(Strings.DiagnosticsC2paReadyDetailFormat), version, location);
    }

    private static string BuildLogsDetail(string? logsPath, string crashLogPath)
    {
        if (string.IsNullOrWhiteSpace(logsPath))
            return Strings.Format(nameof(Strings.DiagnosticsCrashLogPathFormat), crashLogPath);

        return Strings.Format(nameof(Strings.DiagnosticsLogsDetailFormat), logsPath, crashLogPath);
    }

    private static string BuildStorageDetail(string? appDataRoot, string? thumbnailsPath)
    {
        if (appDataRoot is null)
            return Strings.DiagnosticsAppDataUnavailable;

        return thumbnailsPath is null
            ? Strings.Format(nameof(Strings.DiagnosticsAppDataNoThumbnailsFormat), appDataRoot)
            : Strings.Format(nameof(Strings.DiagnosticsAppDataThumbnailsFormat), appDataRoot, thumbnailsPath);
    }

    private static string BuildThumbnailCacheStatus(ThumbnailCacheHealth? health)
    {
        if (health is null || !health.IsAvailable)
            return Strings.DiagnosticsThumbnailCacheUnavailable;

        if (!string.IsNullOrWhiteSpace(health.Error))
            return Strings.DiagnosticsThumbnailCacheAttention;

        if (health.CapBytes > 0 && health.Bytes >= health.CapBytes * 0.9)
            return Strings.DiagnosticsThumbnailCacheNearLimit;

        return Strings.DiagnosticsThumbnailCacheReady;
    }

    private static string BuildThumbnailCacheDetail(ThumbnailCacheHealth? health, string? thumbnailsPath)
    {
        if (health is null)
            return string.IsNullOrWhiteSpace(thumbnailsPath)
                ? Strings.DiagnosticsThumbnailStorageUnavailable
                : Strings.Format(nameof(Strings.DiagnosticsCacheSizeUnavailableFormat), thumbnailsPath);

        if (!string.IsNullOrWhiteSpace(health.Error))
            return Strings.Format(
                nameof(Strings.DiagnosticsCacheScanFailedFormat),
                health.Root ?? thumbnailsPath ?? Strings.DiagnosticsThumbnailCacheObject,
                health.Error);

        if (!health.IsAvailable)
            return Strings.DiagnosticsThumbnailStorageUnavailable;

        var root = health.Root ?? thumbnailsPath ?? Strings.DiagnosticsThumbnailCacheObject;
        var cap = health.CapBytes > 0 ? FormatBytes(health.CapBytes) : Strings.DiagnosticsUncapped;
        var temp = health.TempFileCount == 0
            ? Strings.DiagnosticsNoTempFiles
            : Strings.Format(nameof(Strings.DiagnosticsTempFilesFormat), health.TempFileCount);
        var lastSweep = health.LastEvictionSweepUtc is null
            ? Strings.DiagnosticsNoEvictionSweep
            : Strings.Format(
                nameof(Strings.DiagnosticsLastEvictionSweepFormat),
                health.LastEvictionSweepUtc.Value.ToLocalTime());

        return Strings.Format(
            nameof(Strings.DiagnosticsCacheDetailFormat),
            health.FileCount,
            FormatBytes(health.Bytes),
            cap,
            temp,
            lastSweep,
            root);
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
            return Strings.DiagnosticsUpdateChecksOffDetail;

        if (lastUpdateCheckUtc is null)
            return Strings.DiagnosticsNoSuccessfulUpdateCheck;

        var local = lastUpdateCheckUtc.Value.Kind == DateTimeKind.Local
            ? lastUpdateCheckUtc.Value
            : lastUpdateCheckUtc.Value.ToLocalTime();
        return Strings.Format(nameof(Strings.DiagnosticsLastSuccessfulCheckFormat), local);
    }

    private static string BuildBackgroundWorkStatus(BackgroundTaskSnapshot snapshot)
    {
        if (snapshot.Running > 0)
            return Strings.DiagnosticsBackgroundActive;

        if (snapshot.Faulted > 0)
            return Strings.DiagnosticsBackgroundAttention;

        return Strings.DiagnosticsBackgroundIdle;
    }

    private static string BuildBackgroundWorkDetail(BackgroundTaskSnapshot snapshot)
    {
        if (snapshot.Started == 0)
            return Strings.DiagnosticsNoBackgroundWork;

        return Strings.Format(
            nameof(Strings.DiagnosticsBackgroundDetailFormat),
            snapshot.Running,
            snapshot.Completed,
            snapshot.Faulted,
            snapshot.Canceled);
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
