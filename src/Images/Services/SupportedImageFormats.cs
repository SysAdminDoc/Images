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
        ".jpg", ".jpeg", ".jpe", ".jfif", ".jif", ".png", ".apng", ".gif", ".webp",
        ".heic", ".heif", ".hif", ".avif", ".jxl", ".tif", ".tiff", ".bmp", ".dib",
        ".ico", ".cur", ".hdp", ".jxr", ".wdp", ".wbmp"
    ];

    public static readonly string[] DesignExtensions =
    [
        ".psd", ".psb", ".tga", ".targa", ".pcx", ".dds", ".qoi", ".exr", ".hdr",
        ".pic", ".dpx", ".cin", ".sgi", ".rgb", ".rgba", ".bw", ".jp2", ".j2k",
        ".j2c", ".jpc", ".jpf", ".jpx", ".jpm", ".jpt", ".jps", ".pgx", ".xcf",
        ".ora", ".dcx", ".rle", ".otb", ".pcd", ".pcds", ".picon", ".pix", ".pwp",
        ".sfw", ".tim", ".vicar", ".viff", ".vips", ".xv", ".six", ".sixel",
        ".farbfeld", ".ff"
    ];

    public static readonly string[] PortableAndScientificExtensions =
    [
        ".xpm", ".xbm", ".pbm", ".pgm", ".ppm", ".pnm", ".pam", ".pfm", ".miff",
        ".mng", ".jng", ".dcm", ".dicom", ".fits", ".fit", ".fts", ".pict", ".pct",
        ".ras", ".sun", ".xwd", ".fax", ".g3", ".g4"
    ];

    public static readonly string[] VectorExtensions =
    [
        ".svg", ".svgz", ".emf", ".wmf", ".wpg", ".mvg", ".msvg"
    ];

    public static readonly string[] DocumentPreviewExtensions =
    [
        ".pdf", ".pdfa", ".epdf", ".ps", ".ps2", ".ps3", ".eps", ".epsf", ".epsi",
        ".epi", ".ept", ".ept2", ".ept3", ".ai"
    ];

    public static readonly string[] RawExtensions =
    [
        ".cr2", ".cr3", ".crw", ".nef", ".nrw", ".arw", ".srf", ".sr2",
        ".dng", ".raf", ".rw2", ".orf", ".pef", ".3fr", ".erf", ".mef",
        ".mrw", ".x3f", ".rwl", ".iiq", ".kdc", ".k25", ".dcr", ".srw", ".mos",
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

    /// <summary>
    /// Human-readable family label for an extension — used by the capability matrix and
    /// CLI report (<c>--codec-report</c>). Returns <c>null</c> when the extension is not
    /// recognized; callers should display a generic "unknown" message in that case.
    /// </summary>
    public static string? FamilyLabelForExtension(string extension)
    {
        var ext = Normalize(extension);
        if (string.IsNullOrEmpty(ext)) return null;

        if (CommonExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return "Common images";
        if (DesignExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return "Design and production";
        if (PortableAndScientificExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return "Portable and scientific";
        if (VectorExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return "Vector previews";
        if (DocumentPreviewExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return "Document previews";
        if (RawExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return "Camera RAW";
        return null;
    }

    /// <summary>
    /// Returns a short, human-readable hint for files Images cannot open. Suggestions are
    /// keyed off the extension and point at the closest workflow Images actually supports
    /// (export to a supported format, install Ghostscript, ship a different decoder, etc.).
    /// Returns <c>null</c> when there's no specific guidance — the caller falls back to a
    /// generic "unsupported file type" message in that case.
    /// </summary>
    public static string? SuggestionForUnsupported(string extension)
    {
        var ext = Normalize(extension);
        if (string.IsNullOrEmpty(ext)) return null;

        return ext switch
        {
            ".mp4" or ".mov" or ".m4v" or ".mkv" or ".webm" or ".avi" or ".wmv"
                => "Images is a still-image viewer. Open video files in Films & TV, VLC, or mpv.",

            ".mp3" or ".wav" or ".flac" or ".ogg" or ".aac" or ".m4a" or ".opus"
                => "Audio files aren't viewable. Open them in Groove, foobar2000, or VLC.",

            ".zip" or ".cbz" or ".rar" or ".cbr" or ".7z" or ".cb7"
                => "Archive/comic-book mode is not built yet (V20-33). Extract the archive or wait for the next milestone.",

            ".doc" or ".docx" or ".rtf" or ".odt" or ".pages"
                => "Word-processor documents aren't images. Use Word, LibreOffice, or your default editor.",

            ".xls" or ".xlsx" or ".ods" or ".numbers"
                => "Spreadsheets aren't images. Open in Excel or LibreOffice Calc.",

            ".ppt" or ".pptx" or ".odp" or ".key"
                => "Presentations aren't images. Open in PowerPoint, Keynote, or LibreOffice Impress.",

            ".txt" or ".md" or ".log" or ".json" or ".xml" or ".html" or ".htm"
                => "Text files aren't images. Open in Notepad, VS Code, or a browser.",

            ".indd" or ".idml" or ".cdr" or ".afphoto" or ".afdesign" or ".sketch"
                => "Native design-suite documents aren't supported. Export the artboards to PSD, PDF, PNG, or TIFF and open the export.",

            ".xcf"
                => "GIMP's native XCF should usually open. If this file fails, save a flattened PNG/TIFF copy from GIPM and open that.",

            ".heif" or ".heic" or ".hif"
                => "HEIC/HEIF should open through Magick.NET. If this file fails, install the Microsoft HEIF Image Extension or convert to PNG/JPG first.",

            ".pdf" or ".ps" or ".ps2" or ".ps3" or ".eps" or ".epsf" or ".epsi" or ".ai"
                => "Document previews require Ghostscript. Install Ghostscript or place an approved runtime in the app's Codecs\\Ghostscript folder.",

            _ => null
        };
    }

    private static string Normalize(string extension)
    {
        if (string.IsNullOrEmpty(extension)) return string.Empty;
        return extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
    }
}
