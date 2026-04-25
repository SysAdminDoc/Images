using System.IO;

namespace Images.Services;

/// <summary>
/// Central catalog of file extensions the viewer is willing to discover and attempt to decode.
/// WIC and Magick.NET cover most raster formats in-process. PostScript-family document formats
/// require Ghostscript to be configured before Magick.NET can render them.
/// </summary>
public static class SupportedImageFormats
{
    public static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Windows/WIC classics
        ".bmp", ".dib", ".rle", ".jpg", ".jpeg", ".jpe", ".jfif", ".png", ".gif",
        ".tif", ".tiff", ".ico", ".cur", ".hdp", ".jxr", ".wdp",

        // Modern photo/web formats. Some are WIC when the Windows codec is installed; Magick.NET
        // provides fallback coverage for most current builds.
        ".webp", ".heic", ".heif", ".hif", ".avif", ".jxl",

        // Adobe/pro design raster formats
        ".psd", ".psb", ".tga", ".targa", ".pcx", ".dds", ".qoi",

        // High-bit-depth, scientific, and production formats
        ".exr", ".hdr", ".pic", ".dpx", ".cin", ".sgi", ".rgb", ".rgba", ".bw",
        ".jp2", ".j2k", ".j2c", ".jpc", ".jpf", ".jpx", ".jpm",

        // Portable / X11 / exchange formats
        ".xpm", ".xbm", ".pbm", ".pgm", ".ppm", ".pnm", ".pam", ".pfm", ".miff", ".mng", ".jng",

        // Scientific, medical, and legacy interchange formats
        ".dcm", ".dicom", ".fits", ".fit", ".fts", ".xcf", ".ora", ".pict", ".pct",
        ".ras", ".sun", ".xwd", ".fax", ".g3", ".g4",

        // Vector and metafile formats that Magick.NET can rasterize without Ghostscript.
        ".svg", ".svgz", ".emf", ".wmf",

        // PostScript/PDF/Illustrator-family previews. These need Ghostscript configured.
        ".pdf", ".ps", ".ps2", ".ps3", ".eps", ".epsf", ".epsi", ".epi", ".ept", ".ai",

        // Camera RAW formats
        ".cr2", ".cr3", ".crw", ".nef", ".nrw", ".arw", ".srf", ".sr2",
        ".dng", ".raf", ".rw2", ".orf", ".pef", ".3fr", ".erf", ".mef",
        ".mrw", ".x3f", ".rwl", ".iiq", ".kdc", ".dcr", ".srw", ".mos",
        ".fff", ".gpr", ".bay", ".cap"
    };

    private static readonly HashSet<string> GhostscriptExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".ps", ".ps2", ".ps3", ".eps", ".epsf", ".epsi", ".epi", ".ept", ".ai"
    };

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
