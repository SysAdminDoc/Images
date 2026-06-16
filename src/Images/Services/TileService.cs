using System.IO;
using System.Security.Cryptography;
using ImageMagick;

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

public static class TileService
{
    public const int DefaultTileSize = 256;
    public const int DefaultOverlap = 1;
    public const long LargeImageThresholdBytes = 256 * 1024 * 1024;
    public const int LargeImageThresholdPixels = 50_000_000;

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
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Source image not found.", sourcePath);

        cacheRoot ??= GetDefaultCacheRoot();
        var sourceKey = ComputeSourceKey(sourcePath);
        var cacheDir = Path.Combine(cacheRoot, sourceKey);

        var infoPath = Path.Combine(cacheDir, "pyramid.json");
        if (File.Exists(infoPath))
        {
            var existing = TryReadPyramidInfo(infoPath);
            if (existing is not null && existing.SourcePath == sourcePath)
                return existing;
        }

        Directory.CreateDirectory(cacheDir);

        CodecRuntime.Configure();
        using var source = new MagickImage();
        source.Read(sourcePath, new MagickReadSettings { FrameIndex = 0, FrameCount = 1 });

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
        return info;
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
        var scale = Math.Pow(2, level - pyramid.MaxLevel);
        var levelWidth = Math.Max(1, (int)Math.Ceiling(pyramid.SourceWidth * scale));
        var levelHeight = Math.Max(1, (int)Math.Ceiling(pyramid.SourceHeight * scale));
        var cols = (int)Math.Ceiling((double)levelWidth / pyramid.TileSize);
        var rows = (int)Math.Ceiling((double)levelHeight / pyramid.TileSize);
        return (cols, rows);
    }

    public static void ClearCache(string sourcePath, string? cacheRoot = null)
    {
        cacheRoot ??= GetDefaultCacheRoot();
        var sourceKey = ComputeSourceKey(sourcePath);
        var cacheDir = Path.Combine(cacheRoot, sourceKey);

        try
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
        catch { }
    }

    private static string GetDefaultCacheRoot()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
            appData = Path.GetTempPath();
        return Path.Combine(appData, "Images", "tiles");
    }

    private static string ComputeSourceKey(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path).ToLowerInvariant();
            var info = new FileInfo(path);
            var input = $"{fullPath}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
            var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
            return Convert.ToHexStringLower(hash)[..16];
        }
        catch
        {
            return Guid.NewGuid().ToString("N")[..16];
        }
    }

    private static void WritePyramidInfo(string path, TilePyramidInfo info)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(info);
        File.WriteAllText(path, json, System.Text.Encoding.UTF8);
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
