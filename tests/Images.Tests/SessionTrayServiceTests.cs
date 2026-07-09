using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class SessionTrayServiceTests
{
    [Fact]
    public void Add_NormalizesAndDeduplicates()
    {
        using var temp = TestDirectory.Create();
        var img = temp.WriteFile("test.jpg");
        var service = new SessionTrayService();

        service.Add(img);
        service.Add(img);

        Assert.Single(service.Entries);
    }

    [Fact]
    public void Remove_RemovesByPath()
    {
        using var temp = TestDirectory.Create();
        var img = temp.WriteFile("test.jpg");
        var service = new SessionTrayService();
        service.Add(img);

        Assert.True(service.Remove(img));
        Assert.Empty(service.Entries);
    }

    [Fact]
    public void MoveUp_SwapsWithPreviousEntry()
    {
        using var temp = TestDirectory.Create();
        var a = temp.WriteFile("a.jpg");
        var b = temp.WriteFile("b.jpg");
        var service = new SessionTrayService();
        service.Add(a);
        service.Add(b);

        service.MoveUp(1);

        Assert.Equal(b, service.Entries[0]);
        Assert.Equal(a, service.Entries[1]);
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        using var temp = TestDirectory.Create();
        var img1 = temp.WriteFile("one.jpg");
        var img2 = temp.WriteFile("two.jpg");
        var listPath = Path.Combine(temp.Path, "session.txt");

        var service1 = new SessionTrayService();
        service1.Add(img1);
        service1.Add(img2);
        service1.SaveToFile(listPath);

        var service2 = new SessionTrayService();
        var result = service2.LoadFromFile(listPath);

        Assert.True(result.Success);
        Assert.Equal(2, result.EntriesLoaded);
        Assert.Equal(2, service2.Count);
    }

    [Fact]
    public void SaveToFile_DoesNotClobberSiblingTempFile()
    {
        using var temp = TestDirectory.Create();
        var img = temp.WriteFile("one.jpg");
        var listPath = Path.Combine(temp.Path, "session.txt");
        var siblingTempPath = listPath + ".tmp";
        File.WriteAllText(siblingTempPath, "user data");

        var service = new SessionTrayService();
        service.Add(img);
        service.SaveToFile(listPath);

        Assert.Equal("user data", File.ReadAllText(siblingTempPath));
        Assert.Contains(img, File.ReadAllLines(listPath));
    }

    [Fact]
    public void LoadFromFile_ToleratesMissingFiles()
    {
        using var temp = TestDirectory.Create();
        var existing = temp.WriteFile("exists.jpg");
        var listPath = Path.Combine(temp.Path, "session.txt");
        File.WriteAllLines(listPath, [existing, Path.Combine(temp.Path, "gone.jpg")]);

        var service = new SessionTrayService();
        var result = service.LoadFromFile(listPath);

        Assert.True(result.Success);
        Assert.Equal(2, result.EntriesLoaded);
        Assert.Equal(1, result.MissingOnDisk);
        Assert.Equal(2, service.Count);
    }

    [Fact]
    public void LoadFromFile_CountsOnlyNewEntries()
    {
        using var temp = TestDirectory.Create();
        var existing = temp.WriteFile("exists.jpg");
        var listPath = Path.Combine(temp.Path, "session.txt");
        File.WriteAllLines(listPath, [existing, existing, existing.ToUpperInvariant()]);

        var service = new SessionTrayService();
        var result = service.LoadFromFile(listPath);

        Assert.True(result.Success);
        Assert.Equal(1, result.EntriesLoaded);
        Assert.Equal(1, service.Count);
        Assert.Equal("Loaded 1 entries (0 missing on disk).", result.Message);
    }

    [Fact]
    public void GetValidEntries_FiltersNonExistent()
    {
        using var temp = TestDirectory.Create();
        var existing = temp.WriteFile("exists.jpg");
        var service = new SessionTrayService();
        service.Add(existing);
        service.Add(Path.Combine(temp.Path, "missing.jpg"));

        var valid = service.GetValidEntries();

        Assert.Single(valid);
        Assert.Equal(existing, valid[0]);
    }

    [Fact]
    public void LoadFromFile_SkipsCommentsAndBlankLines()
    {
        using var temp = TestDirectory.Create();
        var img = temp.WriteFile("photo.jpg");
        var listPath = Path.Combine(temp.Path, "session.txt");
        File.WriteAllLines(listPath, ["# My session", "", img, "  "]);

        var service = new SessionTrayService();
        var result = service.LoadFromFile(listPath);

        Assert.Equal(1, result.EntriesLoaded);
        Assert.Single(service.Entries);
    }
}
