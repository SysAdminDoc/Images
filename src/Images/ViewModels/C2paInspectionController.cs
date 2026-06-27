using System.IO;
using System.Windows.Threading;
using Images.Services;
using Microsoft.Extensions.Logging;

namespace Images.ViewModels;

public sealed class C2paInspectionController : IDisposable
{
    public const string LoadingStatusText = "Checking content credentials...";

    private static readonly ILogger _log = Log.Get(nameof(C2paInspectionController));
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private readonly Dispatcher _uiDispatcher;
    private readonly Func<bool> _isDisposed;
    private readonly Func<string?> _currentPath;
    private readonly Func<string, C2paInspectionResult> _readManifest;
    private readonly Func<C2paToolRuntimeStatus> _inspectRuntime;
    private readonly TimeSpan _timeout;
    private CancellationTokenSource _cts = new();
    private int _generation;

    public C2paInspectionController(
        Dispatcher uiDispatcher,
        Func<bool> isDisposed,
        Func<string?> currentPath,
        Func<string, C2paInspectionResult>? readManifest = null,
        Func<C2paToolRuntimeStatus>? inspectRuntime = null,
        TimeSpan? timeout = null)
    {
        _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
        _isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
        _currentPath = currentPath ?? throw new ArgumentNullException(nameof(currentPath));
        _readManifest = readManifest ?? C2paManifestService.Read;
        _inspectRuntime = inspectRuntime ?? C2paToolRuntime.Inspect;
        _timeout = timeout ?? DefaultTimeout;
    }

    public C2paInspectionResult? Result { get; private set; }

    public bool IsLoading { get; private set; }

    public string StatusText { get; private set; } = "";

    public bool HasCredentials => Result?.HasCredentials == true;

    public string TrustBadgeText => Result?.TrustLevel switch
    {
        C2paTrustLevel.Signed => "Signed",
        C2paTrustLevel.Invalid => "Invalid",
        C2paTrustLevel.Present => Result.HasCredentials ? "Present" : "",
        _ => "",
    };

    public string TrustBadgeTooltip => Result?.TrustLevel switch
    {
        C2paTrustLevel.Signed =>
            "This file has C2PA content credentials with a digital signature. " +
            "Provenance shows who created or edited the file — it does not verify whether the content is truthful.",
        C2paTrustLevel.Invalid =>
            "This file has C2PA content credentials but the signature could not be validated. " +
            "The manifest may have been tampered with or the signing certificate may be revoked.",
        _ => "C2PA content credentials indicate provenance, not authenticity.",
    };

    public event EventHandler? StateChanged;

    public void Refresh(string path)
    {
        var runtime = _inspectRuntime();
        if (!runtime.Available)
        {
            _log.LogWarning("C2PA inspection unavailable: {Status}", runtime.StatusText);
            Clear();
            return;
        }

        var generation = ++_generation;
        var previousCts = _cts;
        previousCts.Cancel();
        _ = DisposeSourceLaterAsync(previousCts);
        _cts = new CancellationTokenSource();

        Result = null;
        IsLoading = true;
        StatusText = LoadingStatusText;
        RaiseStateChanged();

        _ = LoadAsync(path, generation, _cts.Token);
    }

    public void Clear()
    {
        _generation++;
        var previousCts = _cts;
        previousCts.Cancel();
        _ = DisposeSourceLaterAsync(previousCts);
        _cts = new CancellationTokenSource();

        Result = null;
        IsLoading = false;
        StatusText = "";
        RaiseStateChanged();
    }

    private async Task LoadAsync(string path, int generation, CancellationToken token)
    {
        var taskName = $"c2pa-inspect:{Path.GetFileName(path)}";
        var readTask = BackgroundTaskTracker.Run(taskName, () =>
        {
            token.ThrowIfCancellationRequested();
            var result = _readManifest(path);
            token.ThrowIfCancellationRequested();
            return result;
        }, token);

        C2paInspectionResult result;

        try
        {
            result = await readTask
                .WaitAsync(_timeout, token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            _ = ObserveCompletionAsync(readTask);
            return;
        }
        catch (TimeoutException)
        {
            _log.LogWarning("C2PA inspection timed out for {File}", Path.GetFileName(path));
            _ = ObserveCompletionAsync(readTask);
            result = C2paInspectionResult.Error("C2PA inspection timed out.");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "C2PA inspection failed for {File}", Path.GetFileName(path));
            result = C2paInspectionResult.Error("C2PA inspection failed.");
        }

        try
        {
            await _uiDispatcher.InvokeAsync(() =>
            {
                if (_isDisposed() ||
                    token.IsCancellationRequested ||
                    generation != _generation ||
                    !string.Equals(path, _currentPath(), StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                Result = result;
                IsLoading = false;
                StatusText = result.Status switch
                {
                    C2paStatus.Found => "",
                    C2paStatus.NoManifest => "",
                    _ => result.ErrorMessage ?? "",
                };
                RaiseStateChanged();
            });
        }
        catch (TaskCanceledException) { }
        catch (InvalidOperationException) { }
    }

    private static async Task ObserveCompletionAsync(Task task)
    {
        try { await task.ConfigureAwait(false); }
        catch (Exception ex) { _log.LogDebug(ex, "C2PA inspection background task completed after cancellation/timeout"); }
    }

    private static async Task DisposeSourceLaterAsync(CancellationTokenSource source)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
        finally { source.Dispose(); }
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _generation++;
        _cts.Cancel();
        _ = DisposeSourceLaterAsync(_cts);
    }
}
