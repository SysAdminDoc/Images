using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using Images.Services;

namespace Images.ViewModels;

public sealed class FolderPreviewController : IDisposable
{
    private readonly Dispatcher _uiDispatcher;
    private readonly Func<bool> _isDisposed;
    private readonly Func<string, CancellationToken, ImageSource?> _loadThumbnail;
    private readonly SemaphoreSlim _thumbnailDecodeGate = new(2);
    private CancellationTokenSource _previewCts = new();
    private int _generation;
    private int _thumbnailFailureCount;

    public FolderPreviewController(
        Dispatcher uiDispatcher,
        Func<bool> isDisposed,
        Func<string, CancellationToken, ImageSource?>? loadThumbnail = null)
    {
        _uiDispatcher = uiDispatcher;
        _isDisposed = isDisposed;
        _loadThumbnail = loadThumbnail ?? ((path, token) => ThumbnailCache.Instance.GetOrCreateImageSource(path, token));
    }

    public ObservableCollection<FolderPreviewItem> Items { get; } = new();

    public int ThumbnailFailureCount => _thumbnailFailureCount;
    public bool HasThumbnailFailures => ThumbnailFailureCount > 0;
    public string ThumbnailFailureStatusText => ThumbnailFailureCount == 1
        ? "One folder thumbnail could not be generated. The image can still be opened."
        : $"{ThumbnailFailureCount} folder thumbnails could not be generated. The images can still be opened.";

    public event EventHandler? StateChanged;

    public void Refresh(IReadOnlyList<string> files, int currentIndex)
    {
        var generation = ++_generation;
        var previousCts = _previewCts;
        previousCts.Cancel();
        _ = DisposeSourceLaterAsync(previousCts);
        _previewCts = new CancellationTokenSource();
        Items.Clear();
        ResetThumbnailFailures();
        RaiseStateChanged();

        if (files.Count < 2 || currentIndex < 0)
            return;

        var token = _previewCts.Token;
        var count = files.Count;

        for (var index = 0; index < count; index++)
        {
            var item = new FolderPreviewItem(
                files[index],
                Path.GetFileName(files[index]),
                $"{index + 1} / {count}",
                index == currentIndex);

            Items.Add(item);
            if (ShouldPreloadThumbnail(count, currentIndex, index))
                QueueThumbnailLoad(item, generation, token);
        }

        RaiseStateChanged();
    }

    public void Clear()
    {
        _generation++;
        _previewCts.Cancel();
        Items.Clear();
        ResetThumbnailFailures();
        RaiseStateChanged();
    }

    public void EnsureThumbnail(FolderPreviewItem? item)
    {
        if (_isDisposed() || item is null || !Items.Contains(item)) return;
        QueueThumbnailLoad(item, _generation, _previewCts.Token);
    }

    public static bool ShouldPreloadThumbnail(int count, int currentIndex, int index)
    {
        if (count <= 9)
            return true;

        var forward = (index - currentIndex + count) % count;
        var backward = (currentIndex - index + count) % count;
        return Math.Min(forward, backward) <= 4;
    }

    private void QueueThumbnailLoad(FolderPreviewItem item, int generation, CancellationToken token)
    {
        if (_isDisposed() || !item.TryMarkThumbnailRequested()) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await _thumbnailDecodeGate.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    if (token.IsCancellationRequested) return;

                    var thumbnail = _loadThumbnail(item.Path, token);
                    if (thumbnail is null || token.IsCancellationRequested) return;

                    _ = _uiDispatcher.InvokeAsync(() =>
                    {
                        if (_isDisposed() || token.IsCancellationRequested || generation != _generation) return;
                        if (!Items.Contains(item)) return;

                        item.Thumbnail = thumbnail;
                    });
                }
                finally
                {
                    _thumbnailDecodeGate.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // Superseded navigation state. The next refresh owns the visible thumbnail set.
            }
            catch (Exception)
            {
                RecordThumbnailFailure(item, generation, token);
            }
        });
    }

    private void RecordThumbnailFailure(
        FolderPreviewItem item,
        int generation,
        CancellationToken token)
    {
        _ = _uiDispatcher.InvokeAsync(() =>
        {
            if (_isDisposed() || token.IsCancellationRequested || generation != _generation) return;
            if (!Items.Contains(item)) return;

            item.MarkThumbnailFailed();
            _thumbnailFailureCount++;
            RaiseStateChanged();
        });
    }

    private void ResetThumbnailFailures()
    {
        _thumbnailFailureCount = 0;
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
        _previewCts.Cancel();
        _ = DisposeSourceLaterAsync(_previewCts);
    }
}

public sealed class FolderPreviewItem : ObservableObject
{
    private ImageSource? _thumbnail;
    private bool _thumbnailRequested;
    private bool _thumbnailFailed;

    public FolderPreviewItem(string path, string fileName, string positionText, bool isCurrent)
    {
        Path = path;
        FileName = fileName;
        PositionText = positionText;
        IsCurrent = isCurrent;
    }

    public string Path { get; }
    public string FileName { get; }
    public string PositionText { get; }
    public bool IsCurrent { get; }

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set
        {
            if (Set(ref _thumbnail, value))
                Raise(nameof(HasThumbnail));
        }
    }

    public bool HasThumbnail => Thumbnail is not null;

    public bool ThumbnailFailed
    {
        get => _thumbnailFailed;
        private set => Set(ref _thumbnailFailed, value);
    }

    public bool TryMarkThumbnailRequested()
    {
        if (_thumbnailRequested || HasThumbnail) return false;
        _thumbnailRequested = true;
        return true;
    }

    public void MarkThumbnailFailed()
    {
        ThumbnailFailed = true;
    }
}
