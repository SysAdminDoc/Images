using System.IO;
using ImageMagick;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Images.Services;

public sealed record InpaintMaskRegion(
    double X,
    double Y,
    double Radius)
{
    public double Left => X - Radius;
    public double Top => Y - Radius;
    public double Diameter => Radius * 2;
}

public sealed record InpaintResult(
    bool Success,
    string? ErrorMessage,
    MagickImage? RepairedImage);

public static class LaMaInpaintService
{
    public const double MinBrushRadius = 5;
    public const double MaxBrushRadius = 200;

    private const string OpenCvLaMaId = "opencv-inpainting-lama-2025jan";
    private const string CarveLaMaId = "carve-lama-fp32";
    private const int CarveLaMaInputSize = 512;

    public static bool IsAvailable()
    {
        var manager = new ModelManagerService();
        var snapshot = manager.GetSnapshot();
        return snapshot.Models.Any(m =>
            (m.Definition.Id == OpenCvLaMaId || m.Definition.Id == CarveLaMaId) &&
            m.IsReady);
    }

    public static InpaintResult Inpaint(
        string imagePath,
        IReadOnlyList<InpaintMaskRegion> maskRegions,
        int imageWidth,
        int imageHeight)
    {
        if (maskRegions.Count == 0)
            return new InpaintResult(false, "No mask regions provided.", null);

        var manager = new ModelManagerService();
        var snapshot = manager.GetSnapshot();

        var model = snapshot.Models.FirstOrDefault(m => m.Definition.Id == CarveLaMaId && m.IsReady)
                    ?? snapshot.Models.FirstOrDefault(m => m.Definition.Id == OpenCvLaMaId && m.IsReady);

        if (model is null || model.InstalledPath is null)
            return new InpaintResult(false,
                "No approved LaMa model is imported. Open Model Manager to import a verified ONNX model file.", null);

        try
        {
            return RunInference(imagePath, maskRegions, imageWidth, imageHeight,
                model.InstalledPath, model.Definition.Id);
        }
        catch (Exception ex)
        {
            return new InpaintResult(false, $"Inpainting failed: {ex.Message}", null);
        }
    }

    internal static InpaintResult RunInference(
        string imagePath,
        IReadOnlyList<InpaintMaskRegion> maskRegions,
        int imageWidth,
        int imageHeight,
        string modelPath,
        string modelId)
    {
        var inputSize = modelId == CarveLaMaId ? CarveLaMaInputSize : 512;

        CodecRuntime.Configure();
        using var sourceImage = new MagickImage();
        using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        sourceImage.Read(stream, new MagickReadSettings { FrameIndex = 0, FrameCount = 1 });

        var origWidth = (int)sourceImage.Width;
        var origHeight = (int)sourceImage.Height;

        using var resizedImage = (MagickImage)sourceImage.Clone();
        resizedImage.Resize(new MagickGeometry((uint)inputSize, (uint)inputSize)
        {
            IgnoreAspectRatio = true,
        });

        var scaleX = (double)inputSize / imageWidth;
        var scaleY = (double)inputSize / imageHeight;

        var imageTensor = ImageToTensor(resizedImage, inputSize);
        var maskTensor = MaskToTensor(maskRegions, inputSize, scaleX, scaleY);

        using var session = OnnxRuntimeService.CreateSession(modelPath);

        var inputs = new List<NamedOnnxValue>();
        var inputNames = session.InputNames;

        if (inputNames.Count >= 2)
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor(inputNames[0],
                new DenseTensor<float>(imageTensor, [1, 3, inputSize, inputSize])));
            inputs.Add(NamedOnnxValue.CreateFromTensor(inputNames[1],
                new DenseTensor<float>(maskTensor, [1, 1, inputSize, inputSize])));
        }
        else
        {
            var combined = new float[4 * inputSize * inputSize];
            Array.Copy(imageTensor, 0, combined, 0, 3 * inputSize * inputSize);
            Array.Copy(maskTensor, 0, combined, 3 * inputSize * inputSize, inputSize * inputSize);
            inputs.Add(NamedOnnxValue.CreateFromTensor(inputNames[0],
                new DenseTensor<float>(combined, [1, 4, inputSize, inputSize])));
        }

        using var results = session.Run(inputs);
        var outputTensor = results.First().AsTensor<float>();

        var repaired = TensorToImage(outputTensor, inputSize);
        repaired.Resize(new MagickGeometry((uint)origWidth, (uint)origHeight)
        {
            IgnoreAspectRatio = true,
        });

        return new InpaintResult(true, null, repaired);
    }

    private static float[] ImageToTensor(MagickImage image, int size)
    {
        var tensor = new float[3 * size * size];
        var pixels = image.GetPixelsUnsafe();

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var pixel = pixels.GetPixel(x, y)!;
                var channels = pixel.ToArray();
                // HDRI can preserve samples above Quantum.Max; model tensors remain SDR-bounded.
                var scale = (float)Quantum.Max;
                var idx = y * size + x;

                tensor[0 * size * size + idx] = channels.Length >= 1 ? Math.Clamp(channels[0] / scale, 0f, 1f) : 0;
                tensor[1 * size * size + idx] = channels.Length >= 2 ? Math.Clamp(channels[1] / scale, 0f, 1f) : Math.Clamp(channels[0] / scale, 0f, 1f);
                tensor[2 * size * size + idx] = channels.Length >= 3 ? Math.Clamp(channels[2] / scale, 0f, 1f) : Math.Clamp(channels[0] / scale, 0f, 1f);
            }
        }

        return tensor;
    }

    private static float[] MaskToTensor(
        IReadOnlyList<InpaintMaskRegion> regions,
        int size,
        double scaleX,
        double scaleY)
    {
        var mask = new float[size * size];

        foreach (var region in regions)
        {
            var cx = region.X * scaleX;
            var cy = region.Y * scaleY;
            var r = region.Radius * Math.Max(scaleX, scaleY);

            var minX = Math.Max(0, (int)(cx - r));
            var maxX = Math.Min(size - 1, (int)(cx + r));
            var minY = Math.Max(0, (int)(cy - r));
            var maxY = Math.Min(size - 1, (int)(cy + r));

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var dx = x - cx;
                    var dy = y - cy;
                    if (dx * dx + dy * dy <= r * r)
                        mask[y * size + x] = 1.0f;
                }
            }
        }

        return mask;
    }

    private static MagickImage TensorToImage(Tensor<float> tensor, int size)
    {
        var image = new MagickImage(MagickColors.Black, (uint)size, (uint)size);
        var pixelCollection = image.GetPixelsUnsafe();

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var r = Math.Clamp(tensor[0, 0, y, x], 0, 1);
                var g = Math.Clamp(tensor[0, 1, y, x], 0, 1);
                var b = Math.Clamp(tensor[0, 2, y, x], 0, 1);

                var pixel = new[] { r * Quantum.Max, g * Quantum.Max, b * Quantum.Max };
                pixelCollection.SetPixel(x, y, pixel);
            }
        }

        return image;
    }
}
