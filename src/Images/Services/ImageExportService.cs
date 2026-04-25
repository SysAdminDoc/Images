using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;

namespace Images.Services;

public static class ImageExportService
{
    public const string ExportFilter =
        "JPEG|*.jpg;*.jpeg;*.jfif|" +
        "PNG|*.png|" +
        "WebP|*.webp|" +
        "AVIF|*.avif|" +
        "JPEG XL|*.jxl|" +
        "HEIC|*.heic;*.heif;*.hif|" +
        "TIFF|*.tif;*.tiff|" +
        "BMP|*.bmp|" +
        "GIF|*.gif|" +
        "TGA|*.tga;*.targa|" +
        "DDS|*.dds|" +
        "QOI|*.qoi|" +
        "OpenEXR|*.exr|" +
        "Radiance HDR|*.hdr|" +
        "JPEG 2000|*.jp2;*.j2k|" +
        "Portable bitmap|*.ppm;*.pgm;*.pbm;*.pfm|" +
        "All files|*.*";

    public static string NormalizeExportExtension(string currentExtension)
    {
        var ext = currentExtension.ToLowerInvariant();
        return ResolveMagickFormat(ext) is null ? ".png" : ext;
    }

    public static string Save(BitmapSource source, string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var format = ResolveMagickFormat(ext);
        if (format is null)
        {
            path = Path.ChangeExtension(path, ".png");
            format = MagickFormat.Png;
        }

        using var image = ToMagickImage(source);

        image.Format = format.Value;
        image.Quality = 92;

        if (RequiresOpaqueBackground(format.Value))
        {
            image.BackgroundColor = MagickColors.White;
            image.Alpha(AlphaOption.Remove);
        }

        image.Write(path);
        return path;
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
        ".png" => MagickFormat.Png,
        ".webp" => MagickFormat.WebP,
        ".avif" => MagickFormat.Avif,
        ".jxl" => MagickFormat.Jxl,
        ".heic" or ".heif" or ".hif" => MagickFormat.Heic,
        ".tif" or ".tiff" => MagickFormat.Tiff,
        ".bmp" => MagickFormat.Bmp,
        ".gif" => MagickFormat.Gif,
        ".tga" or ".targa" => MagickFormat.Tga,
        ".qoi" => MagickFormat.Qoi,
        ".exr" => MagickFormat.Exr,
        ".hdr" => MagickFormat.Hdr,
        ".jp2" => MagickFormat.Jp2,
        ".j2k" => MagickFormat.J2k,
        ".dds" => MagickFormat.Dds,
        ".ppm" => MagickFormat.Ppm,
        ".pgm" => MagickFormat.Pgm,
        ".pbm" => MagickFormat.Pbm,
        ".pfm" => MagickFormat.Pfm,
        _ => null
    };

    private static bool RequiresOpaqueBackground(MagickFormat format) => format is
        MagickFormat.Jpeg or
        MagickFormat.Bmp or
        MagickFormat.Ppm or
        MagickFormat.Pgm or
        MagickFormat.Pbm;
}
