using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Images.ViewModels;

namespace Images.Tests;

public sealed class FolderPreviewControllerTests
{
    [Fact]
    public void Clear_CancelsPendingThumbnailLoadAndKeepsItemsEmpty()
    {
        RunOnSta(() =>
        {
            var thumbnail = CreateThumbnail();
            using var firstStarted = new ManualResetEventSlim();
            using var releaseFirst = new ManualResetEventSlim();
            using var firstFinished = new ManualResetEventSlim();
            var callCount = 0;
            var cancellationObserved = 0;

            using var controller = new FolderPreviewController(
                Dispatcher.CurrentDispatcher,
                isDisposed: () => false,
                loadThumbnail: (_, token) =>
                {
                    if (Interlocked.Increment(ref callCount) == 1)
                    {
                        firstStarted.Set();
                        releaseFirst.Wait(TimeSpan.FromSeconds(5));
                        if (token.IsCancellationRequested)
                            Interlocked.Exchange(ref cancellationObserved, 1);
                        firstFinished.Set();
                    }

                    return thumbnail;
                });

            controller.Refresh(new[] { @"C:\photos\first.png", @"C:\photos\second.png" }, currentIndex: 0);
            Assert.True(firstStarted.Wait(TimeSpan.FromSeconds(1)));

            controller.Clear();
            releaseFirst.Set();

            Assert.True(firstFinished.Wait(TimeSpan.FromSeconds(1)));
            PumpFor(TimeSpan.FromMilliseconds(100));

            Assert.Equal(1, Volatile.Read(ref cancellationObserved));
            Assert.Empty(controller.Items);
        });
    }

    [Fact]
    public void Refresh_WhenPreviousLoadFinishesLate_IgnoresSupersededThumbnail()
    {
        RunOnSta(() =>
        {
            var thumbnail = CreateThumbnail();
            using var firstStarted = new ManualResetEventSlim();
            using var releaseFirst = new ManualResetEventSlim();
            using var firstFinished = new ManualResetEventSlim();
            var callCount = 0;
            var cancellationObserved = 0;

            using var controller = new FolderPreviewController(
                Dispatcher.CurrentDispatcher,
                isDisposed: () => false,
                loadThumbnail: (_, token) =>
                {
                    if (Interlocked.Increment(ref callCount) == 1)
                    {
                        firstStarted.Set();
                        releaseFirst.Wait(TimeSpan.FromSeconds(5));
                        if (token.IsCancellationRequested)
                            Interlocked.Exchange(ref cancellationObserved, 1);
                        firstFinished.Set();
                    }

                    return thumbnail;
                });

            controller.Refresh(new[] { @"C:\photos\old-a.png", @"C:\photos\old-b.png" }, currentIndex: 0);
            var staleItem = controller.Items[0];
            Assert.True(firstStarted.Wait(TimeSpan.FromSeconds(1)));

            controller.Refresh(new[] { @"C:\photos\new-a.png", @"C:\photos\new-b.png" }, currentIndex: 0);
            releaseFirst.Set();

            Assert.True(firstFinished.Wait(TimeSpan.FromSeconds(1)));
            PumpFor(TimeSpan.FromMilliseconds(100));

            Assert.Equal(1, Volatile.Read(ref cancellationObserved));
            Assert.DoesNotContain(staleItem, controller.Items);
            Assert.False(staleItem.HasThumbnail);
            Assert.All(controller.Items, item => Assert.StartsWith(@"C:\photos\new-", item.Path));
        });
    }

    private static ImageSource CreateThumbnail()
    {
        var pixels = new byte[] { 0x21, 0x40, 0x5a, 0xff };
        var bitmap = BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            palette: null,
            pixels,
            stride: 4);
        bitmap.Freeze();
        return bitmap;
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
