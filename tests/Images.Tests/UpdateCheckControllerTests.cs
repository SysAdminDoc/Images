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
        Assert.Equal(3, invalidations);
        Assert.Equal("New version v99.0.0 available", Assert.Single(messages));
    }

    [Fact]
    public async Task CheckAsync_WhileInFlight_ExposesBusyStatusAndSkipsDuplicateManualCheck()
    {
        var calls = 0;
        var invalidations = 0;
        var messages = new List<string>();
        var changed = new List<string>();
        var gate = new TaskCompletionSource<UpdateCheckService.CheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var controller = new UpdateCheckController(
            notify: messages.Add,
            checkAsync: _ =>
            {
                calls++;
                return gate.Task;
            },
            invalidateCommands: () => invalidations++);
        controller.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.PropertyName))
                changed.Add(e.PropertyName);
        };

        var running = controller.CheckAsync(userInitiated: true);

        Assert.True(controller.IsCheckingForUpdates);
        Assert.Equal("Checking GitHub Releases...", controller.UpdateCheckStatusText);
        Assert.Contains(nameof(UpdateCheckController.IsCheckingForUpdates), changed);
        Assert.Contains(nameof(UpdateCheckController.UpdateCheckStatusText), changed);
        Assert.Equal(1, calls);

        await controller.CheckAsync(userInitiated: true);

        Assert.Equal(1, calls);
        Assert.Contains("Update check already in progress", messages);

        gate.SetResult(CurrentResult());
        await running;

        Assert.False(controller.IsCheckingForUpdates);
        Assert.Equal("", controller.UpdateCheckStatusText);
        Assert.Contains(UpdateCheckController.LatestVersionMessage, messages);
        Assert.True(invalidations >= 2);
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
    public async Task CheckAsync_WhenManualCheckRuns_RecordsTrackedUpdateTask()
    {
        var before = SnapshotFor("update-check:manual");
        var controller = new UpdateCheckController(
            notify: _ => { },
            checkAsync: _ => Task.FromResult(CurrentResult()));

        await controller.CheckAsync(userInitiated: true);

        var after = SnapshotFor("update-check:manual");
        Assert.Equal(before.Started + 1, after.Started);
        Assert.Equal(before.Completed + 1, after.Completed);
        Assert.Equal(0, after.Running);
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
        Assert.True(controller.HasUpdateCheckIssue);
        Assert.Equal("Update check failed", controller.UpdateCheckIssueTitle);
        Assert.Contains("GitHub Releases returned: HTTP 500", controller.UpdateCheckIssueDetail);
        Assert.Equal("Update check failed: HTTP 500", Assert.Single(messages));
    }

    [Fact]
    public async Task CheckAsync_WhenManualCheckIsOffline_ExposesActionableIssue()
    {
        var messages = new List<string>();
        var controller = new UpdateCheckController(
            notify: messages.Add,
            checkAsync: _ => Task.FromResult(ErrorResult("network: offline", shouldUpdateLastChecked: false)));

        await controller.CheckAsync(userInitiated: true);

        Assert.True(controller.HasUpdateCheckIssue);
        Assert.Equal("Update check unavailable", controller.UpdateCheckIssueTitle);
        Assert.Contains("could not reach GitHub Releases", controller.UpdateCheckIssueDetail);
        Assert.Contains("no image files were uploaded", controller.UpdateCheckIssueDetail);
        Assert.Equal("Update check failed: network: offline", Assert.Single(messages));
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
        Assert.True(controller.HasUpdateCheckIssue);
        Assert.Equal("Update check unavailable", controller.UpdateCheckIssueTitle);
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

    private static BackgroundTaskSnapshot SnapshotFor(string name)
        => BackgroundTaskTracker.SnapshotByName.TryGetValue(name, out var snapshot)
            ? snapshot
            : default;
}
