using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class ExportCapabilityWarningServiceTests
{
    [Fact]
    public void EstimateFile_WarnsForAlphaLoss()
    {
        using var temp = TestDirectory.Create();
        var source = WriteTransparentPng(temp.Path, "transparent.png");

        var summary = new ExportPreviewService().EstimateFile(
            source,
            new ExportPreviewRequest(".jpg", 80, 0, 0));

        Assert.Contains(summary.Warnings, warning => warning.Contains("alpha", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(summary.Warnings, warning => warning.Contains("lossy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EstimateFile_WarnsForMetadataAndColorProfileLoss()
    {
        using var temp = TestDirectory.Create();
        var source = WriteProfiledJpeg(temp.Path, "profiled.jpg");

        var summary = new ExportPreviewService().EstimateFile(
            source,
            new ExportPreviewRequest(".bmp", 92, 0, 0));

        Assert.Contains(summary.Warnings, warning => warning.Contains("EXIF", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(summary.Warnings, warning => warning.Contains("ICC", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EstimateFile_WarnsWhenAnimatedSourceWouldFlattenToSingleFrame()
    {
        using var temp = TestDirectory.Create();
        var source = WriteAnimatedGif(temp.Path, "animated.gif");

        var summary = new ExportPreviewService().EstimateFile(
            source,
            new ExportPreviewRequest(".png", 92, 0, 0));

        Assert.Contains(summary.Warnings, warning => warning.Contains("Animation has 2 frames", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BatchPreview_WarnsWhenPagedSourceWouldFlattenToSinglePage()
    {
        using var temp = TestDirectory.Create();
        var source = WriteMultiPageTiff(temp.Path, "pages.tif");
        var output = Directory.CreateDirectory(Path.Combine(temp.Path, "out")).FullName;

        var result = new BatchProcessorService().BuildPreview(
            [source],
            new BatchProcessorPreset("Jpeg", ".jpg", 85, 0, 0),
            output);

        var item = Assert.Single(result.Items);
        Assert.Contains("pages/layers", item.WarningsText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BatchDryRun_IncludesCapabilityWarningsInRunMessages()
    {
        using var temp = TestDirectory.Create();
        var source = WriteAnimatedGif(temp.Path, "animated.gif");
        var output = Directory.CreateDirectory(Path.Combine(temp.Path, "out")).FullName;
        var plan = new MacroActionPlan(
            "Dry run warning",
            [
                new MacroActionStep(
                    "export-copy",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["extension"] = ".jpg",
                        ["quality"] = "80"
                    })
            ]);

        var result = new MacroActionService().Run(
            plan,
            [source],
            new MacroRunOptions(output, DryRun: true));

        var item = Assert.Single(result.Items);
        Assert.Contains(item.Messages, message => message.Contains("Animation has 2 frames", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(Directory.EnumerateFiles(output));
    }

    private static string WriteTransparentPng(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(MagickColors.Transparent, 8, 8)
        {
            Format = MagickFormat.Png
        };
        image.Write(path);
        return path;
    }

    private static string WriteProfiledJpeg(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(MagickColors.Red, 8, 8)
        {
            Format = MagickFormat.Jpeg
        };
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.ImageDescription, "Images capability warning test");
        image.SetProfile(exif);
        image.SetProfile(ColorProfiles.SRGB);
        image.Write(path);
        return path;
    }

    private static string WriteAnimatedGif(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        using var collection = new MagickImageCollection();
        var first = new MagickImage(MagickColors.Red, 6, 4)
        {
            Format = MagickFormat.Gif,
            AnimationDelay = 8,
            AnimationIterations = 0
        };
        var second = new MagickImage(MagickColors.Blue, 6, 4)
        {
            Format = MagickFormat.Gif,
            AnimationDelay = 8,
            AnimationIterations = 0
        };

        collection.Add(first);
        collection.Add(second);
        collection.Write(path);
        return path;
    }

    private static string WriteMultiPageTiff(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        using var collection = new MagickImageCollection();
        collection.Add(new MagickImage(MagickColors.Red, 6, 4) { Format = MagickFormat.Tiff });
        collection.Add(new MagickImage(MagickColors.Blue, 6, 4) { Format = MagickFormat.Tiff });
        collection.Write(path);
        return path;
    }
}
