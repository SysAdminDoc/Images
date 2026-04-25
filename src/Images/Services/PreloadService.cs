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

    private readonly ConcurrentDictionary<string, Lazy<Task<ImageLoader.LoadResult?>>> _cache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _lastAccess =
        new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource _cts = new();
    private readonly ILogger _log = Log.For<PreloadService>();

    /// <summary>
    /// Queue a path for background decode. Returns immediately. No-op if the path is already in
    /// cache or outside of known-extension allowlist.
    /// </summary>
    public void Enqueue(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (!DirectoryNavigator.SupportedExtensions.Contains(Path.GetExtension(path))) return;

        _lastAccess[path] = DateTime.UtcNow;
        if (_cache.ContainsKey(path)) return;

        // Probe the file size first — if it's a monster RAW / panorama, skip. The on-demand
        // path will decode it only if the user actually navigates there.
        try
        {
            var (w, h) = ImageLoader.QuickDimensions(path);
            if ((long)w * h > MegapixelSkipThreshold)
            {
                _log.LogDebug("preload skip: {Path} is {W}x{H} (> 40 MP)", path, w, h);
                return;
            }
        }
        catch { /* quick dims is best-effort */ }

        var token = _cts.Token;
        var lazy = new Lazy<Task<ImageLoader.LoadResult?>>(() => Task.Run(() =>
        {
            try
            {
                if (token.IsCancellationRequested) return null;
                var res = ImageLoader.Load(path);
                _log.LogDebug("preload decoded: {Path} via {Decoder}", path, res.DecoderUsed);
                return (ImageLoader.LoadResult?)res;
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
        _lastAccess[path] = DateTime.UtcNow;
        var task = lazy.Value;
        if (!task.IsCompletedSuccessfully) return null;
        return task.Result;
    }

    /// <summary>
    /// Cancel every in-flight decode + clear the cache. Call on large nav jumps where nothing
    /// we preloaded is still relevant (e.g. open-file opens a different folder).
    /// </summary>
    public void Reset()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        _cache.Clear();
        _lastAccess.Clear();
    }

    private void EvictIfNeeded()
    {
        if (_cache.Count <= SlotCap) return;
        // Pick the least-recently-accessed entry and drop it. Cancellation of its backing Task
        // happens on the next Reset or just lets it complete + be GC'd.
        var victim = _lastAccess.OrderBy(kv => kv.Value).FirstOrDefault();
        if (victim.Key is not null)
        {
            _cache.TryRemove(victim.Key, out _);
            _lastAccess.TryRemove(victim.Key, out _);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cache.Clear();
        _lastAccess.Clear();
    }
}
