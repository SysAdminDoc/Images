using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Images.Services;

/// <summary>
/// Read-only page discovery for archive/book mode. The service never extracts entry names
/// to the filesystem, skips recursive archives, rejects unsafe paths, and enforces a
/// per-entry size cap before buffering a page for the decoder.
/// </summary>
public static class ArchiveBookService
{
    private const long MaxArchivePageBytes = 256L * 1024 * 1024;

    public sealed record ArchivePage(
        string EntryName,
        byte[] Bytes,
        int PageIndex,
        int PageCount,
        bool IsCover);

    public sealed record ArchiveSpread(
        IReadOnlyList<ArchivePage> Pages,
        int PageIndex,
        int PageCount)
    {
        public int PageSpan => Pages.Count;
        public bool IsSpread => Pages.Count > 1;
    }

    private sealed record ArchiveEntrySource(
        string Name,
        long DeclaredSize,
        bool IsEncrypted,
        Func<Stream> OpenStream);

    private interface IArchiveReader : IDisposable
    {
        IReadOnlyList<ArchiveEntrySource> ListPageEntries();
    }

    public static bool IsSupportedArchive(string path)
        => SupportedImageFormats.IsArchive(path);

    public static ArchivePage LoadPage(string path, int requestedPageIndex)
    {
        if (!IsSupportedArchive(path))
            throw new InvalidOperationException("This file is not a supported archive book.");

        var fileName = Path.GetFileName(path);

        try
        {
            using var reader = OpenReader(path);
            var entries = GetSortedPageEntries(reader);
            if (entries.Count == 0)
                throw new InvalidOperationException($"'{fileName}' does not contain supported image pages.");

            var pageIndex = Math.Clamp(requestedPageIndex, 0, entries.Count - 1);
            return ReadPage(entries[pageIndex], pageIndex, entries.Count);
        }
        catch (Exception ex) when (IsArchiveReadException(ex))
        {
            throw new InvalidOperationException($"Could not read archive '{fileName}': {ex.Message}", ex);
        }
    }

    public static ArchiveSpread LoadSpread(string path, int requestedPageIndex)
    {
        if (!IsSupportedArchive(path))
            throw new InvalidOperationException("This file is not a supported archive book.");

        var fileName = Path.GetFileName(path);

        try
        {
            using var reader = OpenReader(path);
            var entries = GetSortedPageEntries(reader);
            if (entries.Count == 0)
                throw new InvalidOperationException($"'{fileName}' does not contain supported image pages.");

            var startIndex = GetSpreadStartIndex(entries, requestedPageIndex);
            var pageCount = GetSpreadPageCount(entries, startIndex);
            var pages = new List<ArchivePage>(pageCount);

            for (var i = 0; i < pageCount; i++)
            {
                var pageIndex = startIndex + i;
                pages.Add(ReadPage(entries[pageIndex], pageIndex, entries.Count));
            }

            return new ArchiveSpread(pages, startIndex, entries.Count);
        }
        catch (Exception ex) when (IsArchiveReadException(ex))
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
            using var reader = OpenReader(path);
            return GetSortedPageEntries(reader)
                .Select(entry => entry.Name)
                .ToList();
        }
        catch (Exception ex) when (IsArchiveReadException(ex) || ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IArchiveReader OpenReader(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".cbz", StringComparison.OrdinalIgnoreCase)
            ? new ZipArchiveReader(path)
            : new ManagedArchiveReader(path);
    }

    private static List<ArchiveEntrySource> GetSortedPageEntries(IArchiveReader reader)
    {
        var entries = reader.ListPageEntries().ToList();
        entries.Sort(CompareEntries);
        return entries;
    }

    private static bool TryCreatePageEntry(
        string entryName,
        long declaredSize,
        bool isDirectory,
        bool isEncrypted,
        Func<Stream> openStream,
        out ArchiveEntrySource? page)
    {
        page = null;
        var normalized = NormalizeEntryName(entryName);
        if (isDirectory) return false;
        if (declaredSize == 0) return false;
        if (!IsSafeEntryName(normalized)) return false;

        var fileName = GetEntryFileName(normalized);
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        var ext = Path.GetExtension(fileName);
        if (SupportedImageFormats.IsArchiveExtension(ext)) return false;
        if (SupportedImageFormats.RequiresGhostscriptExtension(ext)) return false;
        if (!SupportedImageFormats.IsSupportedExtension(ext)) return false;

        page = new ArchiveEntrySource(normalized, declaredSize, isEncrypted, openStream);
        return true;
    }

    private static bool IsSafeEntryName(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName)) return false;
        var normalized = NormalizeEntryName(entryName);
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

    private static ArchivePage ReadPage(ArchiveEntrySource entry, int pageIndex, int pageCount)
    {
        if (entry.IsEncrypted)
            throw new InvalidOperationException(
                $"Archive page '{entry.Name}' is encrypted and cannot be previewed.");

        if (entry.DeclaredSize > MaxArchivePageBytes)
            throw new InvalidOperationException(
                $"Archive page '{entry.Name}' is too large to preview safely.");

        using var entryStream = entry.OpenStream();
        var bytes = ReadBounded(entryStream, entry.DeclaredSize, entry.Name);
        return new ArchivePage(entry.Name, bytes, pageIndex, pageCount, IsCoverEntry(entry.Name));
    }

    private static byte[] ReadBounded(Stream stream, long declaredSize, string entryName)
    {
        using var bytes = new MemoryStream(declaredSize > 0 && declaredSize <= int.MaxValue
            ? (int)declaredSize
            : 0);

        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > MaxArchivePageBytes)
                throw new InvalidOperationException(
                    $"Archive page '{entryName}' is too large to preview safely.");

            bytes.Write(buffer, 0, read);
        }

        return bytes.ToArray();
    }

    private static int GetSpreadStartIndex(IReadOnlyList<ArchiveEntrySource> entries, int requestedPageIndex)
    {
        var pageIndex = Math.Clamp(requestedPageIndex, 0, entries.Count - 1);
        if (entries.Count == 0)
            return 0;

        var hasExplicitCover = IsCoverEntry(entries[0].Name);
        if (hasExplicitCover && pageIndex == 0)
            return 0;

        var spreadBase = hasExplicitCover ? 1 : 0;
        if (pageIndex < spreadBase)
            return 0;

        return spreadBase + ((pageIndex - spreadBase) / 2) * 2;
    }

    private static int GetSpreadPageCount(IReadOnlyList<ArchiveEntrySource> entries, int startIndex)
    {
        if (entries.Count == 0 || startIndex >= entries.Count)
            return 0;

        if (startIndex == 0 && IsCoverEntry(entries[0].Name))
            return 1;

        return Math.Min(2, entries.Count - startIndex);
    }

    private static int CompareEntries(ArchiveEntrySource a, ArchiveEntrySource b)
    {
        var coverResult = IsCoverEntry(b.Name).CompareTo(IsCoverEntry(a.Name));
        if (coverResult != 0)
            return coverResult;

        var result = StrCmpLogicalW(a.Name, b.Name);
        return result != 0
            ? result
            : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
    }

    private static bool IsCoverEntry(string entryName)
    {
        var stem = Path.GetFileNameWithoutExtension(GetEntryFileName(entryName));
        if (string.IsNullOrWhiteSpace(stem))
            return false;

        var normalized = new string(stem
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

        return normalized is "cover" or "front" or "frontcover" or "coverfront" or "folder";
    }

    private static bool IsArchiveReadException(Exception ex)
        => ex is InvalidDataException or SharpCompressException;

    private static string NormalizeEntryName(string entryName)
        => entryName.Replace('\\', '/');

    private static string GetEntryFileName(string entryName)
    {
        var normalized = NormalizeEntryName(entryName);
        var index = normalized.LastIndexOf('/');
        return index >= 0 ? normalized[(index + 1)..] : normalized;
    }

    private sealed class ZipArchiveReader : IArchiveReader
    {
        private readonly FileStream _stream;
        private readonly ZipArchive _archive;

        public ZipArchiveReader(string path)
        {
            _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            try
            {
                _archive = new ZipArchive(_stream, ZipArchiveMode.Read, leaveOpen: true);
            }
            catch
            {
                _stream.Dispose();
                throw;
            }
        }

        public IReadOnlyList<ArchiveEntrySource> ListPageEntries()
        {
            var pages = new List<ArchiveEntrySource>();
            foreach (var entry in _archive.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Name)) continue;
                if (TryCreatePageEntry(
                        entry.FullName,
                        entry.Length,
                        isDirectory: false,
                        isEncrypted: false,
                        entry.Open,
                        out var page) &&
                    page is not null)
                {
                    pages.Add(page);
                }
            }

            return pages;
        }

        public void Dispose()
        {
            _archive.Dispose();
            _stream.Dispose();
        }
    }

    private sealed class ManagedArchiveReader : IArchiveReader
    {
        private readonly FileStream _stream;
        private readonly IArchive _archive;

        public ManagedArchiveReader(string path)
        {
            _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            try
            {
                _archive = ArchiveFactory.OpenArchive(
                    _stream,
                    new ReaderOptions
                    {
                        LeaveStreamOpen = true,
                        LookForHeader = true,
                        ExtensionHint = Path.GetExtension(path)
                    });
            }
            catch
            {
                _stream.Dispose();
                throw;
            }
        }

        public IReadOnlyList<ArchiveEntrySource> ListPageEntries()
        {
            var pages = new List<ArchiveEntrySource>();
            foreach (var entry in _archive.Entries)
            {
                var entryName = entry.Key ?? string.Empty;
                if (TryCreatePageEntry(
                        entryName,
                        entry.Size,
                        entry.IsDirectory,
                        entry.IsEncrypted,
                        entry.OpenEntryStream,
                        out var page) &&
                    page is not null)
                {
                    pages.Add(page);
                }
            }

            return pages;
        }

        public void Dispose()
        {
            _archive.Dispose();
            _stream.Dispose();
        }
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string a, string b);
}
