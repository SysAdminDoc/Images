using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class MagickSafeReaderTests
{
    [Fact]
    public void ReadCollection_Bytes_DecodesEveryFrameThroughTheConfiguredRuntime()
    {
        var bytes = BuildAnimatedGif();

        using var collection = MagickSafeReader.ReadCollection(bytes);

        Assert.Equal(2, collection.Count);
        // Reaching a successful multi-frame decode means the native coder allowlist and resource
        // limits were installed first: CodecRuntime.Configure() ran on this path.
        Assert.NotSame(CodecRuntimeStatus.Unconfigured, CodecRuntime.Status);
    }

    [Fact]
    public void ReadCollection_Bytes_RejectsNull()
        => Assert.Throws<ArgumentNullException>(() => MagickSafeReader.ReadCollection((byte[])null!));

    private static byte[] BuildAnimatedGif()
    {
        using var collection = new MagickImageCollection
        {
            new MagickImage(MagickColors.Red, 6, 4) { Format = MagickFormat.Gif, AnimationDelay = 10 },
            new MagickImage(MagickColors.Blue, 6, 4) { Format = MagickFormat.Gif, AnimationDelay = 10 },
        };
        using var stream = new MemoryStream();
        collection.Write(stream);
        return stream.ToArray();
    }
}
