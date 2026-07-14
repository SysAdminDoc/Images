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
    IReadOnlyList<string> Warnings,
    C2paExportHandoff C2paHandoff)
{
    public string DimensionsText => Width == 0 || Height == 0 ? "Unknown" : $"{Width} x {Height}";
    public string WarningText => Warnings.Count == 0 ? "No format warnings." : string.Join(" ", Warnings);
}

public sealed record ExportPreviewResult(
    BitmapSource PreviewImage,
    ExportPreviewSummary Summary);

public sealed class ExportPreviewService
{
    private readonly Func<string?, string, C2paExportHandoff> _planC2paExport;

    public ExportPreviewService(Func<string?, string, C2paExportHandoff>? planC2paExport = null)
    {
        _planC2paExport = planC2paExport ?? C2paManifestService.PlanExportHandoff;
    }

    public ExportPreviewResult BuildPreview(
        BitmapSource source,
        string? sourcePath,
        ExportPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        request = NormalizeRequest(request);

        using var image = ImageExportService.CreateMagickImage(source);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceBytes = TryGetSourceBytes(sourcePath) ?? EstimateDecodedBytes(source.PixelWidth, source.PixelHeight);
        var encoded = Encode(image, request, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var summary = CreateSummary(image, encoded, sourceBytes, request, sourcePath);
        cancellationToken.ThrowIfCancellationRequested();

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
        using var image = MagickSafeReader.Read(normalizedPath);
        var sourceBytes = TryGetSourceBytes(normalizedPath) ?? 0;
        var encoded = Encode(image, request, CancellationToken.None);
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

    private ExportPreviewSummary CreateSummary(
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
            ExportCapabilityWarningService.BuildWarnings(image, sourcePath, request.Extension, format),
            _planC2paExport(sourcePath, request.Extension));
    }

    private static EncodedPreview Encode(MagickImage image, ExportPreviewRequest request, CancellationToken cancellationToken)
    {
        var format = ImageExportService.TryResolveFormat(request.Extension) ?? MagickFormat.Png;
        cancellationToken.ThrowIfCancellationRequested();

        using var copy = image.Clone();
        cancellationToken.ThrowIfCancellationRequested();

        ApplyResize(copy, request);
        cancellationToken.ThrowIfCancellationRequested();

        PrepareForPreview(copy, format, (uint)request.Quality);
        var bytes = copy.ToByteArray(format);
        cancellationToken.ThrowIfCancellationRequested();

        return new EncodedPreview(bytes, copy.Width, copy.Height);
    }

    private static void ApplyResize(IMagickImage<float> image, ExportPreviewRequest request)
    {
        if (request.MaxWidth <= 0 && request.MaxHeight <= 0)
            return;

        var width = request.MaxWidth > 0 ? (uint)request.MaxWidth : image.Width;
        var height = request.MaxHeight > 0 ? (uint)request.MaxHeight : image.Height;
        image.Resize(new MagickGeometry(width, height)
        {
            IgnoreAspectRatio = false,
            // "Max dimensions" is a bound, never an upscale target.
            Greater = true
        });
    }

    private static void PrepareForPreview(IMagickImage<float> image, MagickFormat format, uint quality)
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
