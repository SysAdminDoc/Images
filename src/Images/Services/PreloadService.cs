using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Images.Services;

/// <summary>
/// V20-03: decodes prev / next image in background so navigation feels instant. Bounded at
/// 3 slots (N-1, N, N+1). Cancellation-friendly — every nav request cancels outstanding decodes
/// so we don't decode images the user already moved past.
///
/// Eviction is LRU by access timestamp: slot occupancy never exceeds the cap because every
/// enqueue shifts one out.
///
/// Memory pressure guard: very large images (> 40 megapixels) skip preload entirely — an
/// on-demand load is faster than burning hundreds of MB of managed heap prefetching a RAW or
/// panorama that the user might not even look at.
/// </summary>
public sealed class PreloadService : IDisposable
{
    private const int SlotCap = 3;
    private const long MegapixelSkipThreshold = 40L * 1_000_000;
    private const long FileSizeSkipThreshold = 128L * 1024 * 1024;

    private readonly ConcurrentDictionary<string, Lazy<Task<ImageLoader.LoadResult?>>> _cache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _lastAccess =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (DateTime WriteTimeUtc, long Length)> _sourceStamp =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _entryCts =
        new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource _cts = new();
    private readonly object _ctsGate = new();
    private volatile bool _isDisposed;
    private readonly ILogger _log = Log.For<PreloadService>();

    /// <summary>
    /// Queue a path for background decode. Returns immediately. No-op if the path is already in
    /// cache or outside of known-extension allowlist.
    /// </summary>
    public void Enqueue(string? path)
    {
        if (_isDisposed) return;
        if (string.IsNullOrEmpty(path)) return;
        if (!DirectoryNavigator.SupportedExtensions.Contains(Path.GetExtension(path))) return;
        if (SupportedImageFormats.RequiresGhostscript(path)) return;

        _lastAccess[path] = DateTime.UtcNow;
        if (_cache.TryGetValue(path, out var existing))
        {
            if (!existing.IsValueCreated)
                return;
            var task = existing.Value;
            if (!task.IsFaulted && !task.IsCanceled)
                return;
            _cache.TryRemove(path, out _);
            _log.LogDebug("preload evicted faulted entry: {Path}", path);
        }

        // Probe size first — if it's a monster RAW / panorama / layered production file, skip.
        // The on-demand path will decode it only if the user actually navigates there.
        // Skip paths must drop the _lastAccess entry set above: a phantom key with no
        // matching _cache entry becomes the LRU victim on every eviction and lets the
        // decoded-image cache grow past its slot cap.
        try
        {
            var fi = new FileInfo(path);
            if (!fi.Exists || fi.Length >= FileSizeSkipThreshold)
            {
                _log.LogDebug("preload skip: {Path} is {Bytes} bytes", path, fi.Exists ? fi.Length : 0);
                _lastAccess.TryRemove(path, out _);
                return;
            }

            var (w, h) = ImageLoader.QuickDimensions(path);
            if ((long)w * h > MegapixelSkipThreshold)
            {
                _log.LogDebug("preload skip: {Path} is {W}x{H} (> 40 MP)", path, w, h);
                _lastAccess.TryRemove(path, out _);
                return;
            }

            _sourceStamp[path] = (fi.LastWriteTimeUtc, fi.Length);
        }
        catch { /* quick dims is best-effort */ }

        CancellationTokenSource entryCts;
        lock (_ctsGate)
        {
            if (_isDisposed) return;
            entryCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        }
        var token = entryCts.Token;
        if (_entryCts.TryRemove(path, out var oldEntryCts))
        {
            oldEntryCts.Cancel();
            _ = DisposeCanceledSourceLaterAsync(oldEntryCts);
        }
        _entryCts[path] = entryCts;
        var taskName = $"preload-decode:{Path.GetFileName(path)}";
        var lazy = new Lazy<Task<ImageLoader.LoadResult?>>(() => BackgroundTaskTracker.Run(taskName, () =>
        {
            try
            {
                token.ThrowIfCancellationRequested();
                var res = ImageLoader.Load(path);
                token.ThrowIfCancellationRequested();
                _log.LogDebug("preload decoded: {Path} via {Decoder}", path, res.DecoderUsed);
                return (ImageLoader.LoadResult?)res;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "preload failed for {Path}", path);
                return null;
            }
        }, token));
        _cache[path] = lazy;

        // Kick off the Task right away.
        _ = lazy.Value;

        EvictIfNeeded();
    }

    /// <summary>
    /// Synchronously return a cached decode if it's finished; null if not in cache or still in
    /// flight (MainViewModel falls through to the direct load path on null).
    /// </summary>
    public ImageLoader.LoadResult? TryGet(string path)
    {
        if (!_cache.TryGetValue(path, out var lazy)) return null;
        if (IsStale(path))
        {
            Evict(path);
            return null;
        }
        _lastAccess[path] = DateTime.UtcNow;
        var task = lazy.Value;
        if (task.IsFaulted || task.IsCanceled)
        {
            Evict(path);
            return null;
        }
        if (!task.IsCompletedSuccessfully) return null;
        return task.Result;
    }

    /// <summary>
    /// Return an existing preload task, including one still in flight. Foreground navigation can
    /// await this instead of starting a duplicate decode for the same next/previous image.
    /// </summary>
    public Task<ImageLoader.LoadResult?>? TryGetInFlight(string path)
    {
        if (!_cache.TryGetValue(path, out var lazy)) return null;
        if (IsStale(path))
        {
            Evict(path);
            return null;
        }
        _lastAccess[path] = DateTime.UtcNow;
        var task = lazy.Value;
        if (task.IsFaulted || task.IsCanceled)
        {
            Evict(path);
            return null;
        }
        return task;
    }

    /// <summary>
    /// A cached decode is stale when the file on disk changed after enqueue
    /// (external editor save, in-app writeback). Serving it would show the
    /// pre-edit pixels with a "Reloaded" toast.
    /// </summary>
    private bool IsStale(string path)
    {
        if (!_sourceStamp.TryGetValue(path, out var stamp))
            return false;
        try
        {
            var fi = new FileInfo(path);
            if (!fi.Exists)
                return true;
            return fi.LastWriteTimeUtc != stamp.WriteTimeUtc || fi.Length != stamp.Length;
        }
        catch
        {
            return false;
        }
    }

    private void Evict(string path)
    {
        _cache.TryRemove(path, out _);
        _lastAccess.TryRemove(path, out _);
        _sourceStamp.TryRemove(path, out _);
        if (_entryCts.TryRemove(path, out var cts))
        {
            cts.Cancel();
            _ = DisposeCanceledSourceLaterAsync(cts);
        }
    }

    /// <summary>
    /// Cancel every in-flight decode + clear the cache. Call on large nav jumps where nothing
    /// we preloaded is still relevant (e.g. open-file opens a different folder).
    /// </summary>
    public void Reset()
    {
        CancellationTokenSource old;
        lock (_ctsGate)
        {
            if (_isDisposed) return;
            old = _cts;
            _cts = new CancellationTokenSource();
        }

        old.Cancel();
        _ = DisposeCanceledSourceLaterAsync(old);
        foreach (var kv in _entryCts)
            _ = DisposeCanceledSourceLaterAsync(kv.Value);
        _entryCts.Clear();
        _cache.Clear();
        _lastAccess.Clear();
        _sourceStamp.Clear();
    }

    private void EvictIfNeeded()
    {
        // Victims come from keys actually present in _cache — selecting from
        // _lastAccess alone lets a stale timestamp burn the eviction on a
        // phantom key while the cache stays over cap.
        while (_cache.Count > SlotCap)
        {
            var victim = _cache.Keys
                .OrderBy(k => _lastAccess.TryGetValue(k, out var ts) ? ts : DateTime.MinValue)
                .FirstOrDefault();
            if (victim is null) return;
            Evict(victim);
        }
    }

    public void Dispose()
    {
        CancellationTokenSource old;
        lock (_ctsGate)
        {
            if (_isDisposed) return;
            _isDisposed = true;
            old = _cts;
        }

        old.Cancel();
        old.Dispose();
        foreach (var kv in _entryCts)
            kv.Value.Dispose();
        _entryCts.Clear();
        _cache.Clear();
        _lastAccess.Clear();
        _sourceStamp.Clear();
    }

    private static async Task DisposeCanceledSourceLaterAsync(CancellationTokenSource source)
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
}
