using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using Microsoft.Extensions.Logging;

namespace Images.Services;

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
    private readonly ILogger _log = Log.For<ThumbnailCache>();

    public static readonly ThumbnailCache Instance = new(DefaultRoot(), DefaultThumbSize, DefaultDiskCapBytes);

    private static string DefaultRoot()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Images", "thumbs");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public ThumbnailCache(string root, int thumbSize, long diskCapBytes)
    {
        _root = root;
        _thumbSize = thumbSize;
        _capBytes = diskCapBytes;
    }

    /// <summary>
    /// Return the path a thumbnail for <paramref name="sourcePath"/> would occupy. Creates the
    /// partition subdir. Does NOT create the file; use <see cref="TryGet"/> or
    /// <see cref="GenerateAndCache"/> for that.
    /// </summary>
    public string GetCachePath(string sourcePath, long mtimeTicks, long sizeBytes)
    {
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
    public string? GenerateAndCache(string sourcePath)
    {
        try
        {
            var fi = new FileInfo(sourcePath);
            if (!fi.Exists) return null;
            var cachePath = GetCachePath(sourcePath, fi.LastWriteTimeUtc.Ticks, fi.Length);
            if (File.Exists(cachePath)) return cachePath;

            CodecRuntime.Configure();
            using var image = new ImageMagick.MagickImage(sourcePath);
            image.Resize(new ImageMagick.MagickGeometry((uint)_thumbSize, (uint)_thumbSize) { Greater = true });
            image.Strip(); // drop EXIF + XMP — thumbs don't need it, reduces bytes
            image.Quality = 80;
            image.Format = ImageMagick.MagickFormat.WebP;
            image.Write(cachePath);

            _log.LogDebug("thumb cached: {Cache} ({Bytes} bytes) from {Src}", cachePath, new FileInfo(cachePath).Length, sourcePath);
            return cachePath;
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "GenerateAndCache failed for {Path}", sourcePath);
            return null;
        }
    }

    /// <summary>
    /// Gets a frozen WPF thumbnail image, generating the cache entry first when needed.
    /// </summary>
    public ImageSource? GetOrCreateImageSource(string sourcePath)
    {
        try
        {
            var fi = new FileInfo(sourcePath);
            if (!fi.Exists) return null;

            var cachePath = GetCachePath(sourcePath, fi.LastWriteTimeUtc.Ticks, fi.Length);
            if (!File.Exists(cachePath))
                cachePath = GenerateAndCache(sourcePath);

            if (cachePath is null || !File.Exists(cachePath)) return null;

            return ImageLoader.Load(cachePath).Image;
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "GetOrCreateImageSource failed for {Path}", sourcePath);
            return null;
        }
    }

    /// <summary>
    /// If the cache is over capacity, remove LRU entries until it's back under. Pure disk
    /// bookkeeping — call from a background thread.
    /// </summary>
    public void EvictIfOverCap()
    {
        try
        {
            if (!Directory.Exists(_root)) return;
            var files = Directory.EnumerateFiles(_root, "*.webp", SearchOption.AllDirectories)
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

    private static string ComputeKey(string sourcePath, long mtimeTicks, long sizeBytes)
    {
        var raw = sourcePath.ToLowerInvariant() + "|" + mtimeTicks + "|" + sizeBytes;
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = SHA1.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
