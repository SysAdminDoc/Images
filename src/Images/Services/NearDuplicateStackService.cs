using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Images.Services;

public sealed record NearDuplicateStack(
    string StackId,
    CatalogAssetRecord Cover,
    IReadOnlyList<CatalogAssetRecord> Assets,
    int MaxHashDistance,
    TimeSpan CaptureSpan,
    double? MaxGeoDistanceMeters)
{
    public int AssetCount => Assets.Count;
}

public sealed class NearDuplicateStackService
{
    private const double EarthRadiusMeters = 6_371_008.8d;
    private const int MaxForwardComparisonsPerAsset = 512;

    public IReadOnlyList<NearDuplicateStack> Build(
        IEnumerable<CatalogAssetRecord> assets,
        int maxHashDistance = 6,
        TimeSpan? maxCaptureDelta = null,
        double maxGeoDistanceMeters = 250d,
        int limit = 1000)
    {
        ArgumentNullException.ThrowIfNull(assets);
        maxHashDistance = Math.Clamp(maxHashDistance, 0, 64);
        var captureDelta = maxCaptureDelta ?? TimeSpan.FromMinutes(2);
        if (captureDelta < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maxCaptureDelta));
        if (!double.IsFinite(maxGeoDistanceMeters) || maxGeoDistanceMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(maxGeoDistanceMeters));
        limit = Math.Clamp(limit, 1, 10_000);

        var candidates = assets
            .Where(asset => asset.PerceptualHash.HasValue)
            .DistinctBy(asset => asset.SourcePath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(EffectiveTime)
            .ThenBy(asset => asset.SourcePath, StringComparer.OrdinalIgnoreCase)
            .Take(50_000)
            .ToArray();
        if (candidates.Length < 2)
            return [];

        var components = new DisjointSet(candidates.Length);
        for (var left = 0; left < candidates.Length; left++)
        {
            var leftTime = EffectiveTime(candidates[left]);
            var rightLimit = Math.Min(candidates.Length, left + 1 + MaxForwardComparisonsPerAsset);
            for (var right = left + 1; right < rightLimit; right++)
            {
                var timeDelta = EffectiveTime(candidates[right]) - leftTime;
                if (timeDelta > captureDelta)
                    break;
                if (AreNeighbors(candidates[left], candidates[right], maxHashDistance, captureDelta, maxGeoDistanceMeters))
                    components.Union(left, right);
            }
        }

        return Enumerable.Range(0, candidates.Length)
            .GroupBy(components.Find)
            .Select(group => group.Select(index => candidates[index]).ToArray())
            .Where(group => group.Length > 1)
            .SelectMany(group => SplitCompleteLink(group, maxHashDistance, captureDelta, maxGeoDistanceMeters))
            .Where(group => group.Length > 1)
            .Select(CreateStack)
            .OrderByDescending(stack => EffectiveTime(stack.Cover))
            .ThenBy(stack => stack.StackId, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

    private static IEnumerable<CatalogAssetRecord[]> SplitCompleteLink(
        IReadOnlyList<CatalogAssetRecord> component,
        int maxHashDistance,
        TimeSpan maxCaptureDelta,
        double maxGeoDistanceMeters)
    {
        var clusters = new List<List<CatalogAssetRecord>>();
        foreach (var candidate in component.OrderBy(EffectiveTime).ThenBy(asset => asset.SourcePath, StringComparer.OrdinalIgnoreCase))
        {
            var cluster = clusters.FirstOrDefault(existing => existing.All(member =>
                AreNeighbors(member, candidate, maxHashDistance, maxCaptureDelta, maxGeoDistanceMeters)));
            if (cluster is null)
                clusters.Add([candidate]);
            else
                cluster.Add(candidate);
        }

        return clusters.Select(cluster => cluster.ToArray());
    }

    private static bool AreNeighbors(
        CatalogAssetRecord left,
        CatalogAssetRecord right,
        int maxHashDistance,
        TimeSpan maxCaptureDelta,
        double maxGeoDistanceMeters)
    {
        if (string.Equals(left.Fingerprint, right.Fingerprint, StringComparison.OrdinalIgnoreCase))
            return false;
        if (EffectiveTime(right) - EffectiveTime(left) is var delta && delta.Duration() > maxCaptureDelta)
            return false;
        if (!HasCompatibleAspect(left, right) || !HasCompatibleLocation(left, right, maxGeoDistanceMeters))
            return false;
        return BitOperations.PopCount(left.PerceptualHash!.Value ^ right.PerceptualHash!.Value) <= maxHashDistance;
    }

    private static NearDuplicateStack CreateStack(CatalogAssetRecord[] assets)
    {
        var ordered = assets
            .OrderBy(EffectiveTime)
            .ThenBy(asset => asset.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var cover = assets
            .OrderByDescending(asset => asset.Rating ?? 0)
            .ThenByDescending(asset => (long)asset.Width * asset.Height)
            .ThenBy(EffectiveTime)
            .ThenBy(asset => asset.SourcePath, StringComparer.OrdinalIgnoreCase)
            .First();
        var maxHashDistance = 0;
        double? maxGeoDistance = null;
        for (var left = 0; left < ordered.Length; left++)
        {
            for (var right = left + 1; right < ordered.Length; right++)
            {
                maxHashDistance = Math.Max(
                    maxHashDistance,
                    BitOperations.PopCount(ordered[left].PerceptualHash!.Value ^ ordered[right].PerceptualHash!.Value));
                var distance = GeoDistanceMeters(ordered[left], ordered[right]);
                if (distance is not null)
                    maxGeoDistance = Math.Max(maxGeoDistance ?? 0d, distance.Value);
            }
        }

        return new NearDuplicateStack(
            CreateStableId(ordered.Select(asset => asset.SourcePath)),
            cover,
            ordered,
            maxHashDistance,
            EffectiveTime(ordered[^1]) - EffectiveTime(ordered[0]),
            maxGeoDistance);
    }

    private static bool HasCompatibleAspect(CatalogAssetRecord left, CatalogAssetRecord right)
    {
        if (left.Width <= 0 || left.Height <= 0 || right.Width <= 0 || right.Height <= 0)
            return false;
        var leftRatio = left.Width / (double)left.Height;
        var rightRatio = right.Width / (double)right.Height;
        return Math.Abs(leftRatio - rightRatio) / Math.Max(leftRatio, rightRatio) <= 0.08d;
    }

    private static bool HasCompatibleLocation(
        CatalogAssetRecord left,
        CatalogAssetRecord right,
        double maxGeoDistanceMeters)
    {
        var distance = GeoDistanceMeters(left, right);
        return distance is null || distance.Value <= maxGeoDistanceMeters;
    }

    private static double? GeoDistanceMeters(CatalogAssetRecord left, CatalogAssetRecord right)
    {
        var leftExif = left.ExifFacts;
        var rightExif = right.ExifFacts;
        if (!leftExif.HasGeo || !rightExif.HasGeo)
            return null;

        var latitudeDelta = DegreesToRadians(rightExif.Latitude!.Value - leftExif.Latitude!.Value);
        var longitudeDelta = DegreesToRadians(rightExif.Longitude!.Value - leftExif.Longitude!.Value);
        var leftLatitude = DegreesToRadians(leftExif.Latitude.Value);
        var rightLatitude = DegreesToRadians(rightExif.Latitude.Value);
        var a = Math.Pow(Math.Sin(latitudeDelta / 2d), 2d) +
                Math.Cos(leftLatitude) * Math.Cos(rightLatitude) *
                Math.Pow(Math.Sin(longitudeDelta / 2d), 2d);
        return EarthRadiusMeters * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(Math.Max(0d, 1d - a)));
    }

    private static DateTimeOffset EffectiveTime(CatalogAssetRecord asset)
        => asset.ExifFacts.CapturedUtc ?? asset.ModifiedUtc;

    private static double DegreesToRadians(double value) => value * Math.PI / 180d;

    private static string CreateStableId(IEnumerable<string> paths)
    {
        var payload = string.Join('\n', paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))[..16].ToLowerInvariant();
    }

    private sealed class DisjointSet
    {
        private readonly int[] _parent;
        private readonly byte[] _rank;

        public DisjointSet(int count)
        {
            _parent = Enumerable.Range(0, count).ToArray();
            _rank = new byte[count];
        }

        public int Find(int value)
        {
            if (_parent[value] != value)
                _parent[value] = Find(_parent[value]);
            return _parent[value];
        }

        public void Union(int left, int right)
        {
            var leftRoot = Find(left);
            var rightRoot = Find(right);
            if (leftRoot == rightRoot)
                return;
            if (_rank[leftRoot] < _rank[rightRoot])
                _parent[leftRoot] = rightRoot;
            else if (_rank[leftRoot] > _rank[rightRoot])
                _parent[rightRoot] = leftRoot;
            else
            {
                _parent[rightRoot] = leftRoot;
                _rank[leftRoot]++;
            }
        }
    }
}
