using Images.Services;

namespace Images.Tests;

public sealed class NearDuplicateStackServiceTests
{
    [Fact]
    public void Build_GroupsTimeGeoAndHashNeighborsAndChoosesRatedCover()
    {
        var captured = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
        var first = Asset("first.jpg", "first", 0b1111UL, captured, 48.8583, 2.2945, rating: 2);
        var cover = Asset("cover.jpg", "cover", 0b1110UL, captured.AddSeconds(8), 48.85831, 2.29451, rating: 5);
        var farTime = Asset("later.jpg", "later", 0b1110UL, captured.AddMinutes(5), 48.85831, 2.29451);
        var farGeo = Asset("elsewhere.jpg", "elsewhere", 0b1110UL, captured.AddSeconds(5), 40.7128, -74.0060);

        var stacks = new NearDuplicateStackService().Build([first, cover, farTime, farGeo]);

        var stack = Assert.Single(stacks);
        Assert.Equal(cover.SourcePath, stack.Cover.SourcePath);
        Assert.Equal([first.SourcePath, cover.SourcePath], stack.Assets.Select(asset => asset.SourcePath));
        Assert.Equal(1, stack.MaxHashDistance);
        Assert.Equal(TimeSpan.FromSeconds(8), stack.CaptureSpan);
    }

    [Fact]
    public void Build_ExcludesExactCopiesAndAspectMismatches()
    {
        var captured = DateTimeOffset.UtcNow;
        var original = Asset("original.jpg", "same", 0UL, captured, width: 1600, height: 900);
        var exactCopy = Asset("copy.jpg", "same", 0UL, captured.AddSeconds(1), width: 1600, height: 900);
        var portrait = Asset("portrait.jpg", "portrait", 0UL, captured.AddSeconds(2), width: 900, height: 1600);

        Assert.Empty(new NearDuplicateStackService().Build([original, exactCopy, portrait]));
    }

    private static CatalogAssetRecord Asset(
        string path,
        string fingerprint,
        ulong hash,
        DateTimeOffset captured,
        double? latitude = null,
        double? longitude = null,
        int? rating = null,
        int width = 1600,
        int height = 900)
        => new(
            SourcePath: path,
            Fingerprint: fingerprint,
            SizeBytes: 100,
            CreatedUtc: captured,
            ModifiedUtc: captured,
            Width: width,
            Height: height,
            Format: "JPG",
            Codec: "Jpeg",
            Rating: rating,
            Tags: [],
            SidecarPath: null,
            SidecarModifiedUtc: null,
            ScannedUtc: captured,
            Exif: new CatalogExifFacts(latitude, longitude, captured, null, null, null, null, null, null, null),
            PerceptualHash: hash);
}
