using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Threading;
using Images.Services;
using Images.ViewModels;

namespace Images.Tests;

public sealed class C2paInspectionControllerTests
{
    [Fact]
    public void Refresh_WhenReaderThrows_SurfacesFailureStatus()
    {
        RunOnSta(() =>
        {
            var path = @"C:\photos\credentialed.jpg";
            using var controller = new C2paInspectionController(
                Dispatcher.CurrentDispatcher,
                isDisposed: () => false,
                currentPath: () => path,
                readManifest: _ => throw new IOException("tool failure"),
                inspectRuntime: () => new C2paToolRuntimeStatus(
                    Available: true,
                    ExecutablePath: @"C:\Tools\c2patool.exe",
                    Source: "test",
                    Version: "test",
                    Sha256: "sha256:test",
                    StatusText: "ready"),
                timeout: TimeSpan.FromSeconds(10));

            controller.Refresh(path);
            PumpUntil(() => !controller.IsLoading);

            Assert.Equal(C2paStatus.Error, controller.Result?.Status);
            Assert.Equal("C2PA inspection failed.", controller.StatusText);
        });
    }

    private static void PumpUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
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
