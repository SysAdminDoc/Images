using System.Collections.ObjectModel;
using System.Windows.Threading;
using Images.Services;

namespace Images.ViewModels;

public sealed class PhotoMetadataController : IDisposable
{
    public const string LoadingStatusText = "Reading photo metadata...";
    public const string EmptyStatusText = "No embedded camera metadata.";
    public const string TimeoutStatusText = "Metadata read timed out.";

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly Dispatcher _uiDispatcher;
    private readonly Func<bool> _isDisposed;
    private readonly Func<string?> _currentPath;
    private readonly Func<string, PhotoMetadata> _readMetadata;
    private readonly TimeSpan _timeout;
    private CancellationTokenSource _metadataCts = new();
    private int _generation;

    public PhotoMetadataController(
        Dispatcher uiDispatcher,
        Func<bool> isDisposed,
        Func<string?> currentPath,
        Func<string, PhotoMetadata>? readMetadata = null,
        TimeSpan? timeout = null)
    {
        _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
        _isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
        _currentPath = currentPath ?? throw new ArgumentNullException(nameof(currentPath));
        _readMetadata = readMetadata ?? ImageMetadataService.Read;
        _timeout = timeout ?? DefaultTimeout;
    }

    public ObservableCollection<MetadataFact> Rows { get; } = new();

    public bool IsLoading { get; private set; }

    public string StatusText { get; private set; } = "";

    public event EventHandler? StateChanged;

    public void Refresh(string path)
    {
        var generation = ++_generation;
        var previousCts = _metadataCts;
        previousCts.Cancel();
        _ = DisposeSourceLaterAsync(previousCts);
        _metadataCts = new CancellationTokenSource();

        Rows.Clear();
        IsLoading = true;
        StatusText = LoadingStatusText;
        RaiseStateChanged();

        _ = LoadAsync(path, generation, _metadataCts.Token);
    }

    public void Clear()
    {
        _generation++;
        var previousCts = _metadataCts;
        previousCts.Cancel();
        _ = DisposeSourceLaterAsync(previousCts);
        _metadataCts = new CancellationTokenSource();

        Rows.Clear();
        IsLoading = false;
        StatusText = "";
        RaiseStateChanged();
    }

    private async Task LoadAsync(string path, int generation, CancellationToken token)
    {
        var readTask = Task.Run(() => _readMetadata(path));
        PhotoMetadata metadata;
        string? statusOverride = null;

        try
        {
            metadata = await readTask
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
            metadata = PhotoMetadata.Empty;
            statusOverride = TimeoutStatusText;
        }
        catch
        {
            metadata = PhotoMetadata.Empty;
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
                foreach (var row in metadata.Rows)
                    Rows.Add(row);

                IsLoading = false;
                StatusText = statusOverride ?? (Rows.Count == 0 ? EmptyStatusText : "");
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
        _metadataCts.Cancel();
        _ = DisposeSourceLaterAsync(_metadataCts);
    }
}
