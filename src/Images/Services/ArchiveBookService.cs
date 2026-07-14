using System.IO;
using System.Runtime.InteropServices;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Images.Services;

/// <summary>
/// Read-only page discovery for archive/book mode. The service never extracts entry names
/// to the filesystem, skips recursive archives, rejects unsafe paths, and enforces metadata,
/// aggregate-decompression, compression-ratio, and per-entry limits before decoding.
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
        long? ExpectedCrc,
        Func<Stream> OpenStream);

    private interface IArchiveReader : IDisposable
    {
        IReadOnlyList<ArchiveEntrySource> ListPageEntries();
    }

    public static bool IsSupportedArchive(string path)
        => SupportedImageFormats.IsArchive(path);

    public static ArchivePage LoadPage(string path, int requestedPageIndex, string? password = null)
    {
        if (!IsSupportedArchive(path))
            throw new InvalidOperationException("This file is not a supported archive book.");

        var fileName = Path.GetFileName(path);

        try
        {
            using var reader = OpenReader(path, password);
            var entries = GetSortedPageEntries(reader);
            if (entries.Count == 0)
                throw new InvalidOperationException($"'{fileName}' does not contain supported image pages.");

            var pageIndex = Math.Clamp(requestedPageIndex, 0, entries.Count - 1);
            return ReadPage(entries[pageIndex], pageIndex, entries.Count, hasPassword: password is not null);
        }
        catch (ArchivePasswordRequiredException)
        {
            throw;
        }
        catch (Exception ex) when (IsArchivePasswordException(ex))
        {
            throw new ArchivePasswordRequiredException(fileName);
        }
        catch (Exception ex) when (IsArchiveReadException(ex))
        {
            throw new InvalidOperationException($"Could not read archive '{fileName}': {ex.Message}", ex);
        }
    }

    public static ArchiveSpread LoadSpread(string path, int requestedPageIndex, string? password = null)
    {
        if (!IsSupportedArchive(path))
            throw new InvalidOperationException("This file is not a supported archive book.");

        var fileName = Path.GetFileName(path);

        try
        {
            using var reader = OpenReader(path, password);
            var entries = GetSortedPageEntries(reader);
            if (entries.Count == 0)
                throw new InvalidOperationException($"'{fileName}' does not contain supported image pages.");

            var startIndex = GetSpreadStartIndex(entries, requestedPageIndex);
            var pageCount = GetSpreadPageCount(entries, startIndex);
            var pages = new List<ArchivePage>(pageCount);

            for (var i = 0; i < pageCount; i++)
            {
                var pageIndex = startIndex + i;
                pages.Add(ReadPage(entries[pageIndex], pageIndex, entries.Count, hasPassword: password is not null));
            }

            return new ArchiveSpread(pages, startIndex, entries.Count);
        }
        catch (ArchivePasswordRequiredException)
        {
            throw;
        }
        catch (Exception ex) when (IsArchivePasswordException(ex))
        {
            throw new ArchivePasswordRequiredException(fileName);
        }
        catch (Exception ex) when (IsArchiveReadException(ex))
        {
            throw new InvalidOperationException($"Could not read archive '{fileName}': {ex.Message}", ex);
        }
    }

    public static IReadOnlyList<string> ListPageNames(string path, string? password = null)
    {
        if (!IsSupportedArchive(path))
            return [];

        try
        {
            using var reader = OpenReader(path, password);
            return GetSortedPageEntries(reader)
                .Select(entry => entry.Name)
                .ToList();
        }
        catch (Exception ex) when (IsArchiveReadException(ex) || ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IArchiveReader OpenReader(string path, string? password = null)
    {
        // Zip/CBZ goes through SharpCompress like every other format: the
        // System.IO.Compression reader could not report entry encryption, so
        // password-protected zips failed with a generic read error instead of
        // raising ArchivePasswordRequiredException for the password prompt.
        return new ManagedArchiveReader(path, password);
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
        long? expectedCrc,
        Func<Stream> openStream,
        out ArchiveEntrySource? page)
    {
        page = null;
        var normalized = NormalizeEntryName(entryName);
        if (isDirectory) return false;
        if (declaredSize <= 0) return false;
        if (!IsSafeEntryName(normalized)) return false;

        var fileName = GetEntryFileName(normalized);
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        var ext = Path.GetExtension(fileName);
        if (SupportedImageFormats.IsArchiveExtension(ext)) return false;
        if (SupportedImageFormats.RequiresGhostscriptExtension(ext)) return false;
        if (!SupportedImageFormats.IsSupportedExtension(ext)) return false;

        page = new ArchiveEntrySource(normalized, declaredSize, isEncrypted, expectedCrc, openStream);
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

    public sealed class ArchivePasswordRequiredException : InvalidOperationException
    {
        public ArchivePasswordRequiredException(string entryName)
            : base($"Archive page '{entryName}' is encrypted. A password is required to preview this archive.") { }
    }

    private static ArchivePage ReadPage(ArchiveEntrySource entry, int pageIndex, int pageCount, bool hasPassword = false)
    {
        if (entry.IsEncrypted && !hasPassword)
            throw new ArchivePasswordRequiredException(entry.Name);

        if (entry.DeclaredSize > MaxArchivePageBytes)
            throw new InvalidOperationException(
                $"Archive page '{entry.Name}' is too large to preview safely.");

        try
        {
            using var entryStream = entry.OpenStream();
            var bytes = ReadBounded(entryStream, entry.DeclaredSize, entry.Name);
            if (entry.ExpectedCrc is { } expectedCrc &&
                Crc32Checksum.Compute(bytes) != unchecked((uint)expectedCrc))
            {
                throw new InvalidDataException($"Archive page '{entry.Name}' failed CRC validation.");
            }
            return new ArchivePage(entry.Name, bytes, pageIndex, pageCount, IsCoverEntry(entry.Name));
        }
        catch (Exception ex) when (IsArchivePasswordException(ex))
        {
            throw new ArchivePasswordRequiredException(entry.Name);
        }
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

    internal static bool IsArchiveReadException(Exception ex)
        => ex is InvalidDataException or SharpCompressException or SharpCompress.Common.CryptographicException or System.Security.Cryptography.CryptographicException;

    internal static bool IsArchivePasswordException(Exception ex)
        => ex is SharpCompress.Common.CryptographicException or System.Security.Cryptography.CryptographicException;

    private static string NormalizeEntryName(string entryName)
        => entryName.Replace('\\', '/');

    private static string GetEntryFileName(string entryName)
    {
        var normalized = NormalizeEntryName(entryName);
        var index = normalized.LastIndexOf('/');
        return index >= 0 ? normalized[(index + 1)..] : normalized;
    }

    private sealed class ManagedArchiveReader : IArchiveReader
    {
        private readonly FileStream _stream;
        private readonly IArchive _archive;

        public ManagedArchiveReader(string path, string? password = null)
        {
            _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            try
            {
                var options = new ReaderOptions
                {
                    LeaveStreamOpen = true,
                    LookForHeader = true,
                    ExtensionHint = Path.GetExtension(path),
                    Password = password
                };
                _archive = ArchiveFactory.OpenArchive(_stream, options);
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
            var budget = new ArchiveBudgetPolicy();
            foreach (var entry in _archive.Entries)
            {
                var entryName = entry.Key ?? string.Empty;
                budget.AccountEntry(entryName);
                if (TryCreatePageEntry(
                        entryName,
                        entry.Size,
                        entry.IsDirectory,
                        entry.IsEncrypted,
                        ExpectedCrc(entry),
                        entry.OpenEntryStream,
                        out var page) &&
                    page is not null)
                {
                    budget.AccountPage(entry.Size, entry.CompressedSize);
                    pages.Add(page);
                }
            }

            return pages;
        }

        private long? ExpectedCrc(IArchiveEntry entry)
        {
            if (_archive.Type is not (ArchiveType.Zip or ArchiveType.Rar or ArchiveType.SevenZip))
                return null;

            return entry.Crc is >= 0 and <= uint.MaxValue ? entry.Crc : null;
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

internal static class Crc32Checksum
{
    private static readonly uint[] Table = BuildTable();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in data)
            crc = Table[(byte)(crc ^ value)] ^ (crc >> 8);
        return ~crc;
    }

    private static uint[] BuildTable()
    {
        const uint polynomial = 0xEDB88320;
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++)
                value = (value & 1) != 0 ? polynomial ^ (value >> 1) : value >> 1;
            table[i] = value;
        }
        return table;
    }
}
