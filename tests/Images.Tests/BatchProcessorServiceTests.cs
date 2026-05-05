using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class BatchProcessorServiceTests
{
    [Fact]
    public void BuildPreview_ReportsOutputPathAndResizeDimensions()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImage(temp.Path, "source.png", 16, 8);
        var output = Directory.CreateDirectory(Path.Combine(temp.Path, "out")).FullName;

        var result = new BatchProcessorService().BuildPreview(
            [source],
            new BatchProcessorPreset("Web", ".jpg", 80, 8, 8),
            output);

        var item = Assert.Single(result.Items);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal("8 x 4", item.OutputDimensions);
        Assert.EndsWith("source.jpg", item.OutputPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Resize to 8 x 4", item.StatusText);
    }

    [Fact]
    public void Run_DryRun_DoesNotWriteOutput()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImage(temp.Path, "source.png", 4, 4);
        var output = Directory.CreateDirectory(Path.Combine(temp.Path, "out")).FullName;

        var result = new BatchProcessorService().Run(
            [source],
            new BatchProcessorPreset("PNG", ".png", 92, 0, 0),
            output,
            dryRun: true);

        Assert.Equal(1, result.SuccessCount);
        Assert.Empty(Directory.EnumerateFiles(output));
    }

    [Fact]
    public void PresetJson_NormalizesUnsupportedExtension()
    {
        var preset = BatchProcessorService.ParsePreset(
            """
            {
              "name": "Bad",
              "extension": ".not-real",
              "quality": 200,
              "maxWidth": -10,
              "maxHeight": 1200
            }
            """);

        Assert.Equal(".png", preset.Extension);
        Assert.Equal(100, preset.Quality);
        Assert.Equal(0, preset.MaxWidth);
        Assert.Equal(1200, preset.MaxHeight);
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
