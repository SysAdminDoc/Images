using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Xml.Linq;
using Images.Services;

namespace Images.Tests;

[Collection("WpfSmoke")]
public sealed class FaceReviewWindowContractTests
{
    [Fact]
    public void Xaml_ExposesVisualReviewAndDisabledWriteGateControls()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "Images", "FaceReviewWindow.xaml");
        var document = XDocument.Load(path);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var names = document.Descendants()
            .Select(element => element.Attribute(x + "Name")?.Value)
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("FaceList", names);
        Assert.Contains("PreviewPanel", names);
        Assert.Contains("NameBox", names);
        Assert.Contains("MergeButton", names);
        Assert.Contains("CancelButton", names);
        Assert.Contains(document.Descendants(), element =>
            element.Name.LocalName == "ItemsControl" &&
            element.Attribute("ItemsSource")?.Value == "{Binding Overlays}");
        Assert.Contains(document.Descendants(), element =>
            element.Name.LocalName == "Button" &&
            element.Attribute(x + "Name")?.Value == "MergeButton");
        Assert.Contains(document.Descendants(), element =>
            element.Name.LocalName == "Button" &&
            element.Attribute(x + "Name")?.Value == "CancelButton" &&
            element.Attribute("AutomationProperties.Name")?.Value == "{loc:Loc Cancel}");
    }

    [Fact]
    public void ItemViewModel_TracksReviewDecisionNameAndNeverCarriesEmbedding()
    {
        var candidate = new FaceReviewCandidate(
            new FaceReviewKey("photo.jpg", 0),
            new FaceDetection(10, 10, 40, 40, 0.9, []),
            FaceEmbeddingQuality.Accepted,
            null,
            2);
        var item = new Images.FaceReviewItemViewModel(candidate)
        {
            Decision = FaceReviewDecision.Accepted,
            Name = "Ada",
        };

        var review = item.ToReview();

        Assert.Equal(FaceReviewDecision.Accepted, review.Decision);
        Assert.Equal("Ada", review.Name);
        Assert.Contains("2", item.ClusterLabel, StringComparison.Ordinal);
        Assert.DoesNotContain(
            item.GetType().GetProperties(),
            property => property.Name.Contains("Vector", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Close_CancelsInFlightAnalysisWithoutPublishingPartialResults()
    {
        RunOnStaWithTheme(async () =>
        {
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var window = new Images.FaceReviewWindow(
                currentPath: null,
                folderPaths: [],
                analyze: (_, cancellationToken) =>
                {
                    started.TrySetResult(true);
                    using var registration = cancellationToken.Register(() => canceled.TrySetResult(true));
                    canceled.Task.GetAwaiter().GetResult();
                    cancellationToken.ThrowIfCancellationRequested();
                    return new FaceReviewAnalysis([], []);
                },
                merge: null);
            try
            {
                var analysisTask = window.AnalyzeAsync(["photo.jpg"]);
                await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

                window.Close();
                await canceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await analysisTask.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.Empty(window.Items);
            }
            finally
            {
                if (window.IsVisible)
                    window.Close();
            }
        });
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
            throw new TimeoutException("Timed out while running face-review cancellation test on an STA dispatcher.");
        }

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Images.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Could not locate repository root.");
    }
}
