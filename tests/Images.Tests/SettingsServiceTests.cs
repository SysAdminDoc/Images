using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public void TouchRecentFolderNormalizesAndDeduplicatesFolders()
    {
        using var temp = TestDirectory.Create();
        var service = new SettingsService(System.IO.Path.Combine(temp.Path, "settings.db"));
        var folder = System.IO.Path.Combine(temp.Path, "Photos");
        Directory.CreateDirectory(folder);

        service.TouchRecentFolder(folder);
        service.TouchRecentFolder(folder + System.IO.Path.DirectorySeparatorChar);

        var recent = service.GetRecentFolders();
        var expected = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(folder));
        Assert.Equal([expected], recent);
    }

    [Fact]
    public void TouchRecentFolderSkipsMissingFolders()
    {
        using var temp = TestDirectory.Create();
        var service = new SettingsService(System.IO.Path.Combine(temp.Path, "settings.db"));

        service.TouchRecentFolder(System.IO.Path.Combine(temp.Path, "missing"));

        Assert.Empty(service.GetRecentFolders());
    }

    [Fact]
    public void GetRecentFoldersReturnsEmptyForNonPositiveMax()
    {
        using var temp = TestDirectory.Create();
        var service = new SettingsService(System.IO.Path.Combine(temp.Path, "settings.db"));
        var folder = System.IO.Path.Combine(temp.Path, "Photos");
        Directory.CreateDirectory(folder);
        service.TouchRecentFolder(folder);

        Assert.Empty(service.GetRecentFolders(0));
        Assert.Empty(service.GetRecentFolders(-5));
    }

    [Fact]
    public void ConfirmRecycleBinDeleteSettingDefaultsOnAndPersistsOptOut()
    {
        using var temp = TestDirectory.Create();
        var service = new SettingsService(System.IO.Path.Combine(temp.Path, "settings.db"));

        Assert.True(service.GetBool(Keys.ConfirmRecycleBinDelete, true));

        service.SetBool(Keys.ConfirmRecycleBinDelete, false);

        Assert.False(service.GetBool(Keys.ConfirmRecycleBinDelete, true));
    }
}
