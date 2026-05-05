using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Images.Services;

/// <summary>
/// Read-only ZIP/CBZ page discovery for archive/book mode. This service never extracts entry
/// names to the filesystem, skips recursive archives, and enforces a per-entry size cap before
/// buffering a page for the decoder.
/// </summary>
public static class ArchiveBookService
{
    private const long MaxArchivePageBytes = 256L * 1024 * 1024;

    public sealed record ArchivePage(
        string EntryName,
        byte[] Bytes,
        int PageIndex,
        int PageCount);

    public static bool IsSupportedArchive(string path)
        => SupportedImageFormats.IsArchive(path);

    public static ArchivePage LoadPage(string path, int requestedPageIndex)
    {
        if (!IsSupportedArchive(path))
            throw new InvalidOperationException("This file is not a supported archive book.");

        var fileName = Path.GetFileName(path);

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);
            var entries = GetPageEntries(archive).ToList();
            if (entries.Count == 0)
                throw new InvalidOperationException($"'{fileName}' does not contain supported image pages.");

            entries.Sort(CompareEntries);
            var pageIndex = Math.Clamp(requestedPageIndex, 0, entries.Count - 1);
            var entry = entries[pageIndex];
            if (entry.Length > MaxArchivePageBytes)
                throw new InvalidOperationException(
                    $"Archive page '{entry.FullName}' is too large to preview safely.");

            using var entryStream = entry.Open();
            using var bytes = new MemoryStream(entry.Length > 0 && entry.Length <= int.MaxValue
                ? (int)entry.Length
                : 0);
            entryStream.CopyTo(bytes);
            return new ArchivePage(entry.FullName, bytes.ToArray(), pageIndex, entries.Count);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidOperationException($"Could not read archive '{fileName}': {ex.Message}", ex);
        }
    }

    public static IReadOnlyList<string> ListPageNames(string path)
    {
        if (!IsSupportedArchive(path))
            return [];

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);
            var entries = GetPageEntries(archive).ToList();
            entries.Sort(CompareEntries);
            return entries.Select(e => e.FullName).ToList();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IEnumerable<ZipArchiveEntry> GetPageEntries(ZipArchive archive)
    {
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name)) continue;
            if (entry.Length <= 0) continue;
            if (!IsSafeEntryName(entry.FullName)) continue;

            var ext = Path.GetExtension(entry.Name);
            if (SupportedImageFormats.IsArchiveExtension(ext)) continue;
            if (SupportedImageFormats.RequiresGhostscriptExtension(ext)) continue;
            if (!SupportedImageFormats.IsSupportedExtension(ext)) continue;

            yield return entry;
        }
    }

    private static bool IsSafeEntryName(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName)) return false;
        var normalized = entryName.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal)) return false;
        if (normalized.Contains('\0')) return false;
        if (Path.IsPathRooted(normalized)) return false;

        foreach (var segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or "..") return false;
            if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
        }

        return true;
    }

    private static int CompareEntries(ZipArchiveEntry a, ZipArchiveEntry b)
    {
        var result = StrCmpLogicalW(a.FullName.Replace('\\', '/'), b.FullName.Replace('\\', '/'));
        return result != 0
            ? result
            : StringComparer.OrdinalIgnoreCase.Compare(a.FullName, b.FullName);
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string a, string b);
}
