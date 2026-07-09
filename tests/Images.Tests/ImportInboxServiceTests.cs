using System.IO;
using System.Xml.Linq;
using Images.Services;

namespace Images.Tests;

public sealed class ImportInboxServiceTests
{
    [Fact]
    public void BuildInbox_DetectsInboxAndDestinationDuplicates()
    {
        using var temp = TestDirectory.Create();
        var source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(temp.Path, "library")).FullName;
        var first = WriteFile(source, "first.png", [1, 2, 3]);
        var second = WriteFile(source, "second.png", [1, 2, 3]);
        var unique = WriteFile(source, "unique.png", [9, 8, 7]);
        WriteFile(destination, "already.png", [1, 2, 3]);

        var result = new ImportInboxService().BuildInbox([source], destination);

        Assert.Equal(3, result.SourceCount);
        Assert.Equal(2, result.DuplicateCount);
        Assert.Equal(2, result.DestinationDuplicateCount);
        Assert.Contains(result.Items, item => item.Path == first && item.IsDuplicateInInbox && item.IsDuplicateInDestination);
        Assert.Contains(result.Items, item => item.Path == second && item.DuplicateCount == 2);
        Assert.Contains(result.Items, item => item.Path == unique && !item.IsDuplicate);
    }

    [Fact]
    public void BuildInbox_SkipsReparsePointDirectories()
    {
        using var temp = TestDirectory.Create();
        var source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        var linkedTarget = Directory.CreateDirectory(Path.Combine(temp.Path, "linked-target")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(temp.Path, "library")).FullName;
        var real = WriteFile(source, "real.png", [1, 2, 3]);
        WriteFile(linkedTarget, "linked.png", [4, 5, 6]);
        ReparsePointTestHelper.CreateDirectoryLinkOrSkip(Path.Combine(source, "linked"), linkedTarget);

        var result = new ImportInboxService().BuildInbox([source], destination);

        var item = Assert.Single(result.Items);
        Assert.Equal(real, item.Path);
        Assert.Equal(1, result.SourceCount);
    }

    [Fact]
    public void BuildInbox_SkipsDestinationReparsePointDirectories()
    {
        using var temp = TestDirectory.Create();
        var source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(temp.Path, "library")).FullName;
        var linkedTarget = Directory.CreateDirectory(Path.Combine(temp.Path, "linked-target")).FullName;
        var imported = WriteFile(source, "incoming.png", [4, 5, 6]);
        WriteFile(linkedTarget, "outside-duplicate.png", [4, 5, 6]);
        ReparsePointTestHelper.CreateDirectoryLinkOrSkip(Path.Combine(destination, "linked"), linkedTarget);

        var result = new ImportInboxService().BuildInbox([source], destination);

        var item = Assert.Single(result.Items);
        Assert.Equal(imported, item.Path);
        Assert.False(item.IsDuplicateInDestination);
        Assert.Equal(0, result.DestinationDuplicateCount);
    }

    [Fact]
    public void Commit_CopiesFileWritesExpandedTagsAndRatingSidecar()
    {
        using var temp = TestDirectory.Create();
        var source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(temp.Path, "library")).FullName;
        var sourcePath = WriteFile(source, "photo.png", [4, 5, 6]);
        var tagGraph = new TagGraphService(Path.Combine(temp.Path, "tag-graph.json"));
        Assert.True(tagGraph.AddAlias("ally", "person:Alice").Success);
        Assert.True(tagGraph.AddParent("person:alice", "people").Success);

        var result = new ImportInboxService(tagGraph).Commit(
            [new ImportInboxCommitRequest(sourcePath, destination, "ally", 4, StripGps: false, MoveOriginal: false)]);

        var move = Assert.Single(result.Imported);
        Assert.Empty(result.Failed);
        Assert.True(File.Exists(sourcePath));
        Assert.True(File.Exists(move.DestinationPath));

        var sidecar = XDocument.Load(move.DestinationPath + ".xmp");
        var tags = sidecar.Descendants()
            .Where(element => element.Name.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase))
            .Select(element => element.Value)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Assert.Equal(["people", "person:alice"], tags);
        Assert.Contains(
            sidecar.Descendants().Attributes(),
            attribute => attribute.Name.LocalName.Equals("Rating", StringComparison.OrdinalIgnoreCase) && attribute.Value == "4");
    }

    [Fact]
    public void Commit_MoveOriginalResolvesDestinationCollision()
    {
        using var temp = TestDirectory.Create();
        var source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(temp.Path, "library")).FullName;
        var sourcePath = WriteFile(source, "photo.png", [1, 1, 1]);
        WriteFile(destination, "photo.png", [2, 2, 2]);

        var result = new ImportInboxService().Commit(
            [new ImportInboxCommitRequest(sourcePath, destination, "", null, StripGps: false, MoveOriginal: true)]);

        var move = Assert.Single(result.Imported);
        Assert.Empty(result.Failed);
        Assert.False(File.Exists(sourcePath));
        Assert.EndsWith("photo (2).png", move.DestinationPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(move.DestinationPath));
    }

    [Fact]
    public void Commit_CorruptExistingRatingSidecarFailsOneImportAndContinuesBatch()
    {
        using var temp = TestDirectory.Create();
        var source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(temp.Path, "library")).FullName;
        var corruptSourcePath = WriteFile(source, "corrupt-sidecar.png", [1, 2, 3]);
        var goodSourcePath = WriteFile(source, "good.png", [4, 5, 6]);
        var corruptDestinationPath = Path.Combine(destination, "corrupt-sidecar.png");
        File.WriteAllText(corruptDestinationPath + ".xmp", "<x:xmpmeta><rdf:RDF>");

        var result = new ImportInboxService().Commit(
        [
            new ImportInboxCommitRequest(corruptSourcePath, destination, "", 5, StripGps: false, MoveOriginal: true),
            new ImportInboxCommitRequest(goodSourcePath, destination, "", 4, StripGps: false, MoveOriginal: true)
        ]);

        var move = Assert.Single(result.Imported);
        Assert.Equal(goodSourcePath, move.SourcePath);
        Assert.True(move.MovedOriginal);
        Assert.False(File.Exists(goodSourcePath));
        Assert.True(File.Exists(move.DestinationPath));

        var failure = Assert.Single(result.Failed);
        Assert.Equal(corruptSourcePath, failure.Path);
        Assert.Contains("undeclared prefix", failure.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(corruptSourcePath));
        Assert.False(File.Exists(corruptDestinationPath));
    }

    [Fact]
    public void Commit_TagExportFailureReportsFailureAndPreservesExistingDestinationSidecar()
    {
        using var temp = TestDirectory.Create();
        var source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(temp.Path, "library")).FullName;
        var sourcePath = WriteFile(source, "photo.png", [1, 2, 3]);
        var destinationPath = Path.Combine(destination, "photo.png");
        var sidecarPath = destinationPath + ".xmp";
        const string corruptSidecar = "<x:xmpmeta><rdf:RDF>";
        File.WriteAllText(sidecarPath, corruptSidecar);

        var result = new ImportInboxService().Commit(
            [new ImportInboxCommitRequest(sourcePath, destination, "person:Alice", null, StripGps: false, MoveOriginal: false)]);

        Assert.Empty(result.Imported);
        var failure = Assert.Single(result.Failed);
        Assert.Equal(sourcePath, failure.Path);
        Assert.Contains("Sidecar", failure.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(sourcePath));
        Assert.False(File.Exists(destinationPath));
        Assert.Equal(corruptSidecar, File.ReadAllText(sidecarPath));
    }

    [Fact]
    public void Commit_InPlaceFailureRollsBackWrittenTagSidecar()
    {
        using var temp = TestDirectory.Create();
        var source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        var sourcePath = WriteFile(source, "photo.gif", [1, 2, 3]);

        var result = new ImportInboxService().Commit(
            [new ImportInboxCommitRequest(sourcePath, source, "person:Alice", null, StripGps: true, MoveOriginal: false)]);

        Assert.Empty(result.Imported);
        var failure = Assert.Single(result.Failed);
        Assert.Equal(sourcePath, failure.Path);
        Assert.Contains("GPS metadata", failure.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(sourcePath));
        Assert.False(File.Exists(sourcePath + ".xmp"));
    }

    [Fact]
    public void Commit_CopyWithGpsStripFailureReportsFailureAndDeletesDestinationCopy()
    {
        using var temp = TestDirectory.Create();
        var source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(temp.Path, "library")).FullName;
        var sourcePath = WriteFile(source, "photo.jpg", [1, 2, 3]);

        var result = new ImportInboxService().Commit(
            [new ImportInboxCommitRequest(sourcePath, destination, "", null, StripGps: true, MoveOriginal: false)]);

        Assert.Empty(result.Imported);
        var failure = Assert.Single(result.Failed);
        Assert.Equal(sourcePath, failure.Path);
        Assert.Contains("GPS metadata", failure.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(sourcePath));
        Assert.False(File.Exists(Path.Combine(destination, "photo.jpg")));
    }

    [Fact]
    public void Commit_MoveWithGpsStripFailureReportsFailureAndRestoresOriginal()
    {
        using var temp = TestDirectory.Create();
        var source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(temp.Path, "library")).FullName;
        var sourcePath = WriteFile(source, "photo.jpg", [1, 2, 3]);

        var result = new ImportInboxService().Commit(
            [new ImportInboxCommitRequest(sourcePath, destination, "", null, StripGps: true, MoveOriginal: true)]);

        Assert.Empty(result.Imported);
        var failure = Assert.Single(result.Failed);
        Assert.Equal(sourcePath, failure.Path);
        Assert.Contains("GPS metadata", failure.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(sourcePath));
        Assert.False(File.Exists(Path.Combine(destination, "photo.jpg")));
    }

    [Fact]
    public void Commit_CopyWithGpsStripPreviewReadFailureReportsFailureAndDeletesDestinationCopy()
    {
        using var temp = TestDirectory.Create();
        var source = Directory.CreateDirectory(Path.Combine(temp.Path, "source")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(temp.Path, "library")).FullName;
        var sourcePath = WriteFile(source, "photo.gif", [1, 2, 3]);

        var result = new ImportInboxService().Commit(
            [new ImportInboxCommitRequest(sourcePath, destination, "", null, StripGps: true, MoveOriginal: false)]);

        Assert.Empty(result.Imported);
        var failure = Assert.Single(result.Failed);
        Assert.Equal(sourcePath, failure.Path);
        Assert.Contains("GPS metadata", failure.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(sourcePath));
        Assert.False(File.Exists(Path.Combine(destination, "photo.gif")));
    }

    private static string WriteFile(string folder, string name, byte[] bytes)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
