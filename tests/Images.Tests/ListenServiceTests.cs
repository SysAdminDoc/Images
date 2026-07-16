using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class ListenServiceTests
{
    [Fact]
    public void CreateLoopbackListener_BindsExclusively()
    {
        var listener = ListenService.CreateLoopbackListener(0);
        try
        {
            Assert.True(listener.Server.ExclusiveAddressUse);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Theory]
    [InlineData("s3cr3t-token", "s3cr3t-token", true)]
    [InlineData("s3cr3t-token", "s3cr3t-toke", false)]   // prefix, wrong length
    [InlineData("s3cr3t-token", "S3CR3T-TOKEN", false)]  // case differs
    [InlineData("s3cr3t-token", "", false)]
    [InlineData("s3cr3t-token", "totally-different-and-longer", false)]
    public void FixedTimeTokenEquals_MatchesOnlyTheExactToken(string expected, string candidate, bool matches)
        => Assert.Equal(matches, ListenService.FixedTimeTokenEquals(expected, candidate));

    [Fact]
    public void FixedTimeTokenEquals_NullCandidateDoesNotMatch()
        => Assert.False(ListenService.FixedTimeTokenEquals("token", null));

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
