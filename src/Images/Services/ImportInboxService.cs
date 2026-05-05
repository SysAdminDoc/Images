using System.Globalization;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public sealed record ImportInboxItem(
    string Path,
    string FileName,
    string Folder,
    long SizeBytes,
    string SizeText,
    DateTimeOffset ModifiedUtc,
    string Sha256,
    bool IsDuplicateInInbox,
    bool IsDuplicateInDestination,
    int DuplicateOrdinal,
    int DuplicateCount,
    string StatusText)
{
    public bool IsDuplicate => IsDuplicateInInbox || IsDuplicateInDestination;
}

public sealed record ImportInboxScanResult(
    IReadOnlyList<ImportInboxItem> Items,
    int SourceCount,
    int FailedCount,
    int DuplicateCount,
    int DestinationDuplicateCount);

public sealed record ImportInboxCommitRequest(
    string SourcePath,
    string DestinationFolder,
    string TagsText,
    int? Rating,
    bool StripGps,
    bool MoveOriginal);

public sealed record ImportInboxCommitMove(
    string SourcePath,
    string DestinationPath,
    bool MovedOriginal);

public sealed record ImportInboxFailure(string Path, string Error);

public sealed record ImportInboxCommitResult(
    IReadOnlyList<ImportInboxCommitMove> Imported,
    IReadOnlyList<ImportInboxFailure> Failed)
{
    public int ImportedCount => Imported.Count;
    public int FailedCount => Failed.Count;
}

public sealed class ImportInboxService
{
    private static readonly ILogger Log = Images.Services.Log.Get(nameof(ImportInboxService));
    private readonly TagGraphService _tagGraph;

    public ImportInboxService(TagGraphService? tagGraph = null)
    {
        _tagGraph = tagGraph ?? new TagGraphService();
    }

    public ImportInboxScanResult BuildInbox(
        IEnumerable<string> roots,
        string? destinationFolder = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roots);

        var failed = 0;
        var files = CollectSupportedFiles(roots, ref failed, cancellationToken);
        var snapshots = new List<ImportSnapshot>(files.Count);

        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length <= 0)
                {
                    failed++;
                    continue;
                }

                snapshots.Add(new ImportSnapshot(
                    info.FullName,
                    info.Name,
                    info.DirectoryName ?? "",
                    info.Length,
                    new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                    ComputeSha256(info.FullName, cancellationToken)));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
            {
                failed++;
                Log.LogDebug(ex, "Could not stage import inbox item {Path}", path);
            }
        }

        var destinationHashes = HashDestination(destinationFolder, cancellationToken);
        var items = new List<ImportInboxItem>(snapshots.Count);
        foreach (var group in snapshots.GroupBy(snapshot => snapshot.Sha256, StringComparer.OrdinalIgnoreCase))
        {
            var orderedGroup = group
                .OrderBy(snapshot => snapshot.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(snapshot => snapshot.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var isDuplicateInDestination = destinationHashes.Contains(group.Key);
            for (var i = 0; i < orderedGroup.Count; i++)
            {
                var snapshot = orderedGroup[i];
                var isDuplicateInInbox = orderedGroup.Count > 1;
                items.Add(new ImportInboxItem(
                    snapshot.Path,
                    snapshot.FileName,
                    snapshot.Folder,
                    snapshot.SizeBytes,
                    FormatBytes(snapshot.SizeBytes),
                    snapshot.ModifiedUtc,
                    snapshot.Sha256,
                    isDuplicateInInbox,
                    isDuplicateInDestination,
                    i + 1,
                    orderedGroup.Count,
                    StatusFor(isDuplicateInInbox, isDuplicateInDestination, i + 1, orderedGroup.Count)));
            }
        }

        var orderedItems = items
            .OrderByDescending(item => item.IsDuplicateInDestination)
            .ThenByDescending(item => item.IsDuplicateInInbox)
            .ThenBy(item => item.FileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ImportInboxScanResult(
            orderedItems,
            snapshots.Count,
            failed,
            orderedItems.Count(item => item.IsDuplicateInInbox),
            orderedItems.Count(item => item.IsDuplicateInDestination));
    }

    public ImportInboxCommitResult Commit(
        IEnumerable<ImportInboxCommitRequest> requests,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var imported = new List<ImportInboxCommitMove>();
        var failed = new List<ImportInboxFailure>();

        foreach (var request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (string.IsNullOrWhiteSpace(request.SourcePath) || !File.Exists(request.SourcePath))
                {
                    failed.Add(new ImportInboxFailure(request.SourcePath, "Source file no longer exists."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(request.DestinationFolder))
                {
                    failed.Add(new ImportInboxFailure(request.SourcePath, "Choose a destination folder before importing."));
                    continue;
                }

                var destinationFolder = Path.GetFullPath(request.DestinationFolder);
                Directory.CreateDirectory(destinationFolder);
                var sourcePath = Path.GetFullPath(request.SourcePath);
                var destinationPath = ResolveUniqueDestination(destinationFolder, Path.GetFileName(sourcePath));

                if (SamePath(sourcePath, destinationPath))
                {
                    ApplyPostImportEdits(destinationPath, request);
                    imported.Add(new ImportInboxCommitMove(sourcePath, destinationPath, MovedOriginal: false));
                    continue;
                }

                if (request.MoveOriginal)
                    File.Move(sourcePath, destinationPath);
                else
                    File.Copy(sourcePath, destinationPath);

                ApplyPostImportEdits(destinationPath, request);
                imported.Add(new ImportInboxCommitMove(sourcePath, destinationPath, request.MoveOriginal));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException or InvalidOperationException)
            {
                failed.Add(new ImportInboxFailure(request.SourcePath, ex.Message));
                Log.LogWarning(ex, "Import inbox commit failed for {Path}", request.SourcePath);
            }
        }

        return new ImportInboxCommitResult(imported, failed);
    }

    private void ApplyPostImportEdits(string destinationPath, ImportInboxCommitRequest request)
    {
        if (request.StripGps && SupportsMetadataWrite(destinationPath))
        {
            try
            {
                MetadataEditService.StripGpsMetadata(destinationPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException or InvalidOperationException)
            {
                Log.LogWarning(ex, "Could not strip GPS metadata from imported file {Path}", destinationPath);
            }
        }

        var tags = TagGraphService.ParseTagInput(request.TagsText);
        if (tags.Count > 0)
            _tagGraph.ExportSidecarTags(destinationPath, tags, includeParents: true);

        if (request.Rating is not null)
            WriteRatingSidecar(destinationPath, Math.Clamp(request.Rating.Value, -1, 5));
    }

    private static bool SupportsMetadataWrite(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> CollectSupportedFiles(
        IEnumerable<string> roots,
        ref int failed,
        CancellationToken cancellationToken)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.Where(root => !string.IsNullOrWhiteSpace(root)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var fullRoot = Path.GetFullPath(root);
                if (File.Exists(fullRoot))
                {
                    if (SupportedImageFormats.IsSupported(fullRoot))
                        files.Add(fullRoot);
                    continue;
                }

                if (!Directory.Exists(fullRoot))
                {
                    failed++;
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (SupportedImageFormats.IsSupported(file))
                        files.Add(Path.GetFullPath(file));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
            {
                failed++;
                Log.LogDebug(ex, "Could not enumerate import inbox root {Path}", root);
            }
        }

        return files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static HashSet<string> HashDestination(string? destinationFolder, CancellationToken cancellationToken)
    {
        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(destinationFolder))
            return hashes;

        try
        {
            var folder = Path.GetFullPath(destinationFolder);
            if (!Directory.Exists(folder))
                return hashes;

            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!SupportedImageFormats.IsSupported(file))
                    continue;

                try
                {
                    hashes.Add(ComputeSha256(file, cancellationToken));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
                {
                    Log.LogDebug(ex, "Could not hash destination library file {Path}", file);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
        {
            Log.LogDebug(ex, "Could not scan destination library {Path}", destinationFolder);
        }

        return hashes;
    }

    private static string ComputeSha256(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string ResolveUniqueDestination(string folder, string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        for (var attempt = 0; attempt < 10_000; attempt++)
        {
            var candidateName = attempt == 0
                ? stem + extension
                : string.Create(CultureInfo.InvariantCulture, $"{stem} ({attempt + 1}){extension}");
            var candidate = Path.Combine(folder, candidateName);
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(folder, stem + "-" + Guid.NewGuid().ToString("N")[..8] + extension);
    }

    private static bool SamePath(string left, string right)
        => string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static string StatusFor(
        bool isDuplicateInInbox,
        bool isDuplicateInDestination,
        int ordinal,
        int duplicateCount)
    {
        if (isDuplicateInDestination)
            return isDuplicateInInbox
                ? $"Already in destination; duplicate {ordinal}/{duplicateCount} in inbox"
                : "Already in destination";
        if (isDuplicateInInbox)
            return ordinal == 1
                ? $"First copy of {duplicateCount} inbox duplicates"
                : $"Duplicate {ordinal}/{duplicateCount} in inbox";
        return "Ready to import";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes} B"
            : $"{value:0.#} {units[unit]}";
    }

    private static void WriteRatingSidecar(string imagePath, int rating)
    {
        var sidecarPath = imagePath + ".xmp";
        XNamespace x = "adobe:ns:meta/";
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace xmp = "http://ns.adobe.com/xap/1.0/";

        XDocument document;
        if (File.Exists(sidecarPath))
        {
            document = XDocument.Load(sidecarPath, LoadOptions.None);
        }
        else
        {
            document = new XDocument(
                new XElement(
                    x + "xmpmeta",
                    new XAttribute(XNamespace.Xmlns + "x", x.NamespaceName),
                    new XElement(
                        rdf + "RDF",
                        new XAttribute(XNamespace.Xmlns + "rdf", rdf.NamespaceName),
                        new XAttribute(XNamespace.Xmlns + "xmp", xmp.NamespaceName),
                        new XElement(rdf + "Description"))));
        }

        var rdfRoot = document.Descendants().FirstOrDefault(
            element => element.Name.LocalName.Equals("RDF", StringComparison.OrdinalIgnoreCase));
        if (rdfRoot is null)
        {
            rdfRoot = new XElement(
                rdf + "RDF",
                new XAttribute(XNamespace.Xmlns + "rdf", rdf.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "xmp", xmp.NamespaceName));
            document.Root?.Add(rdfRoot);
        }

        var description = rdfRoot.Elements().FirstOrDefault(
            element => element.Name.LocalName.Equals("Description", StringComparison.OrdinalIgnoreCase));
        if (description is null)
        {
            description = new XElement(rdf + "Description");
            rdfRoot.Add(description);
        }

        description.SetAttributeValue(xmp + "Rating", rating.ToString(CultureInfo.InvariantCulture));
        document.Save(sidecarPath);
    }

    private sealed record ImportSnapshot(
        string Path,
        string FileName,
        string Folder,
        long SizeBytes,
        DateTimeOffset ModifiedUtc,
        string Sha256);
}
