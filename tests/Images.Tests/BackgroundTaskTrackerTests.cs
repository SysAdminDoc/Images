using Images.Services;

namespace Images.Tests;

public sealed class BackgroundTaskTrackerTests
{
    [Fact]
    public async Task Queue_WhenActionCompletes_RecordsCompletion()
    {
        var name = TestTaskName();

        await BackgroundTaskTracker.Queue(name, () => { });

        var snapshot = SnapshotFor(name);
        Assert.Equal(1, snapshot.Started);
        Assert.Equal(0, snapshot.Running);
        Assert.Equal(1, snapshot.Completed);
        Assert.Equal(0, snapshot.Faulted);
        Assert.Equal(0, snapshot.Canceled);
    }

    [Fact]
    public async Task Queue_WhenActionThrows_RecordsFaultWithoutThrowing()
    {
        var name = TestTaskName();

        await BackgroundTaskTracker.Queue(name, () =>
        {
            throw new InvalidOperationException("boom");
        });

        var snapshot = SnapshotFor(name);
        Assert.Equal(1, snapshot.Started);
        Assert.Equal(0, snapshot.Running);
        Assert.Equal(0, snapshot.Completed);
        Assert.Equal(1, snapshot.Faulted);
        Assert.Equal(0, snapshot.Canceled);
    }

    [Fact]
    public async Task Queue_WhenTokenIsCanceled_RecordsCancellationWithoutRunningAction()
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
        Assert.Equal(1, snapshot.Started);
        Assert.Equal(0, snapshot.Running);
        Assert.Equal(0, snapshot.Completed);
        Assert.Equal(0, snapshot.Faulted);
        Assert.Equal(1, snapshot.Canceled);
    }

    [Fact]
    public async Task Run_WhenActionThrows_RecordsFaultAndRethrows()
    {
        var name = TestTaskName();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            BackgroundTaskTracker.Run<int>(name, () => throw new InvalidOperationException("boom")));

        var snapshot = SnapshotFor(name);
        Assert.Equal(1, snapshot.Started);
        Assert.Equal(0, snapshot.Running);
        Assert.Equal(0, snapshot.Completed);
        Assert.Equal(1, snapshot.Faulted);
        Assert.Equal(0, snapshot.Canceled);
    }

    [Fact]
    public async Task RunAsync_WhenActionCompletes_RecordsCompletionAndReturnsResult()
    {
        var name = TestTaskName();

        var result = await BackgroundTaskTracker.RunAsync(
            name,
            () => Task.FromResult(42));

        var snapshot = SnapshotFor(name);
        Assert.Equal(42, result);
        Assert.Equal(1, snapshot.Started);
        Assert.Equal(0, snapshot.Running);
        Assert.Equal(1, snapshot.Completed);
        Assert.Equal(0, snapshot.Faulted);
        Assert.Equal(0, snapshot.Canceled);
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
        Assert.Equal(1, snapshot.Started);
        Assert.Equal(0, snapshot.Running);
        Assert.Equal(0, snapshot.Completed);
        Assert.Equal(1, snapshot.Faulted);
        Assert.Equal(0, snapshot.Canceled);
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
        Assert.Equal(1, completed.Completed);
    }

    private static BackgroundTaskSnapshot SnapshotFor(string name)
        => BackgroundTaskTracker.SnapshotByName.TryGetValue(name, out var snapshot)
            ? snapshot
            : default;

    private static string TestTaskName()
        => $"test-background-task-{Guid.NewGuid():N}";
}
