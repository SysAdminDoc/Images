using Images.Services;

namespace Images.Tests;

public sealed class CodecCapabilityServiceTests
{
    [Fact]
    public void BuildDependencyProvenanceRows_IncludesRuntimeNuGetAndModelStatus()
    {
        var rows = CodecCapabilityService.BuildDependencyProvenanceRows(
            ReadyProvenance(),
            new OcrCapabilityService.OcrCapabilityStatus(
                IsAvailable: false,
                LanguageCount: 0,
                LanguageSummary: "No Windows OCR languages installed",
                StatusTitle: "OCR needs setup",
                StatusDetail: "Install a Windows language pack with OCR support.",
                BadgeText: "Setup needed"));

        Assert.Contains(rows, row =>
            row.Name == ".NET Desktop Runtime"
            && row.Kind == "Runtime"
            && row.Source.Contains("dotnet.microsoft.com", StringComparison.Ordinal));

        Assert.Contains(rows, row =>
            row.Name.StartsWith("Magick.NET", StringComparison.Ordinal)
            && row.Kind == "NuGet"
            && row.Source.Contains("nuget.org/packages/Magick.NET-Q16-AnyCPU", StringComparison.Ordinal)
            && row.AdvisoryStatus.Contains("14.11.0", StringComparison.Ordinal));

        Assert.Contains(rows, row =>
            row.Name == "Magick.NET security policy"
            && row.Kind == "Policy"
            && row.AdvisoryStatus.Contains("Resource limits", StringComparison.Ordinal)
            && row.Action.Contains(".pdf", StringComparison.Ordinal));

        Assert.Contains(rows, row =>
            row.Name == "SharpCompress"
            && row.Kind == "NuGet"
            && row.AdvisoryStatus.Contains("GHSA-6c8g-7p36-r338", StringComparison.Ordinal)
            && row.Action.Contains("0.48.1+", StringComparison.Ordinal));

        Assert.Contains(rows, row =>
            row.Name == "Windows.Media.Ocr"
            && row.Kind == "OS runtime"
            && row.Action.Contains("Install a Windows OCR language capability", StringComparison.Ordinal));

        Assert.Contains(rows, row =>
            row.Name == "Local model registry"
            && row.Kind == "Model"
            && row.AdvisoryStatus.Contains("SHA-256 matched", StringComparison.Ordinal)
            && row.Action.Contains("Model Manager", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildClipboardReport_IncludesDependencyProvenanceActions()
    {
        var report = CodecCapabilityService.BuildClipboardReport();

        Assert.Contains("Dependency provenance", report, StringComparison.Ordinal);
        Assert.Contains("Advisory:", report, StringComparison.Ordinal);
        Assert.Contains("Action:", report, StringComparison.Ordinal);
        Assert.Contains("Local model registry", report, StringComparison.Ordinal);
        Assert.Contains("SharpCompress", report, StringComparison.Ordinal);
        Assert.Contains("Magick security policy", report, StringComparison.Ordinal);
        Assert.Contains("Magick blocked write targets", report, StringComparison.Ordinal);
    }

    private static CodecCapabilityService.RuntimeProvenance ReadyProvenance()
        => new(
            AppVersion: "Images 0.2.14",
            Runtime: ".NET 9.0.15",
            OperatingSystem: "Windows",
            ProcessArchitecture: "X64",
            AppDirectory: @"C:\Images",
            MagickVersion: "14.13.0",
            MagickAssemblyPath: null,
            MagickPolicy: MagickSecurityPolicy.Configure(true, "test Ghostscript"),
            SharpCompressVersion: "0.48.1.0",
            SharpCompressAssemblyPath: null,
            GhostscriptAvailable: true,
            GhostscriptDirectory: @"C:\Images\Codecs\Ghostscript",
            GhostscriptSource: "app-local Codecs\\Ghostscript",
            GhostscriptVersion: "10.07.0",
            GhostscriptDllPath: null,
            GhostscriptDllSha256: "sha256:ghostscript-test",
            JpegTranAvailable: false,
            JpegTranExecutablePath: null,
            JpegTranSource: "Not found",
            JpegTranVersion: null,
            JpegTranSha256: null,
            JpegTranStatus: "jpegtran not available",
            C2paToolAvailable: false,
            C2paToolExecutablePath: null,
            C2paToolSource: "Not found",
            C2paToolVersion: null,
            C2paToolSha256: null,
            C2paToolStatus: "c2patool not available");
}
