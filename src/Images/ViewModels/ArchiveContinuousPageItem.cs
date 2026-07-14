using System.Windows.Input;
using System.Windows.Media.Imaging;
using Images.Localization;

namespace Images.ViewModels;

/// <summary>
/// Lightweight page descriptor for the virtualized archive reader. Pixel data is acquired only
/// while the item is realized and is released again when WPF recycles its container.
/// </summary>
public sealed class ArchiveContinuousPageItem : ObservableObject, IDisposable
{
    private readonly Func<int, CancellationToken, Task<BitmapSource>> _loadPageAsync;
    private CancellationTokenSource? _loadCancellation;
    private int _loadGeneration;
    private BitmapSource? _image;
    private bool _isLoading;
    private string? _errorMessage;

    public ArchiveContinuousPageItem(
        int pageIndex,
        int pageCount,
        Func<int, CancellationToken, Task<BitmapSource>> loadPageAsync)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageCount, 1);
        if (pageIndex >= pageCount)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));

        PageIndex = pageIndex;
        PageCount = pageCount;
        _loadPageAsync = loadPageAsync ?? throw new ArgumentNullException(nameof(loadPageAsync));
        RetryCommand = new RelayCommand(() => _ = RetryAsync(), () => HasError && !IsLoading);
    }

    public int PageIndex { get; }
    public int PageNumber => PageIndex + 1;
    public int PageCount { get; }
    public string PageLabel => Strings.Format(
        nameof(Strings.MainArchiveContinuousPageLabel),
        PageNumber,
        PageCount);

    public BitmapSource? Image
    {
        get => _image;
        private set
        {
            if (Set(ref _image, value))
                Raise(nameof(HasImage));
        }
    }

    public bool HasImage => Image is not null;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (Set(ref _isLoading, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (Set(ref _errorMessage, value))
            {
                Raise(nameof(HasError));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public ICommand RetryCommand { get; }

    public void Seed(BitmapSource image)
    {
        ArgumentNullException.ThrowIfNull(image);
        CancelPendingLoad();
        ErrorMessage = null;
        Image = image;
    }

    public Task EnsureLoadedAsync()
        => Image is not null || IsLoading || HasError ? Task.CompletedTask : LoadAsync(clearError: false);

    public Task RetryAsync()
        => IsLoading ? Task.CompletedTask : LoadAsync(clearError: true);

    private async Task LoadAsync(bool clearError)
    {
        CancelPendingLoad();
        if (clearError)
            ErrorMessage = null;

        var generation = ++_loadGeneration;
        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        IsLoading = true;

        try
        {
            var image = await _loadPageAsync(PageIndex, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (generation == _loadGeneration)
            {
                ErrorMessage = null;
                Image = image;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Recycling is expected and should not turn into a visible error.
        }
        catch (Exception)
        {
            if (generation == _loadGeneration)
                ErrorMessage = Strings.MainArchiveContinuousPageError;
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                IsLoading = false;
                if (ReferenceEquals(_loadCancellation, cancellation))
                    _loadCancellation = null;
            }
            cancellation.Dispose();
        }
    }

    /// <summary>Cancel pending work and release decoded pixels when WPF recycles this item.</summary>
    public void Release()
    {
        CancelPendingLoad();
        Image = null;
        IsLoading = false;
    }

    private void CancelPendingLoad()
    {
        _loadGeneration++;
        var cancellation = Interlocked.Exchange(ref _loadCancellation, null);
        if (cancellation is null)
            return;

        cancellation.Cancel();
    }

    public void Dispose() => Release();
}
