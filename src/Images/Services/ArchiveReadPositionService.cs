using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Images.Services;

public static class ArchiveReadPositionService
{
    private const string KeyPrefix = "archive.read-position.";

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
        settings.SetInt(BuildKey(archivePath), Math.Clamp(pageIndex, 0, maxPageIndex));
    }

    internal static string BuildKey(string archivePath)
    {
        var normalized = Path.GetFullPath(archivePath).ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return KeyPrefix + Convert.ToHexString(hash);
    }
}
