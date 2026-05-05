using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class LaunchModeTests
{
    [Fact]
    public void TryResolvePeekArgs_AcceptsExactTwoTokenPeekLaunch()
    {
        using var temp = TestDirectory.Create();
        var image = temp.WriteFile("photo.jpg");

        var resolved = Images.App.TryResolvePeekArgs(["--peek", image], out var path);

        Assert.True(resolved);
        Assert.Equal(Path.GetFullPath(image), path);
    }

    [Theory]
    [InlineData()]
    [InlineData("--peek")]
    [InlineData("--peek", "missing.jpg")]
    [InlineData("--peek", "missing.jpg", "--extra")]
    [InlineData("--PEEK", @"\\?\C:\temp\photo.jpg")]
    public void TryResolvePeekArgs_RejectsAmbiguousOrUnsafeLaunches(params string[] args)
    {
        var resolved = Images.App.TryResolvePeekArgs(args, out var path);

        Assert.False(resolved);
        Assert.Equal(string.Empty, path);
    }

    [Fact]
    public void LaunchTimingSnapshot_ClampsNegativeProcessElapsed()
    {
        var snapshot = LaunchTiming.CreateSnapshot(
            "test",
            processStartedAt: new DateTimeOffset(2026, 5, 5, 12, 0, 1, TimeSpan.Zero),
            appElapsed: TimeSpan.FromMilliseconds(42),
            now: new DateTimeOffset(2026, 5, 5, 12, 0, 0, TimeSpan.Zero),
            detail: "  ");

        Assert.Equal("test", snapshot.Milestone);
        Assert.Equal(0, snapshot.ProcessElapsedMs);
        Assert.Equal(42, snapshot.AppElapsedMs);
        Assert.Null(snapshot.Detail);
    }
}
