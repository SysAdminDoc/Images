using System.IO;
using Microsoft.Extensions.Logging;

namespace Images.Services;

/// <summary>
/// Keeps registered catalog folders fresh without owning any UI. A full scan runs at startup,
/// FileSystemWatcher events are debounced into one rebuild, and a light registry poll discovers
/// roots added by another process. Offline roots stay registered and their cached rows are kept.
/// </summary>
public sealed class CatalogWatchService : IDisposable
{
    private static readonly ILogger Log = Images.Services.Log.Get(nameof(CatalogWatchService));
    private readonly CatalogService _catalog;
    private readonly TimeSpan _debounce;
    private readonly TimeSpan _rootPollInterval;
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private readonly object _watcherGate = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly List<FileSystemWatcher> _watchers = [];
    private CancellationTokenSource? _debounceCancellation;
    private Task? _monitorTask;
    private bool _started;
    private bool _disposed;

    public CatalogWatchService()
        : this(new CatalogService(), TimeSpan.FromMilliseconds(750), TimeSpan.FromSeconds(30))
    {
    }

    internal CatalogWatchService(CatalogService catalog, TimeSpan debounce, TimeSpan rootPollInterval)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _debounce = debounce;
        _rootPollInterval = rootPollInterval;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
            return;

        _started = true;
        CatalogService.RootRegistryChanged += CatalogRootRegistryChanged;
        await RefreshNowAsync(cancellationToken).ConfigureAwait(false);
        _monitorTask = BackgroundTaskTracker.Queue("catalog-root-monitor", MonitorRootsAsync, _shutdown.Token);
    }

    internal async Task<CatalogRebuildResult?> RefreshNowAsync(CancellationToken cancellationToken = default)
    {
        if (!_catalog.IsAvailable)
            return null;

        await _scanGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var roots = _catalog.GetRoots();
            var result = await Task.Run(
                () => _catalog.Rebuild(roots.Select(root => root.RootPath), cancellationToken),
                cancellationToken).ConfigureAwait(false);
            if (!_shutdown.IsCancellationRequested)
                ConfigureWatchers(_catalog.GetRoots());
            Log.LogInformation(
                "Catalog refresh completed: {IndexedCount} assets, {OfflineCount} offline roots",
                result.IndexedCount,
                result.OfflineRoots.Count);
            return result;
        }
        finally
        {
            _scanGate.Release();
        }
    }

    private async Task MonitorRootsAsync()
    {
        using var timer = new PeriodicTimer(_rootPollInterval);
        while (await timer.WaitForNextTickAsync(_shutdown.Token).ConfigureAwait(false))
            await RefreshNowAsync(_shutdown.Token).ConfigureAwait(false);
    }

    private void ConfigureWatchers(IReadOnlyList<CatalogRootRecord> roots)
    {
        lock (_watcherGate)
        {
            foreach (var watcher in _watchers)
                watcher.Dispose();
            _watchers.Clear();

            foreach (var root in roots.Where(root => root.IsOnline && Directory.Exists(root.RootPath)))
            {
                try
                {
                    var watcher = new FileSystemWatcher(root.RootPath)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                        EnableRaisingEvents = false
                    };
                    watcher.Created += CatalogChanged;
                    watcher.Changed += CatalogChanged;
                    watcher.Deleted += CatalogChanged;
                    watcher.Renamed += CatalogChanged;
                    watcher.Error += CatalogWatcherError;
                    watcher.EnableRaisingEvents = true;
                    _watchers.Add(watcher);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    Log.LogWarning(ex, "Could not watch catalog root {Root}", root.RootPath);
                }
            }
        }
    }

    private void CatalogChanged(object sender, FileSystemEventArgs e)
    {
        var renamedSupportedSource = e is RenamedEventArgs renamed && SupportedImageFormats.IsSupported(renamed.OldFullPath);
        if (e.ChangeType != WatcherChangeTypes.Deleted &&
            !SupportedImageFormats.IsSupported(e.FullPath) &&
            !Directory.Exists(e.FullPath) &&
            !renamedSupportedSource)
            return;
        ScheduleRefresh();
    }

    private void CatalogWatcherError(object sender, ErrorEventArgs e)
    {
        Log.LogWarning(e.GetException(), "Catalog watcher overflowed; scheduling a full refresh");
        ScheduleRefresh();
    }

    private void CatalogRootRegistryChanged(object? sender, string catalogPath)
    {
        if (string.Equals(catalogPath, _catalog.CatalogPath, StringComparison.OrdinalIgnoreCase))
            ScheduleRefresh();
    }

    private void ScheduleRefresh()
    {
        if (_disposed || _shutdown.IsCancellationRequested)
            return;

        var next = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        var previous = Interlocked.Exchange(ref _debounceCancellation, next);
        previous?.Cancel();
        previous?.Dispose();

        _ = BackgroundTaskTracker.Queue("catalog-delta-rescan", async () =>
        {
            try
            {
                await Task.Delay(_debounce, next.Token).ConfigureAwait(false);
                await RefreshNowAsync(next.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (next.IsCancellationRequested)
            {
            }
            finally
            {
                if (ReferenceEquals(Interlocked.CompareExchange(ref _debounceCancellation, null, next), next))
                    next.Dispose();
            }
        }, next.Token);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        CatalogService.RootRegistryChanged -= CatalogRootRegistryChanged;
        _shutdown.Cancel();
        var debounce = Interlocked.Exchange(ref _debounceCancellation, null);
        debounce?.Cancel();
        debounce?.Dispose();
        lock (_watcherGate)
        {
            foreach (var watcher in _watchers)
                watcher.Dispose();
            _watchers.Clear();
        }
        _shutdown.Dispose();
    }
}
