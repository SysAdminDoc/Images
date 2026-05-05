using Images.Services;

namespace Images.ViewModels;

public sealed class UpdateCheckController : ObservableObject
{
    public const string LatestVersionMessage = "You're on the latest version";

    private readonly Func<bool> _isDueForBackgroundCheck;
    private readonly Func<CancellationToken, Task<UpdateCheckService.CheckResult>> _checkAsync;
    private readonly Action<UpdateCheckService.CheckResult> _recordLastChecked;
    private readonly Action<string> _notify;
    private readonly Action<string> _openTarget;
    private readonly Action _invalidateCommands;
    private string? _latestUpdateTag;
    private string? _latestUpdateUrl;
    private bool _isCheckingForUpdates;
    private string _updateCheckStatusText = "";

    public UpdateCheckController(
        Action<string> notify,
        Func<bool>? isDueForBackgroundCheck = null,
        Func<CancellationToken, Task<UpdateCheckService.CheckResult>>? checkAsync = null,
        Action<UpdateCheckService.CheckResult>? recordLastChecked = null,
        Action<string>? openTarget = null,
        Action? invalidateCommands = null)
    {
        _notify = notify ?? throw new ArgumentNullException(nameof(notify));
        _isDueForBackgroundCheck = isDueForBackgroundCheck ?? UpdateCheckService.IsDueForBackgroundCheck;
        _checkAsync = checkAsync ?? (token => UpdateCheckService.CheckAsync(token));
        _recordLastChecked = recordLastChecked ?? UpdateCheckService.RecordLastCheckedIfAppropriate;
        _openTarget = openTarget ?? ShellIntegration.OpenShellTarget;
        _invalidateCommands = invalidateCommands ?? (() => { });
    }

    public string? LatestUpdateTag
    {
        get => _latestUpdateTag;
        private set
        {
            if (Set(ref _latestUpdateTag, value))
            {
                Raise(nameof(HasUpdateAvailable));
                _invalidateCommands();
            }
        }
    }

    public string? LatestUpdateUrl
    {
        get => _latestUpdateUrl;
        private set => Set(ref _latestUpdateUrl, value);
    }

    public bool HasUpdateAvailable => !string.IsNullOrEmpty(LatestUpdateTag);

    public bool IsCheckingForUpdates
    {
        get => _isCheckingForUpdates;
        private set
        {
            if (Set(ref _isCheckingForUpdates, value))
                _invalidateCommands();
        }
    }

    public string UpdateCheckStatusText
    {
        get => _updateCheckStatusText;
        private set => Set(ref _updateCheckStatusText, value);
    }

    public async Task CheckAsync(bool userInitiated, CancellationToken token = default)
    {
        if (!userInitiated && !_isDueForBackgroundCheck())
            return;

        if (IsCheckingForUpdates)
        {
            if (userInitiated)
                _notify("Update check already in progress");
            return;
        }

        IsCheckingForUpdates = true;
        UpdateCheckStatusText = userInitiated
            ? "Checking GitHub Releases..."
            : "Checking GitHub Releases in the background...";

        try
        {
            var result = await _checkAsync(token).ConfigureAwait(true);
            _recordLastChecked(result);

            if (result.Error is not null)
            {
                if (userInitiated)
                    _notify($"Update check failed: {result.Error}");
                return;
            }

            if (result.NewerAvailable)
            {
                LatestUpdateUrl = result.LatestHtmlUrl;
                LatestUpdateTag = result.LatestTag;
                _notify($"New version {result.LatestTag} available");
            }
            else if (userInitiated)
            {
                _notify(LatestVersionMessage);
            }
        }
        finally
        {
            IsCheckingForUpdates = false;
            UpdateCheckStatusText = "";
        }
    }

    public void OpenLatestUpdate()
    {
        if (string.IsNullOrWhiteSpace(LatestUpdateUrl))
            return;

        try
        {
            _openTarget(LatestUpdateUrl);
        }
        catch (Exception ex)
        {
            _notify($"Could not open release page: {ex.Message}");
        }
    }
}
