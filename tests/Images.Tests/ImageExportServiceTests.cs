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

    [Theory]
    [InlineData(".pdf")]
    [InlineData(".pdfa")]
    [InlineData(".eps")]
    [InlineData(".svg")]
    public void ResolveWritablePath_WhenPolicyBlocksDelegateWrite_FallsBackToPng(string extension)
    {
        using var temp = TestDirectory.Create();
        var requested = Path.Combine(temp.Path, "export" + extension);

        var resolved = ImageExportService.ResolveWritablePath(requested);

        Assert.Equal(Path.Combine(temp.Path, "export.png"), resolved);
        Assert.Null(ImageExportService.TryResolveFormat(extension));
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
    public void Save_WithQuality_ClampsAndWritesCopy()
    {
        using var temp = TestDirectory.Create();
        var target = Path.Combine(temp.Path, "export.jpg");
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

        var savedPath = ImageExportService.Save(bitmap, target, 250);

        Assert.Equal(Path.GetFullPath(target), savedPath);
        Assert.True(new FileInfo(target).Length > 0);
    }

    [Fact]
    public void Save_WithMaxDimensions_WritesResizedCopy()
    {
        using var temp = TestDirectory.Create();
        var target = Path.Combine(temp.Path, "export.png");
        var bitmap = BitmapSource.Create(
            16,
            8,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            Enumerable.Repeat((byte)0x80, 16 * 8 * 4).ToArray(),
            16 * 4);
        bitmap.Freeze();

        ImageExportService.Save(bitmap, target, 92, maxWidth: 8, maxHeight: 8);

        Assert.Equal((8, 4), ReadImageSize(target));
    }

    [Fact]
    public void SaveWithC2paHandoff_WritesFileAndReportsExpectedNoManifestOutcome()
    {
        using var temp = TestDirectory.Create();
        var target = Path.Combine(temp.Path, "export.png");
        var handoff = C2paExportHandoff.Omitted(
            C2paExportReason.SourceHasNoManifest,
            "C2PA not written",
            "No source Content Credentials were found.");
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

        var result = ImageExportService.SaveWithC2paHandoff(
            bitmap,
            sourcePath: null,
            path: target,
            quality: 92,
            maxWidth: 0,
            maxHeight: 0,
            planC2paExport: (_, extension) =>
            {
                Assert.Equal(".png", extension);
                return handoff;
            });
        var inspection = C2paManifestService.Read(
            result.OutputPath,
            executableOverride: @"C:\Tools\c2patool.exe",
            processRunner: psi =>
            {
                return (1, "", "no manifest found");
            });

        Assert.Equal(Path.GetFullPath(target), result.OutputPath);
        Assert.Equal(handoff, result.C2paHandoff);
        Assert.True(File.Exists(result.OutputPath));
        Assert.True(
            inspection.Status == C2paStatus.NoManifest,
            $"Expected no manifest, got {inspection.Status}: {inspection.ErrorMessage}");
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

    [Fact]
    public void Overwrite_WithRotateOperation_ReplacesSourcePixelsAndTouchesFile()
    {
        using var temp = TestDirectory.Create();
        var source = Path.Combine(temp.Path, "source.png");
        var bitmap = BitmapSource.Create(
            2,
            3,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[]
            {
                0x10, 0x20, 0x30, 0xFF, 0x40, 0x50, 0x60, 0xFF,
                0x70, 0x80, 0x90, 0xFF, 0xA0, 0xB0, 0xC0, 0xFF,
                0xD0, 0xE0, 0xF0, 0xFF, 0x11, 0x22, 0x33, 0xFF
            },
            8);
        bitmap.Freeze();
        ImageExportService.Save(bitmap, source);
        var previousWriteTime = DateTime.UtcNow - TimeSpan.FromMinutes(10);
        File.SetLastWriteTimeUtc(source, previousWriteTime);
        var operations = new[]
        {
            new EditOperation(
                "rotate-test",
                "rotate",
                DateTimeOffset.UtcNow,
                Enabled: true,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["degrees"] = "90"
                },
                "Rotate test")
        };

        var savedPath = ImageExportService.Overwrite(source, operations);

        Assert.Equal(Path.GetFullPath(source), savedPath);
        Assert.Equal((3, 2), ReadImageSize(source));
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

    [Fact]
    public void SaveCopyWithC2paHandoff_PreservesEmbeddedExifMetadataForRasterSource()
    {
        using var temp = TestDirectory.Create();
        var source = Path.Combine(temp.Path, "photo.jpg");

        var exif = new ImageMagick.ExifProfile();
        exif.SetValue(ImageMagick.ExifTag.Make, "Images-Test");
        using (var original = new ImageMagick.MagickImage(ImageMagick.MagickColors.Red, 8, 8))
        {
            original.Format = ImageMagick.MagickFormat.Jpeg;
            original.SetProfile(exif);
            original.Write(source);
        }

        var target = Path.Combine(temp.Path, "photo_copy.jpg");
        // A throwaway 1x1 pixel source — the metadata-preserving path must ignore it for raster files.
        var fallback = BitmapSource.Create(
            1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0x00, 0x00, 0x00, 0xFF }, 4);
        fallback.Freeze();

        var result = ImageExportService.SaveCopyWithC2paHandoff(
            source,
            fallback,
            target,
            planC2paExport: (_, _) => C2paExportHandoff.Omitted(
                C2paExportReason.SourceHasNoManifest, "C2PA not written", "n/a"));

        using var copy = new ImageMagick.MagickImage(result.OutputPath);
        var copiedExif = copy.GetExifProfile();
        Assert.NotNull(copiedExif);
        Assert.Equal("Images-Test", copiedExif!.GetValue(ImageMagick.ExifTag.Make)?.Value);
        // Original 8x8 pixels prove the file path was used, not the 1x1 fallback.
        Assert.Equal(8u, copy.Width);
        Assert.Equal(8u, copy.Height);
    }

    [Fact]
    public void SaveCopyWithC2paHandoff_WhenNoSourceFile_FallsBackToPixelSource()
    {
        using var temp = TestDirectory.Create();
        var target = Path.Combine(temp.Path, "clip.png");
        var fallback = BitmapSource.Create(
            2, 2, 96, 96, PixelFormats.Bgra32, null,
            new byte[]
            {
                0xFF, 0x00, 0x00, 0xFF, 0x00, 0xFF, 0x00, 0xFF,
                0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
            },
            8);
        fallback.Freeze();

        var result = ImageExportService.SaveCopyWithC2paHandoff(
            sourcePath: null,
            fallback,
            target,
            planC2paExport: (_, _) => C2paExportHandoff.Omitted(
                C2paExportReason.SourceHasNoManifest, "C2PA not written", "n/a"));

        Assert.True(File.Exists(result.OutputPath));
        using var copy = new ImageMagick.MagickImage(result.OutputPath);
        Assert.Equal(2u, copy.Width);
    }

    private static (int Width, int Height) ReadImageSize(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        return (frame.PixelWidth, frame.PixelHeight);
    }
}
