using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media.Imaging;
using ImageMagick;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public sealed record TileKey(int Level, int Column, int Row);

public sealed record TilePyramidInfo(
    string SourcePath,
    string CacheDirectory,
    int SourceWidth,
    int SourceHeight,
    int TileSize,
    int Overlap,
    int MaxLevel,
    int TotalTiles);

public sealed record TileCacheHealth(
    bool Available,
    string? CacheRoot,
    int PyramidCount,
    long TotalBytes,
    long CapBytes,
    DateTime? OldestPyramidUtc);

public static class TileService
{
    public const int DefaultTileSize = 256;
    public const int DefaultOverlap = 1;
    public const long LargeImageThresholdBytes = 256 * 1024 * 1024;
    public const int LargeImageThresholdPixels = 50_000_000;
    public const long DefaultCapBytes = 1024L * 1024 * 1024; // 1 GB
    public const int MaxAgeDays = 30;

    private static readonly Microsoft.Extensions.Logging.ILogger _log =
        Log.Get("Images.TileService");

    public static bool ShouldUseTileEngine(string path)
    {
        try
        {
            var fileSize = new FileInfo(path).Length;
            if (fileSize > LargeImageThresholdBytes) return true;

            CodecRuntime.Configure();
            using var image = new MagickImage();
            image.Ping(path);
            var pixels = (long)image.Width * image.Height;
            return pixels > LargeImageThresholdPixels;
        }
        catch
        {
            return false;
        }
    }

    public static TilePyramidInfo BuildPyramid(
        string sourcePath,
        string? cacheRoot = null,
        int tileSize = DefaultTileSize,
        int overlap = DefaultOverlap,
        int pageIndex = 0,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Source image not found.", sourcePath);

        cacheRoot ??= GetDefaultCacheRoot();
        var sourceKey = ComputeSourceKey(sourcePath, pageIndex);
        var cacheDir = Path.Combine(cacheRoot, sourceKey);

        var infoPath = Path.Combine(cacheDir, "pyramid.json");
        if (File.Exists(infoPath))
        {
            var existing = TryReadPyramidInfo(infoPath);
            if (existing is not null && string.Equals(existing.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                TouchCacheDirectory(existing.CacheDirectory);
                return existing;
            }
        }

        Directory.CreateDirectory(cacheDir);

        try
        {
            CodecRuntime.Configure();
            using var source = new MagickImage();
            source.Read(sourcePath, new MagickReadSettings { FrameIndex = (uint)Math.Max(0, pageIndex), FrameCount = 1 });

            var width = (int)source.Width;
            var height = (int)source.Height;
            var maxLevel = (int)Math.Ceiling(Math.Log2(Math.Max(width, height)));

            var totalTiles = 0;

            for (var level = maxLevel; level >= 0; level--)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var scale = Math.Pow(2, level - maxLevel);
                var levelWidth = Math.Max(1, (int)Math.Ceiling(width * scale));
                var levelHeight = Math.Max(1, (int)Math.Ceiling(height * scale));

                var levelDir = Path.Combine(cacheDir, level.ToString());
                Directory.CreateDirectory(levelDir);

                using var levelImage = (MagickImage)source.Clone();
                levelImage.Resize(new MagickGeometry((uint)levelWidth, (uint)levelHeight)
                {
                    IgnoreAspectRatio = true,
                });

                var cols = (int)Math.Ceiling((double)levelWidth / tileSize);
                var rows = (int)Math.Ceiling((double)levelHeight / tileSize);

                for (var row = 0; row < rows; row++)
                {
                    for (var col = 0; col < cols; col++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var x = col * tileSize - (col > 0 ? overlap : 0);
                        var y = row * tileSize - (row > 0 ? overlap : 0);
                        var w = tileSize + (col > 0 ? overlap : 0) + (col < cols - 1 ? overlap : 0);
                        var h = tileSize + (row > 0 ? overlap : 0) + (row < rows - 1 ? overlap : 0);

                        x = Math.Max(0, x);
                        y = Math.Max(0, y);
                        w = Math.Min(w, levelWidth - x);
                        h = Math.Min(h, levelHeight - y);

                        if (w <= 0 || h <= 0) continue;

                        using var tile = (MagickImage)levelImage.Clone();
                        tile.Crop(new MagickGeometry(x, y, (uint)w, (uint)h));
                        tile.ResetPage();

                        var tilePath = Path.Combine(levelDir, $"{col}_{row}.webp");
                        tile.Quality = 80;
                        tile.Write(tilePath, MagickFormat.WebP);
                        totalTiles++;
                    }
                }
            }

            var info = new TilePyramidInfo(
                SourcePath: sourcePath,
                CacheDirectory: cacheDir,
                SourceWidth: width,
                SourceHeight: height,
                TileSize: tileSize,
                Overlap: overlap,
                MaxLevel: maxLevel,
                TotalTiles: totalTiles);

            WritePyramidInfo(infoPath, info);
            TouchCacheDirectory(cacheDir);
            Task.Run(EvictIfOverCap);
            return info;
        }
        catch
        {
            TryDeleteCacheDirectory(cacheDir);
            throw;
        }
    }

    public static string? GetTilePath(TilePyramidInfo pyramid, TileKey key)
    {
        var tilePath = Path.Combine(
            pyramid.CacheDirectory,
            key.Level.ToString(),
            $"{key.Column}_{key.Row}.webp");

        return File.Exists(tilePath) ? tilePath : null;
    }

    public static MagickImage? LoadTile(TilePyramidInfo pyramid, TileKey key)
    {
        var path = GetTilePath(pyramid, key);
        if (path is null) return null;

        try
        {
            var image = new MagickImage();
            image.Read(path);
            return image;
        }
        catch
        {
            return null;
        }
    }

    public static (int Columns, int Rows) GetLevelDimensions(TilePyramidInfo pyramid, int level)
    {
        var (levelWidth, levelHeight) = GetLevelPixelSize(pyramid, level);
        var cols = (int)Math.Ceiling((double)levelWidth / pyramid.TileSize);
        var rows = (int)Math.Ceiling((double)levelHeight / pyramid.TileSize);
        return (cols, rows);
    }

    public static (int Width, int Height) GetLevelPixelSize(TilePyramidInfo pyramid, int level)
    {
        var clampedLevel = Math.Clamp(level, 0, pyramid.MaxLevel);
        var scale = Math.Pow(2, clampedLevel - pyramid.MaxLevel);
        return (
            Math.Max(1, (int)Math.Ceiling(pyramid.SourceWidth * scale)),
            Math.Max(1, (int)Math.Ceiling(pyramid.SourceHeight * scale)));
    }

    public static int ChooseLevel(TilePyramidInfo pyramid, double viewportWidth, double viewportHeight, double zoomScale)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0 || zoomScale <= 0)
            return Math.Max(0, pyramid.MaxLevel - 4);

        var baselineFit = Math.Min(viewportWidth / Math.Max(1, pyramid.SourceWidth), viewportHeight / Math.Max(1, pyramid.SourceHeight));
        if (!double.IsFinite(baselineFit) || baselineFit <= 0)
            return Math.Max(0, pyramid.MaxLevel - 4);

        var effectiveSourceScale = Math.Clamp(baselineFit * zoomScale, 0.0001, 1.0);
        var level = pyramid.MaxLevel + (int)Math.Ceiling(Math.Log2(effectiveSourceScale));
        return Math.Clamp(level, 0, pyramid.MaxLevel);
    }

    public static IReadOnlyList<TileKey> GetVisibleTiles(
        TilePyramidInfo pyramid,
        int level,
        double viewportWidth,
        double viewportHeight,
        double zoomScale,
        double translateX,
        double translateY,
        double rotation,
        bool flipHorizontal,
        bool flipVertical)
    {
        var clampedLevel = Math.Clamp(level, 0, pyramid.MaxLevel);
        var (columns, rows) = GetLevelDimensions(pyramid, clampedLevel);
        if (columns <= 0 || rows <= 0)
            return [];

        if (viewportWidth <= 0 || viewportHeight <= 0 || zoomScale <= 0 ||
            Math.Abs(rotation % 360) > 0.001 || flipHorizontal || flipVertical)
        {
            return EnumerateTiles(clampedLevel, 0, columns - 1, 0, rows - 1);
        }

        var (levelWidth, levelHeight) = GetLevelPixelSize(pyramid, clampedLevel);
        var fit = Math.Min(viewportWidth / levelWidth, viewportHeight / levelHeight);
        if (!double.IsFinite(fit) || fit <= 0)
            return EnumerateTiles(clampedLevel, 0, columns - 1, 0, rows - 1);

        var renderedWidth = levelWidth * fit;
        var renderedHeight = levelHeight * fit;
        var offsetX = (viewportWidth - renderedWidth) / 2.0;
        var offsetY = (viewportHeight - renderedHeight) / 2.0;
        var centerX = viewportWidth / 2.0;
        var centerY = viewportHeight / 2.0;

        var leftUntransformed = centerX + (0 - centerX - translateX) / zoomScale;
        var rightUntransformed = centerX + (viewportWidth - centerX - translateX) / zoomScale;
        var topUntransformed = centerY + (0 - centerY - translateY) / zoomScale;
        var bottomUntransformed = centerY + (viewportHeight - centerY - translateY) / zoomScale;

        var visibleLeft = Math.Min(leftUntransformed, rightUntransformed);
        var visibleRight = Math.Max(leftUntransformed, rightUntransformed);
        var visibleTop = Math.Min(topUntransformed, bottomUntransformed);
        var visibleBottom = Math.Max(topUntransformed, bottomUntransformed);

        var levelLeft = (visibleLeft - offsetX) / fit;
        var levelRight = (visibleRight - offsetX) / fit;
        var levelTop = (visibleTop - offsetY) / fit;
        var levelBottom = (visibleBottom - offsetY) / fit;

        var firstColumn = Math.Clamp((int)Math.Floor(levelLeft / pyramid.TileSize) - 1, 0, columns - 1);
        var lastColumn = Math.Clamp((int)Math.Floor(levelRight / pyramid.TileSize) + 1, 0, columns - 1);
        var firstRow = Math.Clamp((int)Math.Floor(levelTop / pyramid.TileSize) - 1, 0, rows - 1);
        var lastRow = Math.Clamp((int)Math.Floor(levelBottom / pyramid.TileSize) + 1, 0, rows - 1);

        return EnumerateTiles(clampedLevel, firstColumn, lastColumn, firstRow, lastRow);
    }

    public static BitmapSource? LoadTileBitmap(TilePyramidInfo pyramid, TileKey key)
    {
        using var image = LoadTile(pyramid, key);
        return image is null ? null : MagickToBitmap(image);
    }

    public static void ClearCache(string sourcePath, string? cacheRoot = null, int? pageIndex = null)
    {
        cacheRoot ??= GetDefaultCacheRoot();
        var cacheDirs = pageIndex is int page
            ? [Path.Combine(cacheRoot, ComputeSourceKey(sourcePath, page))]
            : Directory.Exists(cacheRoot)
                ? Directory.EnumerateDirectories(cacheRoot)
                    .Where(dir => TryReadPyramidInfo(Path.Combine(dir, "pyramid.json"))?.SourcePath == sourcePath)
                    .ToList()
                : [];

        foreach (var cacheDir in cacheDirs)
        {
            try
            {
                if (Directory.Exists(cacheDir))
                    Directory.Delete(cacheDir, recursive: true);
            }
            catch (Exception ex) { _log.LogDebug(ex, "tile-cache: could not remove {Dir}", cacheDir); }
        }
    }

    private static string GetDefaultCacheRoot()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
            appData = Path.GetTempPath();
        return Path.Combine(appData, "Images", "tiles");
    }

    private static string ComputeSourceKey(string path, int pageIndex)
    {
        try
        {
            var fullPath = Path.GetFullPath(path).ToLowerInvariant();
            var info = new FileInfo(path);
            var input = $"{fullPath}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|page:{Math.Max(0, pageIndex)}";
            var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
            return Convert.ToHexStringLower(hash)[..16];
        }
        catch
        {
            return Guid.NewGuid().ToString("N")[..16];
        }
    }

    private static IReadOnlyList<TileKey> EnumerateTiles(int level, int firstColumn, int lastColumn, int firstRow, int lastRow)
    {
        var tiles = new List<TileKey>();
        for (var row = firstRow; row <= lastRow; row++)
        {
            for (var col = firstColumn; col <= lastColumn; col++)
                tiles.Add(new TileKey(level, col, row));
        }

        return tiles;
    }

    private static BitmapSource MagickToBitmap(IMagickImage<ushort> image)
    {
        image.Format = MagickFormat.Bgra;
        image.Alpha(AlphaOption.Set);

        var width = (int)image.Width;
        var height = (int)image.Height;
        var stride = checked(width * 4);
        var expectedLength = checked(stride * height);
        var pixels = image.GetPixelsUnsafe().ToByteArray(PixelMapping.BGRA)
                     ?? throw new InvalidOperationException("Magick.NET returned null tile buffer.");

        if (pixels.Length != expectedLength)
            throw new InvalidOperationException("Magick.NET returned an unexpected tile buffer size.");

        var bitmap = new WriteableBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
        bitmap.Lock();
        try
        {
            Marshal.Copy(pixels, 0, bitmap.BackBuffer, pixels.Length);
            bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally
        {
            bitmap.Unlock();
        }

        bitmap.Freeze();
        return bitmap;
    }

    private static void WritePyramidInfo(string path, TilePyramidInfo info)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(info);
        File.WriteAllText(path, json, System.Text.Encoding.UTF8);
    }

    private static void TouchCacheDirectory(string cacheDir)
    {
        try
        {
            if (Directory.Exists(cacheDir))
                Directory.SetLastWriteTimeUtc(cacheDir, DateTime.UtcNow);
        }
        catch
        {
            // Best effort; cache freshness must never block image loading.
        }
    }

    private static void TryDeleteCacheDirectory(string cacheDir)
    {
        try
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
        catch
        {
            // Partial tile caches are disposable and a later clear/eviction can retry.
        }
    }

    public static TileCacheHealth GetHealth()
    {
        var root = GetDefaultCacheRoot();
        if (!Directory.Exists(root))
            return new TileCacheHealth(false, root, 0, 0, DefaultCapBytes, null);

        try
        {
            var dirs = Directory.GetDirectories(root);
            long totalBytes = 0;
            DateTime? oldest = null;

            foreach (var dir in dirs)
            {
                var dirInfo = new DirectoryInfo(dir);
                foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    totalBytes += file.Length;
                    var lastWrite = file.LastWriteTimeUtc;
                    if (oldest is null || lastWrite < oldest)
                        oldest = lastWrite;
                }
            }

            return new TileCacheHealth(true, root, dirs.Length, totalBytes,
                DefaultCapBytes, oldest);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "tile-cache: could not compute health");
            return new TileCacheHealth(true, root, 0, 0, DefaultCapBytes, null);
        }
    }

    public static (int Deleted, long DeletedBytes) ClearAll()
    {
        var root = GetDefaultCacheRoot();
        if (!Directory.Exists(root))
            return (0, 0);

        var deleted = 0;
        long deletedBytes = 0;

        try
        {
            foreach (var dir in Directory.GetDirectories(root))
            {
                var dirInfo = new DirectoryInfo(dir);
                var size = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                    .Sum(f => f.Length);

                try
                {
                    Directory.Delete(dir, recursive: true);
                    deleted++;
                    deletedBytes += size;
                }
                catch (Exception ex) { _log.LogDebug(ex, "tile-cache: could not clear {Dir}", dir); }
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "tile-cache: error during clear");
        }

        return (deleted, deletedBytes);
    }

    public static void EvictIfOverCap()
    {
        var root = GetDefaultCacheRoot();
        if (!Directory.Exists(root)) return;

        try
        {
            var dirs = Directory.GetDirectories(root)
                .Select(d => new DirectoryInfo(d))
                .OrderBy(d => d.LastWriteTimeUtc)
                .ToList();

            long totalBytes = dirs.Sum(d =>
                d.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length));

            var cutoffUtc = DateTime.UtcNow.AddDays(-MaxAgeDays);

            foreach (var dir in dirs)
            {
                if (totalBytes <= DefaultCapBytes && dir.LastWriteTimeUtc >= cutoffUtc)
                    break;

                var size = dir.EnumerateFiles("*", SearchOption.AllDirectories)
                    .Sum(f => f.Length);
                try
                {
                    dir.Delete(recursive: true);
                    totalBytes -= size;
                    _log.LogDebug("tile-cache: evicted {Dir} ({Size} bytes)", dir.Name, size);
                }
                catch (Exception ex) { _log.LogDebug(ex, "tile-cache: could not evict {Dir}", dir.Name); }
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "tile-cache: eviction error");
        }
    }

    private static TilePyramidInfo? TryReadPyramidInfo(string path)
    {
        try
        {
            var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
            return System.Text.Json.JsonSerializer.Deserialize<TilePyramidInfo>(json);
        }
        catch
        {
            return null;
        }
    }
}
