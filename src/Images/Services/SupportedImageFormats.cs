using System.IO;

namespace Images.Services;

/// <summary>
/// Central catalog of file extensions the viewer is willing to discover and attempt to decode.
/// WIC and Magick.NET cover most raster formats in-process. PostScript-family document formats
/// require Ghostscript to be configured before Magick.NET can render them.
/// </summary>
public static class SupportedImageFormats
{
    public static readonly string[] CommonExtensions =
    [
        ".jpg", ".jpeg", ".jpe", ".jfif", ".png", ".gif", ".webp", ".heic", ".heif",
        ".hif", ".avif", ".jxl", ".tif", ".tiff", ".bmp", ".dib", ".ico", ".cur",
        ".hdp", ".jxr", ".wdp"
    ];

    public static readonly string[] DesignExtensions =
    [
        ".psd", ".psb", ".tga", ".targa", ".pcx", ".dds", ".qoi", ".exr", ".hdr",
        ".pic", ".dpx", ".cin", ".sgi", ".rgb", ".rgba", ".bw", ".jp2", ".j2k",
        ".j2c", ".jpc", ".jpf", ".jpx", ".jpm", ".xcf", ".ora"
    ];

    public static readonly string[] PortableAndScientificExtensions =
    [
        ".xpm", ".xbm", ".pbm", ".pgm", ".ppm", ".pnm", ".pam", ".pfm", ".miff",
        ".mng", ".jng", ".dcm", ".dicom", ".fits", ".fit", ".fts", ".pict", ".pct",
        ".ras", ".sun", ".xwd", ".fax", ".g3", ".g4"
    ];

    public static readonly string[] VectorExtensions =
    [
        ".svg", ".svgz", ".emf", ".wmf"
    ];

    public static readonly string[] DocumentPreviewExtensions =
    [
        ".pdf", ".ps", ".ps2", ".ps3", ".eps", ".epsf", ".epsi", ".epi", ".ept", ".ai"
    ];

    public static readonly string[] RawExtensions =
    [
        ".cr2", ".cr3", ".crw", ".nef", ".nrw", ".arw", ".srf", ".sr2",
        ".dng", ".raf", ".rw2", ".orf", ".pef", ".3fr", ".erf", ".mef",
        ".mrw", ".x3f", ".rwl", ".iiq", ".kdc", ".dcr", ".srw", ".mos",
        ".fff", ".gpr", ".bay", ".cap"
    ];

    public static readonly HashSet<string> Extensions = new(
        CommonExtensions
            .Concat(DesignExtensions)
            .Concat(PortableAndScientificExtensions)
            .Concat(VectorExtensions)
            .Concat(DocumentPreviewExtensions)
            .Concat(RawExtensions),
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> GhostscriptExtensions = new(
        DocumentPreviewExtensions,
        StringComparer.OrdinalIgnoreCase);

    public static string OpenDialogFilter => string.Join("|",
    [
        "Supported files|" + ToFilter(Extensions.OrderBy(e => e, StringComparer.OrdinalIgnoreCase)),
        "Common images|" + ToFilter(CommonExtensions),
        "Design and production|" + ToFilter(DesignExtensions),
        "Camera RAW|" + ToFilter(RawExtensions),
        "Vector and document previews|" + ToFilter(VectorExtensions.Concat(DocumentPreviewExtensions)),
        "All files|*.*"
    ]);

    public static string DropUnsupportedMessage =>
        "Drop a supported image, RAW, design, vector, or document preview file.";

    private static string ToFilter(IEnumerable<string> extensions)
        => string.Join(";", extensions.Select(e => "*" + e));

    public static bool IsSupported(string path)
        => Extensions.Contains(Path.GetExtension(path));

    public static bool RequiresGhostscript(string path)
        => GhostscriptExtensions.Contains(Path.GetExtension(path));

    public static string FormatFamily(string path)
    {
        var ext = Path.GetExtension(path);
        if (GhostscriptExtensions.Contains(ext)) return "document/vector";
        if (ext.Equals(".psd", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".psb", StringComparison.OrdinalIgnoreCase)) return "Adobe raster";
        if (ext.Equals(".svg", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".svgz", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".emf", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".wmf", StringComparison.OrdinalIgnoreCase)) return "vector";
        return "image";
    }
}
