using Images.Services;

namespace Images.Tests;

public sealed class BackgroundJobsServiceTests : IDisposable
{
    public BackgroundJobsServiceTests()
    {
        BackgroundJobsService.ResetForTests();
    }

    public void Dispose()
    {
        BackgroundJobsService.ResetForTests();
    }

    [Fact]
    public void StartJob_AppearsInRunning()
    {
        var id = BackgroundJobsService.StartJob("test-job");

        var running = BackgroundJobsService.GetRunning();
        Assert.Single(running);
        Assert.Equal(id, running[0].Id);
        Assert.Equal("test-job", running[0].Name);
        Assert.Equal(BackgroundJobState.Running, running[0].State);
    }

    [Fact]
    public void CompleteJob_MovesToHistory()
    {
        var id = BackgroundJobsService.StartJob("test-complete");

        BackgroundJobsService.CompleteJob(id, affectedCount: 5);

        Assert.Empty(BackgroundJobsService.GetRunning());
        var recent = BackgroundJobsService.GetRecent();
        Assert.Contains(recent, j => j.Id == id && j.State == BackgroundJobState.Completed && j.AffectedCount == 5);
    }

    [Fact]
    public void FailJob_RecordsErrorMessage()
    {
        var id = BackgroundJobsService.StartJob("test-fail");

        BackgroundJobsService.FailJob(id, "something broke");

        Assert.Empty(BackgroundJobsService.GetRunning());
        var recent = BackgroundJobsService.GetRecent();
        var entry = Assert.Single(recent, j => j.Id == id);
        Assert.Equal(BackgroundJobState.Faulted, entry.State);
        Assert.Equal("something broke", entry.ErrorMessage);
    }

    [Fact]
    public void CancelJob_RecordsCancellation()
    {
        var id = BackgroundJobsService.StartJob("test-cancel");

        BackgroundJobsService.CancelJob(id);

        Assert.Empty(BackgroundJobsService.GetRunning());
        var recent = BackgroundJobsService.GetRecent();
        var entry = Assert.Single(recent, j => j.Id == id);
        Assert.Equal(BackgroundJobState.Canceled, entry.State);
    }

    [Fact]
    public void GetAll_CombinesRunningAndHistory()
    {
        var running = BackgroundJobsService.StartJob("still-running");
        var done = BackgroundJobsService.StartJob("already-done");
        BackgroundJobsService.CompleteJob(done);

        var all = BackgroundJobsService.GetAll();
        Assert.Contains(all, j => j.Id == running);
        Assert.Contains(all, j => j.Id == done);
    }
}
