using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class MacroActionServiceTests
{
    [Fact]
    public void SerializeAndParse_RoundTripsInspectablePlan()
    {
        var plan = new MacroActionPlan(
            "Test macro",
            [
                new MacroActionStep("strip-gps", new Dictionary<string, string>()),
                new MacroActionStep(
                    "export-copy",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["extension"] = ".webp",
                        ["quality"] = "80"
                    })
            ]);

        var parsed = MacroActionService.Parse(MacroActionService.Serialize(plan));

        Assert.Equal("Test macro", parsed.Name);
        Assert.Equal(2, parsed.Actions.Count);
        Assert.Equal("export-copy", parsed.Actions[1].Kind);
        Assert.Equal("80", parsed.Actions[1].Parameters["quality"]);
    }

    [Fact]
    public void Run_ExportCopyResizeAndRenamePattern_ProducesCollisionSafeOutput()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImage(temp.Path, "source.png", 16, 8);
        var output = Directory.CreateDirectory(Path.Combine(temp.Path, "out")).FullName;
        var plan = new MacroActionPlan(
            "Export and rename",
            [
                new MacroActionStep(
                    "export-copy",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["extension"] = ".jpg",
                        ["quality"] = "85",
                        ["maxWidth"] = "8"
                    }),
                new MacroActionStep(
                    "rename-pattern",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["pattern"] = "batch-{index}-{name}"
                    })
            ]);

        var result = new MacroActionService().Run(
            plan,
            [source],
            new MacroRunOptions(output, DryRun: false));

        var item = Assert.Single(result.Items);
        Assert.True(item.Success);
        Assert.True(File.Exists(source));
        Assert.True(File.Exists(item.FinalPath));
        Assert.EndsWith("batch-001-source.jpg", item.FinalPath, StringComparison.OrdinalIgnoreCase);

        using var exported = new MagickImage(item.FinalPath);
        Assert.Equal((uint)8, exported.Width);
        Assert.Equal((uint)4, exported.Height);
    }

    [Fact]
    public void Run_DryRun_DoesNotWriteOutput()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImage(temp.Path, "source.png", 4, 4);
        var output = Directory.CreateDirectory(Path.Combine(temp.Path, "out")).FullName;

        var result = new MacroActionService().Run(
            MacroActionPlan.Default,
            [source],
            new MacroRunOptions(output, DryRun: true));

        var item = Assert.Single(result.Items);
        Assert.True(item.Success);
        Assert.Contains(item.Messages, message => message.Contains("Would export", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(Directory.EnumerateFiles(output));
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
