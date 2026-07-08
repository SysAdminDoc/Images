using Images.Services;

namespace Images.Tests;

public sealed class BackgroundTaskTrackerTests
{
    [Fact]
    public async Task Queue_WhenActionCompletes_EvictsIdleEntry()
    {
        var name = TestTaskName();

        await BackgroundTaskTracker.Queue(name, () => { });

        var snapshot = SnapshotFor(name);
        Assert.Equal(0, snapshot.Started);
        Assert.Equal(0, snapshot.Running);
    }

    [Fact]
    public async Task Queue_WhenActionThrows_EvictsIdleEntry()
    {
        var name = TestTaskName();

        await BackgroundTaskTracker.Queue(name, () =>
        {
            throw new InvalidOperationException("boom");
        });

        var snapshot = SnapshotFor(name);
        Assert.Equal(0, snapshot.Started);
    }

    [Fact]
    public async Task Queue_WhenTokenIsCanceled_EvictsIdleEntry()
    {
        var name = TestTaskName();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await BackgroundTaskTracker.Queue(
            name,
            () =>
            {
                throw new InvalidOperationException("Canceled work should not run.");
            },
            cts.Token);

        var snapshot = SnapshotFor(name);
        Assert.Equal(0, snapshot.Started);
    }

    [Fact]
    public async Task Run_WhenActionThrows_RecordsFaultAndRethrows()
    {
        var name = TestTaskName();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            BackgroundTaskTracker.Run<int>(name, () => throw new InvalidOperationException("boom")));

        var snapshot = SnapshotFor(name);
        Assert.Equal(0, snapshot.Started);
    }

    [Fact]
    public async Task RunAsync_WhenActionCompletes_ReturnsResult()
    {
        var name = TestTaskName();

        var result = await BackgroundTaskTracker.RunAsync(
            name,
            () => Task.FromResult(42));

        Assert.Equal(42, result);
        var snapshot = SnapshotFor(name);
        Assert.Equal(0, snapshot.Started);
    }

    [Fact]
    public async Task RunAsync_WhenActionThrows_RecordsFaultAndRethrows()
    {
        var name = TestTaskName();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            BackgroundTaskTracker.RunAsync<int>(
                name,
                () => Task.FromException<int>(new InvalidOperationException("boom"))));

        var snapshot = SnapshotFor(name);
        Assert.Equal(0, snapshot.Started);
    }

    [Fact]
    public async Task Queue_WhileActionIsRunning_ReportsRunningCount()
    {
        var name = TestTaskName();
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        var task = BackgroundTaskTracker.Queue(name, () =>
        {
            started.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(3)));
        });

        Assert.True(started.Wait(TimeSpan.FromSeconds(3)));
        var running = SnapshotFor(name);
        Assert.Equal(1, running.Started);
        Assert.Equal(1, running.Running);

        release.Set();
        await task.WaitAsync(TimeSpan.FromSeconds(3));

        var completed = SnapshotFor(name);
        Assert.Equal(0, completed.Running);
        Assert.Equal(0, completed.Started);
    }

    [Fact]
    public async Task Queue_WhenSameNameOverlaps_KeepsRemainingRunningSnapshot()
    {
        var name = TestTaskName();
        using var firstStarted = new ManualResetEventSlim();
        using var secondStarted = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        using var releaseSecond = new ManualResetEventSlim();

        var first = BackgroundTaskTracker.Queue(name, () =>
        {
            firstStarted.Set();
            Assert.True(releaseFirst.Wait(TimeSpan.FromSeconds(3)));
        });
        Assert.True(firstStarted.Wait(TimeSpan.FromSeconds(3)));

        var second = BackgroundTaskTracker.Queue(name, () =>
        {
            secondStarted.Set();
            Assert.True(releaseSecond.Wait(TimeSpan.FromSeconds(3)));
        });
        Assert.True(secondStarted.Wait(TimeSpan.FromSeconds(3)));

        releaseFirst.Set();
        await first.WaitAsync(TimeSpan.FromSeconds(3));

        var running = SnapshotFor(name);
        Assert.Equal(2, running.Started);
        Assert.Equal(1, running.Running);

        releaseSecond.Set();
        await second.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Queue_GlobalTotals_TrackAcrossAllTasks()
    {
        var before = BackgroundTaskTracker.Snapshot;

        await BackgroundTaskTracker.Queue(TestTaskName(), () => { });
        await BackgroundTaskTracker.Queue(TestTaskName(), () => { });

        var after = BackgroundTaskTracker.Snapshot;
        Assert.Equal(before.Started + 2, after.Started);
        Assert.Equal(before.Completed + 2, after.Completed);
        Assert.Equal(0, after.Running);
    }

    private static BackgroundTaskSnapshot SnapshotFor(string name)
        => BackgroundTaskTracker.SnapshotByName.TryGetValue(name, out var snapshot)
            ? snapshot
            : default;

    private static string TestTaskName()
        => $"test-background-task-{Guid.NewGuid():N}";
}
