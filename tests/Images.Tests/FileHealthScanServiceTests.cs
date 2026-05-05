using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Images.Services;

namespace Images.Tests;

public sealed class FileHealthScanServiceTests
{
    [Fact]
    public void Scan_DetectsBadExtensionFromContentSignature()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "wrong.jpg");
        WritePng(path);

        var result = new FileHealthScanService().Scan([temp.Path]);

        var finding = Assert.Single(result.Findings, item => item.Kind == FileHealthFindingKind.BadExtension);
        Assert.Equal("PNG", finding.DetectedFormat);
        Assert.Equal(".png", finding.SuggestedExtension);
        Assert.True(finding.CanRename);
    }

    [Fact]
    public void Scan_DetectsZeroByteFiles()
    {
        using var temp = TestDirectory.Create();
        File.WriteAllBytes(Path.Combine(temp.Path, "empty.png"), []);

        var result = new FileHealthScanService().Scan([temp.Path]);

        var finding = Assert.Single(result.Findings, item => item.Kind == FileHealthFindingKind.ZeroByte);
        Assert.Equal("empty.png", finding.FileName);
    }

    [Fact]
    public void Scan_DetectsBrokenSupportedImages()
    {
        using var temp = TestDirectory.Create();
        File.WriteAllText(Path.Combine(temp.Path, "broken.png"), "not an image");

        var result = new FileHealthScanService().Scan([temp.Path]);

        var finding = Assert.Single(result.Findings, item => item.Kind == FileHealthFindingKind.BrokenImage);
        Assert.Equal("broken.png", finding.FileName);
    }

    [Fact]
    public void Scan_DetectsSuspiciousTemporaryFiles()
    {
        using var temp = TestDirectory.Create();
        File.WriteAllText(Path.Combine(temp.Path, "download.crdownload"), "partial");

        var result = new FileHealthScanService().Scan([temp.Path]);

        var finding = Assert.Single(result.Findings, item => item.Kind == FileHealthFindingKind.TemporaryFile);
        Assert.Equal("download.crdownload", finding.FileName);
    }

    [Fact]
    public void RenameToSuggestedExtension_UsesConflictSafeTarget()
    {
        using var temp = TestDirectory.Create();
        var wrong = Path.Combine(temp.Path, "wrong.jpg");
        var existing = Path.Combine(temp.Path, "wrong.png");
        WritePng(wrong);
        WritePng(existing, red: 0xAA);
        var service = new FileHealthScanService();
        var finding = Assert.Single(service.Scan([temp.Path]).Findings, item => item.Path == wrong);

        var result = service.RenameToSuggestedExtension(finding);

        Assert.Equal(FileHealthActionStatus.Completed, result.Status);
        Assert.False(File.Exists(wrong));
        Assert.Equal(Path.Combine(temp.Path, "wrong (2).png"), result.DestinationPath);
        Assert.True(File.Exists(result.DestinationPath));
    }

    [Fact]
    public void Quarantine_MovesFileAndWritesManifest()
    {
        using var temp = TestDirectory.Create();
        var quarantineRoot = Path.Combine(temp.Path, "quarantine");
        var broken = Path.Combine(temp.Path, "broken.png");
        File.WriteAllText(broken, "not an image");
        var service = new FileHealthScanService(() => quarantineRoot);
        var finding = Assert.Single(service.Scan([temp.Path]).Findings, item => item.Kind == FileHealthFindingKind.BrokenImage);

        var result = service.Quarantine(finding);

        Assert.Equal(FileHealthActionStatus.Completed, result.Status);
        Assert.False(File.Exists(broken));
        Assert.NotNull(result.DestinationPath);
        Assert.True(File.Exists(result.DestinationPath));
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(result.DestinationPath)!, "manifest.tsv")));
    }

    private static void WritePng(
        string path,
        byte alpha = 0xFF,
        byte red = 0xC9,
        byte green = 0xCB,
        byte blue = 0xFF)
    {
        const int width = 8;
        const int height = 8;
        const int stride = width * 4;
        var pixels = new byte[stride * height];

        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = blue;
            pixels[i + 1] = green;
            pixels[i + 2] = red;
            pixels[i + 3] = alpha;
        }

        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        bitmap.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
