using System.IO;
using System.Text;
using Images.Services;

namespace Images.Tests;

public sealed class GainMapInspectionServiceTests
{
    [Fact]
    public void Inspect_UltraHdrXmpWithMultiPicture_ReportsUltraHdrAndBoostRange()
    {
        var bytes = Compose(
            MultiPictureMarker(),
            Xmp("""
                hdrgm:Version="1.0"
                hdrgm:GainMapMin="0.0"
                hdrgm:GainMapMax="3.0"
                hdrgm:HDRCapacityMin="0.0"
                hdrgm:HDRCapacityMax="3.0"
                """));

        var result = GainMapInspectionService.Inspect(bytes);

        Assert.True(result.Present);
        Assert.Equal(GainMapFlavor.UltraHdr, result.Flavor);
        Assert.True(result.HasMultiPictureImage);
        Assert.Equal("1.0", result.Version);
        Assert.Equal(0.0, result.MinBoostStops);
        Assert.Equal(3.0, result.MaxBoostStops);
        Assert.Equal(3.0, result.HdrCapacityMaxStops);
    }

    [Fact]
    public void Inspect_HdrgmElementFormWithoutMultiPicture_ReportsGainMapMetadata()
    {
        var bytes = Encoding.Latin1.GetBytes(
            "<x:xmpmeta xmlns:hdrgm=\"http://ns.adobe.com/hdr-gain-map/1.0/\">" +
            "<hdrgm:Version>1.0</hdrgm:Version>" +
            "<hdrgm:GainMapMax>2.5</hdrgm:GainMapMax>" +
            "</x:xmpmeta>");

        var result = GainMapInspectionService.Inspect(bytes);

        Assert.True(result.Present);
        Assert.Equal(GainMapFlavor.GainMapMetadata, result.Flavor);
        Assert.False(result.HasMultiPictureImage);
        Assert.Equal(2.5, result.MaxBoostStops);
    }

    [Fact]
    public void Inspect_PerChannelBoost_TakesMinAndMaxAcrossChannels()
    {
        var bytes = Xmp("""hdrgm:GainMapMin="0.1 0.2 0.0" hdrgm:GainMapMax="2.0,3.5,3.0" """);

        var result = GainMapInspectionService.Inspect(bytes);

        Assert.Equal(0.0, result.MinBoostStops);
        Assert.Equal(3.5, result.MaxBoostStops);
    }

    [Fact]
    public void Inspect_AppleAuxMarker_ReportsAppleFlavor()
    {
        var bytes = Encoding.Latin1.GetBytes("....urn:com:apple:photo:2020:aux:hdrgainmap....");

        var result = GainMapInspectionService.Inspect(bytes);

        Assert.True(result.Present);
        Assert.Equal(GainMapFlavor.AppleGainMap, result.Flavor);
    }

    [Fact]
    public void Inspect_IsoMarker_ReportsIsoFlavor()
    {
        var bytes = Encoding.Latin1.GetBytes("....urn:iso:std:iso:ts:21496:-1....");

        var result = GainMapInspectionService.Inspect(bytes);

        Assert.True(result.Present);
        Assert.Equal(GainMapFlavor.Iso21496, result.Flavor);
    }

    [Fact]
    public void Inspect_PlainImageWithMultiPictureButNoGainMap_ReportsAbsent()
    {
        // A multi-picture JPEG (e.g. a thumbnail directory) with no gain-map metadata is not a gain map.
        var result = GainMapInspectionService.Inspect(MultiPictureMarker());

        Assert.False(result.Present);
        Assert.Equal(GainMapFlavor.None, result.Flavor);
    }

    [Fact]
    public void Inspect_MissingFile_ReportsAbsent()
    {
        var result = GainMapInspectionService.Inspect(@"C:\__nonexistent__\photo.jpg");
        Assert.False(result.Present);
    }

    private static byte[] MultiPictureMarker() => [0x4D, 0x50, 0x46, 0x00, 0x00, 0x00];

    private static byte[] Xmp(string body)
        => Encoding.Latin1.GetBytes(
            "<x:xmpmeta xmlns:hdrgm=\"http://ns.adobe.com/hdr-gain-map/1.0/\" " + body + "></x:xmpmeta>");

    private static byte[] Compose(params byte[][] parts)
    {
        using var stream = new MemoryStream();
        foreach (var part in parts)
            stream.Write(part);
        return stream.ToArray();
    }
}
