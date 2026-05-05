using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Security;
using System.Text;
using ImageMagick;

namespace Images.Services;

public enum DuplicateCleanupFindingKind
{
    ExactDuplicate,
    SimilarImage
}

public sealed record DuplicateCleanupCandidate(
    string Path,
    string FileName,
    string Folder,
    long SizeBytes,
    string SizeText,
    string Sha256,
    ulong? PerceptualHash,
    bool IsReference)
{
    public bool HasPerceptualHash => PerceptualHash.HasValue;
    public string ReferenceText => IsReference ? "Reference folder" : "Scan folder";
    public string ShortHash => Sha256.Length > 12 ? Sha256[..12] : Sha256;
}

public sealed record DuplicateCleanupFinding(
    DuplicateCleanupFindingKind Kind,
    IReadOnlyList<DuplicateCleanupCandidate> Candidates,
    int Distance,
    int MaxDistance)
{
    public DuplicateCleanupCandidate? PrimaryCandidate => Candidates.Count > 0 ? Candidates[0] : null;
    public DuplicateCleanupCandidate? SecondaryCandidate => Candidates.Count > 1 ? Candidates[1] : null;
    public IReadOnlyList<DuplicateCleanupCandidate> ExtraCandidates => Candidates.Skip(1).ToArray();
    public int CandidateCount => Candidates.Count;

    public string Title => Kind == DuplicateCleanupFindingKind.ExactDuplicate
        ? $"Exact duplicate group - {Candidates.Count} files"
        : $"Similar images - distance {Distance}/{MaxDistance}";

    public string Summary
    {
        get
        {
            if (Candidates.Count == 0)
                return "No files.";

            var referenceCount = Candidates.Count(candidate => candidate.IsReference);
            var size = Candidates[0].SizeText;
            var root = referenceCount > 0
                ? $"{referenceCount} reference candidate{Plural(referenceCount)}"
                : "No reference candidate";

            return Kind == DuplicateCleanupFindingKind.ExactDuplicate
                ? $"{size} each - {root}"
                : $"{Candidates[0].FileName} vs {Candidates[1].FileName} - {root}";
        }
    }

    public string ActionSummary => ExtraCandidates.Count == 1
        ? $"Extra candidate: {ExtraCandidates[0].FileName}"
        : $"Extra candidates: {ExtraCandidates.Count}";

    private static string Plural(int count) => count == 1 ? string.Empty : "s";
}

public sealed record DuplicateCleanupScanResult(
    IReadOnlyList<DuplicateCleanupFinding> Findings,
    int FileCount,
    int FailedCount,
    int ExactGroupCount,
    int SimilarPairCount)
{
    public bool HasFindings => Findings.Count > 0;
}

public sealed record DuplicateCleanupMoveResult(string SourcePath, string DestinationPath);

public sealed record DuplicateCleanupFailure(string Path, string Error);

public sealed record DuplicateCleanupQuarantineResult(
    bool IsAvailable,
    string? BatchDirectory,
    IReadOnlyList<DuplicateCleanupMoveResult> Moved,
    IReadOnlyList<DuplicateCleanupFailure> Failed)
{
    public int MovedCount => Moved.Count;
    public int FailedCount => Failed.Count;
}

public sealed class DuplicateCleanupService
{
    private const int HashSize = 8;
    private const int MaxSimilarPairFindings = 1000;
    private readonly Func<string?> _getQuarantineRoot;

    public DuplicateCleanupService(Func<string?>? getQuarantineRoot = null)
    {
        _getQuarantineRoot = getQuarantineRoot ?? (() => AppStorage.TryGetAppDirectory("quarantine"));
    }

    public DuplicateCleanupScanResult Scan(
        IEnumerable<string> scanRoots,
        IEnumerable<string>? referenceRoots = null,
        int similarityThreshold = 6,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scanRoots);

        var threshold = Math.Clamp(similarityThreshold, 0, 64);
        var failures = 0;
        var references = NormalizeReferenceRoots(referenceRoots);
        var paths = CollectCandidateFiles(scanRoots, ref failures, cancellationToken);
        var candidates = new List<DuplicateCleanupCandidate>(paths.Count);

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length <= 0)
                {
                    failures++;
                    continue;
                }

                var sha256 = ComputeSha256(path, cancellationToken);
                var perceptualHash = TryComputeAverageHash(path, cancellationToken);
                if (perceptualHash is null)
                    failures++;

                candidates.Add(new DuplicateCleanupCandidate(
                    System.IO.Path.GetFullPath(path),
                    info.Name,
                    info.DirectoryName ?? string.Empty,
                    info.Length,
                    FormatBytes(info.Length),
                    sha256,
                    perceptualHash,
                    IsUnderReferenceRoot(info.FullName, references)));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
            {
                failures++;
            }
        }

        var exactFindings = BuildExactFindings(candidates);
        var similarFindings = BuildSimilarFindings(candidates, threshold);
        var findings = exactFindings
            .Concat(similarFindings)
            .OrderBy(finding => finding.Kind == DuplicateCleanupFindingKind.ExactDuplicate ? 0 : 1)
            .ThenBy(finding => finding.Distance)
            .ThenByDescending(finding => finding.Candidates.Count)
            .ThenBy(finding => finding.Candidates.FirstOrDefault()?.FileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new DuplicateCleanupScanResult(
            findings,
            candidates.Count,
            failures,
            exactFindings.Count,
            similarFindings.Count);
    }

    public DuplicateCleanupQuarantineResult Quarantine(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var root = _getQuarantineRoot();
        if (string.IsNullOrWhiteSpace(root))
        {
            return new DuplicateCleanupQuarantineResult(
                IsAvailable: false,
                BatchDirectory: null,
                Moved: [],
                Failed: []);
        }

        var batch = System.IO.Path.Combine(
            root,
            DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(batch);

        var moved = new List<DuplicateCleanupMoveResult>();
        var failed = new List<DuplicateCleanupFailure>();

        foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!File.Exists(path))
                {
                    failed.Add(new DuplicateCleanupFailure(path, "File no longer exists."));
                    continue;
                }

                var destination = ResolveUniqueDestination(batch, System.IO.Path.GetFileName(path));
                File.Move(path, destination);
                moved.Add(new DuplicateCleanupMoveResult(path, destination));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                failed.Add(new DuplicateCleanupFailure(path, ex.Message));
            }
        }

        WriteManifest(batch, moved, failed);
        return new DuplicateCleanupQuarantineResult(true, batch, moved, failed);
    }

    public static int HammingDistance(ulong left, ulong right)
        => BitOperations.PopCount(left ^ right);

    private static IReadOnlyList<DuplicateCleanupFinding> BuildExactFindings(IReadOnlyList<DuplicateCleanupCandidate> candidates)
        => candidates
            .GroupBy(candidate => candidate.Sha256, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new DuplicateCleanupFinding(
                DuplicateCleanupFindingKind.ExactDuplicate,
                SortCandidatesForReview(group).ToArray(),
                Distance: 0,
                MaxDistance: 0))
            .ToArray();

    private static IReadOnlyList<DuplicateCleanupFinding> BuildSimilarFindings(
        IReadOnlyList<DuplicateCleanupCandidate> candidates,
        int threshold)
    {
        var findings = new List<DuplicateCleanupFinding>();
        var tree = new PerceptualHashTree();

        foreach (var candidate in candidates
                     .Where(candidate => candidate.PerceptualHash.HasValue)
                     .OrderBy(candidate => candidate.FileName, StringComparer.OrdinalIgnoreCase))
        {
            var hash = candidate.PerceptualHash!.Value;
            foreach (var match in tree.FindWithin(hash, threshold))
            {
                if (match.Candidate.Sha256.Equals(candidate.Sha256, StringComparison.OrdinalIgnoreCase))
                    continue;

                findings.Add(new DuplicateCleanupFinding(
                    DuplicateCleanupFindingKind.SimilarImage,
                    SortCandidatesForReview([match.Candidate, candidate]).ToArray(),
                    match.Distance,
                    threshold));

                if (findings.Count >= MaxSimilarPairFindings)
                    return SortSimilarFindings(findings);
            }

            tree.Add(candidate, hash);
        }

        return SortSimilarFindings(findings);
    }

    private static IReadOnlyList<DuplicateCleanupFinding> SortSimilarFindings(List<DuplicateCleanupFinding> findings)
        => findings
            .OrderBy(finding => finding.Distance)
            .ThenBy(finding => finding.Candidates[0].FileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(finding => finding.Candidates[1].FileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IEnumerable<DuplicateCleanupCandidate> SortCandidatesForReview(IEnumerable<DuplicateCleanupCandidate> candidates)
        => candidates
            .OrderByDescending(candidate => candidate.IsReference)
            .ThenByDescending(candidate => candidate.SizeBytes)
            .ThenBy(candidate => candidate.FileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase);

    private static List<string> CollectCandidateFiles(
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
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or SecurityException)
            {
                failures++;
                continue;
            }

            if (File.Exists(fullRoot))
            {
                AddCandidateFile(fullRoot, files, seen);
                continue;
            }

            if (!Directory.Exists(fullRoot))
            {
                failures++;
                continue;
            }

            CollectDirectoryFiles(fullRoot, files, seen, ref failures, cancellationToken);
        }

        return files;
    }

    private static void CollectDirectoryFiles(
        string root,
        List<string> files,
        HashSet<string> seen,
        ref int failures,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();

            try
            {
                foreach (var file in Directory.EnumerateFiles(directory))
                    AddCandidateFile(file, files, seen);
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

    private static void AddCandidateFile(string path, List<string> files, HashSet<string> seen)
    {
        if (!SupportedImageFormats.IsSupported(path) || SupportedImageFormats.IsArchive(path))
            return;

        var fullPath = System.IO.Path.GetFullPath(path);
        if (seen.Add(fullPath))
            files.Add(fullPath);
    }

    private static string ComputeSha256(string path, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[128 * 1024];

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0)
                break;

            hash.AppendData(buffer, 0, read);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static ulong? TryComputeAverageHash(string path, CancellationToken cancellationToken)
    {
        try
        {
            CodecRuntime.Configure();
            cancellationToken.ThrowIfCancellationRequested();

            using var image = new MagickImage(path);
            image.AutoOrient();
            image.BackgroundColor = MagickColors.White;
            image.Alpha(AlphaOption.Remove);
            image.Resize(new MagickGeometry((uint)HashSize, (uint)HashSize) { IgnoreAspectRatio = true });
            image.Format = MagickFormat.Bgra;

            cancellationToken.ThrowIfCancellationRequested();
            var pixels = image.GetPixelsUnsafe().ToByteArray(PixelMapping.BGRA);
            if (pixels is null || pixels.Length < HashSize * HashSize * 4)
                return null;

            Span<double> luminance = stackalloc double[HashSize * HashSize];
            var sum = 0.0;
            for (var i = 0; i < luminance.Length; i++)
            {
                var offset = i * 4;
                var blue = pixels[offset];
                var green = pixels[offset + 1];
                var red = pixels[offset + 2];
                var value = (0.299 * red) + (0.587 * green) + (0.114 * blue);
                luminance[i] = value;
                sum += value;
            }

            var average = sum / luminance.Length;
            var hash = 0UL;
            for (var i = 0; i < luminance.Length; i++)
            {
                if (luminance[i] >= average)
                    hash |= 1UL << i;
            }

            return hash;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> NormalizeReferenceRoots(IEnumerable<string>? referenceRoots)
    {
        if (referenceRoots is null)
            return [];

        return referenceRoots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(TryNormalizeReferenceRoot)
            .Where(root => root is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? TryNormalizeReferenceRoot(string root)
    {
        try
        {
            var full = System.IO.Path.GetFullPath(root);
            if (File.Exists(full))
                full = System.IO.Path.GetDirectoryName(full) ?? full;

            if (!Directory.Exists(full))
                return null;

            return EnsureTrailingSeparator(full);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or SecurityException)
        {
            return null;
        }
    }

    private static bool IsUnderReferenceRoot(string path, IReadOnlyList<string> referenceRoots)
    {
        if (referenceRoots.Count == 0)
            return false;

        string fullPath;
        try
        {
            fullPath = System.IO.Path.GetFullPath(path);
        }
        catch
        {
            return false;
        }

        return referenceRoots.Any(root => fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase));
    }

    private static string EnsureTrailingSeparator(string path)
    {
        var separator = System.IO.Path.DirectorySeparatorChar;
        return path.EndsWith(separator) || path.EndsWith(System.IO.Path.AltDirectorySeparatorChar)
            ? path
            : path + separator;
    }

    private static string ResolveUniqueDestination(string directory, string fileName)
    {
        var safeName = string.IsNullOrWhiteSpace(fileName) ? "file" : fileName;
        var destination = System.IO.Path.Combine(directory, safeName);
        if (!File.Exists(destination))
            return destination;

        var stem = System.IO.Path.GetFileNameWithoutExtension(safeName);
        var extension = System.IO.Path.GetExtension(safeName);
        for (var i = 1; i < 10_000; i++)
        {
            destination = System.IO.Path.Combine(directory, $"{stem} ({i}){extension}");
            if (!File.Exists(destination))
                return destination;
        }

        return System.IO.Path.Combine(directory, $"{stem}-{Guid.NewGuid():N}{extension}");
    }

    private static void WriteManifest(
        string batch,
        IReadOnlyList<DuplicateCleanupMoveResult> moved,
        IReadOnlyList<DuplicateCleanupFailure> failed)
    {
        try
        {
            var manifest = new StringBuilder();
            manifest.AppendLine("Images duplicate-cleanup quarantine");
            manifest.AppendLine("Created: " + DateTimeOffset.Now.ToString("O"));
            manifest.AppendLine();

            foreach (var item in moved)
                manifest.AppendLine($"MOVED\t{item.SourcePath}\t{item.DestinationPath}");

            foreach (var item in failed)
                manifest.AppendLine($"FAILED\t{item.Path}\t{item.Error}");

            File.WriteAllText(System.IO.Path.Combine(batch, "manifest.tsv"), manifest.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Quarantine must not fail because the recovery manifest could not be written.
        }
    }

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

    private sealed class PerceptualHashTree
    {
        private Node? _root;

        public void Add(DuplicateCleanupCandidate candidate, ulong hash)
        {
            if (_root is null)
            {
                _root = new Node(candidate, hash);
                return;
            }

            var current = _root;
            while (true)
            {
                var distance = HammingDistance(hash, current.Hash);
                if (!current.Children.TryGetValue(distance, out var child))
                {
                    current.Children[distance] = new Node(candidate, hash);
                    return;
                }

                current = child;
            }
        }

        public IEnumerable<(DuplicateCleanupCandidate Candidate, int Distance)> FindWithin(ulong hash, int threshold)
        {
            if (_root is null)
                yield break;

            foreach (var match in FindWithin(_root, hash, threshold))
                yield return match;
        }

        private static IEnumerable<(DuplicateCleanupCandidate Candidate, int Distance)> FindWithin(
            Node node,
            ulong hash,
            int threshold)
        {
            var distance = HammingDistance(hash, node.Hash);
            if (distance <= threshold)
                yield return (node.Candidate, distance);

            var min = Math.Max(0, distance - threshold);
            var max = distance + threshold;
            foreach (var child in node.Children.Where(pair => pair.Key >= min && pair.Key <= max))
            {
                foreach (var match in FindWithin(child.Value, hash, threshold))
                    yield return match;
            }
        }

        private sealed class Node(DuplicateCleanupCandidate candidate, ulong hash)
        {
            public DuplicateCleanupCandidate Candidate { get; } = candidate;
            public ulong Hash { get; } = hash;
            public Dictionary<int, Node> Children { get; } = [];
        }
    }
}
