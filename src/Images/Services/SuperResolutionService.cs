using System.IO;
using ImageMagick;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Images.Services;

public sealed record SuperResolutionResult(
    bool Success,
    string? ErrorMessage,
    MagickImage? UpscaledImage,
    int OutputWidth,
    int OutputHeight);

public static class SuperResolutionService
{
    private const int DefaultTileSize = 256;
    private const int DefaultScaleFactor = 4;

    public static bool IsAvailable()
    {
        var manager = new ModelManagerService();
        var snapshot = manager.GetSnapshot();
        return snapshot.Models.Any(m =>
            m.Definition.StorageGroup == "upscale" && m.IsReady);
    }

    public static SuperResolutionResult Upscale(string imagePath, int scaleFactor = DefaultScaleFactor)
    {
        var manager = new ModelManagerService();
        var snapshot = manager.GetSnapshot();

        var model = snapshot.Models.FirstOrDefault(m =>
            m.Definition.StorageGroup == "upscale" && m.IsReady);

        if (model?.InstalledPath is null)
            return new SuperResolutionResult(false,
                "No approved super-resolution model is imported. Open Model Manager to import a verified ONNX model file.",
                null, 0, 0);

        try
        {
            return RunInference(imagePath, model.InstalledPath, scaleFactor);
        }
        catch (Exception ex)
        {
            return new SuperResolutionResult(false,
                $"Super-resolution failed: {ex.Message}", null, 0, 0);
        }
    }

    internal static SuperResolutionResult RunInference(
        string imagePath,
        string modelPath,
        int scaleFactor)
    {
        CodecRuntime.Configure();
        using var sourceImage = new MagickImage();
        using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        sourceImage.Read(stream, new MagickReadSettings { FrameIndex = 0, FrameCount = 1 });

        var origWidth = (int)sourceImage.Width;
        var origHeight = (int)sourceImage.Height;

        using var session = OnnxRuntimeService.CreateSession(modelPath);

        var inputMeta = session.InputMetadata.Values.First();
        var hasDynamicInput = inputMeta.Dimensions.Any(d => d <= 0);

        MagickImage result;
        if (hasDynamicInput && origWidth <= DefaultTileSize && origHeight <= DefaultTileSize)
        {
            result = InferWhole(session, sourceImage, origWidth, origHeight);
        }
        else if (hasDynamicInput)
        {
            result = InferTiled(session, sourceImage, origWidth, origHeight, scaleFactor);
        }
        else
        {
            var fixedSize = inputMeta.Dimensions.Length >= 4 ? inputMeta.Dimensions[2] : DefaultTileSize;
            if (fixedSize <= 0) fixedSize = DefaultTileSize;
            result = InferFixedSize(session, sourceImage, origWidth, origHeight, fixedSize, scaleFactor);
        }

        return new SuperResolutionResult(true, null, result,
            (int)result.Width, (int)result.Height);
    }

    private static MagickImage InferWhole(
        InferenceSession session, MagickImage source, int width, int height)
    {
        var tensor = ImageToTensor(source, width, height);
        var inputName = session.InputNames.First();
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputName,
                new DenseTensor<float>(tensor, [1, 3, height, width]))
        };

        using var results = session.Run(inputs);
        var output = results.First().AsTensor<float>();
        var outH = output.Dimensions[2];
        var outW = output.Dimensions[3];

        return TensorToImage(output, outW, outH);
    }

    private static MagickImage InferTiled(
        InferenceSession session, MagickImage source,
        int origWidth, int origHeight, int scaleFactor)
    {
        // Canvas is sized once we know the model's true scale (from the first
        // tile's output/input ratio). Trusting the caller's scaleFactor when
        // the model upscales by a different factor leaves gaps/overlaps.
        MagickImage? result = null;
        var actualScale = scaleFactor;

        var tileSize = DefaultTileSize;
        var overlap = 16;

        try
        {
            for (var ty = 0; ty < origHeight; ty += tileSize - overlap)
            {
                for (var tx = 0; tx < origWidth; tx += tileSize - overlap)
                {
                    var tw = Math.Min(tileSize, origWidth - tx);
                    var th = Math.Min(tileSize, origHeight - ty);

                    using var tile = (MagickImage)source.Clone();
                    tile.Crop(new MagickGeometry(tx, ty, (uint)tw, (uint)th));
                    tile.ResetPage();

                    var tensor = ImageToTensor(tile, tw, th);
                    var inputName = session.InputNames.First();
                    var inputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor(inputName,
                            new DenseTensor<float>(tensor, [1, 3, th, tw]))
                    };

                    using var results = session.Run(inputs);
                    var output = results.First().AsTensor<float>();
                    var outTileW = output.Dimensions[3];
                    var outTileH = output.Dimensions[2];

                    if (result is null)
                    {
                        // Derive the true integer scale from the model output.
                        actualScale = tw > 0 ? Math.Max(1, outTileW / tw) : scaleFactor;
                        // Widen to long before multiplying so an extreme model scale factor
                        // cannot overflow int and wrap to a bogus (or negative) dimension.
                        result = new MagickImage(
                            MagickColors.Black,
                            (uint)Math.Min((long)origWidth * actualScale, uint.MaxValue),
                            (uint)Math.Min((long)origHeight * actualScale, uint.MaxValue));
                    }

                    using var upscaledTile = TensorToImage(output, outTileW, outTileH);
                    result.Composite(upscaledTile, tx * actualScale, ty * actualScale,
                        CompositeOperator.Over);
                }
            }
        }
        catch
        {
            result?.Dispose();
            throw;
        }

        return result ?? new MagickImage(MagickColors.Black, (uint)origWidth, (uint)origHeight);
    }

    private static MagickImage InferFixedSize(
        InferenceSession session, MagickImage source,
        int origWidth, int origHeight, int fixedSize, int scaleFactor)
    {
        using var resized = (MagickImage)source.Clone();
        resized.Resize(new MagickGeometry((uint)fixedSize, (uint)fixedSize)
        {
            IgnoreAspectRatio = true,
        });

        var tensor = ImageToTensor(resized, fixedSize, fixedSize);
        var inputName = session.InputNames.First();
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputName,
                new DenseTensor<float>(tensor, [1, 3, fixedSize, fixedSize]))
        };

        using var results = session.Run(inputs);
        var output = results.First().AsTensor<float>();
        var outSize = output.Dimensions[2];

        var result = TensorToImage(output, outSize, outSize);
        result.Resize(new MagickGeometry(
            (uint)(origWidth * scaleFactor),
            (uint)(origHeight * scaleFactor))
        {
            IgnoreAspectRatio = true,
        });

        return result;
    }

    private static float[] ImageToTensor(MagickImage image, int width, int height)
    {
        var tensor = new float[3 * height * width];
        var pixels = image.GetPixelsUnsafe();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = pixels.GetPixel(x, y)!;
                var channels = pixel.ToArray();
                // HDRI can preserve samples above Quantum.Max; model tensors remain SDR-bounded.
                var scale = (float)Quantum.Max;
                var idx = y * width + x;

                tensor[0 * height * width + idx] = channels.Length >= 1 ? Math.Clamp(channels[0] / scale, 0f, 1f) : 0;
                tensor[1 * height * width + idx] = channels.Length >= 2 ? Math.Clamp(channels[1] / scale, 0f, 1f) : Math.Clamp(channels[0] / scale, 0f, 1f);
                tensor[2 * height * width + idx] = channels.Length >= 3 ? Math.Clamp(channels[2] / scale, 0f, 1f) : Math.Clamp(channels[0] / scale, 0f, 1f);
            }
        }

        return tensor;
    }

    private static MagickImage TensorToImage(Tensor<float> tensor, int width, int height)
    {
        var image = new MagickImage(MagickColors.Black, (uint)width, (uint)height);
        var pixels = image.GetPixelsUnsafe();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var r = Math.Clamp(tensor[0, 0, y, x], 0, 1);
                var g = Math.Clamp(tensor[0, 1, y, x], 0, 1);
                var b = Math.Clamp(tensor[0, 2, y, x], 0, 1);
                pixels.SetPixel(x, y, [r * Quantum.Max, g * Quantum.Max, b * Quantum.Max]);
            }
        }

        return image;
    }
}
