using System.IO;
using System.Security.Cryptography;
using System.Text;
using ImageMagick;
using SharpCompress.Archives;

namespace Images.Services;

public static class CodecCapabilityService
{
    public sealed record CodecCapabilitySummary(
        string OpenTitle,
        string OpenDetail,
        string ExportTitle,
        string ExportDetail,
        string DocumentTitle,
        string DocumentDetail,
        bool DocumentReady);

    /// <summary>
    /// Per-family row of the capability matrix surfaced in About and the <c>--codec-report</c>
    /// CLI. Boolean fields use ternary semantics: <c>true</c> = supported, <c>false</c> = not
    /// supported by this build, <c>null</c> = not applicable to the family.
    /// </summary>
    public sealed record CapabilityRow(
        string Family,
        int OpenCount,
        int ExportCount,
        bool? Animation,
        bool? MultiPage,
        bool? Metadata,
        string Runtime,
        string Notes);

    public sealed record DependencyProvenanceRow(
        string Name,
        string Kind,
        string Source,
        string Version,
        string? Path,
        string? Sha256,
        string AdvisoryStatus,
        string Action);

    /// <summary>
    /// Compact runtime/provenance snapshot — used by About and <c>--system-info</c>. None of
    /// the values are derived at the call site; callers display whatever is here.
    /// </summary>
    public sealed record RuntimeProvenance(
        string AppVersion,
        string Runtime,
        string OperatingSystem,
        string ProcessArchitecture,
        string AppDirectory,
        string MagickVersion,
        string? MagickAssemblyPath,
        string SharpCompressVersion,
        string? SharpCompressAssemblyPath,
        bool GhostscriptAvailable,
        string? GhostscriptDirectory,
        string GhostscriptSource,
        string? GhostscriptVersion,
        string? GhostscriptDllPath,
        string? GhostscriptDllSha256,
        bool JpegTranAvailable,
        string? JpegTranExecutablePath,
        string JpegTranSource,
        string? JpegTranVersion,
        string? JpegTranSha256,
        string JpegTranStatus,
        bool C2paToolAvailable,
        string? C2paToolExecutablePath,
        string C2paToolSource,
        string? C2paToolVersion,
        string? C2paToolSha256,
        string C2paToolStatus);

    public static string BuildOverviewText()
        => $"WIC + bundled Magick.NET + archive readers; {SupportedImageFormats.Extensions.Count} open extensions; " +
           $"{ImageExportService.ExportExtensions.Length} export extensions";

    public static CodecCapabilitySummary BuildSummary()
    {
        var codec = CodecRuntime.Status;
        var writableExports = CountWritableExportFormats();
        var ghostscriptDetail = codec.GhostscriptAvailable
            ? $"PDF, EPS, PS, and AI previews are enabled through {CodecRuntime.GetGhostscriptVersion() ?? codec.GhostscriptSource}."
            : "PDF, EPS, PS, and AI previews need bundled Ghostscript, IMAGES_GHOSTSCRIPT_DIR, or an installed runtime.";

        return new CodecCapabilitySummary(
            "Broad open support",
            $"{SupportedImageFormats.Extensions.Count} extensions across common images, archive books, RAW, PSD/PSB, vector, scientific, and document preview formats.",
            "Format-aware export",
            $"{writableExports} verified writable targets are available through Save a copy.",
            codec.GhostscriptAvailable ? "Document previews ready" : "Document previews need Ghostscript",
            ghostscriptDetail,
            codec.GhostscriptAvailable);
    }

    public static string BuildDocumentStatusText()
    {
        var codec = CodecRuntime.Status;
        return codec.GhostscriptAvailable
            ? $"Ghostscript enabled ({CodecRuntime.GetGhostscriptVersion() ?? codec.GhostscriptSource})"
            : "Ghostscript not bundled or installed; PDF, EPS, PS, and AI previews are unavailable";
    }

    public static RuntimeProvenance BuildProvenance()
    {
        var info = AppInfo.Current;
        var status = CodecRuntime.Status;
        var jpegTran = JpegTranRuntime.Inspect();
        var c2paTool = C2paToolRuntime.Inspect();
        return new RuntimeProvenance(
            AppVersion: $"Images {info.DisplayVersion} ({info.ProductVersion})",
            Runtime: info.RuntimeDescription,
            OperatingSystem: info.OsDescription,
            ProcessArchitecture: System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            AppDirectory: AppContext.BaseDirectory,
            MagickVersion: CodecRuntime.GetMagickAssemblyVersion(),
            MagickAssemblyPath: CodecRuntime.GetMagickAssemblyPath(),
            SharpCompressVersion: GetSharpCompressAssemblyVersion(),
            SharpCompressAssemblyPath: GetSharpCompressAssemblyPath(),
            GhostscriptAvailable: status.GhostscriptAvailable,
            GhostscriptDirectory: status.GhostscriptDirectory,
            GhostscriptSource: status.GhostscriptSource,
            GhostscriptVersion: status.GhostscriptAvailable ? CodecRuntime.GetGhostscriptVersion() : null,
            GhostscriptDllPath: CodecRuntime.GetGhostscriptDllPath(),
            GhostscriptDllSha256: CodecRuntime.GetGhostscriptDllSha256(),
            JpegTranAvailable: jpegTran.Available,
            JpegTranExecutablePath: jpegTran.ExecutablePath,
            JpegTranSource: jpegTran.Source,
            JpegTranVersion: jpegTran.Version,
            JpegTranSha256: jpegTran.Sha256,
            JpegTranStatus: jpegTran.StatusText,
            C2paToolAvailable: c2paTool.Available,
            C2paToolExecutablePath: c2paTool.ExecutablePath,
            C2paToolSource: c2paTool.Source,
            C2paToolVersion: c2paTool.Version,
            C2paToolSha256: c2paTool.Sha256,
            C2paToolStatus: c2paTool.StatusText);
    }

    public static IReadOnlyList<DependencyProvenanceRow> BuildDependencyProvenanceRows()
        => BuildDependencyProvenanceRows(BuildProvenance(), OcrCapabilityService.GetStatus());

    internal static IReadOnlyList<DependencyProvenanceRow> BuildDependencyProvenanceRows(
        RuntimeProvenance provenance,
        OcrCapabilityService.OcrCapabilityStatus ocrStatus)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(ocrStatus);
        var modelSnapshot = new ModelManagerService().GetSnapshot();

        return
        [
            new(
                Name: ".NET Desktop Runtime",
                Kind: "Runtime",
                Source: "Microsoft .NET support policy: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core",
                Version: provenance.Runtime,
                Path: System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
                Sha256: null,
                AdvisoryStatus: ".NET servicing comes from Microsoft; release builds must use a current supported SDK/runtime.",
                Action: "Update the build image/runtime when Microsoft ships .NET 9 servicing releases."),

            new(
                Name: "Magick.NET-Q16-AnyCPU / Magick.NET.Core",
                Kind: "NuGet",
                Source: "NuGet: https://www.nuget.org/packages/Magick.NET-Q16-AnyCPU; releases: https://github.com/dlemstra/Magick.NET/releases",
                Version: provenance.MagickVersion,
                Path: provenance.MagickAssemblyPath,
                Sha256: TrySha256(provenance.MagickAssemblyPath),
                AdvisoryStatus: IsVersionAtLeast(provenance.MagickVersion, 14, 11, 0)
                    ? "OK: current version is above the project-reviewed 14.11.0 security floor; the NuGet vulnerability gate still runs before release."
                    : "Needs review: version is below the project-reviewed 14.11.0 security floor.",
                Action: "Keep Magick.NET package versions aligned and run the release readiness/vulnerability gate before shipping."),

            new(
                Name: "SharpCompress",
                Kind: "NuGet",
                Source: "NuGet: https://www.nuget.org/packages/SharpCompress; advisory: https://github.com/advisories/GHSA-6c8g-7p36-r338",
                Version: provenance.SharpCompressVersion,
                Path: provenance.SharpCompressAssemblyPath,
                Sha256: TrySha256(provenance.SharpCompressAssemblyPath),
                AdvisoryStatus: IsVersionAtLeast(provenance.SharpCompressVersion, 0, 48, 1)
                    ? "OK: 0.48.1+ clears GHSA-6c8g-7p36-r338 / CVE-2026-44788 for this package gate; Images uses read-only archive streams."
                    : "Needs upgrade: GHSA-6c8g-7p36-r338 / CVE-2026-44788 affects older SharpCompress versions.",
                Action: "Keep SharpCompress at 0.48.1+ and avoid WriteToDirectory() extraction helpers."),

            new(
                Name: "Ghostscript",
                Kind: "Optional runtime",
                Source: $"Resolver: {provenance.GhostscriptSource}; Artifex releases: https://ghostscript.com/releases/; CVE index: https://ghostscript.com/releases/cve/index.html",
                Version: provenance.GhostscriptVersion ?? (provenance.GhostscriptAvailable ? "version unavailable" : "not loaded"),
                Path: provenance.GhostscriptDllPath ?? provenance.GhostscriptDirectory,
                Sha256: provenance.GhostscriptDllSha256,
                AdvisoryStatus: provenance.GhostscriptAvailable
                    ? "Runtime present; compare version and SHA-256 against the approved Ghostscript artifact review and Artifex CVE index."
                    : "Not loaded; document/vector preview runtime is absent.",
                Action: provenance.GhostscriptAvailable
                    ? "Keep bundled license/source/checksum receipts attached to release artifacts."
                    : "Install Ghostscript, set IMAGES_GHOSTSCRIPT_DIR, or place an approved runtime under Codecs\\Ghostscript."),

            new(
                Name: "jpegtran",
                Kind: "Optional runtime",
                Source: "libjpeg-turbo releases: https://github.com/libjpeg-turbo/libjpeg-turbo/releases; policy: docs/lossless-jpeg-transform-policy.md",
                Version: provenance.JpegTranVersion ?? (provenance.JpegTranAvailable ? "version unavailable" : "not loaded"),
                Path: provenance.JpegTranExecutablePath,
                Sha256: provenance.JpegTranSha256,
                AdvisoryStatus: provenance.JpegTranAvailable
                    ? "Runtime present; verify hash, license files, and release source before bundling."
                    : "Optional child-process runtime is not bundled by current releases.",
                Action: provenance.JpegTranAvailable
                    ? "Match this binary to the approved libjpeg-turbo artifact before release."
                    : "Stage approved libjpeg-turbo jpegtran.exe under Codecs\\JpegTran or set IMAGES_JPEGTRAN_EXE."),

            new(
                Name: "c2patool",
                Kind: "Optional runtime",
                Source: "C2PA content credentials CLI: https://github.com/contentauth/c2patool; spec: https://c2pa.org",
                Version: provenance.C2paToolVersion ?? (provenance.C2paToolAvailable ? "version unavailable" : "not loaded"),
                Path: provenance.C2paToolExecutablePath,
                Sha256: provenance.C2paToolSha256,
                AdvisoryStatus: provenance.C2paToolAvailable
                    ? "Runtime present; C2PA content credential inspection is available for supported image formats."
                    : "Optional child-process runtime is not installed; C2PA inspection is unavailable.",
                Action: provenance.C2paToolAvailable
                    ? "C2PA provenance shows who created or edited a file, not whether the content is truthful."
                    : "Install c2patool (cargo install c2patool), place under Codecs\\C2paTool, or set IMAGES_C2PATOOL_EXE."),

            new(
                Name: "Windows.Media.Ocr",
                Kind: "OS runtime",
                Source: "Windows OCR API: https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr",
                Version: ocrStatus.IsAvailable ? ocrStatus.LanguageSummary : "no OCR language installed",
                Path: null,
                Sha256: null,
                AdvisoryStatus: "OS-managed Windows API and language packs; Images does not bundle OCR models.",
                Action: ocrStatus.IsAvailable
                    ? "No action needed unless additional OCR languages are required."
                    : "Install a Windows OCR language capability from Windows language settings."),

            new(
                Name: "ONNX Runtime DirectML",
                Kind: "NuGet",
                Source: "NuGet: https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime.DirectML; ONNX Runtime: https://onnxruntime.ai",
                Version: GetOnnxRuntimeVersion(),
                Path: GetOnnxRuntimeAssemblyPath(),
                Sha256: TrySha256(GetOnnxRuntimeAssemblyPath()),
                AdvisoryStatus: "ONNX Runtime DirectML provides GPU-accelerated inference for CLIP semantic search, LaMa inpainting, and future AI features.",
                Action: "Keep runtime versions current and run model smoke tests before enabling AI tools."),

            new(
                Name: "AI inference runtime",
                Kind: "Runtime",
                Source: "Windows ML: https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/overview; ONNX Runtime DirectML: https://onnxruntime.ai/docs/execution-providers/DirectML-ExecutionProvider.html",
                Version: modelSnapshot.Runtime.PreferredBackend,
                Path: null,
                Sha256: null,
                AdvisoryStatus: modelSnapshot.Runtime.StatusText,
                Action: modelSnapshot.Runtime.WindowsMlReferenced || modelSnapshot.Runtime.OnnxDirectMlReferenced
                    ? "Keep runtime package versions visible here and run model smoke tests before enabling AI tools."
                    : "Inference packages are still disabled; use Model Manager to prepare verified local model files first."),

            new(
                Name: "Local model registry",
                Kind: "Model",
                Source: "Approved sources include https://huggingface.co/opencv/inpainting_lama and https://huggingface.co/Carve/LaMa-ONNX; no automatic downloads.",
                Version: modelSnapshot.RegistrySummary,
                Path: modelSnapshot.ModelRoot ?? BuildAppDataPath("models"),
                Sha256: null,
                AdvisoryStatus: "Model files must be user imported, app-local, and SHA-256 matched to an approved registry entry before model-backed tools can use them.",
                Action: modelSnapshot.ReadyCount > 0
                    ? "Keep model hashes pinned and visible; do not run model-backed tools without matching runtime validation."
                    : "Open Model Manager, download manually from an approved source, and import the exact ONNX file.")
        ];
    }

    /// <summary>
    /// Per-format-family capability matrix. Rows align with how Images groups extensions in
    /// <see cref="SupportedImageFormats"/>; the boolean facets describe what the current
    /// runtime can actually do — open, animate, navigate multi-page, read metadata, export
    /// — together with the underlying decoder/runtime and any caveats.
    /// </summary>
    public static IReadOnlyList<CapabilityRow> BuildCapabilityMatrix()
    {
        var status = CodecRuntime.Status;
        var rows = new List<CapabilityRow>
        {
            new(
                Family: "Common images",
                OpenCount: SupportedImageFormats.CommonExtensions.Length,
                ExportCount: WritableCountIn(SupportedImageFormats.CommonExtensions),
                Animation: true,
                MultiPage: true,
                Metadata: true,
                Runtime: "WIC primary, Magick.NET fallback",
                Notes: "Animated GIF/APNG/animated WebP play inline; TIFF/ICO/CUR navigate by frame."),

            new(
                Family: "Design and production",
                OpenCount: SupportedImageFormats.DesignExtensions.Length,
                ExportCount: WritableCountIn(SupportedImageFormats.DesignExtensions),
                Animation: false,
                MultiPage: true,
                Metadata: true,
                Runtime: "Magick.NET",
                Notes: "PSD/PSB layers flatten on display; multi-frame DPX/EXR/JPEG 2000 navigate by frame."),

            new(
                Family: "Portable and scientific",
                OpenCount: SupportedImageFormats.PortableAndScientificExtensions.Length,
                ExportCount: WritableCountIn(SupportedImageFormats.PortableAndScientificExtensions),
                Animation: false,
                MultiPage: true,
                Metadata: true,
                Runtime: "Magick.NET",
                Notes: "DICOM and FITS expose multi-frame stacks; channel/Z navigation is a future milestone."),

            new(
                Family: "Vector previews",
                OpenCount: SupportedImageFormats.VectorExtensions.Length,
                ExportCount: WritableCountIn(SupportedImageFormats.VectorExtensions),
                Animation: false,
                MultiPage: false,
                Metadata: true,
                Runtime: "Magick.NET",
                Notes: "SVG/EMF/WMF rasterize at viewer DPI; round-trip vector editing is a future editor feature."),

            new(
                Family: "Document previews",
                OpenCount: SupportedImageFormats.DocumentPreviewExtensions.Length,
                ExportCount: WritableCountIn(SupportedImageFormats.DocumentPreviewExtensions),
                Animation: false,
                MultiPage: true,
                Metadata: false,
                Runtime: status.GhostscriptAvailable
                    ? $"Magick.NET + Ghostscript ({status.GhostscriptSource})"
                    : "Magick.NET (Ghostscript unavailable)",
                Notes: status.GhostscriptAvailable
                    ? "PDF/EPS/PS/AI rasterized at 144 DPI; multi-page documents navigate page-by-page."
                    : "Install Ghostscript or place a redistributable runtime under Codecs\\Ghostscript to enable previews."),

            new(
                Family: "Archive books",
                OpenCount: SupportedImageFormats.ArchiveExtensions.Length,
                ExportCount: 0,
                Animation: false,
                MultiPage: true,
                Metadata: false,
                Runtime: "System.IO.Compression + SharpCompress",
                Notes: "ZIP/CBZ use built-in .NET ZIP; RAR/CBR and 7z/CB7 read through SharpCompress without extracting entries to disk."),

            new(
                Family: "Camera RAW",
                OpenCount: SupportedImageFormats.RawExtensions.Length,
                ExportCount: 0,
                Animation: false,
                MultiPage: null,
                Metadata: true,
                Runtime: "Magick.NET (libraw embedded)",
                Notes: "RAW files are read-only previews; export through Save a copy to JPG/PNG/TIFF."),
        };

        return rows;
    }

    public static string BuildClipboardReport()
    {
        var status = CodecRuntime.Status;
        var provenance = BuildProvenance();
        var dependencyRows = BuildDependencyProvenanceRows(provenance, OcrCapabilityService.GetStatus());
        var writableExports = CountWritableExportFormats();
        var sb = new StringBuilder();

        sb.AppendLine("Images codec capability report");
        sb.AppendLine();
        sb.AppendLine("Runtime");
        sb.AppendLine($"- Application: {provenance.AppVersion}");
        sb.AppendLine($"- .NET runtime: {provenance.Runtime} ({provenance.ProcessArchitecture})");
        sb.AppendLine($"- Operating system: {provenance.OperatingSystem}");
        sb.AppendLine($"- App directory: {provenance.AppDirectory}");
        sb.AppendLine($"- Magick.NET: {provenance.MagickVersion} — {writableExports} writable export extensions active");
        if (provenance.MagickAssemblyPath is not null)
            sb.AppendLine($"- Magick.NET assembly: {provenance.MagickAssemblyPath}");
        sb.AppendLine($"- SharpCompress: {provenance.SharpCompressVersion} — archive book reader active");
        if (provenance.SharpCompressAssemblyPath is not null)
            sb.AppendLine($"- SharpCompress assembly: {provenance.SharpCompressAssemblyPath}");
        sb.AppendLine($"- Ghostscript: {(status.GhostscriptAvailable ? "available" : "not available")}");
        sb.AppendLine($"- Ghostscript source: {status.GhostscriptDirectory ?? status.GhostscriptSource}");
        if (provenance.GhostscriptVersion is not null)
            sb.AppendLine($"- Ghostscript version: {provenance.GhostscriptVersion}");
        if (provenance.GhostscriptDllPath is not null)
            sb.AppendLine($"- Ghostscript DLL: {provenance.GhostscriptDllPath}");
        if (provenance.GhostscriptDllSha256 is not null)
            sb.AppendLine($"- Ghostscript DLL SHA-256: {provenance.GhostscriptDllSha256}");
        sb.AppendLine($"- jpegtran: {(provenance.JpegTranAvailable ? "available" : "not available")}");
        sb.AppendLine($"- jpegtran source: {provenance.JpegTranExecutablePath ?? provenance.JpegTranSource}");
        if (provenance.JpegTranVersion is not null)
            sb.AppendLine($"- jpegtran version: {provenance.JpegTranVersion}");
        if (provenance.JpegTranSha256 is not null)
            sb.AppendLine($"- jpegtran SHA-256: {provenance.JpegTranSha256}");
        sb.AppendLine($"- c2patool: {(provenance.C2paToolAvailable ? "available" : "not available")}");
        sb.AppendLine($"- c2patool source: {provenance.C2paToolExecutablePath ?? provenance.C2paToolSource}");
        if (provenance.C2paToolVersion is not null)
            sb.AppendLine($"- c2patool version: {provenance.C2paToolVersion}");
        if (provenance.C2paToolSha256 is not null)
            sb.AppendLine($"- c2patool SHA-256: {provenance.C2paToolSha256}");
        sb.AppendLine($"- ONNX Runtime DirectML: {GetOnnxRuntimeVersion()}");
        sb.AppendLine();

        AppendDependencyProvenance(sb, dependencyRows);
        sb.AppendLine();

        sb.AppendLine("Capability matrix");
        foreach (var row in BuildCapabilityMatrix())
        {
            sb.AppendLine($"- {row.Family} — open: {row.OpenCount}, export: {row.ExportCount}, animation: {Tri(row.Animation)}, " +
                          $"multi-page: {Tri(row.MultiPage)}, metadata: {Tri(row.Metadata)}, runtime: {row.Runtime}");
            sb.AppendLine($"    {row.Notes}");
        }
        sb.AppendLine();

        AppendFamily(sb, "Common images", SupportedImageFormats.CommonExtensions);
        AppendFamily(sb, "Design and production", SupportedImageFormats.DesignExtensions);
        AppendFamily(sb, "Portable and scientific", SupportedImageFormats.PortableAndScientificExtensions);
        AppendFamily(sb, "Vector previews", SupportedImageFormats.VectorExtensions);
        AppendFamily(sb, "Document previews", SupportedImageFormats.DocumentPreviewExtensions);
        AppendFamily(sb, "Archive books", SupportedImageFormats.ArchiveExtensions);
        AppendFamily(sb, "Camera RAW", SupportedImageFormats.RawExtensions);
        AppendFamily(sb, "Export targets", ImageExportService.ExportExtensions);

        sb.AppendLine("Notes");
        sb.AppendLine("- PSD and PSB are handled in-process by Magick.NET.");
        sb.AppendLine("- Archive books are read-only; nested archives, document-preview entries, and unsafe paths are ignored.");
        sb.AppendLine("- PDF, EPS, PS, and AI preview rendering requires Ghostscript.");
        sb.AppendLine("- Lossless JPEG writeback will require an approved libjpeg-turbo jpegtran.exe sidecar.");
        sb.AppendLine("- HEIC/HEIF is available as a read format in this Magick.NET build; export to AVIF, JXL, WebP, PNG, or TIFF.");
        sb.AppendLine("- Camera RAW formats are read-only preview formats; export through Save a copy.");

        return sb.ToString();
    }

    internal static void AppendDependencyProvenance(
        StringBuilder sb,
        IEnumerable<DependencyProvenanceRow> rows)
    {
        sb.AppendLine("Dependency provenance");
        foreach (var row in rows)
        {
            sb.AppendLine($"- {row.Name} [{row.Kind}]");
            sb.AppendLine($"    Source: {row.Source}");
            sb.AppendLine($"    Version: {row.Version}");
            sb.AppendLine($"    Path: {row.Path ?? "(not applicable)"}");
            sb.AppendLine($"    SHA-256: {row.Sha256 ?? "(not applicable)"}");
            sb.AppendLine($"    Advisory: {row.AdvisoryStatus}");
            sb.AppendLine($"    Action: {row.Action}");
        }
    }

    private static string Tri(bool? value) => value switch
    {
        true => "yes",
        false => "no",
        _ => "n/a"
    };

    private static int WritableCountIn(IEnumerable<string> extensions)
    {
        var count = 0;
        foreach (var ext in extensions.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var format = ImageExportService.TryResolveFormat(ext);
            if (format is null) continue;

            var info = MagickFormatInfo.Create(format.Value);
            if (info?.SupportsWriting == true) count++;
        }
        return count;
    }

    private static void AppendFamily(StringBuilder sb, string name, IEnumerable<string> extensions)
    {
        var items = extensions
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        sb.AppendLine($"{name} ({items.Length})");
        sb.AppendLine(string.Join(", ", items));
        sb.AppendLine();
    }

    private static int CountWritableExportFormats()
    {
        var count = 0;
        foreach (var ext in ImageExportService.ExportExtensions.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var format = ImageExportService.TryResolveFormat(ext);
            if (format is null) continue;

            var info = MagickFormatInfo.Create(format.Value);
            if (info?.SupportsWriting == true) count++;
        }

        return count;
    }

    private static string GetSharpCompressAssemblyVersion()
    {
        var assembly = typeof(ArchiveFactory).Assembly;
        return assembly.GetName().Version?.ToString() ?? "unknown";
    }

    private static string? GetSharpCompressAssemblyPath()
    {
        var location = typeof(ArchiveFactory).Assembly.Location;
        return string.IsNullOrWhiteSpace(location) ? null : location;
    }

    private static string? TrySha256(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsVersionAtLeast(string value, int major, int minor, int patch)
    {
        var normalized = new string(value
            .TakeWhile(c => char.IsDigit(c) || c == '.')
            .ToArray());

        if (!Version.TryParse(normalized, out var version))
            return false;

        var required = new Version(major, minor, patch);
        return version >= required;
    }

    private static string GetOnnxRuntimeVersion()
    {
        try
        {
            var assembly = typeof(Microsoft.ML.OnnxRuntime.InferenceSession).Assembly;
            return assembly.GetName().Version?.ToString() ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    private static string? GetOnnxRuntimeAssemblyPath()
    {
        try
        {
            var location = typeof(Microsoft.ML.OnnxRuntime.InferenceSession).Assembly.Location;
            return string.IsNullOrWhiteSpace(location) ? null : location;
        }
        catch
        {
            return null;
        }
    }

    private static string? BuildAppDataPath(params string[] relativeSegments)
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
            return null;

        var parts = new string[relativeSegments.Length + 2];
        parts[0] = root;
        parts[1] = "Images";
        for (var i = 0; i < relativeSegments.Length; i++)
            parts[i + 2] = relativeSegments[i];

        return Path.Combine(parts);
    }
}
