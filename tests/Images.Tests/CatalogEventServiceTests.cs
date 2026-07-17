using Images.Services;

namespace Images.Tests;

public sealed class CatalogEventServiceTests
{
    [Fact]
    public void Build_ClustersByGapAndChoosesRatedKeyPhoto()
    {
        var first = Asset("first.jpg", "2026-07-10T10:00:00Z", rating: 2);
        var keyPhoto = Asset("key.jpg", "2026-07-10T12:00:00Z", rating: 5);
        var later = Asset("later.jpg", "2026-07-11T08:00:00Z");

        var events = new CatalogEventService().Build([later, keyPhoto, first], TimeSpan.FromHours(6));

        Assert.Equal(2, events.Count);
        var morning = events[1];
        Assert.Equal(keyPhoto.SourcePath, morning.KeyPhoto.SourcePath);
        Assert.Equal([first.SourcePath, keyPhoto.SourcePath], morning.Assets.Select(asset => asset.SourcePath));
        Assert.Equal(TimeSpan.FromHours(2), morning.Duration);
    }

    [Fact]
    public void Build_UsesModifiedTimeFallbackAndIsStableAcrossInputOrder()
    {
        var first = Asset("a.jpg", "2026-07-10T10:00:00Z", hasCaptureTime: false);
        var second = Asset("b.jpg", "2026-07-10T11:00:00Z", hasCaptureTime: false);
        var service = new CatalogEventService();

        var forward = Assert.Single(service.Build([first, second]));
        var reverse = Assert.Single(service.Build([second, first, first]));

        Assert.Equal(forward.EventId, reverse.EventId);
        Assert.Equal(2, reverse.AssetCount);
        Assert.Equal(first.ModifiedUtc, reverse.StartedUtc);
    }

    private static CatalogAssetRecord Asset(
        string path,
        string timestamp,
        int? rating = null,
        bool hasCaptureTime = true)
    {
        var time = DateTimeOffset.Parse(timestamp, System.Globalization.CultureInfo.InvariantCulture);
        return new CatalogAssetRecord(
            SourcePath: path,
            Fingerprint: path,
            SizeBytes: 100,
            CreatedUtc: time,
            ModifiedUtc: time,
            Width: 1600,
            Height: 900,
            Format: "JPG",
            Codec: "Jpeg",
            Rating: rating,
            Tags: [],
            SidecarPath: null,
            SidecarModifiedUtc: null,
            ScannedUtc: time,
            Exif: new CatalogExifFacts(null, null, hasCaptureTime ? time : null, null, null, null, null, null, null, null));
    }
}
