using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public enum ClipboardImportStatus
{
    OpenExistingFile,
    OpenSavedImage,
    NoSupportedFile,
    ImageUnavailable,
    StorageUnavailable,
    SaveFailed,
    NothingImageLike
}

public sealed record ClipboardImportResult(
    ClipboardImportStatus Status,
    string? Path,
    string Message,
    Exception? Exception = null);

public sealed record ClipboardPruneOptions(int MaxCount, long MaxBytes, TimeSpan MaxAge)
{
    public static ClipboardPruneOptions Default { get; } =
        new(200, 256L * 1024 * 1024, TimeSpan.FromDays(7));
}

public interface IClipboardDataSource
{
    bool ContainsFileDropList();
    IReadOnlyList<string> GetFileDropList();
    bool ContainsImage();
    BitmapSource? GetImage();
}

public sealed class WpfClipboardDataSource : IClipboardDataSource
{
    public bool ContainsFileDropList() => Clipboard.ContainsFileDropList();

    public IReadOnlyList<string> GetFileDropList()
    {
        var files = Clipboard.GetFileDropList();
        var result = new List<string>(files.Count);
        foreach (string? file in files)
        {
            if (!string.IsNullOrWhiteSpace(file))
                result.Add(file);
        }

        return result;
    }

    public bool ContainsImage() => Clipboard.ContainsImage();

    public BitmapSource? GetImage() => Clipboard.GetImage();
}

public sealed class ClipboardImportService
{
    private static readonly ILogger _log = Log.For<ClipboardImportService>();

    private readonly IClipboardDataSource _clipboard;
    private readonly Func<string?> _getClipboardDirectory;
    private readonly Func<DateTimeOffset> _getUtcNow;
    private readonly Func<Guid> _newGuid;
    private readonly Action<string> _queuePrune;

    public ClipboardImportService()
        : this(
            new WpfClipboardDataSource(),
            () => AppStorage.TryGetAppDirectory("clipboard"),
            () => DateTimeOffset.UtcNow,
            Guid.NewGuid,
            clipDir => _ = Task.Run(() => PruneClipboardImagesSafely(clipDir)))
    {
    }

    public ClipboardImportService(
        IClipboardDataSource clipboard,
        Func<string?> getClipboardDirectory,
        Func<DateTimeOffset> getUtcNow,
        Func<Guid> newGuid,
        Action<string>? queuePrune = null)
    {
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _getClipboardDirectory = getClipboardDirectory ?? throw new ArgumentNullException(nameof(getClipboardDirectory));
        _getUtcNow = getUtcNow ?? throw new ArgumentNullException(nameof(getUtcNow));
        _newGuid = newGuid ?? throw new ArgumentNullException(nameof(newGuid));
        _queuePrune = queuePrune ?? (_ => { });
    }

    public ClipboardImportResult Import()
    {
        if (_clipboard.ContainsFileDropList())
        {
            foreach (var file in _clipboard.GetFileDropList())
            {
                if (SupportedImageFormats.IsSupported(file) && File.Exists(file))
                    return new ClipboardImportResult(ClipboardImportStatus.OpenExistingFile, file, "");
            }

            return new ClipboardImportResult(
                ClipboardImportStatus.NoSupportedFile,
                null,
                "No supported image in the clipboard file list");
        }

        if (_clipboard.ContainsImage())
        {
            var bitmap = _clipboard.GetImage();
            if (bitmap is null)
            {
                return new ClipboardImportResult(
                    ClipboardImportStatus.ImageUnavailable,
                    null,
                    "Could not read clipboard image data");
            }

            var clipDir = _getClipboardDirectory();
            if (clipDir is null)
            {
                return new ClipboardImportResult(
                    ClipboardImportStatus.StorageUnavailable,
                    null,
                    "Paste failed: could not create temp folder");
            }

            try
            {
                _queuePrune(clipDir);

                var tempPath = CreateClipboardImagePath(clipDir, _getUtcNow(), _newGuid());
                SavePng(bitmap, tempPath);

                return new ClipboardImportResult(
                    ClipboardImportStatus.OpenSavedImage,
                    tempPath,
                    "Pasted from clipboard");
            }
            catch (Exception ex)
            {
                return new ClipboardImportResult(
                    ClipboardImportStatus.SaveFailed,
                    null,
                    $"Paste failed: {ex.Message}",
                    ex);
            }
        }

        return new ClipboardImportResult(
            ClipboardImportStatus.NothingImageLike,
            null,
            "Nothing image-like in the clipboard");
    }

    public static string CreateClipboardImagePath(string clipDir, DateTimeOffset utcNow, Guid guid)
    {
        if (string.IsNullOrWhiteSpace(clipDir))
            throw new ArgumentException("Clipboard directory is required.", nameof(clipDir));

        var stamp = utcNow.UtcDateTime.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
        return Path.Combine(clipDir, $"clipboard-{stamp}-{guid:N}.png");
    }

    public static int PruneClipboardImages(
        string clipDir,
        ClipboardPruneOptions? options = null,
        DateTime? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(clipDir) || !Directory.Exists(clipDir)) return 0;

        options ??= ClipboardPruneOptions.Default;
        var maxCount = Math.Max(0, options.MaxCount);
        var maxBytes = Math.Max(0, options.MaxBytes);
        var cutoff = (utcNow ?? DateTime.UtcNow) - options.MaxAge;
        var deleted = 0;

        var files = Directory.EnumerateFiles(clipDir, "clipboard-*.png", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(file => file.Exists)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();

        long totalBytes = 0;
        var kept = new List<FileInfo>(files.Count);

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            file.Refresh();
            if (!file.Exists) continue;

            var length = file.Length;
            totalBytes += length;

            if (file.LastWriteTimeUtc < cutoff || i >= maxCount)
            {
                if (TryDeleteDisposableFile(file.FullName))
                {
                    totalBytes -= length;
                    deleted++;
                    continue;
                }
            }

            kept.Add(file);
        }

        if (totalBytes <= maxBytes) return deleted;

        foreach (var file in kept.OrderBy(file => file.LastWriteTimeUtc))
        {
            if (totalBytes <= maxBytes) break;

            file.Refresh();
            if (!file.Exists) continue;

            var length = file.Length;
            if (TryDeleteDisposableFile(file.FullName))
            {
                totalBytes -= length;
                deleted++;
            }
        }

        return deleted;
    }

    private static void PruneClipboardImagesSafely(string clipDir)
    {
        try
        {
            PruneClipboardImages(clipDir);
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Clipboard image pruning failed for {Directory}", clipDir);
        }
    }

    private static void SavePng(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        encoder.Save(stream);
    }

    private static bool TryDeleteDisposableFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
