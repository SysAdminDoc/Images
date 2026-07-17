using System.Security.Cryptography;
using System.Text;

namespace Images.Services;

public sealed record CatalogEvent(
    string EventId,
    CatalogAssetRecord KeyPhoto,
    IReadOnlyList<CatalogAssetRecord> Assets,
    DateTimeOffset StartedUtc,
    DateTimeOffset EndedUtc)
{
    public int AssetCount => Assets.Count;
    public TimeSpan Duration => EndedUtc - StartedUtc;
}

public sealed class CatalogEventService
{
    public IReadOnlyList<CatalogEvent> Build(
        IEnumerable<CatalogAssetRecord> assets,
        TimeSpan? maxGap = null,
        int limit = 1000)
    {
        ArgumentNullException.ThrowIfNull(assets);
        var eventGap = maxGap ?? TimeSpan.FromHours(6);
        if (eventGap < TimeSpan.Zero || eventGap > TimeSpan.FromDays(31))
            throw new ArgumentOutOfRangeException(nameof(maxGap));
        limit = Math.Clamp(limit, 1, 10_000);

        var timeline = assets
            .DistinctBy(asset => asset.SourcePath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(EffectiveTime)
            .ThenBy(asset => asset.SourcePath, StringComparer.OrdinalIgnoreCase)
            .Take(50_000)
            .ToArray();
        if (timeline.Length == 0)
            return [];

        var groups = new List<List<CatalogAssetRecord>>();
        foreach (var asset in timeline)
        {
            var current = groups.Count == 0 ? null : groups[^1];
            if (current is null || EffectiveTime(asset) - EffectiveTime(current[^1]) > eventGap)
            {
                current = [];
                groups.Add(current);
            }

            current.Add(asset);
        }

        return groups
            .Select(CreateEvent)
            .OrderByDescending(item => item.StartedUtc)
            .ThenBy(item => item.EventId, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

    private static CatalogEvent CreateEvent(IReadOnlyList<CatalogAssetRecord> assets)
    {
        var ordered = assets
            .OrderBy(EffectiveTime)
            .ThenBy(asset => asset.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var midpointTicks = EffectiveTime(ordered[0]).Ticks +
                            ((EffectiveTime(ordered[^1]).Ticks - EffectiveTime(ordered[0]).Ticks) / 2);
        var keyPhoto = assets
            .OrderByDescending(asset => asset.Rating ?? 0)
            .ThenByDescending(asset => (long)asset.Width * asset.Height)
            .ThenBy(asset => Math.Abs(EffectiveTime(asset).Ticks - midpointTicks))
            .ThenBy(asset => asset.SourcePath, StringComparer.OrdinalIgnoreCase)
            .First();

        return new CatalogEvent(
            CreateStableId(ordered.Select(asset => asset.SourcePath)),
            keyPhoto,
            ordered,
            EffectiveTime(ordered[0]),
            EffectiveTime(ordered[^1]));
    }

    private static DateTimeOffset EffectiveTime(CatalogAssetRecord asset)
        => asset.ExifFacts.CapturedUtc ?? asset.ModifiedUtc;

    private static string CreateStableId(IEnumerable<string> paths)
    {
        var payload = string.Join('\n', paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))[..16].ToLowerInvariant();
    }
}
