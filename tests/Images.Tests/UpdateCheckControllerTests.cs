using Images.Services;
using Images.ViewModels;

namespace Images.Tests;

public sealed class UpdateCheckControllerTests
{
    [Fact]
    public async Task CheckAsync_WhenBackgroundCheckNotDue_SkipsNetworkAndStateWrite()
    {
        var checkedNetwork = false;
        var recorded = false;
        var messages = new List<string>();
        var controller = new UpdateCheckController(
            notify: messages.Add,
            isDueForBackgroundCheck: () => false,
            checkAsync: _ =>
            {
                checkedNetwork = true;
                return Task.FromResult(CurrentResult());
            },
            recordLastChecked: _ => recorded = true);

        await controller.CheckAsync(userInitiated: false);

        Assert.False(checkedNetwork);
        Assert.False(recorded);
        Assert.Empty(messages);
    }

    [Fact]
    public async Task CheckAsync_WhenNewerReleaseExists_StoresReleaseAndInvalidatesCommands()
    {
        var invalidations = 0;
        var recorded = new List<UpdateCheckService.CheckResult>();
        var messages = new List<string>();
        var result = NewerResult();
        var controller = new UpdateCheckController(
            notify: messages.Add,
            checkAsync: _ => Task.FromResult(result),
            recordLastChecked: recorded.Add,
            invalidateCommands: () => invalidations++);

        await controller.CheckAsync(userInitiated: true);

        Assert.Equal("v99.0.0", controller.LatestUpdateTag);
        Assert.Equal("https://github.com/SysAdminDoc/Images/releases/tag/v99.0.0", controller.LatestUpdateUrl);
        Assert.True(controller.HasUpdateAvailable);
        Assert.Equal(new[] { result }, recorded);
        Assert.Equal(1, invalidations);
        Assert.Equal("New version v99.0.0 available", Assert.Single(messages));
    }

    [Fact]
    public async Task CheckAsync_WhenCurrentVersionAndUserInitiated_NotifiesLatestVersion()
    {
        var messages = new List<string>();
        var controller = new UpdateCheckController(
            notify: messages.Add,
            checkAsync: _ => Task.FromResult(CurrentResult()));

        await controller.CheckAsync(userInitiated: true);

        Assert.False(controller.HasUpdateAvailable);
        Assert.Equal(UpdateCheckController.LatestVersionMessage, Assert.Single(messages));
    }

    [Fact]
    public async Task CheckAsync_WhenManualCheckFails_NotifiesErrorAndRecordsPolicy()
    {
        var recorded = new List<UpdateCheckService.CheckResult>();
        var messages = new List<string>();
        var result = ErrorResult("HTTP 500", shouldUpdateLastChecked: true);
        var controller = new UpdateCheckController(
            notify: messages.Add,
            checkAsync: _ => Task.FromResult(result),
            recordLastChecked: recorded.Add);

        await controller.CheckAsync(userInitiated: true);

        Assert.Equal(new[] { result }, recorded);
        Assert.Equal("Update check failed: HTTP 500", Assert.Single(messages));
    }

    [Fact]
    public async Task CheckAsync_WhenBackgroundCheckFails_DoesNotToast()
    {
        var messages = new List<string>();
        var controller = new UpdateCheckController(
            notify: messages.Add,
            isDueForBackgroundCheck: () => true,
            checkAsync: _ => Task.FromResult(ErrorResult("timed out", shouldUpdateLastChecked: false)));

        await controller.CheckAsync(userInitiated: false);

        Assert.Empty(messages);
    }

    [Fact]
    public async Task OpenLatestUpdate_WhenReleaseUrlExists_OpensStoredUrl()
    {
        var opened = new List<string>();
        var controller = new UpdateCheckController(
            notify: _ => { },
            checkAsync: _ => Task.FromResult(NewerResult()),
            openTarget: opened.Add);

        await controller.CheckAsync(userInitiated: true);
        controller.OpenLatestUpdate();

        Assert.Equal(new[] { "https://github.com/SysAdminDoc/Images/releases/tag/v99.0.0" }, opened);
    }

    [Fact]
    public async Task OpenLatestUpdate_WhenShellOpenFails_NotifiesFailure()
    {
        var messages = new List<string>();
        var controller = new UpdateCheckController(
            notify: messages.Add,
            checkAsync: _ => Task.FromResult(NewerResult()),
            openTarget: _ => throw new InvalidOperationException("blocked"));

        await controller.CheckAsync(userInitiated: true);
        messages.Clear();

        controller.OpenLatestUpdate();

        Assert.Equal("Could not open release page: blocked", Assert.Single(messages));
    }

    private static UpdateCheckService.CheckResult NewerResult()
        => new(
            NewerAvailable: true,
            LatestTag: "v99.0.0",
            LatestHtmlUrl: "https://github.com/SysAdminDoc/Images/releases/tag/v99.0.0",
            Error: null,
            ShouldUpdateLastChecked: true);

    private static UpdateCheckService.CheckResult CurrentResult()
        => new(
            NewerAvailable: false,
            LatestTag: "v0.2.9",
            LatestHtmlUrl: "https://github.com/SysAdminDoc/Images/releases/latest",
            Error: null,
            ShouldUpdateLastChecked: true);

    private static UpdateCheckService.CheckResult ErrorResult(string error, bool shouldUpdateLastChecked)
        => new(
            NewerAvailable: false,
            LatestTag: null,
            LatestHtmlUrl: null,
            Error: error,
            ShouldUpdateLastChecked: shouldUpdateLastChecked);
}
