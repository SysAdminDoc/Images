using System.IO;
using System.Windows.Media.Imaging;
using ImageMagick;

namespace Images.Services;

public sealed record ExportPreviewPreset(
    string Name,
    string Extension,
    int Quality,
    int MaxWidth = 0,
    int MaxHeight = 0)
{
    public static IReadOnlyList<ExportPreviewPreset> Defaults { get; } =
    [
        new("JPEG web copy", ".jpg", 88, 2400, 2400),
        new("PNG archive copy", ".png", 92, 0, 0),
        new("WebP balanced", ".webp", 82, 2400, 2400),
        new("AVIF compact", ".avif", 72, 2400, 2400),
        new("JPEG XL archive", ".jxl", 90, 0, 0)
    ];
}

public sealed record ExportPreviewRequest(
    string Extension,
    int Quality,
    int MaxWidth = 0,
    int MaxHeight = 0);

public sealed record ExportPreviewSummary(
    long SourceBytes,
    long EstimatedBytes,
    uint Width,
    uint Height,
    string FormatText,
    string QualityText,
    string SourceSizeText,
    string EstimatedSizeText,
    string DeltaText,
    IReadOnlyList<string> Warnings)
{
    public string DimensionsText => Width == 0 || Height == 0 ? "Unknown" : $"{Width} x {Height}";
    public string WarningText => Warnings.Count == 0 ? "No format warnings." : string.Join(" ", Warnings);
}

public sealed record ExportPreviewResult(
    BitmapSource PreviewImage,
    ExportPreviewSummary Summary);

public sealed class ExportPreviewService
{
    public ExportPreviewResult BuildPreview(BitmapSource source, string? sourcePath, ExportPreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(source);
        request = NormalizeRequest(request);

        using var image = ImageExportService.CreateMagickImage(source);
        var sourceBytes = TryGetSourceBytes(sourcePath) ?? EstimateDecodedBytes(source.PixelWidth, source.PixelHeight);
        var encoded = Encode(image, request);
        var summary = CreateSummary(image, encoded, sourceBytes, request, sourcePath);

        using var preview = new MagickImage(encoded.Bytes);
        return new ExportPreviewResult(ImageExportService.CreateBitmapSource(preview), summary);
    }

    public ExportPreviewSummary EstimateFile(string sourcePath, ExportPreviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("A source path is required.", nameof(sourcePath));

        var normalizedPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(normalizedPath))
            throw new IOException("Source image does not exist.");

        request = NormalizeRequest(request);
        using var image = new MagickImage(normalizedPath);
        var sourceBytes = TryGetSourceBytes(normalizedPath) ?? 0;
        var encoded = Encode(image, request);
        return CreateSummary(image, encoded, sourceBytes, request, normalizedPath);
    }

    public BitmapSource BuildDifference(BitmapSource source, BitmapSource encodedPreview)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(encodedPreview);

        using var sourceImage = ImageExportService.CreateMagickImage(source);
        using var encodedImage = ImageExportService.CreateMagickImage(encodedPreview);
        using var sourceComparable = (MagickImage)sourceImage.Clone();
        using var difference = (MagickImage)encodedImage.Clone();

        if (sourceComparable.Width != difference.Width || sourceComparable.Height != difference.Height)
        {
            sourceComparable.Resize(new MagickGeometry(difference.Width, difference.Height)
            {
                IgnoreAspectRatio = false
            });
        }

        difference.Composite(sourceComparable, CompositeOperator.Difference);
        difference.AutoLevel();
        return ImageExportService.CreateBitmapSource(difference);
    }

    public static ExportPreviewRequest NormalizeRequest(ExportPreviewRequest request)
    {
        var extension = RenameService.NormalizeExtension(request.Extension);
        var format = string.IsNullOrWhiteSpace(extension)
            ? null
            : ImageExportService.TryResolveFormat(extension);
        if (format is null || !ImageExportService.CanWriteFormat(format.Value))
            extension = ".png";

        return request with
        {
            Extension = extension,
            Quality = Math.Clamp(request.Quality, 1, 100),
            MaxWidth = Math.Max(0, request.MaxWidth),
            MaxHeight = Math.Max(0, request.MaxHeight)
        };
    }

    public static ExportPreviewRequest FromPreset(ExportPreviewPreset preset)
        => NormalizeRequest(new ExportPreviewRequest(preset.Extension, preset.Quality, preset.MaxWidth, preset.MaxHeight));

    private static ExportPreviewSummary CreateSummary(
        MagickImage image,
        EncodedPreview encoded,
        long sourceBytes,
        ExportPreviewRequest request,
        string? sourcePath)
    {
        var format = ImageExportService.TryResolveFormat(request.Extension) ?? MagickFormat.Png;
        var delta = encoded.Bytes.LongLength - sourceBytes;
        return new ExportPreviewSummary(
            sourceBytes,
            encoded.Bytes.LongLength,
            encoded.Width,
            encoded.Height,
            request.Extension.TrimStart('.').ToUpperInvariant(),
            request.Quality.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ImageExportService.FormatBytes(sourceBytes),
            ImageExportService.FormatBytes(encoded.Bytes.LongLength),
            FormatDelta(delta),
            ExportCapabilityWarningService.BuildWarnings(image, sourcePath, request.Extension, format));
    }

    private static EncodedPreview Encode(MagickImage image, ExportPreviewRequest request)
    {
        var format = ImageExportService.TryResolveFormat(request.Extension) ?? MagickFormat.Png;
        using var copy = image.Clone();
        ApplyResize(copy, request);
        PrepareForPreview(copy, format, (uint)request.Quality);
        return new EncodedPreview(copy.ToByteArray(format), copy.Width, copy.Height);
    }

    private static void ApplyResize(IMagickImage<ushort> image, ExportPreviewRequest request)
    {
        if (request.MaxWidth <= 0 && request.MaxHeight <= 0)
            return;

        var width = request.MaxWidth > 0 ? (uint)request.MaxWidth : image.Width;
        var height = request.MaxHeight > 0 ? (uint)request.MaxHeight : image.Height;
        image.Resize(new MagickGeometry(width, height)
        {
            IgnoreAspectRatio = false
        });
    }

    private static void PrepareForPreview(IMagickImage<ushort> image, MagickFormat format, uint quality)
    {
        image.Format = format;
        image.Quality = Math.Clamp(quality, 1, 100);

        if (ImageExportService.FormatRequiresOpaqueBackground(format))
        {
            image.BackgroundColor = MagickColors.White;
            image.Alpha(AlphaOption.Remove);
        }
    }

    private static long? TryGetSourceBytes(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            return null;

        try
        {
            var info = new FileInfo(sourcePath);
            return info.Exists ? info.Length : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static long EstimateDecodedBytes(int width, int height)
        => width <= 0 || height <= 0 ? 0 : checked((long)width * height * 4);

    private static string FormatDelta(long bytes)
    {
        if (bytes == 0)
            return "same size";

        var sign = bytes > 0 ? "+" : "-";
        return sign + ImageExportService.FormatBytes(Math.Abs(bytes));
    }

    private sealed record EncodedPreview(byte[] Bytes, uint Width, uint Height);
}
