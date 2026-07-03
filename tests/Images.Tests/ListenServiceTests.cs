using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class ListenServiceTests
{
    [Fact]
    public void TryNormalizeIncomingPath_AcceptsExistingLocalFile()
    {
        using var temp = TestDirectory.Create();
        var image = temp.WriteFile("photo.png");

        var accepted = ListenService.TryNormalizeIncomingPath($"  {image}  ", out var path);

        Assert.True(accepted);
        Assert.Equal(Path.GetFullPath(image), path);
    }

    [Theory]
    [InlineData("relative\\photo.png")]
    [InlineData(@"\\server\share\photo.png")]
    [InlineData("//server/share/photo.png")]
    [InlineData(@"\\?\UNC\server\share\photo.png")]
    [InlineData(@"//server\share/photo.png")]
    public void TryNormalizeIncomingPath_RejectsUnsafeOrAmbiguousPaths(string input)
    {
        var accepted = ListenService.TryNormalizeIncomingPath(input, out _);

        Assert.False(accepted);
    }
}
