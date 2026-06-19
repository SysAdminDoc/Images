using System.IO;
using ImageMagick;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Images.Services;

public sealed record BackgroundRemovalResult(
    bool Success,
    string? ErrorMessage,
    MagickImage? ResultImage,
    MagickImage? MaskImage);

public static class BackgroundRemovalService
{
    private const int DefaultInputSize = 1024;

    public static bool IsAvailable()
    {
        var manager = new ModelManagerService();
        var snapshot = manager.GetSnapshot();
        return snapshot.Models.Any(m =>
            m.Definition.StorageGroup == "segmentation" && m.IsReady);
    }

    public static BackgroundRemovalResult RemoveBackground(
        string imagePath,
        bool returnMaskOnly = false)
    {
        var manager = new ModelManagerService();
        var snapshot = manager.GetSnapshot();

        var model = snapshot.Models.FirstOrDefault(m =>
            m.Definition.StorageGroup == "segmentation" && m.IsReady);

        if (model?.InstalledPath is null)
            return new BackgroundRemovalResult(false,
                "No approved segmentation model is imported. Open Model Manager to import a verified ONNX model file.",
                null, null);

        try
        {
            return RunInference(imagePath, model.InstalledPath, returnMaskOnly);
        }
        catch (Exception ex)
        {
            return new BackgroundRemovalResult(false,
                $"Background removal failed: {ex.Message}", null, null);
        }
    }

    internal static BackgroundRemovalResult RunInference(
        string imagePath,
        string modelPath,
        bool returnMaskOnly)
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
        var inputSize = inputMeta.Dimensions.Length >= 4
            ? Math.Max(inputMeta.Dimensions[2], inputMeta.Dimensions[3])
            : DefaultInputSize;
        if (inputSize <= 0) inputSize = DefaultInputSize;

        using var resized = (MagickImage)sourceImage.Clone();
        resized.Resize(new MagickGeometry((uint)inputSize, (uint)inputSize)
        {
            IgnoreAspectRatio = true,
        });

        var imageTensor = PreprocessImage(resized, inputSize);
        var inputName = session.InputNames.First();
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputName,
                new DenseTensor<float>(imageTensor, [1, 3, inputSize, inputSize]))
        };

        using var results = session.Run(inputs);
        var outputTensor = results.First().AsTensor<float>();

        var maskImage = MaskFromTensor(outputTensor, inputSize, origWidth, origHeight);

        if (returnMaskOnly)
            return new BackgroundRemovalResult(true, null, null, maskImage);

        var result = (MagickImage)sourceImage.Clone();
        result.HasAlpha = true;
        result.Composite(maskImage, CompositeOperator.CopyAlpha);

        return new BackgroundRemovalResult(true, null, result, maskImage);
    }

    private static float[] PreprocessImage(MagickImage image, int size)
    {
        float[] mean = [0.485f, 0.456f, 0.406f];
        float[] std = [0.229f, 0.224f, 0.225f];
        var tensor = new float[3 * size * size];
        var pixels = image.GetPixelsUnsafe();

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var pixel = pixels.GetPixel(x, y)!;
                var channels = pixel.ToArray();
                var scale = channels.Length >= 1 && channels[0] > 255 ? 65535f : 255f;
                var idx = y * size + x;

                var r = channels.Length >= 1 ? channels[0] / scale : 0f;
                var g = channels.Length >= 2 ? channels[1] / scale : r;
                var b = channels.Length >= 3 ? channels[2] / scale : r;

                tensor[0 * size * size + idx] = (r - mean[0]) / std[0];
                tensor[1 * size * size + idx] = (g - mean[1]) / std[1];
                tensor[2 * size * size + idx] = (b - mean[2]) / std[2];
            }
        }

        return tensor;
    }

    private static MagickImage MaskFromTensor(
        Tensor<float> tensor, int modelSize, int targetWidth, int targetHeight)
    {
        var maskWidth = tensor.Dimensions.Length >= 4 ? tensor.Dimensions[3] : modelSize;
        var maskHeight = tensor.Dimensions.Length >= 4 ? tensor.Dimensions[2] : modelSize;

        var mask = new MagickImage(MagickColors.Black, (uint)maskWidth, (uint)maskHeight);
        var pixels = mask.GetPixelsUnsafe();

        for (var y = 0; y < maskHeight; y++)
        {
            for (var x = 0; x < maskWidth; x++)
            {
                var value = Math.Clamp(tensor[0, 0, y, x], 0, 1);
                var gray = (ushort)(value * 65535);
                pixels.SetPixel(x, y, [gray]);
            }
        }

        mask.Resize(new MagickGeometry((uint)targetWidth, (uint)targetHeight)
        {
            IgnoreAspectRatio = true,
        });

        return mask;
    }
}
