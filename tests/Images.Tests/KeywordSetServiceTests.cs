using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class KeywordSetServiceTests
{
    [Fact]
    public void Add_CreatesSetAndPersists()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "sets.json");
        var service = new KeywordSetService(path);

        var added = service.Add("Vacation", ["beach", "sunset", "travel"]);

        Assert.True(added);
        Assert.Single(service.Sets);
        Assert.Equal("Vacation", service.Sets[0].Name);
        Assert.Equal(["beach", "sunset", "travel"], service.Sets[0].Keywords);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Add_RejectsDuplicateName()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "sets.json");
        var service = new KeywordSetService(path);

        service.Add("Vacation", ["beach"]);
        var duplicate = service.Add("vacation", ["mountains"]);

        Assert.False(duplicate);
        Assert.Single(service.Sets);
    }

    [Fact]
    public void Add_NormalizesAndDeduplicatesKeywords()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "sets.json");
        var service = new KeywordSetService(path);

        service.Add("Test", ["  Beach ", "beach", "Sunset", "", " "]);

        Assert.Equal(["Beach", "Sunset"], service.Sets[0].Keywords);
    }

    [Fact]
    public void Upsert_ReplacesExistingCategoryLayoutAndPersists()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "sets.json");
        var service = new KeywordSetService(path);
        service.Add("Editorial", ["draft", "needs-review"]);

        Assert.True(service.Upsert("editorial", ["approved", "publish"]));

        var reloaded = new KeywordSetService(path);
        var set = Assert.Single(reloaded.Sets);
        Assert.Equal("editorial", set.Name);
        Assert.Equal(["approved", "publish"], set.Keywords);
    }

    [Fact]
    public void Remove_DeletesSetAndPersists()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "sets.json");
        var service = new KeywordSetService(path);
        service.Add("ToRemove", ["tag"]);

        var removed = service.Remove("toremove");

        Assert.True(removed);
        Assert.Empty(service.Sets);
    }

    [Fact]
    public void Rename_UpdatesName()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "sets.json");
        var service = new KeywordSetService(path);
        service.Add("OldName", ["tag"]);

        var renamed = service.Rename("OldName", "NewName");

        Assert.True(renamed);
        Assert.Equal("NewName", service.Sets[0].Name);
    }

    [Fact]
    public void ExportImportJson_RoundTrips()
    {
        using var temp = TestDirectory.Create();
        var path1 = Path.Combine(temp.Path, "export.json");
        var service1 = new KeywordSetService(path1);
        service1.Add("Set A", ["alpha", "bravo"]);
        service1.Add("Set B", ["charlie"]);

        var json = service1.ExportJson();

        var path2 = Path.Combine(temp.Path, "import.json");
        var service2 = new KeywordSetService(path2);
        var imported = service2.ImportJson(json);

        Assert.Equal(2, imported);
        Assert.Equal(2, service2.Sets.Count);
        Assert.Equal("Set A", service2.Sets[0].Name);
        Assert.Equal("Set B", service2.Sets[1].Name);
    }

    [Fact]
    public void Load_RestoresPersistedSets()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "sets.json");
        var service1 = new KeywordSetService(path);
        service1.Add("Persisted", ["keyword"]);

        var service2 = new KeywordSetService(path);

        Assert.Single(service2.Sets);
        Assert.Equal("Persisted", service2.Sets[0].Name);
    }
}
