using System.Buffers.Binary;
using System.IO;
using ImageMagick;

namespace Images.Services;

public static class PerceptualHashService
{
    public const int HashSize = 8;
    public const int SampleCount = HashSize * HashSize;

    public static ulong? TryComputeAverageHash(
        string path,
        Span<double> luminance,
        CancellationToken cancellationToken = default)
    {
        if (luminance.Length < SampleCount)
            throw new ArgumentException($"A {SampleCount}-value luminance buffer is required.", nameof(luminance));

        try
        {
            CodecRuntime.Configure();
            cancellationToken.ThrowIfCancellationRequested();

            using var image = MagickSafeReader.Read(
                path,
                new MagickReadSettings { Width = 64, Height = 64 });
            image.AutoOrient();
            image.BackgroundColor = MagickColors.White;
            image.Alpha(AlphaOption.Remove);
            image.Resize(new MagickGeometry(HashSize, HashSize) { IgnoreAspectRatio = true });
            image.Format = MagickFormat.Bgra;

            cancellationToken.ThrowIfCancellationRequested();
            var pixels = image.GetPixelsUnsafe().ToByteArray(PixelMapping.BGRA);
            if (pixels is null || pixels.Length < SampleCount * 4)
                return null;

            luminance = luminance[..SampleCount];
            luminance.Clear();
            var sum = 0d;
            for (var i = 0; i < luminance.Length; i++)
            {
                var offset = i * 4;
                var value = (0.299d * pixels[offset + 2]) +
                            (0.587d * pixels[offset + 1]) +
                            (0.114d * pixels[offset]);
                luminance[i] = value;
                sum += value;
            }

            var average = sum / luminance.Length;
            var hash = 0UL;
            for (var i = 0; i < luminance.Length; i++)
            {
                if (luminance[i] >= average)
                    hash |= 1UL << i;
            }

            return hash;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    public static byte[] ToBytes(ulong hash)
    {
        var bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, hash);
        return bytes;
    }

    public static ulong FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != sizeof(ulong))
            throw new ArgumentException("A perceptual hash must contain exactly 8 bytes.", nameof(bytes));
        return BinaryPrimitives.ReadUInt64BigEndian(bytes);
    }
}
