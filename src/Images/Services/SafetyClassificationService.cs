using System.IO;
using ImageMagick;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Images.Services;

public enum SafetyClassificationStatus
{
    Success,
    ModelUnavailable,
    ModelLoadFailed,
    Failed
}

public sealed record SafetyPrediction(string Label, double Probability);

public sealed record SafetyClassificationResult(
    SafetyClassificationStatus Status,
    string? ErrorMessage,
    string SourcePath,
    int ImageWidth,
    int ImageHeight,
    string? Runtime,
    string? MostLikelyLabel,
    double Confidence,
    IReadOnlyList<SafetyPrediction> Predictions)
{
    public bool Success => Status == SafetyClassificationStatus.Success;
}

public static class SafetyClassificationService
{
    public const string ModelId = "marqo-nsfw-image-detection-384";
    public const string SourceRevision = "0c26ec22111b83f106d72a55f611ec35962bcb65";
    public const string ArtifactSha256 = "b62f7bebe571a425629a42374df204fc8d345918314f231ce13702446c9b91e3";
    internal const int InputSize = 384;
    private static readonly string[] Labels = ["NSFW", "SFW"];
    private static readonly ILogger _log = Log.Get(nameof(SafetyClassificationService));

    public static SafetyClassificationResult Classify(string imagePath, ModelManagerService? modelManager = null) =>
        ClassifyMany([imagePath], modelManager).Single();

    public static IReadOnlyList<SafetyClassificationResult> ClassifyMany(
        IReadOnlyList<string> imagePaths,
        ModelManagerService? modelManager = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imagePaths);
        if (imagePaths.Count == 0)
            return [];

        var manager = modelManager ?? new ModelManagerService();
        var model = manager.GetSnapshot().Models.FirstOrDefault(item =>
            item.Definition.Id == ModelId && item.IsReady);
        if (model?.InstalledPath is null)
        {
            return imagePaths.Select(path => Failure(
                SafetyClassificationStatus.ModelUnavailable,
                "The approved safety classifier is not imported. Open Model Manager and import the pinned model file first.",
                path)).ToArray();
        }

        InferenceSession session;
        try
        {
            session = OnnxRuntimeService.CreateSession(model.InstalledPath);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "The safety classifier could not be loaded.");
            return imagePaths.Select(path => Failure(
                SafetyClassificationStatus.ModelLoadFailed,
                "The safety classifier could not be loaded. Check diagnostics for technical details.",
                path)).ToArray();
        }

        using (session)
        {
            var results = new List<SafetyClassificationResult>(imagePaths.Count);
            foreach (var imagePath in imagePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    results.Add(RunInference(Path.GetFullPath(imagePath), session));
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Safety classification failed for {Path}", imagePath);
                    results.Add(Failure(
                        SafetyClassificationStatus.Failed,
                        "This image could not be classified. Check diagnostics for technical details.",
                        imagePath));
                }
            }
            return results;
        }
    }

    internal static SafetyClassificationResult RunInference(string imagePath, InferenceSession session)
    {
        using var image = MagickSafeReader.Read(imagePath, new MagickReadSettings { FrameIndex = 0, FrameCount = 1 });
        image.AutoOrient();
        image.ColorSpace = ColorSpace.sRGB;
        var width = checked((int)image.Width);
        var height = checked((int)image.Height);
        var input = BuildInputTensor(image);

        using var results = session.Run(
        [
            NamedOnnxValue.CreateFromTensor(
                session.InputNames.Single(),
                new DenseTensor<float>(input, [1, 3, InputSize, InputSize]))
        ]);
        return Evaluate(
            imagePath,
            width,
            height,
            OnnxRuntimeService.ProviderDetail,
            results.Single().AsTensor<float>().ToArray());
    }

    internal static SafetyClassificationResult Evaluate(
        string sourcePath,
        int imageWidth,
        int imageHeight,
        string? runtime,
        IReadOnlyList<float> logits)
    {
        if (logits.Count != Labels.Length || logits.Any(value => !float.IsFinite(value)))
            throw new InvalidDataException("Safety output does not match the reviewed two-class contract.");

        var maximum = logits.Max();
        var exponentials = logits.Select(value => Math.Exp(value - maximum)).ToArray();
        var sum = exponentials.Sum();
        var predictions = Labels.Select((label, index) =>
                new SafetyPrediction(label, exponentials[index] / sum))
            .OrderByDescending(item => item.Probability)
            .ToArray();

        return new SafetyClassificationResult(
            SafetyClassificationStatus.Success,
            null,
            sourcePath,
            imageWidth,
            imageHeight,
            runtime,
            predictions[0].Label,
            predictions[0].Probability,
            predictions);
    }

    internal static float[] BuildInputTensor(MagickImage image)
    {
        using var resized = (MagickImage)image.Clone();
        var width = checked((int)resized.Width);
        var height = checked((int)resized.Height);
        var scaledWidth = width <= height
            ? InputSize
            : Math.Max(InputSize, checked((int)((long)InputSize * width / height)));
        var scaledHeight = height <= width
            ? InputSize
            : Math.Max(InputSize, checked((int)((long)InputSize * height / width)));
        resized.Resize(
            new MagickGeometry((uint)scaledWidth, (uint)scaledHeight) { IgnoreAspectRatio = true },
            FilterType.Cubic);
        var cropX = (scaledWidth - InputSize) / 2;
        var cropY = (scaledHeight - InputSize) / 2;
        resized.Crop(new MagickGeometry(cropX, cropY, InputSize, InputSize));
        resized.ColorSpace = ColorSpace.sRGB;

        var tensor = new float[3 * InputSize * InputSize];
        var pixels = resized.GetPixelsUnsafe();
        var quantumToUnit = 1f / (float)Quantum.Max;
        var plane = InputSize * InputSize;
        for (var y = 0; y < InputSize; y++)
        for (var x = 0; x < InputSize; x++)
        {
            var channels = pixels.GetPixel(x, y)!.ToArray();
            var index = y * InputSize + x;
            tensor[index] = channels[0] * quantumToUnit * 2 - 1;
            tensor[plane + index] = (channels.Length >= 2 ? channels[1] : channels[0]) * quantumToUnit * 2 - 1;
            tensor[plane * 2 + index] = (channels.Length >= 3 ? channels[2] : channels[0]) * quantumToUnit * 2 - 1;
        }
        return tensor;
    }

    private static SafetyClassificationResult Failure(
        SafetyClassificationStatus status,
        string message,
        string sourcePath) => new(status, message, sourcePath, 0, 0, null, null, 0, []);
}
