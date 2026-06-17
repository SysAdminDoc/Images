using System.IO;
using System.Security;
using System.Text;
using System.Windows.Media.Imaging;

namespace Images.Services;

public enum FileHealthFindingKind
{
    BadExtension,
    BrokenImage,
    ZeroByte,
    TemporaryFile
}

public sealed record FileHealthFinding(
    FileHealthFindingKind Kind,
    string Path,
    string FileName,
    string Folder,
    long SizeBytes,
    string SizeText,
    string? DetectedFormat,
    string? SuggestedExtension,
    string Detail)
{
    public bool CanRename => !string.IsNullOrWhiteSpace(SuggestedExtension);
    public string Title => Kind switch
    {
        FileHealthFindingKind.BadExtension => "Extension does not match content",
        FileHealthFindingKind.BrokenImage => "Supported image failed to decode",
        FileHealthFindingKind.ZeroByte => "Zero-byte file",
        FileHealthFindingKind.TemporaryFile => "Suspicious temporary file",
        _ => "File health issue"
    };

    public string Summary => Kind switch
    {
        FileHealthFindingKind.BadExtension => $"{FileName} looks like {DetectedFormat}; suggested extension {SuggestedExtension}",
        FileHealthFindingKind.BrokenImage => $"{FileName} is listed as supported but could not be decoded",
        FileHealthFindingKind.ZeroByte => $"{FileName} is empty",
        FileHealthFindingKind.TemporaryFile => $"{FileName} looks like a temporary or partial file",
        _ => FileName
    };
}

public sealed record FileHealthScanResult(
    IReadOnlyList<FileHealthFinding> Findings,
    int ScannedCount,
    int FailedCount)
{
    public bool HasFindings => Findings.Count > 0;
}

public enum FileHealthActionStatus
{
    Completed,
    Unavailable,
    Failed
}

public sealed record FileHealthActionResult(
    FileHealthActionStatus Status,
    string? SourcePath = null,
    string? DestinationPath = null,
    string? Error = null)
{
    public static FileHealthActionResult Completed(string sourcePath, string destinationPath) =>
        new(FileHealthActionStatus.Completed, sourcePath, destinationPath);

    public static FileHealthActionResult Unavailable(string error) =>
        new(FileHealthActionStatus.Unavailable, Error: error);

    public static FileHealthActionResult Failed(string sourcePath, string error) =>
        new(FileHealthActionStatus.Failed, sourcePath, Error: error);
}

public sealed class FileHealthScanService
{
    private readonly Func<string?> _getQuarantineRoot;
    private readonly Func<string, ImageLoader.LoadResult> _loadImage;

    public FileHealthScanService(
        Func<string?>? getQuarantineRoot = null,
        Func<string, ImageLoader.LoadResult>? loadImage = null)
    {
        _getQuarantineRoot = getQuarantineRoot ?? (() => AppStorage.TryGetAppDirectory("quarantine"));
        _loadImage = loadImage ?? (path => ImageLoader.Load(path));
    }

    public FileHealthScanResult Scan(
        IEnumerable<string> scanRoots,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scanRoots);

        var failed = 0;
        var files = CollectFiles(scanRoots, ref failed, cancellationToken);
        var findings = new List<FileHealthFinding>();

        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    failed++;
                    continue;
                }

                var finding = InspectFile(info, cancellationToken);
                if (finding is not null)
                    findings.Add(finding);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
            {
                failed++;
            }
        }

        return new FileHealthScanResult(
            findings
                .OrderBy(finding => SortRank(finding.Kind))
                .ThenBy(finding => finding.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(finding => finding.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            files.Count,
            failed);
    }

    public FileHealthActionResult RenameToSuggestedExtension(FileHealthFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);

        if (string.IsNullOrWhiteSpace(finding.SuggestedExtension))
            return FileHealthActionResult.Unavailable("This finding does not have a suggested extension.");

        try
        {
            if (!File.Exists(finding.Path))
                return FileHealthActionResult.Failed(finding.Path, "File no longer exists.");

            var target = ResolveUniquePath(
                System.IO.Path.GetDirectoryName(finding.Path) ?? string.Empty,
                System.IO.Path.GetFileNameWithoutExtension(finding.Path),
                finding.SuggestedExtension);
            File.Move(finding.Path, target);
            return FileHealthActionResult.Completed(finding.Path, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return FileHealthActionResult.Failed(finding.Path, ex.Message);
        }
    }

    public FileHealthActionResult Quarantine(FileHealthFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);

        var root = _getQuarantineRoot();
        if (string.IsNullOrWhiteSpace(root))
            return FileHealthActionResult.Unavailable("Quarantine storage is not available.");

        try
        {
            if (!File.Exists(finding.Path))
                return FileHealthActionResult.Failed(finding.Path, "File no longer exists.");

            var batch = System.IO.Path.Combine(
                root,
                "file-health-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(batch);

            var destination = ResolveUniquePath(
                batch,
                System.IO.Path.GetFileNameWithoutExtension(finding.Path),
                System.IO.Path.GetExtension(finding.Path));
            File.Move(finding.Path, destination);
            WriteManifest(batch, finding, destination);
            return FileHealthActionResult.Completed(finding.Path, destination);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return FileHealthActionResult.Failed(finding.Path, ex.Message);
        }
    }

    private FileHealthFinding? InspectFile(FileInfo info, CancellationToken cancellationToken)
    {
        if (info.Length == 0)
        {
            return CreateFinding(
                FileHealthFindingKind.ZeroByte,
                info,
                null,
                null,
                "The file is empty. It cannot decode as an image and is usually safe to quarantine after review.");
        }

        var signature = FormatSignatureDetector.Detect(info.FullName);
        if (signature is not null && !signature.MatchesExtension(info.Extension))
        {
            return CreateFinding(
                FileHealthFindingKind.BadExtension,
                info,
                signature.FormatName,
                signature.SuggestedExtension,
                $"Detected {signature.FormatName} content from the file header, but the extension is {info.Extension}.");
        }

        if (ShouldDecodeForHealth(info.FullName))
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _loadImage(info.FullName);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException or FileFormatException or ArgumentException)
            {
                return CreateFinding(
                    FileHealthFindingKind.BrokenImage,
                    info,
                    signature?.FormatName ?? SupportedImageFormats.FamilyLabelForExtension(info.Extension),
                    null,
                    ex.Message);
            }
        }

        if (LooksTemporary(info))
        {
            return CreateFinding(
                FileHealthFindingKind.TemporaryFile,
                info,
                signature?.FormatName,
                signature?.SuggestedExtension,
                "The filename or extension looks like a temporary, partial, or interrupted-download artifact.");
        }

        return null;
    }

    private static FileHealthFinding CreateFinding(
        FileHealthFindingKind kind,
        FileInfo info,
        string? detectedFormat,
        string? suggestedExtension,
        string detail)
        => new(
            kind,
            info.FullName,
            info.Name,
            info.DirectoryName ?? string.Empty,
            info.Length,
            FormatBytes(info.Length),
            detectedFormat,
            suggestedExtension,
            detail);

    private static bool ShouldDecodeForHealth(string path)
    {
        if (!SupportedImageFormats.IsSupported(path))
            return false;

        if (SupportedImageFormats.RequiresGhostscript(path) && !CodecRuntime.Status.GhostscriptAvailable)
            return false;

        return true;
    }

    private static bool LooksTemporary(FileInfo info)
    {
        var ext = info.Extension;
        if (TempExtensions.Contains(ext))
            return true;

        return info.Name.StartsWith("~$", StringComparison.OrdinalIgnoreCase) ||
               info.Name.StartsWith(".~", StringComparison.OrdinalIgnoreCase) ||
               info.Name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> CollectFiles(
        IEnumerable<string> scanRoots,
        ref int failures,
        CancellationToken cancellationToken)
    {
        var files = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in scanRoots.Where(root => !string.IsNullOrWhiteSpace(root)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fullRoot;
            try
            {
                fullRoot = System.IO.Path.GetFullPath(root);
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or SecurityException)
            {
                failures++;
                continue;
            }

            if (File.Exists(fullRoot))
            {
                AddFile(fullRoot, files, seen);
                continue;
            }

            if (!Directory.Exists(fullRoot))
            {
                failures++;
                continue;
            }

            var pending = new Stack<string>();
            pending.Push(fullRoot);
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = pending.Pop();

                try
                {
                    foreach (var file in Directory.EnumerateFiles(directory))
                        AddFile(file, files, seen);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
                {
                    failures++;
                }

                try
                {
                    foreach (var child in Directory.EnumerateDirectories(directory))
                        pending.Push(child);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
                {
                    failures++;
                }
            }
        }

        return files;
    }

    private static void AddFile(string path, List<string> files, HashSet<string> seen)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        if (seen.Add(fullPath))
            files.Add(fullPath);
    }


    private static string ResolveUniquePath(string directory, string stem, string extension)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new IOException("Destination directory is not available.");

        Directory.CreateDirectory(directory);

        var normalizedExtension = extension.StartsWith('.') ? extension : "." + extension;
        var target = System.IO.Path.Combine(directory, stem + normalizedExtension);
        if (!File.Exists(target))
            return target;

        for (var i = 2; i < 10_000; i++)
        {
            target = System.IO.Path.Combine(directory, $"{stem} ({i}){normalizedExtension}");
            if (!File.Exists(target))
                return target;
        }

        return System.IO.Path.Combine(directory, $"{stem}-{Guid.NewGuid():N}{normalizedExtension}");
    }

    private static void WriteManifest(string batch, FileHealthFinding finding, string destination)
    {
        try
        {
            var manifest = new StringBuilder();
            manifest.AppendLine("Images file-health quarantine");
            manifest.AppendLine("Created: " + DateTimeOffset.Now.ToString("O"));
            manifest.AppendLine($"KIND\t{finding.Kind}");
            manifest.AppendLine($"SOURCE\t{finding.Path}");
            manifest.AppendLine($"DESTINATION\t{destination}");
            manifest.AppendLine($"DETAIL\t{finding.Detail}");
            File.WriteAllText(System.IO.Path.Combine(batch, "manifest.tsv"), manifest.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Keep quarantine reliable even if the recovery manifest cannot be written.
        }
    }

    private static int SortRank(FileHealthFindingKind kind) => kind switch
    {
        FileHealthFindingKind.BadExtension => 0,
        FileHealthFindingKind.BrokenImage => 1,
        FileHealthFindingKind.ZeroByte => 2,
        FileHealthFindingKind.TemporaryFile => 3,
        _ => 4
    };

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)Math.Max(0, bytes);
        var index = 0;
        while (value >= 1024 && index < units.Length - 1)
        {
            value /= 1024;
            index++;
        }

        return index == 0
            ? $"{bytes} {units[index]}"
            : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{value:0.##} {units[index]}");
    }

    private static readonly HashSet<string> TempExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tmp", ".temp", ".part", ".partial", ".crdownload", ".download"
    };

}
