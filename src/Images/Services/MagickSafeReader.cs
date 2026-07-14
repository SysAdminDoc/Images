using System.IO;
using System.IO.Compression;
using ImageMagick;

namespace Images.Services;

/// <summary>
/// Central entry point for file-backed ImageMagick reads. SVG is pinned to ImageMagick's
/// in-process MSVG coder so disabling all external delegates does not silently route it through
/// Inkscape; gzip-wrapped SVGZ is decompressed in managed code before that same coder is used.
/// </summary>
public static class MagickSafeReader
{
    public static MagickImage Read(string path, MagickReadSettings? settings = null)
    {
        CodecRuntime.Configure();
        var image = new MagickImage();
        try
        {
            ReadPath(image, path, settings);
            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    public static MagickImage Read(byte[] bytes, string extension, MagickReadSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        using var stream = new MemoryStream(bytes, writable: false);
        return Read(stream, extension, settings);
    }

    public static MagickImage Read(Stream stream, string extension, MagickReadSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        CodecRuntime.Configure();

        var image = new MagickImage();
        try
        {
            ReadStream(image, stream, extension, settings);
            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    public static MagickImage Ping(string path)
        => Ping(path, settings: null);

    public static MagickImage Ping(string path, MagickReadSettings? settings)
    {
        CodecRuntime.Configure();

        // The native SVG delegate selected by extension is intentionally disabled. A full MSVG
        // read is still resource-bounded and is the only trustworthy way to obtain its dimensions.
        if (IsSvg(path))
            return Read(path);

        var image = new MagickImage();
        try
        {
            using var stream = OpenSharedRead(path);
            if (settings is null)
                image.Ping(stream);
            else
                image.Ping(stream, settings);
            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    public static MagickImageCollection ReadCollection(string path, MagickReadSettings? settings = null)
    {
        CodecRuntime.Configure();

        if (IsSvg(path))
        {
            var collection = new MagickImageCollection();
            try
            {
                collection.Add(Read(path, settings));
                return collection;
            }
            catch
            {
                collection.Dispose();
                throw;
            }
        }

        using var stream = OpenSharedRead(path);
        return settings is null
            ? new MagickImageCollection(stream)
            : new MagickImageCollection(stream, settings);
    }

    public static int CountFrames(string path, MagickReadSettings? settings = null)
    {
        CodecRuntime.Configure();
        if (IsSvg(path)) return 1;

        using var stream = OpenSharedRead(path);
        var frames = settings is null
            ? MagickImageInfo.ReadCollection(stream)
            : MagickImageInfo.ReadCollection(stream, settings);
        return Math.Max(1, frames.Count());
    }

    internal static FileStream OpenSharedRead(string path)
        => new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static void ReadPath(MagickImage image, string path, MagickReadSettings? settings)
    {
        using var file = OpenSharedRead(path);
        ReadStream(image, file, Path.GetExtension(path), settings);
    }

    private static void ReadStream(
        MagickImage image,
        Stream stream,
        string extension,
        MagickReadSettings? settings)
    {
        if (!IsSvgExtension(extension))
        {
            if (settings is null)
                image.Read(stream);
            else
                image.Read(stream, settings);
            return;
        }

        settings = WithMsvgFormat(settings);
        if (!NormalizeExtension(extension).Equals(".svgz", StringComparison.OrdinalIgnoreCase))
        {
            image.Read(stream, settings);
            return;
        }

        using var gzip = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
        image.Read(gzip, settings);
    }

    private static MagickReadSettings WithMsvgFormat(MagickReadSettings? settings)
    {
        settings ??= new MagickReadSettings();
        settings.Format = MagickFormat.Msvg;
        // Keep CSS pixel dimensions stable. MSVG otherwise treats caller density as a physical
        // scale factor, unlike the prior delegate-backed path, and the same SVG changes size.
        settings.Density = new Density(96);
        return settings;
    }

    private static bool IsSvg(string path)
        => IsSvgExtension(Path.GetExtension(path));

    private static bool IsSvgExtension(string extension)
    {
        var normalized = NormalizeExtension(extension);
        return normalized.Equals(".svg", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals(".svgz", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return string.Empty;
        return extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension;
    }
}
