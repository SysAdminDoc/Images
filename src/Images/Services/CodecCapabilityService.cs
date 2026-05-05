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
        string? GhostscriptDllSha256);

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
            GhostscriptDllSha256: CodecRuntime.GetGhostscriptDllSha256());
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
        sb.AppendLine("- HEIC/HEIF is available as a read format in this Magick.NET build; export to AVIF, JXL, WebP, PNG, or TIFF.");
        sb.AppendLine("- Camera RAW formats are read-only preview formats; export through Save a copy.");

        return sb.ToString();
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
}
