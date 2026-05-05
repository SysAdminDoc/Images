using Images.Services;

namespace Images.Tests;

public sealed class DiagnosticsStatusServiceTests
{
    [Fact]
    public void BuildStatusItems_WhenRuntimesReady_ReturnsCoreDiagnostics()
    {
        var items = DiagnosticsStatusService.BuildStatusItems(
            ReadyProvenance(),
            new OcrCapabilityService.OcrCapabilityStatus(
                IsAvailable: true,
                LanguageCount: 1,
                LanguageSummary: "English (United States) (en-US)",
                StatusTitle: "Text extraction ready",
                StatusDetail: "OCR runs locally.",
                BadgeText: "Ready"),
            updateChecksEnabled: true,
            lastUpdateCheckUtc: new DateTime(2026, 5, 5, 14, 0, 0, DateTimeKind.Utc),
            appDataRoot: @"C:\Users\test\AppData\Local\Images",
            logsPath: AppContext.BaseDirectory,
            thumbnailsPath: @"C:\Users\test\AppData\Local\Images\thumbs",
            crashLogPath: @"C:\Users\test\AppData\Local\Images\crash.log",
            thumbnailCache: new ThumbnailCacheHealth(
                IsAvailable: true,
                Root: @"C:\Users\test\AppData\Local\Images\thumbs",
                Bytes: 2 * 1024 * 1024,
                FileCount: 42,
                TempFileCount: 1,
                CapBytes: 512L * 1024 * 1024,
                LastEvictionSweepUtc: new DateTime(2026, 5, 5, 13, 0, 0, DateTimeKind.Utc)));

        Assert.Contains(items, item => item.Title == "Text extraction" && item.Tone == DiagnosticsStatusService.ReadyTone);
        Assert.Contains(items, item => item.Title == "Document previews" && item.Status == "Ghostscript ready");
        Assert.Contains(items, item => item.Title == "Image codecs" && item.Detail.Contains("14.13.0", StringComparison.Ordinal));
        Assert.Contains(items, item => item.Title == "Logs" && item.Tone == DiagnosticsStatusService.ReadyTone);
        Assert.Contains(items, item => item.Title == "Storage" && item.Status == "Writable storage ready");
        Assert.Contains(items, item => item.Title == "Thumbnail cache" && item.Status == "Thumbnail cache ready" && item.Detail.Contains("42 files", StringComparison.Ordinal));
        Assert.Contains(items, item => item.Title == "Update checks" && item.Detail.StartsWith("Last successful check:", StringComparison.Ordinal));
        Assert.Contains(items, item => item.Title == "Background work" && item.Status == "Background work idle");
    }

    [Fact]
    public void BuildStatusItems_WhenOptionalCapabilitiesMissing_FlagsActionableWarnings()
    {
        var items = DiagnosticsStatusService.BuildStatusItems(
            ReadyProvenance() with
            {
                GhostscriptAvailable = false,
                GhostscriptDirectory = null,
                GhostscriptVersion = null,
                GhostscriptDllPath = null,
                GhostscriptDllSha256 = null
            },
            new OcrCapabilityService.OcrCapabilityStatus(
                IsAvailable: false,
                LanguageCount: 0,
                LanguageSummary: "No Windows OCR languages installed",
                StatusTitle: "OCR needs setup",
                StatusDetail: "Install a Windows language pack with OCR support.",
                BadgeText: "Setup needed"),
            updateChecksEnabled: false,
            lastUpdateCheckUtc: null,
            appDataRoot: null,
            logsPath: null,
            thumbnailsPath: null,
            crashLogPath: @"C:\Temp\Images\crash.log");

        Assert.Contains(items, item => item.Title == "Text extraction" && item.Tone == DiagnosticsStatusService.WarningTone);
        Assert.Contains(items, item => item.Title == "Document previews" && item.Tone == DiagnosticsStatusService.WarningTone);
        Assert.Contains(items, item => item.Title == "Storage" && item.Tone == DiagnosticsStatusService.WarningTone);
        Assert.Contains(items, item => item.Title == "Update checks" && item.Tone == DiagnosticsStatusService.InfoTone);
    }

    [Fact]
    public void BuildStatusItems_WhenBackgroundWorkHasFailures_FlagsWarning()
    {
        var items = DiagnosticsStatusService.BuildStatusItems(
            ReadyProvenance(),
            new OcrCapabilityService.OcrCapabilityStatus(
                IsAvailable: true,
                LanguageCount: 1,
                LanguageSummary: "English (United States) (en-US)",
                StatusTitle: "Text extraction ready",
                StatusDetail: "OCR runs locally.",
                BadgeText: "Ready"),
            updateChecksEnabled: false,
            lastUpdateCheckUtc: null,
            appDataRoot: @"C:\Users\test\AppData\Local\Images",
            logsPath: AppContext.BaseDirectory,
            thumbnailsPath: @"C:\Users\test\AppData\Local\Images\thumbs",
            crashLogPath: @"C:\Users\test\AppData\Local\Images\crash.log",
            backgroundTasks: new BackgroundTaskSnapshot(
                Started: 3,
                Running: 0,
                Completed: 1,
                Faulted: 2,
                Canceled: 0));

        Assert.Contains(items, item =>
            item.Title == "Background work"
            && item.Status == "Background work needs attention"
            && item.Tone == DiagnosticsStatusService.WarningTone
            && item.Detail.Contains("2 failed", StringComparison.Ordinal));
    }

    private static CodecCapabilityService.RuntimeProvenance ReadyProvenance()
        => new(
            AppVersion: "Images 0.2.9",
            Runtime: ".NET 9.0.15",
            OperatingSystem: "Windows",
            ProcessArchitecture: "X64",
            AppDirectory: @"C:\Images",
            MagickVersion: "14.13.0",
            MagickAssemblyPath: @"C:\Images\Magick.NET-Q16-AnyCPU.dll",
            SharpCompressVersion: "0.47.4.0",
            SharpCompressAssemblyPath: @"C:\Images\SharpCompress.dll",
            GhostscriptAvailable: true,
            GhostscriptDirectory: @"C:\Program Files\gs\gs10.04.0\bin",
            GhostscriptSource: "installed Ghostscript",
            GhostscriptVersion: "10.04.0",
            GhostscriptDllPath: @"C:\Program Files\gs\gs10.04.0\bin\gsdll64.dll",
            GhostscriptDllSha256: "sha256:test");
}
