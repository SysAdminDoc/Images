using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class JxlInspectionServiceTests
{
    private static readonly byte[] ContainerSignature =
        [0x00, 0x00, 0x00, 0x0C, 0x4A, 0x58, 0x4C, 0x20, 0x0D, 0x0A, 0x87, 0x0A];

    [Fact]
    public void Inspect_BareCodestream_ReportsCodestream()
    {
        var result = JxlInspectionService.Inspect(new byte[] { 0xFF, 0x0A, 0x00, 0x01 });

        Assert.True(result.IsJxl);
        Assert.Equal(JxlContainerKind.Codestream, result.Kind);
    }

    [Fact]
    public void Inspect_ContainerWithoutReconstruction_ReportsContainer()
    {
        var bytes = Compose(ContainerSignature, Box("jxlc", [1, 2, 3, 4]));

        var result = JxlInspectionService.Inspect(bytes);

        Assert.True(result.IsJxl);
        Assert.Equal(JxlContainerKind.Container, result.Kind);
    }

    [Fact]
    public void Inspect_ContainerWithReconstructionBox_ReportsJpegTranscode()
    {
        var bytes = Compose(ContainerSignature, Box("jbrd", [0, 0, 0, 0]), Box("jxlc", [9, 9]));

        var result = JxlInspectionService.Inspect(bytes);

        Assert.True(result.IsJxl);
        Assert.Equal(JxlContainerKind.JpegTranscode, result.Kind);
    }

    [Fact]
    public void Inspect_NonJxlBytes_ReportsNotJxl()
    {
        var result = JxlInspectionService.Inspect(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // JPEG SOI

        Assert.False(result.IsJxl);
        Assert.Equal(JxlContainerKind.None, result.Kind);
    }

    [Fact]
    public void Inspect_MissingFile_ReportsNotJxl()
        => Assert.False(JxlInspectionService.Inspect(@"C:\__nonexistent__\image.jxl").IsJxl);

    private static byte[] Box(string type, byte[] payload)
    {
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        return Compose(typeBytes, payload);
    }

    private static byte[] Compose(params byte[][] parts)
    {
        using var stream = new MemoryStream();
        foreach (var part in parts)
            stream.Write(part);
        return stream.ToArray();
    }
}
