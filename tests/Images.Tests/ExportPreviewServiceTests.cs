using System.IO;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class ExportPreviewServiceTests
{
    [Fact]
    public void BuildPreview_EncodesDisplayBitmapAndReportsSizeDelta()
    {
        var bitmap = CreateBitmap(16, 8);

        var result = new ExportPreviewService().BuildPreview(
            bitmap,
            sourcePath: null,
            new ExportPreviewRequest(".jpg", 80, 8, 8));

        Assert.Equal("JPG", result.Summary.FormatText);
        Assert.Equal("8 x 4", result.Summary.DimensionsText);
        Assert.True(result.Summary.EstimatedBytes > 0);
        Assert.NotNull(result.PreviewImage);
        Assert.Contains(result.Summary.Warnings, warning => warning.Contains("lossy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EstimateFile_UsesInMemoryEncodeWithoutWritingOutput()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImage(temp.Path, "source.png", 12, 12);

        var summary = new ExportPreviewService().EstimateFile(
            source,
            new ExportPreviewRequest(".webp", 75, 6, 6));

        Assert.Equal("WEBP", summary.FormatText);
        Assert.Equal("6 x 6", summary.DimensionsText);
        Assert.True(summary.EstimatedBytes > 0);
        Assert.Single(Directory.EnumerateFiles(temp.Path));
    }

    [Fact]
    public void BuildDifference_ReturnsEncodedPreviewDimensions()
    {
        var bitmap = CreateBitmap(16, 8);
        var service = new ExportPreviewService();
        var preview = service.BuildPreview(
            bitmap,
            sourcePath: null,
            new ExportPreviewRequest(".jpg", 80, 8, 8));

        var difference = service.BuildDifference(bitmap, preview.PreviewImage);

        Assert.Equal(preview.PreviewImage.PixelWidth, difference.PixelWidth);
        Assert.Equal(preview.PreviewImage.PixelHeight, difference.PixelHeight);
    }

    [Fact]
    public void BuildPreview_IncludesC2paExportHandoff()
    {
        var bitmap = CreateBitmap(16, 8);
        var handoff = C2paExportHandoff.Omitted(
            C2paExportReason.SourceHasNoManifest,
            "C2PA not written",
            "No source Content Credentials were found.");
        var service = new ExportPreviewService((sourcePath, extension) =>
        {
            Assert.Null(sourcePath);
            Assert.Equal(".png", extension);
            return handoff;
        });

        var result = service.BuildPreview(
            bitmap,
            sourcePath: null,
            new ExportPreviewRequest(".png", 92, 0, 0));

        Assert.Equal(handoff, result.Summary.C2paHandoff);
    }

    [Fact]
    public void BuildPreview_WhenCanceledBeforeEncoding_Throws()
    {
        var bitmap = CreateBitmap(16, 8);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => new ExportPreviewService().BuildPreview(
            bitmap,
            sourcePath: null,
            new ExportPreviewRequest(".jpg", 80, 8, 8),
            cts.Token));
    }

    [Fact]
    public void NormalizeRequest_WhenFormatCannotBeWritten_FallsBackToPng()
    {
        var request = ExportPreviewService.NormalizeRequest(new ExportPreviewRequest(".not-real", 200, -4, 320));

        Assert.Equal(".png", request.Extension);
        Assert.Equal(100, request.Quality);
        Assert.Equal(0, request.MaxWidth);
        Assert.Equal(320, request.MaxHeight);
    }

    private static BitmapSource CreateBitmap(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index + 0] = 0x20;
            pixels[index + 1] = 0x80;
            pixels[index + 2] = 0xF0;
            pixels[index + 3] = 0xFF;
        }

        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static string WriteImage(string folder, string name, uint width, uint height)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(MagickColors.Red, width, height)
        {
            Format = MagickFormat.Png
        };
        image.Write(path);
        return path;
    }
}
