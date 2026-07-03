using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;

namespace Images.Services;

public sealed record ImageExportResult(string OutputPath, C2paExportHandoff C2paHandoff);

public static class ImageExportService
{
    public static readonly string[] ExportExtensions =
    [
        ".jpg", ".jpeg", ".jpe", ".jfif", ".jif", ".png", ".webp", ".avif", ".jxl",
        ".tif", ".tiff", ".bmp", ".dib", ".gif",
        ".psd", ".psb", ".tga", ".targa", ".dds",
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
        "GIF|*.gif",
        "Photoshop|*.psd;*.psb",
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
    {
        var normalized = extension.ToLowerInvariant();
        if (!MagickSecurityPolicy.IsWriteTargetAllowed(normalized))
            return null;

        return ResolveMagickFormat(normalized);
    }

    public static string ResolveWritablePath(string requestedPath)
        => ResolveWritableTarget(requestedPath).Path;

    public static string Save(BitmapSource source, string path)
        => Save(source, path, 92);

    public static string Save(BitmapSource source, string path, uint quality)
        => Save(source, path, quality, maxWidth: 0, maxHeight: 0);

    public static string Save(BitmapSource source, string path, uint quality, int maxWidth, int maxHeight)
    {
        var target = ResolveWritableTarget(path);

        using var image = ToMagickImage(source);
        ApplyResize(image, Math.Max(0, maxWidth), Math.Max(0, maxHeight));

        PrepareForWrite(image, target.Format, Math.Clamp(quality, 1, 100));

        WriteAtomically(image, target.Path);
        return target.Path;
    }

    public static ImageExportResult SaveWithC2paHandoff(
        BitmapSource source,
        string? sourcePath,
        string path,
        uint quality = 92,
        int maxWidth = 0,
        int maxHeight = 0)
        => SaveWithC2paHandoff(
            source,
            sourcePath,
            path,
            quality,
            maxWidth,
            maxHeight,
            planC2paExport: null);

    internal static ImageExportResult SaveWithC2paHandoff(
        BitmapSource source,
        string? sourcePath,
        string path,
        uint quality,
        int maxWidth,
        int maxHeight,
        Func<string?, string, C2paExportHandoff>? planC2paExport)
    {
        var target = ResolveWritableTarget(path);
        var c2pa = (planC2paExport ?? C2paManifestService.PlanExportHandoff)(
            sourcePath,
            Path.GetExtension(target.Path));
        var savedPath = Save(source, target.Path, quality, maxWidth, maxHeight);
        return new ImageExportResult(savedPath, c2pa);
    }

    public static BitmapSource RenderPreview(BitmapSource source, IReadOnlyList<EditOperation> operations)
    {
        if (operations.Count == 0)
            return source;

        using var image = ToMagickImage(source);
        NonDestructiveEditService.ApplyOperations(image, operations);
        return ToBitmapSource(image);
    }

    public static string Save(string sourcePath, string path, IReadOnlyList<EditOperation> operations)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("A source image path is required.", nameof(sourcePath));

        var normalizedSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(normalizedSourcePath))
            throw new IOException("Source image does not exist.");

        var target = ResolveWritableTarget(path);

        using var image = new MagickImage(normalizedSourcePath);
        NonDestructiveEditService.ApplyOperations(image, operations);
        PrepareForWrite(image, target.Format, 92);

        WriteAtomically(image, target.Path);
        return target.Path;
    }

    public static ImageExportResult SaveWithC2paHandoff(
        string sourcePath,
        string path,
        IReadOnlyList<EditOperation> operations)
        => SaveWithC2paHandoff(sourcePath, path, operations, planC2paExport: null);

    internal static ImageExportResult SaveWithC2paHandoff(
        string sourcePath,
        string path,
        IReadOnlyList<EditOperation> operations,
        Func<string?, string, C2paExportHandoff>? planC2paExport)
    {
        var target = ResolveWritableTarget(path);
        var c2pa = (planC2paExport ?? C2paManifestService.PlanExportHandoff)(
            sourcePath,
            Path.GetExtension(target.Path));
        var savedPath = Save(sourcePath, target.Path, operations);
        return new ImageExportResult(savedPath, c2pa);
    }

    public static string Overwrite(string sourcePath, IReadOnlyList<EditOperation> operations)
        => Overwrite(sourcePath, operations, jpegTranRuntime: null, jpegTranProcessRunner: null);

    internal static string Overwrite(
        string sourcePath,
        IReadOnlyList<EditOperation> operations,
        bool allowLosslessJpegTrim)
        => Overwrite(
            sourcePath,
            operations,
            jpegTranRuntime: null,
            jpegTranProcessRunner: null,
            allowLosslessJpegTrim);

    internal static string Overwrite(
        string sourcePath,
        IReadOnlyList<EditOperation> operations,
        JpegTranRuntimeStatus? jpegTranRuntime,
        JpegTranProcessRunner? jpegTranProcessRunner,
        bool allowLosslessJpegTrim = false)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("A source image path is required.", nameof(sourcePath));

        var normalizedSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(normalizedSourcePath))
            throw new IOException("Source image does not exist.");

        var extension = Path.GetExtension(normalizedSourcePath).ToLowerInvariant();
        var format = ResolveMagickFormat(extension);
        if (!SupportedImageFormats.IsCropWritableRasterExtension(extension) || format is null || !CanWrite(format.Value))
            throw new InvalidOperationException("Source overwrite supports only flat raster image files such as JPEG, PNG, WebP, TIFF, GIF, BMP, HEIC/AVIF/JXL, and similar bitmap formats.");

        using var image = new MagickImage(normalizedSourcePath);
        var losslessJpegTransform = TryOverwriteLosslessJpegTransform(
            normalizedSourcePath,
            operations,
            image,
            jpegTranRuntime ?? JpegTranRuntime.Inspect(),
            jpegTranProcessRunner,
            allowLosslessJpegTrim);
        if (losslessJpegTransform.Attempted)
        {
            if (losslessJpegTransform.Applied)
                return normalizedSourcePath;

            throw new InvalidOperationException(losslessJpegTransform.Message);
        }

        NonDestructiveEditService.ApplyOperations(image, operations);
        PrepareForWrite(image, format.Value, 92);

        WriteAtomically(image, normalizedSourcePath);
        File.SetLastWriteTimeUtc(normalizedSourcePath, DateTime.UtcNow);
        return normalizedSourcePath;
    }

    private static JpegTranWriteResult TryOverwriteLosslessJpegTransform(
        string sourcePath,
        IReadOnlyList<EditOperation> operations,
        MagickImage image,
        JpegTranRuntimeStatus jpegTranRuntime,
        JpegTranProcessRunner? jpegTranProcessRunner,
        bool allowLosslessJpegTrim)
    {
        var enabledOperations = operations.Where(operation => operation.Enabled).ToList();
        if (enabledOperations.Count != 1)
            return JpegTranWriteResult.NotAttempted("Lossless JPEG writeback requires exactly one enabled operation.");

        var operation = enabledOperations[0];
        if (!IsDefaultJpegOrientation(image.Orientation.ToString()))
            return JpegTranWriteResult.NotAttempted("Lossless JPEG writeback is skipped for files with EXIF orientation metadata.");

        return NormalizeEditKind(operation.Kind) switch
        {
            "crop" when TryReadCropSelection(operation, out var selection) =>
                JpegTranTransformService.TryApplyExactCrop(
                    sourcePath,
                    selection,
                    (int)image.Width,
                    (int)image.Height,
                    jpegTranRuntime,
                    jpegTranProcessRunner,
                    allowTrim: allowLosslessJpegTrim),
            "crop" => JpegTranWriteResult.NotAttempted("Crop operation parameters are not valid for lossless JPEG writeback."),
            "rotate" when TryReadLosslessRotation(operation, out var rotation) =>
                JpegTranTransformService.TryApplyExactRotation(
                    sourcePath,
                    rotation,
                    (int)image.Width,
                    (int)image.Height,
                    jpegTranRuntime,
                    jpegTranProcessRunner,
                    allowTrim: allowLosslessJpegTrim),
            "rotate" => JpegTranWriteResult.NotAttempted("Rotate operation parameters are not valid for lossless JPEG writeback."),
            _ => JpegTranWriteResult.NotAttempted("Lossless JPEG writeback currently supports crop and right-angle rotate operations only.")
        };
    }

    internal static LosslessJpegCropPlan? TryPlanLosslessJpegCropTrimConfirmation(
        string sourcePath,
        PixelSelection requestedSelection,
        int imageWidth,
        int imageHeight,
        IReadOnlyList<EditOperation> existingOperations,
        JpegTranRuntimeStatus? jpegTranRuntime = null)
    {
        if (existingOperations.Any(operation => operation.Enabled))
            return null;

        if (!IsJpegExtension(sourcePath))
            return null;

        var runtime = jpegTranRuntime ?? JpegTranRuntime.Inspect();
        if (!runtime.Available || string.IsNullOrWhiteSpace(runtime.ExecutablePath))
            return null;

        if (!TryReadJpegDimensionsAndOrientation(sourcePath, out _, out _, out var orientation) ||
            !IsDefaultJpegOrientation(orientation))
            return null;

        var plan = LosslessJpegTransformPolicy.PlanCrop(
            requestedSelection,
            imageWidth,
            imageHeight,
            JpegMcuSize.Conservative420);

        return plan is { CanApplyLosslessly: true, RequiresTrimConfirmation: true }
            ? plan
            : null;
    }

    internal static LosslessJpegRotationPlan? TryPlanLosslessJpegRotationTrimConfirmation(
        string sourcePath,
        LosslessJpegRotation rotation,
        IReadOnlyList<EditOperation> existingOperations,
        JpegTranRuntimeStatus? jpegTranRuntime = null)
    {
        if (existingOperations.Any(operation => operation.Enabled))
            return null;

        if (!IsJpegExtension(sourcePath))
            return null;

        var runtime = jpegTranRuntime ?? JpegTranRuntime.Inspect();
        if (!runtime.Available || string.IsNullOrWhiteSpace(runtime.ExecutablePath))
            return null;

        if (!TryReadJpegDimensionsAndOrientation(sourcePath, out var imageWidth, out var imageHeight, out var orientation) ||
            !IsDefaultJpegOrientation(orientation))
            return null;

        var plan = LosslessJpegTransformPolicy.PlanRotation(
            imageWidth,
            imageHeight,
            rotation,
            JpegMcuSize.Conservative420);

        return plan is { CanApplyLosslessly: true, RequiresTrimConfirmation: true }
            ? plan
            : null;
    }

    private static bool TryReadCropSelection(EditOperation operation, out PixelSelection selection)
    {
        selection = default;
        if (!TryReadInt(operation.Parameters, "x", out var x) ||
            !TryReadInt(operation.Parameters, "y", out var y) ||
            !TryReadInt(operation.Parameters, "width", out var width) ||
            !TryReadInt(operation.Parameters, "height", out var height) ||
            x < 0 ||
            y < 0 ||
            width <= 0 ||
            height <= 0)
        {
            return false;
        }

        selection = new PixelSelection(x, y, width, height);
        return true;
    }

    private static bool TryReadInt(IReadOnlyDictionary<string, string> parameters, string key, out int value)
    {
        value = 0;
        return parameters.TryGetValue(key, out var raw) &&
               int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadLosslessRotation(EditOperation operation, out LosslessJpegRotation rotation)
    {
        rotation = default;
        if (!operation.Parameters.TryGetValue("degrees", out var raw) ||
            !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        var normalized = ((int)Math.Round(parsed) % 360 + 360) % 360;
        rotation = normalized switch
        {
            90 => LosslessJpegRotation.Rotate90,
            180 => LosslessJpegRotation.Rotate180,
            270 => LosslessJpegRotation.Rotate270,
            _ => default
        };
        return normalized is 90 or 180 or 270;
    }

    private static string NormalizeEditKind(string kind)
        => (kind ?? "").Trim().ToLowerInvariant().Replace("_", "-", StringComparison.OrdinalIgnoreCase);

    private static bool IsDefaultJpegOrientation(string orientation)
        => orientation.Equals("Undefined", StringComparison.OrdinalIgnoreCase) ||
           orientation.Equals("TopLeft", StringComparison.OrdinalIgnoreCase);

    private static bool TryReadJpegDimensionsAndOrientation(
        string sourcePath,
        out int width,
        out int height,
        out string orientation)
    {
        width = 0;
        height = 0;
        orientation = "";
        try
        {
            using var image = new MagickImage(sourcePath);
            width = (int)image.Width;
            height = (int)image.Height;
            orientation = image.Orientation.ToString();
            return true;
        }
        catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool IsJpegExtension(string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpe", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jfif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jif", StringComparison.OrdinalIgnoreCase);
    }

    private static (string Path, MagickFormat Format) ResolveWritableTarget(string requestedPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
            throw new ArgumentException("A destination path is required.", nameof(requestedPath));

        var normalizedPath = Path.GetFullPath(requestedPath);
        var ext = Path.GetExtension(normalizedPath).ToLowerInvariant();
        var format = TryResolveFormat(ext);
        return format is not null && CanWrite(format.Value)
            ? (normalizedPath, format.Value)
            : (Path.ChangeExtension(normalizedPath, ".png"), MagickFormat.Png);
    }

    private static void WriteAtomically(MagickImage image, string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new IOException("Export destination has no directory.");
        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $".images-export-{Guid.NewGuid():N}{Path.GetExtension(targetPath)}.tmp");
        try
        {
            image.Write(tempPath);
            File.Move(tempPath, targetPath, overwrite: true);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private static void PrepareForWrite(MagickImage image, MagickFormat format, uint quality)
    {
        image.Format = format;
        image.Quality = quality;

        if (RequiresOpaqueBackground(format))
        {
            image.BackgroundColor = MagickColors.White;
            image.Alpha(AlphaOption.Remove);
        }
    }

    private static void ApplyResize(MagickImage image, int maxWidth, int maxHeight)
    {
        if (maxWidth <= 0 && maxHeight <= 0)
            return;

        var width = maxWidth > 0 ? (uint)maxWidth : image.Width;
        var height = maxHeight > 0 ? (uint)maxHeight : image.Height;
        image.Resize(new MagickGeometry(width, height)
        {
            IgnoreAspectRatio = false,
            // "Max dimensions" is a bound, never an upscale target.
            Greater = true
        });
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup; failed temp deletes are harmless and rare.
        }
    }

    internal static MagickImage CreateMagickImage(BitmapSource source)
        => ToMagickImage(source);

    internal static BitmapSource CreateBitmapSource(MagickImage image)
        => ToBitmapSource(image);

    internal static void PrepareForExport(MagickImage image, MagickFormat format, uint quality)
        => PrepareForWrite(image, format, Math.Clamp(quality, 1, 100));

    internal static bool FormatRequiresOpaqueBackground(MagickFormat format)
        => RequiresOpaqueBackground(format);

    internal static bool CanWriteFormat(MagickFormat format)
        => CanWrite(format);

    internal static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes} B"
            : $"{value:0.#} {units[unit]}";
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

    private static BitmapSource ToBitmapSource(MagickImage image)
    {
        var bytes = image.ToByteArray(MagickFormat.Png);
        using var stream = new MemoryStream(bytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
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
        => format is not MagickFormat.APng &&
           MagickSecurityPolicy.IsWriteFormatAllowed(format) &&
           MagickFormatInfo.Create(format)?.SupportsWriting == true;
}
