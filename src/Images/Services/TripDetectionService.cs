using System.Security.Cryptography;
using System.Text;

namespace Images.Services;

public sealed record CatalogTrip(
    string TripId,
    CatalogAssetRecord Cover,
    IReadOnlyList<CatalogAssetRecord> Assets,
    DateTimeOffset StartedUtc,
    DateTimeOffset EndedUtc,
    double CentroidLatitude,
    double CentroidLongitude,
    double MaxDistanceFromHomeKm)
{
    public int AssetCount => Assets.Count;
}

public sealed class TripDetectionService
{
    private const double EarthRadiusKm = 6_371.0088d;

    public IReadOnlyList<CatalogTrip> Build(
        IEnumerable<CatalogAssetRecord> assets,
        double homeLatitude,
        double homeLongitude,
        double minDistanceFromHomeKm = 50d,
        int maxGapDays = 1,
        int limit = 1000)
    {
        ArgumentNullException.ThrowIfNull(assets);
        if (!double.IsFinite(homeLatitude) || homeLatitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(homeLatitude));
        if (!double.IsFinite(homeLongitude) || homeLongitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(homeLongitude));
        if (!double.IsFinite(minDistanceFromHomeKm) || minDistanceFromHomeKm <= 0)
            throw new ArgumentOutOfRangeException(nameof(minDistanceFromHomeKm));
        if (maxGapDays is < 0 or > 31)
            throw new ArgumentOutOfRangeException(nameof(maxGapDays));
        limit = Math.Clamp(limit, 1, 10_000);

        var away = assets
            .Where(asset => asset.ExifFacts.HasGeo && asset.ExifFacts.CapturedUtc.HasValue)
            .DistinctBy(asset => asset.SourcePath, StringComparer.OrdinalIgnoreCase)
            .Select(asset => new Candidate(
                asset,
                asset.ExifFacts.CapturedUtc!.Value,
                DistanceKm(
                    homeLatitude,
                    homeLongitude,
                    asset.ExifFacts.Latitude!.Value,
                    asset.ExifFacts.Longitude!.Value)))
            .Where(candidate => candidate.DistanceFromHomeKm >= minDistanceFromHomeKm)
            .OrderBy(candidate => candidate.CapturedUtc)
            .ThenBy(candidate => candidate.Asset.SourcePath, StringComparer.OrdinalIgnoreCase)
            .Take(50_000)
            .ToArray();
        if (away.Length == 0)
            return [];

        var groups = new List<List<Candidate>>();
        foreach (var candidate in away)
        {
            var current = groups.Count == 0 ? null : groups[^1];
            if (current is null || CalendarDayGap(current[^1].CapturedUtc, candidate.CapturedUtc) > maxGapDays)
            {
                current = [];
                groups.Add(current);
            }

            current.Add(candidate);
        }

        return groups
            .Select(CreateTrip)
            .OrderByDescending(trip => trip.StartedUtc)
            .ThenBy(trip => trip.TripId, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

    private static CatalogTrip CreateTrip(IReadOnlyList<Candidate> candidates)
    {
        var assets = candidates.Select(candidate => candidate.Asset).ToArray();
        var cover = assets
            .OrderByDescending(asset => asset.Rating ?? 0)
            .ThenByDescending(asset => (long)asset.Width * asset.Height)
            .ThenBy(asset => asset.ExifFacts.CapturedUtc)
            .ThenBy(asset => asset.SourcePath, StringComparer.OrdinalIgnoreCase)
            .First();
        var (latitude, longitude) = SphericalCentroid(candidates);

        return new CatalogTrip(
            CreateStableId(assets.Select(asset => asset.SourcePath)),
            cover,
            assets,
            candidates[0].CapturedUtc,
            candidates[^1].CapturedUtc,
            latitude,
            longitude,
            candidates.Max(candidate => candidate.DistanceFromHomeKm));
    }

    private static int CalendarDayGap(DateTimeOffset left, DateTimeOffset right)
        => (right.UtcDateTime.Date - left.UtcDateTime.Date).Days;

    private static (double Latitude, double Longitude) SphericalCentroid(IEnumerable<Candidate> candidates)
    {
        var x = 0d;
        var y = 0d;
        var z = 0d;
        var count = 0;
        foreach (var candidate in candidates)
        {
            var latitude = DegreesToRadians(candidate.Asset.ExifFacts.Latitude!.Value);
            var longitude = DegreesToRadians(candidate.Asset.ExifFacts.Longitude!.Value);
            x += Math.Cos(latitude) * Math.Cos(longitude);
            y += Math.Cos(latitude) * Math.Sin(longitude);
            z += Math.Sin(latitude);
            count++;
        }

        x /= count;
        y /= count;
        z /= count;
        var longitudeResult = Math.Atan2(y, x);
        var latitudeResult = Math.Atan2(z, Math.Sqrt((x * x) + (y * y)));
        return (RadiansToDegrees(latitudeResult), RadiansToDegrees(longitudeResult));
    }

    private static double DistanceKm(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var latitudeDelta = DegreesToRadians(latitude2 - latitude1);
        var longitudeDelta = DegreesToRadians(longitude2 - longitude1);
        var firstLatitude = DegreesToRadians(latitude1);
        var secondLatitude = DegreesToRadians(latitude2);
        var a = Math.Pow(Math.Sin(latitudeDelta / 2d), 2d) +
                Math.Cos(firstLatitude) * Math.Cos(secondLatitude) *
                Math.Pow(Math.Sin(longitudeDelta / 2d), 2d);
        return EarthRadiusKm * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(Math.Max(0d, 1d - a)));
    }

    private static string CreateStableId(IEnumerable<string> paths)
    {
        var payload = string.Join('\n', paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))[..16].ToLowerInvariant();
    }

    private static double DegreesToRadians(double value) => value * Math.PI / 180d;
    private static double RadiansToDegrees(double value) => value * 180d / Math.PI;

    private sealed record Candidate(
        CatalogAssetRecord Asset,
        DateTimeOffset CapturedUtc,
        double DistanceFromHomeKm);
}
