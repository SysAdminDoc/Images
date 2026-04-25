using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;

namespace Images.Services;

public static class ImageExportService
{
    public static readonly string[] ExportExtensions =
    [
        ".jpg", ".jpeg", ".jpe", ".jfif", ".jif", ".png", ".webp", ".avif", ".jxl",
        ".tif", ".tiff", ".bmp", ".dib", ".gif", ".apng",
        ".psd", ".psb", ".pdf", ".pdfa", ".eps", ".svg", ".tga", ".targa", ".dds",
        ".qoi", ".exr", ".hdr", ".jp2", ".j2k", ".j2c", ".jpc", ".jpm", ".jpt",
        ".jps", ".ppm", ".pgm", ".pbm", ".pnm", ".pam", ".pfm", ".xpm", ".xbm",
        ".miff", ".mng", ".jng", ".wbmp", ".farbfeld", ".ff", ".dcx", ".pcx",
        ".pcd", ".pcds", ".pgx", ".six", ".sixel", ".vicar", ".viff", ".vips"
    ];

    public static readonly string ExportFilter = string.Join("|",
    [
        "JPEG|*.jpg;*.jpeg;*.jpe;*.jfif;*.jif",
        "PNG|*.png",
        "WebP|*.webp",
        "AVIF|*.avif",
        "JPEG XL|*.jxl",
        "TIFF|*.tif;*.tiff",
        "BMP|*.bmp;*.dib",
        "GIF / APNG|*.gif;*.apng",
        "Photoshop|*.psd;*.psb",
        "PDF / EPS / SVG|*.pdf;*.pdfa;*.eps;*.svg",
        "TGA|*.tga;*.targa",
        "DDS|*.dds",
        "QOI|*.qoi",
        "OpenEXR|*.exr",
        "Radiance HDR|*.hdr",
        "JPEG 2000|*.jp2;*.j2k;*.j2c;*.jpc;*.jpm;*.jpt;*.jps",
        "Portable bitmap|*.ppm;*.pgm;*.pbm;*.pnm;*.pam;*.pfm",
        "X11 / Magick|*.xpm;*.xbm;*.miff;*.mng;*.jng;*.wbmp;*.farbfeld;*.ff",
        "Production and scientific|*.dcx;*.pcx;*.pcd;*.pcds;*.pgx;*.six;*.sixel;*.vicar;*.viff;*.vips",
        "All files|*.*"
    ]);

    public static string NormalizeExportExtension(string currentExtension)
    {
        var ext = currentExtension.ToLowerInvariant();
        var format = ResolveMagickFormat(ext);
        return format is not null && CanWrite(format.Value) ? ext : ".png";
    }

    public static MagickFormat? TryResolveFormat(string extension)
        => ResolveMagickFormat(extension.ToLowerInvariant());

    public static string ResolveWritablePath(string requestedPath)
        => ResolveWritableTarget(requestedPath).Path;

    public static string Save(BitmapSource source, string path)
    {
        var target = ResolveWritableTarget(path);

        using var image = ToMagickImage(source);

        image.Format = target.Format;
        image.Quality = 92;

        if (RequiresOpaqueBackground(target.Format))
        {
            image.BackgroundColor = MagickColors.White;
            image.Alpha(AlphaOption.Remove);
        }

        image.Write(target.Path);
        return target.Path;
    }

    private static (string Path, MagickFormat Format) ResolveWritableTarget(string requestedPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
            throw new ArgumentException("A destination path is required.", nameof(requestedPath));

        var ext = Path.GetExtension(requestedPath).ToLowerInvariant();
        var format = ResolveMagickFormat(ext);
        return format is not null && CanWrite(format.Value)
            ? (requestedPath, format.Value)
            : (Path.ChangeExtension(requestedPath, ".png"), MagickFormat.Png);
    }

    private static MagickImage ToMagickImage(BitmapSource source)
    {
        var normalized = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        var width = normalized.PixelWidth;
        var height = normalized.PixelHeight;
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("The displayed image has no pixel data to export.");

        var stride = checked(width * 4);
        var pixels = new byte[checked(stride * height)];
        normalized.CopyPixels(pixels, stride, 0);

        var settings = new PixelReadSettings((uint)width, (uint)height, StorageType.Char, PixelMapping.BGRA);
        return new MagickImage(pixels, settings);
    }

    private static MagickFormat? ResolveMagickFormat(string ext) => ext switch
    {
        ".jpg" or ".jpeg" or ".jpe" or ".jfif" => MagickFormat.Jpeg,
        ".jif" => MagickFormat.Jpeg,
        ".png" => MagickFormat.Png,
        ".webp" => MagickFormat.WebP,
        ".avif" => MagickFormat.Avif,
        ".jxl" => MagickFormat.Jxl,
        ".heic" or ".heif" or ".hif" => MagickFormat.Heic,
        ".tif" or ".tiff" => MagickFormat.Tiff,
        ".bmp" => MagickFormat.Bmp,
        ".dib" => MagickFormat.Dib,
        ".gif" => MagickFormat.Gif,
        ".apng" => MagickFormat.APng,
        ".psd" => MagickFormat.Psd,
        ".psb" => MagickFormat.Psb,
        ".pdf" => MagickFormat.Pdf,
        ".pdfa" => MagickFormat.Pdfa,
        ".eps" => MagickFormat.Eps,
        ".svg" => MagickFormat.Svg,
        ".tga" or ".targa" => MagickFormat.Tga,
        ".qoi" => MagickFormat.Qoi,
        ".exr" => MagickFormat.Exr,
        ".hdr" => MagickFormat.Hdr,
        ".jp2" => MagickFormat.Jp2,
        ".j2k" => MagickFormat.J2k,
        ".j2c" => MagickFormat.J2c,
        ".jpc" => MagickFormat.Jpc,
        ".jpm" => MagickFormat.Jpm,
        ".jpt" => MagickFormat.Jpt,
        ".jps" => MagickFormat.Jps,
        ".dds" => MagickFormat.Dds,
        ".ppm" => MagickFormat.Ppm,
        ".pgm" => MagickFormat.Pgm,
        ".pbm" => MagickFormat.Pbm,
        ".pnm" => MagickFormat.Pnm,
        ".pam" => MagickFormat.Pam,
        ".pfm" => MagickFormat.Pfm,
        ".xpm" => MagickFormat.Xpm,
        ".xbm" => MagickFormat.Xbm,
        ".miff" => MagickFormat.Miff,
        ".mng" => MagickFormat.Mng,
        ".jng" => MagickFormat.Jng,
        ".wbmp" => MagickFormat.Wbmp,
        ".farbfeld" or ".ff" => MagickFormat.Farbfeld,
        ".dcx" => MagickFormat.Dcx,
        ".pcx" => MagickFormat.Pcx,
        ".pcd" => MagickFormat.Pcd,
        ".pcds" => MagickFormat.Pcds,
        ".pgx" => MagickFormat.Pgx,
        ".six" => MagickFormat.Six,
        ".sixel" => MagickFormat.Sixel,
        ".vicar" => MagickFormat.Vicar,
        ".viff" => MagickFormat.Viff,
        ".vips" => MagickFormat.Vips,
        _ => null
    };

    private static bool RequiresOpaqueBackground(MagickFormat format) => format is
        MagickFormat.Jpeg or
        MagickFormat.Bmp or
        MagickFormat.Ppm or
        MagickFormat.Pgm or
        MagickFormat.Pbm;

    private static bool CanWrite(MagickFormat format)
        => MagickFormatInfo.Create(format)?.SupportsWriting == true;
}
