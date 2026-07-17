using Images.Services;

namespace Images.Tests;

public sealed class TripDetectionServiceTests
{
    [Fact]
    public void Build_GroupsContiguousAwayDaysAndChoosesRatedCover()
    {
        var first = Asset("first.jpg", 40.7128, -74.0060, "2026-07-10T10:00:00Z", rating: 2);
        var cover = Asset("cover.jpg", 40.7306, -73.9352, "2026-07-11T12:00:00Z", rating: 5);
        var laterTrip = Asset("later.jpg", 34.0522, -118.2437, "2026-07-15T09:00:00Z");
        var atHome = Asset("home.jpg", 48.8566, 2.3522, "2026-07-12T09:00:00Z");

        var trips = new TripDetectionService().Build(
            [first, cover, laterTrip, atHome],
            homeLatitude: 48.8566,
            homeLongitude: 2.3522,
            minDistanceFromHomeKm: 100,
            maxGapDays: 1);

        Assert.Equal(2, trips.Count);
        var newYork = trips[1];
        Assert.Equal(cover.SourcePath, newYork.Cover.SourcePath);
        Assert.Equal([first.SourcePath, cover.SourcePath], newYork.Assets.Select(asset => asset.SourcePath));
        Assert.Equal(TimeSpan.FromHours(26), newYork.EndedUtc - newYork.StartedUtc);
        Assert.InRange(newYork.CentroidLatitude, 40.71, 40.74);
        Assert.DoesNotContain(atHome.SourcePath, trips.SelectMany(trip => trip.Assets).Select(asset => asset.SourcePath));
    }

    [Fact]
    public void Build_RequiresGpsAndCaptureTimeAndHandlesAntimeridianCentroid()
    {
        var west = Asset("west.jpg", 10, 179, "2026-07-10T10:00:00Z");
        var east = Asset("east.jpg", 10, -179, "2026-07-10T11:00:00Z");
        var noCapture = west with { SourcePath = "no-time.jpg", Exif = west.ExifFacts with { CapturedUtc = null } };
        var noGps = west with { SourcePath = "no-gps.jpg", Exif = west.ExifFacts with { Latitude = null, Longitude = null } };

        var trip = Assert.Single(new TripDetectionService().Build([west, east, noCapture, noGps], 0, 0, 10));

        Assert.Equal(2, trip.AssetCount);
        Assert.True(Math.Abs(trip.CentroidLongitude) > 178);
    }

    private static CatalogAssetRecord Asset(
        string path,
        double latitude,
        double longitude,
        string capturedUtc,
        int? rating = null)
    {
        var captured = DateTimeOffset.Parse(capturedUtc, System.Globalization.CultureInfo.InvariantCulture);
        return new CatalogAssetRecord(
            SourcePath: path,
            Fingerprint: path,
            SizeBytes: 100,
            CreatedUtc: captured,
            ModifiedUtc: captured,
            Width: 1600,
            Height: 900,
            Format: "JPG",
            Codec: "Jpeg",
            Rating: rating,
            Tags: [],
            SidecarPath: null,
            SidecarModifiedUtc: null,
            ScannedUtc: captured,
            Exif: new CatalogExifFacts(latitude, longitude, captured, null, null, null, null, null, null, null));
    }
}
