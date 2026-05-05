using System.IO;
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

    [Fact]
    public void Refresh_WhenThumbnailLoadFails_TracksFailureAndMarksItem()
    {
        RunOnSta(() =>
        {
            using var controller = new FolderPreviewController(
                Dispatcher.CurrentDispatcher,
                isDisposed: () => false,
                loadThumbnail: (_, _) => throw new IOException("cache unavailable"));

            controller.Refresh(new[] { @"C:\photos\a.png", @"C:\photos\b.png" }, currentIndex: 0);

            PumpUntil(() => controller.ThumbnailFailureCount == 2);

            Assert.Equal(2, controller.ThumbnailFailureCount);
            Assert.Contains("2 folder thumbnails could not be generated", controller.ThumbnailFailureStatusText);
            Assert.All(controller.Items, item => Assert.True(item.ThumbnailFailed));
            Assert.All(controller.Items, item => Assert.False(item.HasThumbnail));
        });
    }

    [Fact]
    public void Refresh_WithThousandsOfFiles_OnlyRequestsNearbyThumbnails()
    {
        RunOnSta(() =>
        {
            var requested = 0;
            var files = Enumerable
                .Range(1, 5000)
                .Select(i => $@"C:\photos\image{i}.jpg")
                .ToArray();

            using var controller = new FolderPreviewController(
                Dispatcher.CurrentDispatcher,
                isDisposed: () => false,
                loadThumbnail: (_, _) =>
                {
                    Interlocked.Increment(ref requested);
                    return null;
                });

            controller.Refresh(files, currentIndex: 2500);

            PumpUntil(() => Volatile.Read(ref requested) == 9);

            Assert.Equal(5000, controller.Items.Count);
            Assert.Equal(9, Volatile.Read(ref requested));
        });
    }

    [Fact]
    public void Refresh_WithSameFiles_UpdatesCurrentItemWithoutRebuildingCollection()
    {
        RunOnSta(() =>
        {
            var files = Enumerable
                .Range(1, 1000)
                .Select(i => $@"C:\photos\image{i}.jpg")
                .ToArray();

            using var controller = new FolderPreviewController(
                Dispatcher.CurrentDispatcher,
                isDisposed: () => false,
                loadThumbnail: (_, _) => null);

            controller.Refresh(files, currentIndex: 10);
            var firstItem = controller.Items[10];

            controller.Refresh(files, currentIndex: 11);

            Assert.Same(firstItem, controller.Items[10]);
            Assert.False(controller.Items[10].IsCurrent);
            Assert.True(controller.Items[11].IsCurrent);
            Assert.Equal(1000, controller.Items.Count);
        });
    }

    [Fact]
    public void Refresh_WithReorderedFiles_RebuildsItemsToMatchSortOrder()
    {
        RunOnSta(() =>
        {
            var files = new List<string>
            {
                @"C:\photos\image1.jpg",
                @"C:\photos\image2.jpg",
                @"C:\photos\image10.jpg"
            };

            using var controller = new FolderPreviewController(
                Dispatcher.CurrentDispatcher,
                isDisposed: () => false,
                loadThumbnail: (_, _) => null);

            controller.Refresh(files, currentIndex: 2);
            files.Reverse();
            controller.Refresh(files, currentIndex: 0);

            Assert.Equal(files, controller.Items.Select(item => item.Path));
            Assert.True(controller.Items[0].IsCurrent);
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
