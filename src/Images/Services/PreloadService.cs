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
    private readonly Func<string, PreloadProbeResult> _probeCandidate;

    public PreloadService()
        : this(ProbeCandidate)
    {
    }

    internal PreloadService(Func<string, PreloadProbeResult> probeCandidate)
    {
        _probeCandidate = probeCandidate ?? throw new ArgumentNullException(nameof(probeCandidate));
    }

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
                try
                {
                    var probe = _probeCandidate(path);
                    if (!probe.ShouldPreload)
                    {
                        _log.LogDebug("preload skip: {Path} ({Reason})", path, probe.SkipReason);
                        return null;
                    }

                    if (probe.SourceStamp is { } stamp)
                        _sourceStamp[path] = stamp;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or NotSupportedException or ImageMagick.MagickException)
                {
                    // Candidate probes are advisory. Decode can still succeed when dimensions or
                    // metadata are unavailable, and all probe I/O remains on this worker thread.
                    _log.LogDebug(ex, "preload probe failed for {Path}; trying decode", path);
                }

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
        _lastAccess[path] = DateTime.UtcNow;
        return AwaitValidPreloadAsync(path, lazy);
    }

    private async Task<ImageLoader.LoadResult?> AwaitValidPreloadAsync(
        string path,
        Lazy<Task<ImageLoader.LoadResult?>> lazy)
    {
        var stale = await BackgroundTaskTracker.Run(
            $"preload-stamp:{Path.GetFileName(path)}",
            () => IsStale(path)).ConfigureAwait(false);
        if (stale)
        {
            Evict(path);
            return null;
        }

        var task = lazy.Value;
        if (task.IsFaulted || task.IsCanceled)
        {
            Evict(path);
            return null;
        }

        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private static PreloadProbeResult ProbeCandidate(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists)
            return PreloadProbeResult.Skip("file is missing");
        if (file.Length >= FileSizeSkipThreshold)
            return PreloadProbeResult.Skip($"{file.Length} bytes is at or above the preload limit");

        var (width, height) = ImageLoader.QuickDimensions(path);
        if ((long)width * height > MegapixelSkipThreshold)
            return PreloadProbeResult.Skip($"{width}x{height} exceeds 40 MP");

        return PreloadProbeResult.Allow((file.LastWriteTimeUtc, file.Length));
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

internal readonly record struct PreloadProbeResult(
    bool ShouldPreload,
    (DateTime WriteTimeUtc, long Length)? SourceStamp,
    string SkipReason)
{
    public static PreloadProbeResult Allow((DateTime WriteTimeUtc, long Length) sourceStamp)
        => new(true, sourceStamp, "");

    public static PreloadProbeResult Skip(string reason)
        => new(false, null, reason);
}
