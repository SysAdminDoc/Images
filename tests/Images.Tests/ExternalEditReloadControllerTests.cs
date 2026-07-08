using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Threading;
using Images.ViewModels;

namespace Images.Tests;

public sealed class ExternalEditReloadControllerTests
{
    [Fact]
    public void ScheduleReload_CoalescesEventsAndShowsOneSuccessMessage()
    {
        RunOnSta(() =>
        {
            var reloadCount = 0;
            var messages = new List<string>();

            using var controller = new ExternalEditReloadController(
                Dispatcher.CurrentDispatcher,
                isDisposed: () => false,
                reload: () =>
                {
                    reloadCount++;
                    return Task.FromResult(true);
                },
                notify: messages.Add,
                debounceInterval: TimeSpan.FromMilliseconds(20));

            controller.ScheduleReload();
            controller.ScheduleReload();

            PumpUntil(() => messages.Count == 1);
            PumpFor(TimeSpan.FromMilliseconds(50));

            Assert.Equal(1, reloadCount);
            Assert.Equal(new[] { ExternalEditReloadController.ReloadedToastMessage }, messages);
        });
    }

    [Fact]
    public void ScheduleReload_WhenReloadFails_DoesNotNotify()
    {
        RunOnSta(() =>
        {
            var reloadCount = 0;
            var messages = new List<string>();

            using var controller = new ExternalEditReloadController(
                Dispatcher.CurrentDispatcher,
                isDisposed: () => false,
                reload: () =>
                {
                    reloadCount++;
                    return Task.FromResult(false);
                },
                notify: messages.Add,
                debounceInterval: TimeSpan.FromMilliseconds(20));

            controller.ScheduleReload();

            PumpUntil(() => reloadCount == 1);
            PumpFor(TimeSpan.FromMilliseconds(50));

            Assert.Empty(messages);
        });
    }

    [Fact]
    public void Disarm_CancelsPendingReload()
    {
        RunOnSta(() =>
        {
            var reloadCount = 0;

            using var controller = new ExternalEditReloadController(
                Dispatcher.CurrentDispatcher,
                isDisposed: () => false,
                reload: () =>
                {
                    reloadCount++;
                    return Task.FromResult(true);
                },
                notify: _ => { },
                debounceInterval: TimeSpan.FromMilliseconds(20));

            controller.ScheduleReload();
            controller.Disarm();

            PumpFor(TimeSpan.FromMilliseconds(75));

            Assert.Equal(0, reloadCount);
        });
    }

    [Fact]
    public void Arm_WhenWatcherCreationFails_LeavesControllerDisarmed()
    {
        RunOnSta(() =>
        {
            using var tempDir = TestDirectory.Create();
            var path = tempDir.WriteFile("image.png", "not an image");

            using var controller = new ExternalEditReloadController(
                Dispatcher.CurrentDispatcher,
                isDisposed: () => false,
                reload: () => Task.FromResult(true),
                notify: _ => { },
                watcherFactory: (_, _) => throw new IOException("Watcher unavailable."));

            controller.Arm(path);

            Assert.False(controller.IsArmed);
            Assert.Null(controller.WatchedPath);
        });
    }

    [Fact]
    public void ScheduleReload_WhenQueuedPathNoLongerMatchesWatchedPath_DoesNotReload()
    {
        RunOnSta(() =>
        {
            using var tempDir = TestDirectory.Create();
            var oldPath = tempDir.WriteFile("old.png", "old");
            var newPath = tempDir.WriteFile("new.png", "new");
            var reloadCount = 0;

            using var controller = new ExternalEditReloadController(
                Dispatcher.CurrentDispatcher,
                isDisposed: () => false,
                reload: () =>
                {
                    reloadCount++;
                    return Task.FromResult(true);
                },
                notify: _ => { },
                debounceInterval: TimeSpan.FromMilliseconds(20));

            controller.Arm(newPath);
            controller.ScheduleReload(oldPath);

            PumpFor(TimeSpan.FromMilliseconds(75));

            Assert.Equal(0, reloadCount);

            controller.ScheduleReload(newPath);
            PumpUntil(() => reloadCount == 1);

            Assert.Equal(1, reloadCount);
        });
    }

    [Fact]
    public void Arm_ConfiguresWatcherForRenameOverSaves()
    {
        RunOnSta(() =>
        {
            using var tempDir = TestDirectory.Create();
            var path = tempDir.WriteFile("image.png", "not an image");
            FileSystemWatcher? capturedWatcher = null;

            using var controller = new ExternalEditReloadController(
                Dispatcher.CurrentDispatcher,
                isDisposed: () => false,
                reload: () => Task.FromResult(true),
                notify: _ => { },
                watcherFactory: (directory, fileName) =>
                {
                    capturedWatcher = new FileSystemWatcher(directory, fileName);
                    return capturedWatcher;
                });

            controller.Arm(path);

            Assert.True(controller.IsArmed);
            Assert.NotNull(capturedWatcher);
            Assert.True(capturedWatcher.NotifyFilter.HasFlag(NotifyFilters.LastWrite));
            Assert.True(capturedWatcher.NotifyFilter.HasFlag(NotifyFilters.Size));
            Assert.True(capturedWatcher.NotifyFilter.HasFlag(NotifyFilters.FileName));
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
