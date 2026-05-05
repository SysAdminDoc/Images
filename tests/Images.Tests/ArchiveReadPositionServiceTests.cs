using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class ArchiveReadPositionServiceTests
{
    [Fact]
    public void BuildKey_NormalizesArchivePathCase()
    {
        var lower = ArchiveReadPositionService.BuildKey(@"C:\Photos\book.cbz");
        var upper = ArchiveReadPositionService.BuildKey(@"c:\photos\BOOK.CBZ");

        Assert.Equal(lower, upper);
        Assert.StartsWith("archive.read-position.", lower, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveLastPageIndex_ClampsPersistedValue()
    {
        using var temp = TestDirectory.Create();
        var archive = temp.WriteFile("book.cbz");
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));

        ArchiveReadPositionService.SaveLastPageIndex(settings, archive, pageIndex: 99, pageCount: 3);

        Assert.Equal(2, ArchiveReadPositionService.GetLastPageIndex(settings, archive));
    }

    [Fact]
    public void GetRecentArchives_ReturnsProgressOrderedByLastOpenAndSkipsMissingFiles()
    {
        using var temp = TestDirectory.Create();
        var first = temp.WriteFile("first.cbz");
        var second = temp.WriteFile("second.zip");
        var settings = new SettingsService(Path.Combine(temp.Path, "settings.db"));

        ArchiveReadPositionService.SaveLastPageIndex(settings, first, pageIndex: 1, pageCount: 3);
        ArchiveReadPositionService.SaveLastPageIndex(settings, second, pageIndex: 0, pageCount: 1);

        var recent = ArchiveReadPositionService.GetRecentArchives(settings);

        Assert.Collection(
            recent,
            item =>
            {
                Assert.Equal(Path.GetFullPath(second), item.Path);
                Assert.Equal("Page 1", item.ProgressText);
            },
            item =>
            {
                Assert.Equal(Path.GetFullPath(first), item.Path);
                Assert.Equal("Page 2 / 3", item.ProgressText);
            });

        File.Delete(second);

        var filtered = Assert.Single(ArchiveReadPositionService.GetRecentArchives(settings));
        Assert.Equal(Path.GetFullPath(first), filtered.Path);
    }
}
