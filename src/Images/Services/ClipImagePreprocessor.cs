using System.IO;
using System.Text.Json;
using ImageMagick;

namespace Images.Services;

public sealed class ClipImagePreprocessor
{
    public const int ImageSize = 224;

    private readonly float[] _mean;
    private readonly float[] _std;

    public ClipImagePreprocessor(float[]? mean = null, float[]? std = null)
    {
        _mean = mean ?? [0.48145466f, 0.4578275f, 0.40821073f];
        _std = std ?? [0.26862954f, 0.26130258f, 0.27577711f];
    }

    public static ClipImagePreprocessor Load(string preprocessorConfigPath)
    {
        var json = File.ReadAllText(preprocessorConfigPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        float[]? mean = null;
        float[]? std = null;

        if (root.TryGetProperty("image_mean", out var meanArr))
            mean = ReadFloatArray(meanArr);
        if (root.TryGetProperty("image_std", out var stdArr))
            std = ReadFloatArray(stdArr);

        return new ClipImagePreprocessor(mean, std);
    }

    public float[] Preprocess(string imagePath)
    {
        CodecRuntime.Configure();
        using var image = new MagickImage();
        var settings = new MagickReadSettings { FrameIndex = 0, FrameCount = 1 };

        using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        image.Read(stream, settings);

        return PreprocessImage(image);
    }

    public float[] Preprocess(MagickImage image)
        => PreprocessImage(image);

    private float[] PreprocessImage(MagickImage image)
    {
        using var resized = (MagickImage)image.Clone();
        resized.Resize(new MagickGeometry(ImageSize, ImageSize)
        {
            IgnoreAspectRatio = true,
        });
        resized.ColorSpace = ColorSpace.sRGB;

        var pixels = resized.GetPixelsUnsafe();
        var tensor = new float[3 * ImageSize * ImageSize];

        for (var y = 0; y < ImageSize; y++)
        {
            for (var x = 0; x < ImageSize; x++)
            {
                var pixel = pixels.GetPixel(x, y)!;
                var channels = pixel.ToArray();

                // HDRI can preserve samples above Quantum.Max; CLIP tensors remain SDR-bounded.
                var scale = (float)Quantum.Max;
                float r, g, b;
                if (channels.Length >= 3)
                {
                    r = Math.Clamp(channels[0] / scale, 0f, 1f);
                    g = Math.Clamp(channels[1] / scale, 0f, 1f);
                    b = Math.Clamp(channels[2] / scale, 0f, 1f);
                }
                else
                {
                    r = g = b = Math.Clamp(channels[0] / scale, 0f, 1f);
                }

                var idx = y * ImageSize + x;
                tensor[0 * ImageSize * ImageSize + idx] = (r - _mean[0]) / _std[0];
                tensor[1 * ImageSize * ImageSize + idx] = (g - _mean[1]) / _std[1];
                tensor[2 * ImageSize * ImageSize + idx] = (b - _mean[2]) / _std[2];
            }
        }

        return tensor;
    }

    private static float[]? ReadFloatArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array) return null;
        var list = new List<float>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.TryGetSingle(out var value))
                list.Add(value);
        }
        return list.Count > 0 ? list.ToArray() : null;
    }
}
