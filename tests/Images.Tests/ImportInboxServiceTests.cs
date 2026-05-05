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

    private static string WriteFile(string folder, string name, byte[] bytes)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
