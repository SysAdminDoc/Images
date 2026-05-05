using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using ImageMagick;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public sealed record ThumbnailCacheHealth(
    bool IsAvailable,
    string? Root,
    long Bytes,
    int FileCount,
    int TempFileCount,
    long CapBytes,
    DateTime? LastEvictionSweepUtc,
    string? Error = null);

public sealed record ThumbnailCacheClearResult(
    bool IsAvailable,
    string? Root,
    int DeletedCount,
    long DeletedBytes,
    int FailedCount);

/// <summary>
/// V20-04: on-disk thumbnail cache at <c>%LOCALAPPDATA%\Images\thumbs\<AB>\<full-hash>.webp</c>.
/// Keyed by SHA1(path.lower() + mtime_ticks + size_bytes) so the key survives path renames
/// (different key ⇒ different thumb) and file edits (different mtime ⇒ different thumb).
///
/// Git-like 2-char partition directory avoids a single 100K-entry directory on large libraries.
///
/// Per SCH-01: the cache is disposable. Deleting the thumbs dir is always a safe recovery —
/// everything rebuilds from originals on next access.
///
/// The main viewer consumes this as a compact folder preview strip. Cached WebP files stay
/// disposable implementation detail; callers receive frozen WPF image sources.
/// </summary>
public sealed class ThumbnailCache
{
    public const int DefaultThumbSize = 256;
    public const long DefaultDiskCapBytes = 512L * 1024 * 1024; // 512 MB

    private readonly string _root;
    private readonly int _thumbSize;
    private readonly long _capBytes;
    private readonly bool _isAvailable;
    private readonly ILogger _log = Log.For<ThumbnailCache>();
    private readonly object _evictionSync = new();
    private DateTime _lastEvictionSweepUtc = DateTime.MinValue;

    public static readonly ThumbnailCache Instance = CreateDefault();

    private static ThumbnailCache CreateDefault()
        => CreateDefault(() => AppStorage.TryGetAppDirectory("thumbs"));

    internal static ThumbnailCache CreateDefault(Func<string?> getRoot)
    {
        ArgumentNullException.ThrowIfNull(getRoot);

        var root = getRoot();
        return root is null
            ? new ThumbnailCache(string.Empty, DefaultThumbSize, DefaultDiskCapBytes, isAvailable: false)
            : new ThumbnailCache(root, DefaultThumbSize, DefaultDiskCapBytes);
    }

    public ThumbnailCache(string root, int thumbSize, long diskCapBytes)
        : this(root, thumbSize, diskCapBytes, isAvailable: true)
    {
    }

    private ThumbnailCache(string root, int thumbSize, long diskCapBytes, bool isAvailable)
    {
        _root = root;
        _thumbSize = thumbSize;
        _capBytes = diskCapBytes;
        _isAvailable = isAvailable;
    }

    /// <summary>
    /// Return the path a thumbnail for <paramref name="sourcePath"/> would occupy. Creates the
    /// partition subdir. Does NOT create the file; use <see cref="TryGet"/> or
    /// <see cref="GenerateAndCache"/> for that.
    /// </summary>
    public string GetCachePath(string sourcePath, long mtimeTicks, long sizeBytes)
    {
        if (!_isAvailable)
            throw new InvalidOperationException("Thumbnail cache storage is not available.");

        var key = ComputeKey(sourcePath, mtimeTicks, sizeBytes);
        var partition = key[..2];
        var dir = Path.Combine(_root, partition);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, key + ".webp");
    }

    /// <summary>
    /// Fast-path: if a fresh thumb is already cached, return its bytes; else null.
    /// </summary>
    public byte[]? TryGet(string sourcePath)
    {
        if (!_isAvailable) return null;

        try
        {
            var fi = new FileInfo(sourcePath);
            if (!fi.Exists) return null;
            var cachePath = GetCachePath(sourcePath, fi.LastWriteTimeUtc.Ticks, fi.Length);
            return File.Exists(cachePath) ? File.ReadAllBytes(cachePath) : null;
        }
        catch (IOException ex)
        {
            _log.LogDebug(ex, "TryGet failed for {Path}", sourcePath);
            return null;
        }
    }

    /// <summary>
    /// Decode <paramref name="sourcePath"/> via Magick.NET, resize to the cache thumb size with
    /// aspect ratio preserved (longest edge = <see cref="_thumbSize"/>), re-encode as WebP, write
    /// to the cache path. Returns the cache path on success, null on any failure.
    /// </summary>
    public string? GenerateAndCache(string sourcePath, CancellationToken ct = default)
    {
        if (!_isAvailable) return null;

        try
        {
            ct.ThrowIfCancellationRequested();
            var fi = new FileInfo(sourcePath);
            if (!fi.Exists) return null;
            var cachePath = GetCachePath(sourcePath, fi.LastWriteTimeUtc.Ticks, fi.Length);
            if (File.Exists(cachePath)) return cachePath;

            CodecRuntime.Configure();
            ct.ThrowIfCancellationRequested();
            using var image = ReadFirstFrameForThumbnail(sourcePath);
            ct.ThrowIfCancellationRequested();
            image.Resize(new MagickGeometry((uint)_thumbSize, (uint)_thumbSize) { Greater = true });
            image.Strip(); // drop EXIF + XMP — thumbs don't need it, reduces bytes
            image.Quality = 80;
            image.Format = MagickFormat.WebP;
            ct.ThrowIfCancellationRequested();
            cachePath = WriteAtomically(image, cachePath);

            _log.LogDebug("thumb cached: {Cache} ({Bytes} bytes) from {Src}", cachePath, new FileInfo(cachePath).Length, sourcePath);
            ScheduleEvictionSweep();
            return cachePath;
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "GenerateAndCache failed for {Path}", sourcePath);
            return null;
        }
    }

    private static string WriteAtomically(MagickImage image, string cachePath)
    {
        var dir = Path.GetDirectoryName(cachePath) ?? throw new IOException("Thumbnail cache path has no directory.");
        Directory.CreateDirectory(dir);

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(cachePath);
        var tempPath = Path.Combine(dir, $"{nameWithoutExtension}.tmp-{Guid.NewGuid():N}.webp");

        try
        {
            image.Write(tempPath);

            try
            {
                File.Move(tempPath, cachePath);
                return cachePath;
            }
            catch (IOException) when (File.Exists(cachePath))
            {
                // Another navigation task or app instance completed the same thumbnail first.
                // Keep the complete winner and discard our duplicate temp file.
                return cachePath;
            }
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private static MagickImage ReadFirstFrameForThumbnail(string sourcePath)
    {
        var settings = new MagickReadSettings
        {
            FrameIndex = 0,
            FrameCount = 1,
            BackgroundColor = MagickColors.White
        };

        if (SupportedImageFormats.RequiresGhostscript(sourcePath))
            settings.Density = new Density(72);

        using var frames = new MagickImageCollection(new FileInfo(sourcePath), settings);
        if (frames.Count == 0)
            throw new InvalidOperationException("No thumbnail frame was decoded.");

        var image = (MagickImage)frames[0].Clone();

        if (SupportedImageFormats.RequiresGhostscript(sourcePath))
        {
            image.BackgroundColor = MagickColors.White;
            image.Alpha(AlphaOption.Remove);
        }

        return image;
    }

    private void ScheduleEvictionSweep()
    {
        lock (_evictionSync)
        {
            if ((DateTime.UtcNow - _lastEvictionSweepUtc) < TimeSpan.FromMinutes(5))
                return;

            _lastEvictionSweepUtc = DateTime.UtcNow;
        }

        _ = BackgroundTaskTracker.Queue("thumbnail-cache-eviction", EvictIfOverCap);
    }

    /// <summary>
    /// Gets a frozen WPF thumbnail image, generating the cache entry first when needed.
    /// </summary>
    public ImageSource? GetOrCreateImageSource(string sourcePath, CancellationToken ct = default)
    {
        if (!_isAvailable) return null;

        try
        {
            ct.ThrowIfCancellationRequested();
            var fi = new FileInfo(sourcePath);
            if (!fi.Exists) return null;

            var cachePath = GetCachePath(sourcePath, fi.LastWriteTimeUtc.Ticks, fi.Length);
            if (!File.Exists(cachePath))
                cachePath = GenerateAndCache(sourcePath, ct);

            ct.ThrowIfCancellationRequested();
            if (cachePath is null || !File.Exists(cachePath)) return null;

            return ImageLoader.Load(cachePath).Image;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "GetOrCreateImageSource failed for {Path}", sourcePath);
            return null;
        }
    }

    public ThumbnailCacheHealth GetHealth()
    {
        if (!_isAvailable)
            return new ThumbnailCacheHealth(false, null, 0, 0, 0, _capBytes, null);

        try
        {
            if (!Directory.Exists(_root))
            {
                return new ThumbnailCacheHealth(
                    true,
                    _root,
                    0,
                    0,
                    0,
                    _capBytes,
                    LastEvictionSweepForHealth());
            }

            long bytes = 0;
            var files = 0;
            var tempFiles = 0;

            foreach (var path in Directory.EnumerateFiles(_root, "*.webp", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(path);
                var isTemp = name.Contains(".tmp-", StringComparison.OrdinalIgnoreCase);

                try
                {
                    var info = new FileInfo(path);
                    if (!info.Exists) continue;

                    if (isTemp)
                    {
                        tempFiles++;
                    }
                    else
                    {
                        files++;
                        bytes += info.Length;
                    }
                }
                catch
                {
                    // A thumbnail may be deleted while diagnostics scans the disposable cache.
                }
            }

            return new ThumbnailCacheHealth(
                true,
                _root,
                bytes,
                files,
                tempFiles,
                _capBytes,
                LastEvictionSweepForHealth());
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Thumbnail cache health scan failed");
            return new ThumbnailCacheHealth(
                false,
                _root,
                0,
                0,
                0,
                _capBytes,
                LastEvictionSweepForHealth(),
                ex.Message);
        }
    }

    public ThumbnailCacheClearResult Clear()
    {
        if (!_isAvailable)
            return new ThumbnailCacheClearResult(false, null, 0, 0, 0);

        if (!Directory.Exists(_root))
            return new ThumbnailCacheClearResult(true, _root, 0, 0, 0);

        var deleted = 0;
        long deletedBytes = 0;
        var failed = 0;
        List<string> files;

        try
        {
            files = Directory.EnumerateFiles(_root, "*.webp", SearchOption.AllDirectories).ToList();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not enumerate thumbnail cache for clearing");
            return new ThumbnailCacheClearResult(true, _root, 0, 0, 1);
        }

        foreach (var path in files)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists) continue;

                var length = info.Length;
                info.Delete();
                deleted++;
                deletedBytes += length;
            }
            catch (Exception ex)
            {
                failed++;
                _log.LogDebug(ex, "Could not delete thumbnail cache file {Path}", path);
            }
        }

        DeleteEmptyCacheDirectories();
        return new ThumbnailCacheClearResult(true, _root, deleted, deletedBytes, failed);
    }

    /// <summary>
    /// If the cache is over capacity, remove LRU entries until it's back under. Pure disk
    /// bookkeeping — call from a background thread.
    /// </summary>
    public void EvictIfOverCap()
    {
        if (!_isAvailable) return;

        try
        {
            if (!Directory.Exists(_root)) return;
            DeleteStaleTempFiles();

            var files = Directory.EnumerateFiles(_root, "*.webp", SearchOption.AllDirectories)
                                 .Where(p => !Path.GetFileName(p).Contains(".tmp-", StringComparison.OrdinalIgnoreCase))
                                 .Select(p => new FileInfo(p))
                                 .OrderBy(fi => fi.LastAccessTimeUtc)
                                 .ToList();
            long total = 0;
            foreach (var f in files) total += f.Length;
            if (total <= _capBytes) return;

            _log.LogInformation("thumb cache over cap ({Total} MB / {Cap} MB) — evicting LRU",
                total / (1024 * 1024), _capBytes / (1024 * 1024));
            foreach (var f in files)
            {
                if (total <= _capBytes) break;
                try { total -= f.Length; f.Delete(); } catch { /* best-effort */ }
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "EvictIfOverCap failed");
        }
    }

    private void DeleteStaleTempFiles()
    {
        try
        {
            var cutoff = DateTime.UtcNow - TimeSpan.FromHours(1);
            foreach (var path in Directory.EnumerateFiles(_root, "*.tmp-*.webp", SearchOption.AllDirectories))
            {
                try
                {
                    var fi = new FileInfo(path);
                    if (fi.LastWriteTimeUtc < cutoff)
                        fi.Delete();
                }
                catch
                {
                    // Best-effort cleanup; temp files are disposable cache artifacts.
                }
            }
        }
        catch
        {
            // Best-effort cleanup; eviction itself can still continue.
        }
    }

    private void DeleteEmptyCacheDirectories()
    {
        try
        {
            if (!Directory.Exists(_root)) return;

            foreach (var directory in Directory.EnumerateDirectories(_root, "*", SearchOption.AllDirectories)
                         .OrderByDescending(path => path.Length))
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(directory).Any())
                        Directory.Delete(directory);
                }
                catch
                {
                    // Best-effort cleanup; empty partition folders are harmless.
                }
            }
        }
        catch
        {
            // Best-effort cleanup; the cache files were already handled above.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Disposable temp file; a later eviction sweep removes stale leftovers.
        }
    }

    private static string ComputeKey(string sourcePath, long mtimeTicks, long sizeBytes)
    {
        var raw = sourcePath.ToLowerInvariant() + "|" + mtimeTicks + "|" + sizeBytes;
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = SHA1.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private DateTime? LastEvictionSweepForHealth()
        => _lastEvictionSweepUtc == DateTime.MinValue ? null : _lastEvictionSweepUtc;
}
