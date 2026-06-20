using Images.Services;

namespace Images.Tests;

public sealed class LaunchTimingTests
{
    [Fact]
    public void CreateSnapshot_RecordsMilestoneAndPositiveTiming()
    {
        var processStart = DateTimeOffset.UtcNow.AddSeconds(-2);
        var appElapsed = TimeSpan.FromMilliseconds(500);
        var now = DateTimeOffset.UtcNow;

        var snapshot = LaunchTiming.CreateSnapshot("test-milestone", processStart, appElapsed, now, "detail-text");

        Assert.Equal("test-milestone", snapshot.Milestone);
        Assert.True(snapshot.ProcessElapsedMs > 0);
        Assert.Equal(500, snapshot.AppElapsedMs, precision: 1);
        Assert.Equal("detail-text", snapshot.Detail);
    }

    [Fact]
    public void CreateSnapshot_NegativeElapsed_ClampedToZero()
    {
        var futureStart = DateTimeOffset.UtcNow.AddHours(1);
        var negativeElapsed = TimeSpan.FromMilliseconds(-100);

        var snapshot = LaunchTiming.CreateSnapshot("clamp-test", futureStart, negativeElapsed, DateTimeOffset.UtcNow, null);

        Assert.Equal(0, snapshot.ProcessElapsedMs);
        Assert.Equal(0, snapshot.AppElapsedMs);
    }

    [Fact]
    public void CreateSnapshot_NullDetail_SetsDetailNull()
    {
        var snapshot = LaunchTiming.CreateSnapshot("no-detail", DateTimeOffset.UtcNow, TimeSpan.Zero, DateTimeOffset.UtcNow, null);

        Assert.Null(snapshot.Detail);
    }

    [Fact]
    public void CreateSnapshot_WhitespaceDetail_SetsDetailNull()
    {
        var snapshot = LaunchTiming.CreateSnapshot("blank-detail", DateTimeOffset.UtcNow, TimeSpan.Zero, DateTimeOffset.UtcNow, "   ");

        Assert.Null(snapshot.Detail);
    }

    [Fact]
    public void Mark_ReturnsSnapshotWithPositiveValues()
    {
        var snapshot = LaunchTiming.Mark("integration-mark");

        Assert.Equal("integration-mark", snapshot.Milestone);
        Assert.True(snapshot.ProcessElapsedMs >= 0);
        Assert.True(snapshot.AppElapsedMs >= 0);
    }
}
