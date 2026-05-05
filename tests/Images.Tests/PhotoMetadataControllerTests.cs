using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Threading;
using Images.Services;
using Images.ViewModels;

namespace Images.Tests;

public sealed class PhotoMetadataControllerTests
{
    [Fact]
    public void Refresh_WhenReaderReturnsRows_PopulatesRowsAndClearsStatus()
    {
        RunOnSta(() =>
        {
            var path = @"C:\photos\frame.jpg";
            using var controller = new PhotoMetadataController(
                Dispatcher.CurrentDispatcher,
                isDisposed: () => false,
                currentPath: () => path,
                readMetadata: _ => new PhotoMetadata([new MetadataFact("Camera", "Test Body")]),
                timeout: TimeSpan.FromSeconds(1));

            controller.Refresh(path);
            PumpUntil(() => !controller.IsLoading);

            var row = Assert.Single(controller.Rows);
            Assert.Equal("Camera", row.Label);
            Assert.Equal("Test Body", row.Value);
            Assert.Equal("", controller.StatusText);
        });
    }

    [Fact]
    public void Refresh_WhenSuperseded_IgnoresOlderResult()
    {
        RunOnSta(() =>
        {
            var first = @"C:\photos\first.jpg";
            var second = @"C:\photos\second.jpg";
            var currentPath = first;
            using var firstStarted = new ManualResetEventSlim();
            using var releaseFirst = new ManualResetEventSlim();

            using var controller = new PhotoMetadataController(
                Dispatcher.CurrentDispatcher,
                isDisposed: () => false,
                currentPath: () => currentPath,
                readMetadata: path =>
                {
                    if (path == first)
                    {
                        firstStarted.Set();
                        releaseFirst.Wait(TimeSpan.FromSeconds(5));
                        return new PhotoMetadata([new MetadataFact("Path", "first")]);
                    }

                    return new PhotoMetadata([new MetadataFact("Path", "second")]);
                },
                timeout: TimeSpan.FromSeconds(1));

            controller.Refresh(first);
            Assert.True(firstStarted.Wait(TimeSpan.FromSeconds(1)));

            currentPath = second;
            controller.Refresh(second);
            PumpUntil(() => !controller.IsLoading && controller.Rows.FirstOrDefault()?.Value == "second");

            releaseFirst.Set();
            PumpFor(TimeSpan.FromMilliseconds(50));

            var row = Assert.Single(controller.Rows);
            Assert.Equal("second", row.Value);
        });
    }

    [Fact]
    public void Refresh_WhenReaderTimesOut_ShowsTimeoutStatus()
    {
        RunOnSta(() =>
        {
            var path = @"C:\photos\slow.jpg";
            using var releaseReader = new ManualResetEventSlim();

            using var controller = new PhotoMetadataController(
                Dispatcher.CurrentDispatcher,
                isDisposed: () => false,
                currentPath: () => path,
                readMetadata: _ =>
                {
                    releaseReader.Wait(TimeSpan.FromSeconds(5));
                    return new PhotoMetadata([new MetadataFact("Camera", "Late")]);
                },
                timeout: TimeSpan.FromMilliseconds(20));

            controller.Refresh(path);
            PumpUntil(() => !controller.IsLoading);
            releaseReader.Set();

            Assert.Empty(controller.Rows);
            Assert.Equal(PhotoMetadataController.TimeoutStatusText, controller.StatusText);
        });
    }

    private static void PumpUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException("Timed out while waiting for dispatcher work.");

            PumpFor(TimeSpan.FromMilliseconds(10));
        }
    }

    private static void PumpFor(TimeSpan interval)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = interval
        };

        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };

        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
