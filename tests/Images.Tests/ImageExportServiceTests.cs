using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Images.Services;

namespace Images.Tests;

public sealed class ImageExportServiceTests
{
    [Fact]
    public void ResolveWritablePath_WhenExtensionCannotBeWritten_NormalizesAndFallsBackToPng()
    {
        using var temp = TestDirectory.Create();
        var requested = Path.Combine(temp.Path, "nested", "..", "export.not-a-real-format");

        var resolved = ImageExportService.ResolveWritablePath(requested);

        Assert.Equal(Path.Combine(temp.Path, "export.png"), resolved);
    }

    [Fact]
    public void Save_WhenTargetExists_ReplacesAtomically()
    {
        using var temp = TestDirectory.Create();
        var target = temp.WriteFile("export.png", "old");
        var oldBytes = File.ReadAllBytes(target);
        var bitmap = BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[] { 0xFF, 0x00, 0x00, 0xFF },
            4);
        bitmap.Freeze();

        var savedPath = ImageExportService.Save(bitmap, target);

        Assert.Equal(Path.GetFullPath(target), savedPath);
        Assert.True(new FileInfo(target).Length > 0);
        Assert.NotEqual(oldBytes, File.ReadAllBytes(target));
    }

    [Fact]
    public void Overwrite_WithCropOperation_ReplacesSourcePixelsAndTouchesFile()
    {
        using var temp = TestDirectory.Create();
        var source = Path.Combine(temp.Path, "source.png");
        var bitmap = BitmapSource.Create(
            2,
            2,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[]
            {
                0xFF, 0x00, 0x00, 0xFF, 0x00, 0xFF, 0x00, 0xFF,
                0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
            },
            8);
        bitmap.Freeze();
        ImageExportService.Save(bitmap, source);
        var previousWriteTime = DateTime.UtcNow - TimeSpan.FromMinutes(10);
        File.SetLastWriteTimeUtc(source, previousWriteTime);
        var operations = new[]
        {
            new EditOperation(
                "crop-test",
                "crop",
                DateTimeOffset.UtcNow,
                Enabled: true,
                CropSelectionService.ToEditParameters(new PixelSelection(0, 0, 1, 2)),
                "Crop test")
        };

        var savedPath = ImageExportService.Overwrite(source, operations);

        Assert.Equal(Path.GetFullPath(source), savedPath);
        Assert.Equal((1, 2), ReadImageSize(source));
        Assert.True(File.GetLastWriteTimeUtc(source) > previousWriteTime);
    }

    [Theory]
    [InlineData(".jpg", true)]
    [InlineData(".png", true)]
    [InlineData(".webp", true)]
    [InlineData(".psd", false)]
    [InlineData(".pdf", false)]
    [InlineData(".svg", false)]
    [InlineData(".ai", false)]
    [InlineData(".cbz", false)]
    [InlineData(".dng", false)]
    public void CropWritableRasterAllowlist_MatchesSourceOverwritePolicy(string extension, bool expected)
        => Assert.Equal(expected, SupportedImageFormats.IsCropWritableRasterExtension(extension));

    [Theory]
    [InlineData("source.psd")]
    [InlineData("source.pdf")]
    [InlineData("source.svg")]
    public void Overwrite_WhenSourceFormatIsNotFlatRaster_RejectsBeforeDecode(string fileName)
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile(fileName, "not an image");

        var ex = Assert.Throws<InvalidOperationException>(() => ImageExportService.Overwrite(source, []));

        Assert.Contains("flat raster image files", ex.Message);
    }

    private static (int Width, int Height) ReadImageSize(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        return (frame.PixelWidth, frame.PixelHeight);
    }
}
