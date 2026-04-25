using System.Text;
using ImageMagick;

namespace Images.Services;

public static class CodecCapabilityService
{
    public static string BuildOverviewText()
        => $"WIC + bundled Magick.NET; {SupportedImageFormats.Extensions.Count} open extensions; " +
           $"{ImageExportService.ExportExtensions.Length} export extensions";

    public static string BuildDocumentStatusText()
    {
        var codec = CodecRuntime.Status;
        return codec.GhostscriptAvailable
            ? $"Ghostscript enabled ({CodecRuntime.GetGhostscriptVersion() ?? codec.GhostscriptSource})"
            : "Ghostscript not bundled or installed; PDF, EPS, PS, and AI previews are unavailable";
    }

    public static string BuildClipboardReport()
    {
        var codec = CodecRuntime.Status;
        var writableExports = CountWritableExportFormats();
        var sb = new StringBuilder();

        sb.AppendLine("Images codec capability report");
        sb.AppendLine();
        sb.AppendLine("Runtime");
        sb.AppendLine($"- Magick.NET: bundled package, {writableExports} writable export extensions active");
        sb.AppendLine($"- Ghostscript: {(codec.GhostscriptAvailable ? "available" : "not available")}");
        sb.AppendLine($"- Ghostscript source: {codec.GhostscriptDirectory ?? codec.GhostscriptSource}");
        sb.AppendLine();

        AppendFamily(sb, "Common images", SupportedImageFormats.CommonExtensions);
        AppendFamily(sb, "Design and production", SupportedImageFormats.DesignExtensions);
        AppendFamily(sb, "Portable and scientific", SupportedImageFormats.PortableAndScientificExtensions);
        AppendFamily(sb, "Vector previews", SupportedImageFormats.VectorExtensions);
        AppendFamily(sb, "Document previews", SupportedImageFormats.DocumentPreviewExtensions);
        AppendFamily(sb, "Camera RAW", SupportedImageFormats.RawExtensions);
        AppendFamily(sb, "Export targets", ImageExportService.ExportExtensions);

        sb.AppendLine("Notes");
        sb.AppendLine("- PSD and PSB are handled in-process by Magick.NET.");
        sb.AppendLine("- PDF, EPS, PS, and AI preview rendering requires Ghostscript.");
        sb.AppendLine("- HEIC/HEIF is available as a read format in this Magick.NET build; export to AVIF, JXL, WebP, PNG, or TIFF.");
        sb.AppendLine("- Camera RAW formats are read-only preview formats; export through Save a copy.");

        return sb.ToString();
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
}
