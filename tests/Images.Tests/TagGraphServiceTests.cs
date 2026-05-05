using System.IO;
using System.Xml.Linq;
using Images.Services;

namespace Images.Tests;

public sealed class TagGraphServiceTests
{
    [Fact]
    public void AddAliasAndParent_ResolvesCanonicalTagsAndTransitiveParents()
    {
        using var temp = TestDirectory.Create();
        var service = new TagGraphService(Path.Combine(temp.Path, "tag-graph.json"));

        Assert.True(service.AddAlias("NYC", "place:New York").Success);
        Assert.True(service.AddParent("place:new-york", "place:travel").Success);
        Assert.True(service.AddParent("place:travel", "archive").Success);

        var expansion = service.Expand("nyc");

        Assert.Equal("place:new-york", expansion.Canonical);
        Assert.Equal(["archive", "place:travel"], expansion.Parents);
        Assert.Equal(["place:new-york", "archive", "place:travel"], expansion.AllTags);
    }

    [Fact]
    public void AddAliasAndParent_RejectsCycles()
    {
        using var temp = TestDirectory.Create();
        var service = new TagGraphService(Path.Combine(temp.Path, "tag-graph.json"));

        Assert.True(service.AddAlias("person:ally", "person:alice").Success);
        var aliasCycle = service.AddAlias("person:alice", "person:ally");

        Assert.False(aliasCycle.Success);
        Assert.Contains("same tag", aliasCycle.Message, StringComparison.OrdinalIgnoreCase);

        Assert.True(service.AddParent("person:alice", "people").Success);
        var parentCycle = service.AddParent("people", "person:alice");

        Assert.False(parentCycle.Success);
        Assert.Contains("cycle", parentCycle.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportSidecarTags_ResolvesAliasesIncludesParentsAndImportsBack()
    {
        using var temp = TestDirectory.Create();
        var imagePath = Path.Combine(temp.Path, "sample.png");
        File.WriteAllBytes(imagePath, [1, 2, 3]);
        var service = new TagGraphService(Path.Combine(temp.Path, "tag-graph.json"));
        Assert.True(service.AddAlias("ally", "person:Alice Smith").Success);
        Assert.True(service.AddParent("person:alice-smith", "people").Success);

        var export = service.ExportSidecarTags(imagePath, ["ally", "project:Roadmap"], includeParents: true);

        Assert.True(export.Success);
        Assert.Equal(imagePath + ".xmp", export.SidecarPath);
        Assert.Equal(3, export.TagCount);

        var document = XDocument.Load(export.SidecarPath);
        var exportedTags = document.Descendants()
            .Where(element => element.Name.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase))
            .Select(element => element.Value)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Assert.Equal(["people", "person:alice-smith", "project:roadmap"], exportedTags);

        var import = service.ImportSidecarTags(imagePath);

        Assert.True(import.Success);
        Assert.Equal(exportedTags, import.Tags);
    }

    [Fact]
    public void Service_LoadsDefaultsAndQuarantinesCorruptStore()
    {
        using var temp = TestDirectory.Create();
        var storePath = Path.Combine(temp.Path, "tag-graph.json");
        File.WriteAllText(storePath, "{not json");

        var service = new TagGraphService(storePath);
        var snapshot = service.Snapshot;

        Assert.Contains(snapshot.Namespaces, item => item.Prefix == "person");
        Assert.Contains(snapshot.Namespaces, item => item.Prefix == "place");
        Assert.Contains(snapshot.Namespaces, item => item.Prefix == "project");
        Assert.True(Directory.EnumerateFiles(temp.Path, "tag-graph.json.corrupt-*").Any());
    }
}
