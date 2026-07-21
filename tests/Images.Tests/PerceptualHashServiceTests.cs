using System.IO;
using System.Numerics;
using ImageMagick;
using ImageMagick.Drawing;
using Images.Services;

namespace Images.Tests;

public sealed class PerceptualHashServiceTests
{
    [Fact]
    public void ToBytes_FromBytes_RoundTripsBigEndian()
    {
        const ulong hash = 0x0123_4567_89AB_CDEFUL;

        var bytes = PerceptualHashService.ToBytes(hash);

        Assert.Equal(8, bytes.Length);
        Assert.Equal(0x01, bytes[0]);
        Assert.Equal(0xEF, bytes[7]);
        Assert.Equal(hash, PerceptualHashService.FromBytes(bytes));
    }

    [Fact]
    public void FromBytes_RejectsWrongLength()
    {
        Assert.Throws<ArgumentException>(() => PerceptualHashService.FromBytes(new byte[7]));
    }

    [Fact]
    public void TryComputeAverageHash_UniformImageSetsEveryBit()
    {
        // Every sampled pixel equals the average, so `>= average` holds for all 64 bits.
        var path = WriteImage(new MagickImage(MagickColors.Gray, 64, 64));
        try
        {
            Span<double> buffer = stackalloc double[PerceptualHashService.SampleCount];

            var hash = PerceptualHashService.TryComputeAverageHash(path, buffer);

            Assert.NotNull(hash);
            Assert.Equal(ulong.MaxValue, hash!.Value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryComputeAverageHash_IsStableAndSeparatesTopBottomSplit()
    {
        using var split = new MagickImage(MagickColors.Black, 64, 64);
        split.Draw(new Drawables().FillColor(MagickColors.White).Rectangle(0, 32, 63, 63));
        var path = WriteImage(split);
        try
        {
            Span<double> first = stackalloc double[PerceptualHashService.SampleCount];
            Span<double> second = stackalloc double[PerceptualHashService.SampleCount];

            var a = PerceptualHashService.TryComputeAverageHash(path, first);
            var b = PerceptualHashService.TryComputeAverageHash(path, second);

            Assert.NotNull(a);
            // Deterministic: identical input yields an identical hash.
            Assert.Equal(a, b);
            // The bottom 32 samples are bright (bits set); the top 32 are dark.
            Assert.Equal(32, BitOperations.PopCount(a!.Value));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryComputeAverageHash_ReturnsNullForMissingFile()
    {
        Span<double> buffer = stackalloc double[PerceptualHashService.SampleCount];

        var hash = PerceptualHashService.TryComputeAverageHash(
            Path.Combine(Path.GetTempPath(), "images-phash-does-not-exist.png"),
            buffer);

        Assert.Null(hash);
    }

    private static string WriteImage(MagickImage image)
    {
        using (image)
        {
            var path = Path.Combine(Path.GetTempPath(), $"images-phash-{Guid.NewGuid():N}.png");
            image.Write(path, MagickFormat.Png);
            return path;
        }
    }
}
