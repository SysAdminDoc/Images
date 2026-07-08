using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class SmartCollectionServiceTests
{
    [Fact]
    public void Add_CreatesAndPersists()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "collections.json");
        var service = new SmartCollectionService(path);

        var added = service.Add("Landscape Photos", new SmartCollectionCriteria(
            Orientation: "landscape",
            MinRating: 3));

        Assert.True(added);
        Assert.Single(service.Collections);
        Assert.Equal("Landscape Photos", service.Collections[0].Name);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Add_RejectsDuplicateName()
    {
        using var temp = TestDirectory.Create();
        var service = new SmartCollectionService(Path.Combine(temp.Path, "c.json"));
        service.Add("Test", new SmartCollectionCriteria());
        Assert.False(service.Add("test", new SmartCollectionCriteria()));
    }

    [Fact]
    public void Remove_DeletesCollection()
    {
        using var temp = TestDirectory.Create();
        var service = new SmartCollectionService(Path.Combine(temp.Path, "c.json"));
        service.Add("Remove Me", new SmartCollectionCriteria());

        Assert.True(service.Remove("remove me"));
        Assert.Empty(service.Collections);
    }

    [Fact]
    public void Rename_UpdatesName()
    {
        using var temp = TestDirectory.Create();
        var service = new SmartCollectionService(Path.Combine(temp.Path, "c.json"));
        service.Add("Old", new SmartCollectionCriteria());

        Assert.True(service.Rename("Old", "New"));
        Assert.Equal("New", service.Collections[0].Name);
    }

    [Fact]
    public void MoveUp_ReordersCollections()
    {
        using var temp = TestDirectory.Create();
        var service = new SmartCollectionService(Path.Combine(temp.Path, "c.json"));
        service.Add("A", new SmartCollectionCriteria());
        service.Add("B", new SmartCollectionCriteria());

        service.MoveUp(1);

        Assert.Equal("B", service.Collections[0].Name);
        Assert.Equal("A", service.Collections[1].Name);
    }

    [Fact]
    public void Apply_FiltersByCriteria()
    {
        using var temp = TestDirectory.Create();
        var service = new SmartCollectionService(Path.Combine(temp.Path, "c.json"));
        service.Add("Rated", new SmartCollectionCriteria(MinRating: 4));

        var items = new[]
        {
            CreateItem("a.jpg", rating: 5),
            CreateItem("b.jpg", rating: 2),
            CreateItem("c.jpg", rating: 4),
        };

        var result = service.Apply("Rated", items);

        Assert.Equal(2, result.Count);
        Assert.All(result, item => Assert.True(item.Rating >= 4));
    }

    [Fact]
    public void Criteria_MatchesOrientation()
    {
        var criteria = new SmartCollectionCriteria(Orientation: "landscape");
        Assert.True(criteria.Matches(CreateItem("wide.jpg", orientation: "landscape")));
        Assert.False(criteria.Matches(CreateItem("tall.jpg", orientation: "portrait")));
    }

    [Fact]
    public void Criteria_MatchesTags()
    {
        var criteria = new SmartCollectionCriteria(RequiredTags: ["vacation"]);
        Assert.True(criteria.Matches(CreateItem("beach.jpg", tags: ["vacation", "beach"])));
        Assert.False(criteria.Matches(CreateItem("work.jpg", tags: ["office"])));
    }

    [Fact]
    public void Criteria_MatchesDuplicatesOnly()
    {
        var criteria = new SmartCollectionCriteria(DuplicatesOnly: true);
        Assert.True(criteria.Matches(CreateItem("dup.jpg", isDuplicate: true)));
        Assert.False(criteria.Matches(CreateItem("unique.jpg", isDuplicate: false)));
    }

    [Fact]
    public void Load_RestoresPersistedCollections()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "c.json");
        var service1 = new SmartCollectionService(path);
        service1.Add("Persisted", new SmartCollectionCriteria(MinRating: 3));

        var service2 = new SmartCollectionService(path);
        Assert.Single(service2.Collections);
        Assert.Equal("Persisted", service2.Collections[0].Name);
        Assert.Equal(3, service2.Collections[0].Criteria.MinRating);
    }

    [Fact]
    public void Add_UsesGuidTempFileAndLeavesFixedTempUntouched()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "c.json");
        var fixedTemp = path + ".tmp";
        File.WriteAllText(fixedTemp, "sentinel");
        var service = new SmartCollectionService(path);

        Assert.True(service.Add("Persisted", new SmartCollectionCriteria(MinRating: 3)));

        Assert.Equal("sentinel", File.ReadAllText(fixedTemp));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "c.json.*.tmp"));
    }

    [Fact]
    public void Criteria_SummaryDescribesFilters()
    {
        var criteria = new SmartCollectionCriteria(
            MinRating: 4,
            Orientation: "landscape",
            RequiredTags: ["vacation"]);
        var summary = criteria.ToSummary();

        Assert.Contains("rating", summary);
        Assert.Contains("landscape", summary);
        Assert.Contains("1 tag", summary);
    }

    private static AssetSmartFilterItem CreateItem(
        string fileName,
        int? rating = null,
        string orientation = "landscape",
        IReadOnlyList<string>? tags = null,
        bool isDuplicate = false)
    {
        return new AssetSmartFilterItem(
            Path: $@"C:\photos\{fileName}",
            FileName: fileName,
            Folder: @"C:\photos",
            Extension: Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant(),
            Format: "JPG",
            Length: 1024,
            ModifiedUtc: DateTimeOffset.UtcNow,
            Width: orientation == "landscape" ? 1920 : 1080,
            Height: orientation == "landscape" ? 1080 : 1920,
            Orientation: orientation,
            DimensionBucket: "HD",
            DateBucket: "This week",
            IsDuplicate: isDuplicate,
            Rating: rating,
            Tags: tags ?? [],
            Palette: "warm");
    }
}
