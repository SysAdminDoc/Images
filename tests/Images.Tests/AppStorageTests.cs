using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class AppStorageTests
{
    [Fact]
    public void TryGetAppDirectoryForRoots_WhenFirstRootFails_FallsBackToNextRoot()
    {
        using var temp = TestDirectory.Create();

        var path = AppStorage.TryGetAppDirectoryForRoots(["\0invalid", temp.Path], "thumbs");

        Assert.Equal(Path.Combine(temp.Path, "Images", "thumbs"), path);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void TryGetAppDirectoryForRoots_WhenAllRootsFail_ReturnsNull()
    {
        var path = AppStorage.TryGetAppDirectoryForRoots(["\0invalid"], "thumbs");

        Assert.Null(path);
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("thumbs\\nested")]
    [InlineData("thumbs/nested")]
    public void TryGetAppDirectoryForRoots_WhenSegmentCanEscapeImagesRoot_ReturnsNull(string segment)
    {
        using var temp = TestDirectory.Create();

        var path = AppStorage.TryGetAppDirectoryForRoots([temp.Path], segment);

        Assert.Null(path);
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "Images")));
    }
}
