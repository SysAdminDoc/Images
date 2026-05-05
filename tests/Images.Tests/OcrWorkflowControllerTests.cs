using Images.ViewModels;

namespace Images.Tests;

public sealed class OcrWorkflowControllerTests
{
    [Fact]
    public async Task ToggleAsync_WhenNoImageLoaded_NotifiesNoImage()
    {
        var messages = new List<string>();
        using var controller = new OcrWorkflowController(
            currentPath: () => null,
            hasImage: () => false,
            notify: messages.Add,
            extractLinesAsync: (_, _) => throw new InvalidOperationException("Extraction should not run."));

        await controller.ToggleAsync();

        Assert.Equal(new[] { OcrWorkflowController.NoImageMessage }, messages);
        Assert.False(controller.IsOcrBusy);
        Assert.False(controller.IsOcrMode);
    }

    [Fact]
    public async Task ToggleAsync_WhenExtractionReturnsLines_ShowsOverlayAndSuccess()
    {
        var path = @"C:\photos\text.png";
        var messages = new List<string>();
        using var controller = new OcrWorkflowController(
            currentPath: () => path,
            hasImage: () => true,
            notify: messages.Add,
            extractLinesAsync: (_, _) => Task.FromResult<IReadOnlyList<OcrTextLine>?>(
                new[]
                {
                    new OcrTextLine
                    {
                        Text = "Invoice 123",
                        BoundingBox = new Windows.Foundation.Rect(10, 20, 100, 24)
                    }
                }));

        await controller.ToggleAsync();

        var line = Assert.Single(controller.OcrOverlayLines!);
        Assert.Equal("Invoice 123", line.Text);
        Assert.True(controller.IsOcrMode);
        Assert.False(controller.IsOcrBusy);
        Assert.True(controller.ShowOcrStatusPanel);
        Assert.Equal("1 text region found", controller.OcrRegionCountText);
        Assert.Equal(new[] { OcrWorkflowController.ExtractingMessage, "1 text region found" }, messages);
    }

    [Fact]
    public async Task ToggleAsync_WhenExtractionReturnsNoLines_ShowsNoTextState()
    {
        var path = @"C:\photos\blank.png";
        var messages = new List<string>();
        using var controller = new OcrWorkflowController(
            currentPath: () => path,
            hasImage: () => true,
            notify: messages.Add,
            extractLinesAsync: (_, _) => Task.FromResult<IReadOnlyList<OcrTextLine>?>(Array.Empty<OcrTextLine>()));

        await controller.ToggleAsync();

        Assert.Null(controller.OcrOverlayLines);
        Assert.False(controller.IsOcrMode);
        Assert.False(controller.IsOcrBusy);
        Assert.Equal(new[] { OcrWorkflowController.ExtractingMessage, OcrWorkflowController.NoTextFoundMessage }, messages);
    }

    [Fact]
    public async Task ToggleAsync_WhenCanceled_ClearsBusyStateAndReportsCancellation()
    {
        var path = @"C:\photos\slow.png";
        var messages = new List<string>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var controller = new OcrWorkflowController(
            currentPath: () => path,
            hasImage: () => true,
            notify: messages.Add,
            extractLinesAsync: async (_, token) =>
            {
                started.SetResult();
                await Task.Delay(TimeSpan.FromSeconds(5), token);
                return Array.Empty<OcrTextLine>();
            });

        var firstToggle = controller.ToggleAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(controller.IsOcrBusy);

        await controller.ToggleAsync();
        await firstToggle;

        Assert.False(controller.IsOcrBusy);
        Assert.False(controller.IsOcrMode);
        Assert.Null(controller.OcrOverlayLines);
        Assert.Equal(OcrWorkflowController.CanceledMessage, messages.Last());
    }

    [Fact]
    public async Task ToggleAsync_WhenCurrentPathChanges_IgnoresStaleResult()
    {
        var currentPath = @"C:\photos\first.png";
        var messages = new List<string>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<IReadOnlyList<OcrTextLine>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var controller = new OcrWorkflowController(
            currentPath: () => currentPath,
            hasImage: () => true,
            notify: messages.Add,
            extractLinesAsync: (_, _) =>
            {
                started.SetResult();
                return release.Task;
            });

        var extraction = controller.ToggleAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        currentPath = @"C:\photos\second.png";
        release.SetResult(new[]
        {
            new OcrTextLine
            {
                Text = "stale",
                BoundingBox = new Windows.Foundation.Rect(1, 1, 10, 10)
            }
        });
        await extraction;

        Assert.Null(controller.OcrOverlayLines);
        Assert.False(controller.IsOcrMode);
        Assert.False(controller.IsOcrBusy);
        Assert.Equal(new[] { OcrWorkflowController.ExtractingMessage }, messages);
    }
}
