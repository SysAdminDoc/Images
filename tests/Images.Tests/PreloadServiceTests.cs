using Images.Services;

namespace Images.Tests;

public sealed class PreloadServiceTests
{
    [Fact]
    public async Task Enqueue_QueuesCandidateProbeOffCallerThread()
    {
        using var probeEntered = new ManualResetEventSlim();
        using var releaseProbe = new ManualResetEventSlim();
        var callerThreadId = Environment.CurrentManagedThreadId;
        var probeThreadId = callerThreadId;
        var service = new PreloadService(_ =>
        {
            probeThreadId = Environment.CurrentManagedThreadId;
            probeEntered.Set();
            releaseProbe.Wait(TimeSpan.FromSeconds(5));
            return PreloadProbeResult.Skip("test probe");
        });

        try
        {
            var started = DateTime.UtcNow;
            service.Enqueue("blocked-probe.jpg");
            var elapsed = DateTime.UtcNow - started;

            Assert.True(elapsed < TimeSpan.FromSeconds(1), $"Enqueue blocked for {elapsed}.");
            Assert.True(probeEntered.Wait(TimeSpan.FromSeconds(5)), "The background probe did not start.");
            Assert.NotEqual(callerThreadId, probeThreadId);

            releaseProbe.Set();
            var preload = service.TryGetInFlight("blocked-probe.jpg");
            Assert.NotNull(preload);
            Assert.Null(await preload.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            releaseProbe.Set();
            service.Dispose();
        }
    }

    [Fact]
    public void CreateAndDispose_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
        {
            using var service = new PreloadService();
        });

        Assert.Null(ex);
    }

    [Fact]
    public void TryGet_OnEmptyCache_ReturnsNull()
    {
        using var service = new PreloadService();

        var result = service.TryGet("nonexistent.jpg");

        Assert.Null(result);
    }

    [Fact]
    public void Reset_OnEmptyCache_DoesNotThrow()
    {
        using var service = new PreloadService();

        var ex = Record.Exception(() => service.Reset());

        Assert.Null(ex);
    }

    [Fact]
    public void TryGet_AfterDispose_ReturnsNull()
    {
        var service = new PreloadService();
        service.Dispose();

        var result = service.TryGet("test.jpg");

        Assert.Null(result);
    }
}
