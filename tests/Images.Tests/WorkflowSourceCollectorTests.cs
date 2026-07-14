using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class WorkflowSourceCollectorTests
{
    [Fact]
    public void Collect_DeduplicatesSupportedFilesAndReportsMissingSources()
    {
        using var temp = TestDirectory.Create();
        var image = temp.WriteFile("image.png", "payload");
        temp.WriteFile("notes.txt", "ignored");
        var missing = Path.Combine(temp.Path, "missing");

        var result = WorkflowSourceCollector.Collect([temp.Path, image, missing]);

        Assert.Equal([image], result.Files);
        Assert.Equal([missing], result.SkippedSources);
    }

    [Fact]
    public void Collect_SkipsNestedReparsePointDirectories()
    {
        using var temp = TestDirectory.Create();
        var selected = Directory.CreateDirectory(Path.Combine(temp.Path, "selected")).FullName;
        var linkedTarget = Directory.CreateDirectory(Path.Combine(temp.Path, "outside")).FullName;
        var local = Path.Combine(selected, "local.png");
        var linked = Path.Combine(linkedTarget, "linked.png");
        File.WriteAllText(local, "local");
        File.WriteAllText(linked, "linked");
        ReparsePointTestHelper.CreateDirectoryLinkOrSkip(Path.Combine(selected, "linked"), linkedTarget);

        var result = WorkflowSourceCollector.Collect([selected]);

        Assert.Equal([local], result.Files);
        Assert.DoesNotContain(linked, result.Files, StringComparer.OrdinalIgnoreCase);
    }
}
