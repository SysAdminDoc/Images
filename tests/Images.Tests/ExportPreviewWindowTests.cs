using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Images.Controls;
using Images.Localization;
using Images.Services;

namespace Images.Tests;

[Collection("WpfSmoke")]
public sealed class ExportPreviewWindowTests
{
    [Fact]
    public void BuildPreviewAsync_CancelsSupersededPreviewAndKeepsLatestResult()
    {
        RunOnStaWithTheme(async () =>
        {
            var firstStarted = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstCanceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondStarted = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseSecond = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var source = CreateBitmap(4, 4, 0x10);
            var firstPreview = CreateBitmap(4, 4, 0x40);
            var secondPreview = CreateBitmap(4, 4, 0x80);
            var callCount = 0;

            var window = new ExportPreviewWindow(
                source,
                sourcePath: null,
                initialExtension: ".png",
                buildPreview: (_, _, _, token) =>
                {
                    var call = Interlocked.Increment(ref callCount);
                    if (call == 1)
                    {
                        firstStarted.TrySetResult(token);
                        using var registration = token.Register(() => firstCanceled.TrySetResult(true));
                        releaseFirst.Task.GetAwaiter().GetResult();
                        token.ThrowIfCancellationRequested();
                        return BuildResult(firstPreview, "PNG");
                    }

                    secondStarted.TrySetResult(token);
                    releaseSecond.Task.GetAwaiter().GetResult();
                    token.ThrowIfCancellationRequested();
                    return BuildResult(secondPreview, "WEBP");
                });
            try
            {
                var firstTask = window.BuildPreviewAsync();
                var firstToken = await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

                var secondTask = window.BuildPreviewAsync();
                var secondToken = await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await firstCanceled.Task.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.True(firstToken.IsCancellationRequested);
                Assert.False(secondToken.IsCancellationRequested);

                releaseSecond.SetResult(true);
                await secondTask.WaitAsync(TimeSpan.FromSeconds(5));

                var previewCanvas = Assert.IsType<ZoomPanImage>(window.FindName("PreviewCanvas"));
                Assert.Same(secondPreview, previewCanvas.Source);
                Assert.Equal(Strings.Format(nameof(Strings.ExportPreviewFormatQualityFormat), "WEBP", "90"), window.FormatText.Text);

                releaseFirst.SetResult(true);
                await firstTask.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.Same(secondPreview, previewCanvas.Source);
                Assert.Equal(Strings.Format(nameof(Strings.ExportPreviewFormatQualityFormat), "WEBP", "90"), window.FormatText.Text);
                Assert.Equal(2, callCount);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static ExportPreviewResult BuildResult(BitmapSource previewImage, string format)
        => new(
            previewImage,
            new ExportPreviewSummary(
                SourceBytes: 64,
                EstimatedBytes: 32,
                Width: (uint)previewImage.PixelWidth,
                Height: (uint)previewImage.PixelHeight,
                FormatText: format,
                QualityText: "90",
                SourceSizeText: "64 B",
                EstimatedSizeText: "32 B",
                DeltaText: "-32 B",
                Warnings: [],
                C2paHandoff: C2paExportHandoff.Omitted(
                    C2paExportReason.SourceHasNoManifest,
                    "C2PA not written",
                    "No source Content Credentials were found.")));

    private static BitmapSource CreateBitmap(int width, int height, byte channel)
    {
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index + 0] = channel;
            pixels[index + 1] = channel;
            pixels[index + 2] = channel;
            pixels[index + 3] = 0xFF;
        }

        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static void RunOnStaWithTheme(Func<Task> action)
    {
        Exception? failure = null;
        Dispatcher? dispatcher = null;
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            var createdApplication = Application.Current is null;
            var application = Application.Current ?? new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };

            try
            {
                application.Resources.MergedDictionaries.Clear();
                application.Resources.MergedDictionaries.Add(
                    (ResourceDictionary)Application.LoadComponent(
                        new Uri("/Images;component/Themes/DarkTheme.xaml", UriKind.Relative)));

                action().ContinueWith(
                    task =>
                    {
                        if (task.IsFaulted)
                            failure = task.Exception?.GetBaseException() ?? task.Exception;
                        else if (task.IsCanceled)
                            failure = new TaskCanceledException(task);

                        dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.FromCurrentSynchronizationContext());
                Dispatcher.Run();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                if (createdApplication)
                    application.Shutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        if (!thread.Join(TimeSpan.FromSeconds(20)))
        {
            dispatcher?.BeginInvokeShutdown(DispatcherPriority.Send);
            throw new TimeoutException("Timed out while running export preview window test on an STA dispatcher.");
        }

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
