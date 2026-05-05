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
}
