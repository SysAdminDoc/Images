using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using Images.Localization;
using Images.Services;
using Microsoft.Extensions.Logging;

namespace Images.ViewModels;

public sealed class ColorAnalysisController : IDisposable
{
    private static readonly ILogger _log = Log.Get(nameof(ColorAnalysisController));

    public static string LoadingStatusText => Strings.ColorAnalysisLoading;

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly Dispatcher _uiDispatcher;
    private readonly Func<bool> _isDisposed;
    private readonly Func<string?> _currentPath;
    private readonly Func<string, ImageColorAnalysis> _readAnalysis;
    private readonly TimeSpan _timeout;
    private CancellationTokenSource _analysisCts = new();
    private int _generation;

    public ColorAnalysisController(
        Dispatcher uiDispatcher,
        Func<bool> isDisposed,
        Func<string?> currentPath,
        Func<string, ImageColorAnalysis>? readAnalysis = null,
        TimeSpan? timeout = null)
    {
        _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
        _isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
        _currentPath = currentPath ?? throw new ArgumentNullException(nameof(currentPath));
        _readAnalysis = readAnalysis ?? ImageColorAnalysisService.Read;
        _timeout = timeout ?? DefaultTimeout;
    }

    public ObservableCollection<MetadataFact> Rows { get; } = [];

    public bool IsLoading { get; private set; }

    public string StatusText { get; private set; } = "";

    public string WarningText { get; private set; } = "";

    public event EventHandler? StateChanged;

    public void Refresh(string path)
    {
        var generation = ++_generation;
        var previousCts = _analysisCts;
        previousCts.Cancel();
        _ = DisposeSourceLaterAsync(previousCts);
        _analysisCts = new CancellationTokenSource();

        Rows.Clear();
        IsLoading = true;
        StatusText = LoadingStatusText;
        WarningText = "";
        RaiseStateChanged();

        _ = LoadAsync(path, generation, _analysisCts.Token);
    }

    public void Clear()
    {
        _generation++;
        var previousCts = _analysisCts;
        previousCts.Cancel();
        _ = DisposeSourceLaterAsync(previousCts);
        _analysisCts = new CancellationTokenSource();

        Rows.Clear();
        IsLoading = false;
        StatusText = "";
        WarningText = "";
        RaiseStateChanged();
    }

    private async Task LoadAsync(string path, int generation, CancellationToken token)
    {
        var taskName = $"color-analysis:{Path.GetFileName(path)}";
        var readTask = BackgroundTaskTracker.Run(taskName, () =>
        {
            token.ThrowIfCancellationRequested();
            var analysis = _readAnalysis(path);
            token.ThrowIfCancellationRequested();
            return analysis;
        }, token);
        ImageColorAnalysis analysis;
        string? statusOverride = null;

        try
        {
            analysis = await readTask
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
            _ = ObserveCompletionAsync(readTask);
            analysis = ImageColorAnalysis.Empty;
            statusOverride = Strings.ColorAnalysisTimeout;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Color analysis failed for {File}", Path.GetFileName(path));
            analysis = new ImageColorAnalysis([], Strings.ColorAnalysisUnavailable, "");
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

                Rows.Clear();
                foreach (var row in analysis.Rows)
                    Rows.Add(row);

                IsLoading = false;
                StatusText = statusOverride ?? analysis.StatusText;
                WarningText = analysis.WarningText;
                RaiseStateChanged();
            });
        }
        catch (TaskCanceledException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static async Task ObserveCompletionAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static async Task DisposeSourceLaterAsync(CancellationTokenSource source)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        finally
        {
            source.Dispose();
        }
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _generation++;
        _analysisCts.Cancel();
        _ = DisposeSourceLaterAsync(_analysisCts);
    }
}
