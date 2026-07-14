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
        Assert.False(string.IsNullOrWhiteSpace(item.EstimatedOutputSizeText));
        Assert.False(string.IsNullOrWhiteSpace(item.DeltaText));
        Assert.Contains("lossy", item.WarningsText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Resize max 8 x 8", item.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Export JPG", item.StatusText, StringComparison.OrdinalIgnoreCase);
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
    public void Run_WithOperationChain_AppliesOrderedStepsAndRenamePattern()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImage(temp.Path, "source.png", 16, 8);
        var output = Directory.CreateDirectory(Path.Combine(temp.Path, "out")).FullName;
        var preset = new BatchProcessorPreset(
            "Chain",
            ".png",
            92,
            0,
            0,
            [
                new BatchOperationStep(
                    BatchOperationKinds.Rotate,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["degrees"] = "90"
                    }),
                new BatchOperationStep(
                    BatchOperationKinds.Resize,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["maxWidth"] = "8",
                        ["maxHeight"] = "8"
                    }),
                new BatchOperationStep(
                    BatchOperationKinds.RenamePattern,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["pattern"] = "processed-{index}-{name}"
                    }),
                new BatchOperationStep(
                    BatchOperationKinds.ExportCopy,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["extension"] = ".png",
                        ["quality"] = "92"
                    })
            ]);

        var result = new BatchProcessorService().Run(
            [source],
            preset,
            output,
            dryRun: false);

        var item = Assert.Single(result.Items);
        Assert.True(item.Success);
        Assert.EndsWith("processed-001-source.png", item.FinalPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(item.FinalPath));
        using var image = new MagickImage(item.FinalPath);
        Assert.Equal(4u, image.Width);
        Assert.Equal(8u, image.Height);
    }

    [Fact]
    public void BuildPreview_WithRenamePattern_SanitizesInvalidOutputCharacters()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImage(temp.Path, "source.png", 4, 4);
        var output = Directory.CreateDirectory(Path.Combine(temp.Path, "out")).FullName;
        var preset = new BatchProcessorPreset(
            "Rename",
            ".png",
            92,
            0,
            0,
            [
                new BatchOperationStep(
                    BatchOperationKinds.RenamePattern,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["pattern"] = "bad:name:{index}"
                    }),
                new BatchOperationStep(
                    BatchOperationKinds.ExportCopy,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["extension"] = ".png",
                        ["quality"] = "92"
                    })
            ]);

        var result = new BatchProcessorService().BuildPreview([source], preset, output);

        var item = Assert.Single(result.Items);
        Assert.EndsWith("bad_name_001.png", item.OutputPath, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void ParsePreset_WhenOperationCountExceedsLimit_RejectsPreset()
    {
        var operations = string.Join(',', Enumerable.Repeat("{\"kind\":\"rotate\",\"parameters\":{}}", BatchProcessorService.MaxImportedOperations + 1));
        var json = $"{{\"name\":\"Too many\",\"extension\":\".png\",\"quality\":92,\"maxWidth\":0,\"maxHeight\":0,\"operations\":[{operations}]}}";

        var error = Assert.Throws<System.Text.Json.JsonException>(() => BatchProcessorService.ParsePreset(json));

        Assert.Contains("operation limit", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ProcessesFilesInParallel()
    {
        using var temp = TestDirectory.Create();
        var output = Directory.CreateDirectory(Path.Combine(temp.Path, "out")).FullName;
        var sources = new List<string>();
        for (var i = 0; i < 8; i++)
            sources.Add(WriteImage(temp.Path, $"source_{i}.png", 4, 4));

        var progressUpdates = new List<BatchProgressUpdate>();
        var progress = new Progress<BatchProgressUpdate>(update => progressUpdates.Add(update));

        var result = await new BatchProcessorService().RunAsync(
            sources,
            new BatchProcessorPreset("PNG", ".png", 92, 0, 0),
            output,
            dryRun: false,
            maxConcurrency: 4,
            progress: progress);

        Assert.Equal(8, result.SuccessCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(8, result.Items.Count);
        Assert.Equal(8, Directory.EnumerateFiles(output).Count());
        foreach (var item in result.Items)
            Assert.True(item.Success);
    }

    [Fact]
    public async Task RunAsync_PreservesResultOrder()
    {
        using var temp = TestDirectory.Create();
        var output = Directory.CreateDirectory(Path.Combine(temp.Path, "out")).FullName;
        var sources = new List<string>();
        for (var i = 0; i < 4; i++)
            sources.Add(WriteImage(temp.Path, $"file_{i:D2}.png", 4, 4));

        var result = await new BatchProcessorService().RunAsync(
            sources,
            new BatchProcessorPreset("PNG", ".png", 92, 0, 0),
            output,
            dryRun: false,
            maxConcurrency: 2);

        Assert.Equal(4, result.Items.Count);
        for (var i = 0; i < result.Items.Count; i++)
            Assert.Contains($"file_{i:D2}", result.Items[i].SourcePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_DryRun_DoesNotWriteOutput()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImage(temp.Path, "source.png", 4, 4);
        var output = Directory.CreateDirectory(Path.Combine(temp.Path, "out")).FullName;

        var result = await new BatchProcessorService().RunAsync(
            [source],
            new BatchProcessorPreset("PNG", ".png", 92, 0, 0),
            output,
            dryRun: true,
            maxConcurrency: 2);

        Assert.Equal(1, result.SuccessCount);
        Assert.Empty(Directory.EnumerateFiles(output));
    }

    [Fact]
    public void CompactResults_DropsNullPartialResults()
    {
        var success = new MacroRunItemResult("source.png", "output.png", ["Wrote output.png."], null);
        var failure = new MacroRunItemResult("broken.png", "broken.png", [], "failed");
        MacroRunItemResult?[] partial = [null, success, null, failure];

        var result = BatchProcessorService.CompactResults(partial);

        Assert.Equal([success, failure], result);
    }

    [Fact]
    public void BuildPreview_CollidingOutputStems_ReportDistinctPaths()
    {
        // Two sources renamed to the same stem must preview distinct destinations — matching what a
        // real run reserves — instead of both reporting the identical output path.
        using var temp = TestDirectory.Create();
        var a = WriteImage(temp.Path, "a.png", 4, 4);
        var b = WriteImage(temp.Path, "b.png", 4, 4);
        var output = Directory.CreateDirectory(Path.Combine(temp.Path, "out")).FullName;
        var preset = new BatchProcessorPreset(
            "Dup",
            ".png",
            92,
            0,
            0,
            [
                new BatchOperationStep(
                    BatchOperationKinds.RenamePattern,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["pattern"] = "shared" }),
                new BatchOperationStep(
                    BatchOperationKinds.ExportCopy,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["extension"] = ".png", ["quality"] = "92" })
            ]);

        var result = new BatchProcessorService().BuildPreview([a, b], preset, output);

        Assert.Equal(2, result.Items.Count);
        var paths = result.Items.Select(i => i.OutputPath).ToList();
        Assert.Equal(paths.Count, paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
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
