using System.Globalization;
using System.IO;
using System.Xml.Linq;
using ImageMagick;

namespace Images.Services;

public enum FaceReviewDecision
{
    Pending,
    Accepted,
    Rejected,
}

public sealed record FaceReviewKey(string SourcePath, int FaceIndex);

public sealed record FaceReviewCandidate(
    FaceReviewKey Key,
    FaceDetection Detection,
    FaceEmbeddingQuality EmbeddingQuality,
    string? QualityNote,
    int? ClusterId);

public sealed record FaceReviewEntry(FaceReviewKey Key, FaceReviewDecision Decision, string? Name);

public sealed record FaceReviewAnalysis(
    IReadOnlyList<FaceReviewCandidate> Candidates,
    IReadOnlyList<FaceRecognitionResult> Failures);

public sealed record FaceReviewSidecarResult(
    string SourcePath,
    string SidecarPath,
    bool Success,
    int AcceptedRegions,
    string Message);

public sealed record FaceReviewMergeResult(
    bool Success,
    IReadOnlyList<FaceReviewSidecarResult> Sidecars,
    string Message);

public static class FaceReviewService
{
    private static readonly XNamespace X = "adobe:ns:meta/";
    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace MwgRs = "http://www.metadataworkinggroup.com/schemas/regions/";
    private static readonly XNamespace StArea = "http://ns.adobe.com/xmp/sType/Area#";
    private static readonly XNamespace Imv = "https://github.com/SysAdminDoc/Images/ns/1.0/";

    public static FaceReviewAnalysis Analyze(
        IReadOnlyList<string> imagePaths,
        Func<string, FaceRecognitionResult>? analyzer = null)
    {
        ArgumentNullException.ThrowIfNull(imagePaths);
        analyzer ??= path => FaceRecognitionService.Analyze(path);
        var analyses = imagePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(analyzer)
            .ToArray();
        var successful = analyses.Where(result => result.Success).ToArray();
        var embeddings = successful.SelectMany(result => result.Faces).ToArray();
        var clusters = FaceClusterService.Cluster(embeddings);
        var clusterIds = clusters.Clusters
            .SelectMany(cluster => cluster.Members.Select(member =>
                new KeyValuePair<FaceReviewKey, int>(
                    new FaceReviewKey(member.SourcePath, member.FaceIndex),
                    cluster.ClusterId)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, FaceReviewKeyComparer.Instance);

        var candidates = embeddings.Select(face =>
        {
            var key = new FaceReviewKey(face.SourcePath, face.FaceIndex);
            return new FaceReviewCandidate(
                key,
                face.Detection,
                face.Quality,
                face.RejectionReason,
                clusterIds.GetValueOrDefault(key));
        }).ToArray();
        return new FaceReviewAnalysis(candidates, analyses.Where(result => !result.Success).ToArray());
    }

    public static bool CanMerge(
        IReadOnlyList<FaceReviewCandidate> candidates,
        IReadOnlyList<FaceReviewEntry> reviews,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(reviews);
        if (candidates.Count == 0)
        {
            reason = "No detected face regions are available to review.";
            return false;
        }

        var candidateKeys = candidates.Select(candidate => candidate.Key)
            .ToHashSet(FaceReviewKeyComparer.Instance);
        if (candidateKeys.Count != candidates.Count)
        {
            reason = "Detected face region keys are not unique.";
            return false;
        }

        var reviewMap = new Dictionary<FaceReviewKey, FaceReviewEntry>(FaceReviewKeyComparer.Instance);
        foreach (var review in reviews)
        {
            if (!candidateKeys.Contains(review.Key) || !reviewMap.TryAdd(review.Key, review))
            {
                reason = "The review set does not match the detected face regions.";
                return false;
            }
        }
        if (reviewMap.Count != candidates.Count)
        {
            reason = "Every detected face region must be reviewed.";
            return false;
        }
        if (reviewMap.Values.Any(review => review.Decision == FaceReviewDecision.Pending))
        {
            reason = "Accept or reject every detected face region before merging.";
            return false;
        }
        if (reviewMap.Values.Any(review =>
                review.Decision == FaceReviewDecision.Accepted && string.IsNullOrWhiteSpace(review.Name)))
        {
            reason = "Every accepted face region needs a name before merging.";
            return false;
        }

        reason = "All detected face regions have been reviewed.";
        return true;
    }

    public static FaceReviewMergeResult MergeReviewedRegions(
        IReadOnlyList<FaceReviewCandidate> candidates,
        IReadOnlyList<FaceReviewEntry> reviews)
    {
        if (!CanMerge(candidates, reviews, out var reason))
            return new FaceReviewMergeResult(false, [], reason);

        var reviewMap = reviews.ToDictionary(review => review.Key, FaceReviewKeyComparer.Instance);
        var sidecars = new List<FaceReviewSidecarResult>();
        foreach (var imageGroup in candidates.GroupBy(
                     candidate => candidate.Key.SourcePath,
                     StringComparer.OrdinalIgnoreCase))
        {
            var sourcePath = imageGroup.Key;
            var sidecarPath = sourcePath + ".xmp";
            try
            {
                var document = File.Exists(sidecarPath)
                    ? BoundedXmlReader.Load(sidecarPath, LoadOptions.PreserveWhitespace)
                    : CreateEmptySidecar();
                var description = EnsureDescription(document);
                EnsureNamespaces(document);
                var bag = EnsureRegionBag(description);
                bag.Elements(Rdf + "li")
                    .Where(item => string.Equals(
                        item.Attribute(Imv + "FaceReviewSource")?.Value,
                        "Images",
                        StringComparison.Ordinal))
                    .Remove();

                var accepted = imageGroup
                    .Select(candidate => (Candidate: candidate, Review: reviewMap[candidate.Key]))
                    .Where(item => item.Review.Decision == FaceReviewDecision.Accepted)
                    .ToArray();
                using var image = MagickSafeReader.Read(
                    sourcePath,
                    new MagickReadSettings { FrameIndex = 0, FrameCount = 1 });
                image.AutoOrient();
                var sourceWidth = checked((int)image.Width);
                var sourceHeight = checked((int)image.Height);
                foreach (var item in accepted)
                    bag.Add(BuildRegion(
                        item.Candidate,
                        item.Review.Name!.Trim(),
                        sourceWidth,
                        sourceHeight));

                description.SetAttributeValue(
                    Imv + "FaceReviewMergedUtc",
                    DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                SidecarWriter.SaveAtomically(document, sidecarPath);
                sidecars.Add(new FaceReviewSidecarResult(
                    sourcePath,
                    sidecarPath,
                    true,
                    accepted.Length,
                    $"Merged {accepted.Length} reviewed face region(s)."));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Xml.XmlException or MagickException)
            {
                sidecars.Add(new FaceReviewSidecarResult(
                    sourcePath,
                    sidecarPath,
                    false,
                    0,
                    $"Could not merge reviewed face regions: {ex.Message}"));
            }
        }

        var failures = sidecars.Count(result => !result.Success);
        var acceptedCount = sidecars.Where(result => result.Success).Sum(result => result.AcceptedRegions);
        return new FaceReviewMergeResult(
            failures == 0,
            sidecars,
            failures == 0
                ? $"Merged {acceptedCount} reviewed face region(s) into {sidecars.Count} XMP sidecar(s)."
                : $"Merged reviewed face regions into {sidecars.Count - failures} sidecar(s); {failures} failed.");
    }

    private static XElement BuildRegion(
        FaceReviewCandidate candidate,
        string name,
        int sourceWidth,
        int sourceHeight)
    {
        var face = candidate.Detection;
        return new XElement(
            Rdf + "li",
            new XAttribute(Rdf + "parseType", "Resource"),
            new XAttribute(Imv + "FaceReviewSource", "Images"),
            new XElement(MwgRs + "Name", name),
            new XElement(MwgRs + "Type", "Face"),
            new XElement(
                MwgRs + "Area",
                new XAttribute(Rdf + "parseType", "Resource"),
                new XAttribute(StArea + "x", Format(face.NormalizedCenterX(sourceWidth))),
                new XAttribute(StArea + "y", Format(face.NormalizedCenterY(sourceHeight))),
                new XAttribute(StArea + "w", Format(face.NormalizedWidth(sourceWidth))),
                new XAttribute(StArea + "h", Format(face.NormalizedHeight(sourceHeight))),
                new XAttribute(StArea + "unit", "normalized")),
            new XElement(Imv + "DetectionConfidence", face.Confidence.ToString("0.######", CultureInfo.InvariantCulture)),
            candidate.ClusterId is null ? null : new XElement(Imv + "FaceClusterId", candidate.ClusterId.Value));
    }

    private static XDocument CreateEmptySidecar() => new(
        new XElement(
            X + "xmpmeta",
            new XAttribute(XNamespace.Xmlns + "x", X.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "rdf", Rdf.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "mwg-rs", MwgRs.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "stArea", StArea.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "imv", Imv.NamespaceName),
            new XElement(Rdf + "RDF", new XElement(Rdf + "Description"))));

    private static XElement EnsureDescription(XDocument document)
    {
        if (document.Root is null)
            document.Add(CreateEmptySidecar().Root!);
        var rdf = document.Descendants(Rdf + "RDF").FirstOrDefault();
        if (rdf is null)
        {
            rdf = new XElement(Rdf + "RDF");
            document.Root!.Add(rdf);
        }
        var description = rdf.Elements(Rdf + "Description").FirstOrDefault();
        if (description is null)
        {
            description = new XElement(Rdf + "Description");
            rdf.Add(description);
        }
        return description;
    }

    private static XElement EnsureRegionBag(XElement description)
    {
        var regions = description.Element(MwgRs + "Regions");
        if (regions is null)
        {
            regions = new XElement(MwgRs + "Regions", new XAttribute(Rdf + "parseType", "Resource"));
            description.Add(regions);
        }
        var list = regions.Element(MwgRs + "RegionList");
        if (list is null)
        {
            list = new XElement(MwgRs + "RegionList");
            regions.Add(list);
        }
        var bag = list.Element(Rdf + "Bag");
        if (bag is null)
        {
            bag = new XElement(Rdf + "Bag");
            list.Add(bag);
        }
        return bag;
    }

    private static void EnsureNamespaces(XDocument document)
    {
        var root = document.Root!;
        root.SetAttributeValue(XNamespace.Xmlns + "rdf", Rdf.NamespaceName);
        root.SetAttributeValue(XNamespace.Xmlns + "mwg-rs", MwgRs.NamespaceName);
        root.SetAttributeValue(XNamespace.Xmlns + "stArea", StArea.NamespaceName);
        root.SetAttributeValue(XNamespace.Xmlns + "imv", Imv.NamespaceName);
    }

    private static string Format(double value) =>
        Math.Clamp(value, 0, 1).ToString("0.######", CultureInfo.InvariantCulture);

    private sealed class FaceReviewKeyComparer : IEqualityComparer<FaceReviewKey>
    {
        public static FaceReviewKeyComparer Instance { get; } = new();

        public bool Equals(FaceReviewKey? left, FaceReviewKey? right) =>
            ReferenceEquals(left, right) || left is not null && right is not null &&
            left.FaceIndex == right.FaceIndex &&
            string.Equals(left.SourcePath, right.SourcePath, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(FaceReviewKey value) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(value.SourcePath), value.FaceIndex);
    }
}
