using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Images.Services;

public static class ArchiveReadPositionService
{
    private const string KeyPrefix = "archive.read-position.";
    private const string HistoryKey = "archive.read-history";
    private const int DefaultHistoryLimit = 10;

    public sealed record ArchiveReadHistoryItem(
        string Path,
        int PageIndex,
        int PageCount,
        DateTimeOffset LastOpenedUtc)
    {
        [JsonIgnore]
        public string ProgressText
        {
            get
            {
                var pageCount = Math.Max(1, PageCount);
                var pageNumber = Math.Clamp(PageIndex + 1, 1, pageCount);
                return pageCount > 1 ? $"Page {pageNumber} / {pageCount}" : "Page 1";
            }
        }
    }

    public static int GetLastPageIndex(SettingsService settings, string archivePath)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!SupportedImageFormats.IsArchive(archivePath))
            return 0;

        return Math.Max(0, settings.GetInt(BuildKey(archivePath), 0));
    }

    public static void SaveLastPageIndex(SettingsService settings, string archivePath, int pageIndex, int pageCount)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!SupportedImageFormats.IsArchive(archivePath))
            return;

        var maxPageIndex = Math.Max(0, pageCount - 1);
        var normalizedPath = NormalizeArchivePath(archivePath);
        var clampedPageIndex = Math.Clamp(pageIndex, 0, maxPageIndex);

        settings.SetInt(BuildKey(normalizedPath), clampedPageIndex);
        TouchHistory(settings, normalizedPath, clampedPageIndex, Math.Max(1, pageCount), DefaultHistoryLimit);
    }

    public static IReadOnlyList<ArchiveReadHistoryItem> GetRecentArchives(SettingsService settings, int max = DefaultHistoryLimit)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (max <= 0)
            return [];

        max = Math.Min(max, 50);
        return ReadHistory(settings)
            .Where(item => SupportedImageFormats.IsArchive(item.Path) && File.Exists(item.Path))
            .OrderByDescending(item => item.LastOpenedUtc)
            .Take(max)
            .ToList();
    }

    internal static string BuildKey(string archivePath)
    {
        var normalized = NormalizeArchivePath(archivePath).ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return KeyPrefix + Convert.ToHexString(hash);
    }

    private static void TouchHistory(
        SettingsService settings,
        string archivePath,
        int pageIndex,
        int pageCount,
        int max)
    {
        var history = ReadHistory(settings)
            .Where(item => !string.Equals(item.Path, archivePath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        history.Insert(0, new ArchiveReadHistoryItem(archivePath, pageIndex, pageCount, DateTimeOffset.UtcNow));

        settings.SetString(
            HistoryKey,
            JsonSerializer.Serialize(history.Take(Math.Max(1, max)).ToArray()));
    }

    private static IReadOnlyList<ArchiveReadHistoryItem> ReadHistory(SettingsService settings)
    {
        var raw = settings.GetString(HistoryKey);
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        try
        {
            return JsonSerializer.Deserialize<ArchiveReadHistoryItem[]>(raw) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string NormalizeArchivePath(string archivePath)
        => Path.GetFullPath(archivePath);
}
