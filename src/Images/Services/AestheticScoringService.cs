using System.IO;
using ImageMagick;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Images.Services;

public enum AestheticScoreStatus
{
    Success,
    ModelUnavailable,
    ModelLoadFailed,
    Failed
}

public sealed record AestheticScoreResult(
    AestheticScoreStatus Status,
    string? ErrorMessage,
    string SourcePath,
    int ImageWidth,
    int ImageHeight,
    string? Runtime,
    double MeanScore,
    double StandardDeviation,
    IReadOnlyList<double> Distribution)
{
    public bool Success => Status == AestheticScoreStatus.Success;
}

public static class AestheticScoringService
{
    public const string ModelId = "idealo-nima-mobilenet-aesthetic";
    public const string SourceRevision = "dceaf7c2d218bc6e80b21d6e147e3b56a21b7f31";
    public const string ArtifactSha256 = "35e73929cb5d92602760f4011c71faf355a8c98dfd711025c5a96d8cbbfafeea";
    internal const int InputSize = 224;
    private static readonly ILogger _log = Log.Get(nameof(AestheticScoringService));

    public static AestheticScoreResult Score(string imagePath, ModelManagerService? modelManager = null) =>
        ScoreMany([imagePath], modelManager).Single();

    public static IReadOnlyList<AestheticScoreResult> ScoreMany(
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
                AestheticScoreStatus.ModelUnavailable,
                "The approved NIMA aesthetic model is not imported. Open Model Manager and import the pinned model file first.",
                path)).ToArray();
        }

        InferenceSession session;
        try
        {
            session = OnnxRuntimeService.CreateSession(model.InstalledPath);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "The NIMA aesthetic model could not be loaded.");
            return imagePaths.Select(path => Failure(
                AestheticScoreStatus.ModelLoadFailed,
                "The aesthetic model could not be loaded. Check diagnostics for technical details.",
                path)).ToArray();
        }

        using (session)
        {
            var results = new List<AestheticScoreResult>(imagePaths.Count);
            foreach (var imagePath in imagePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    results.Add(RunInference(Path.GetFullPath(imagePath), session));
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Aesthetic scoring failed for {Path}", imagePath);
                    results.Add(Failure(
                        AestheticScoreStatus.Failed,
                        "This image could not be scored. Check diagnostics for technical details.",
                        imagePath));
                }
            }
            return results;
        }
    }

    internal static AestheticScoreResult RunInference(string imagePath, InferenceSession session)
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
                new DenseTensor<float>(input, [1, InputSize, InputSize, 3]))
        ]);
        return Evaluate(
            imagePath,
            width,
            height,
            OnnxRuntimeService.ProviderDetail,
            results.Single().AsTensor<float>().ToArray());
    }

    internal static AestheticScoreResult Evaluate(
        string sourcePath,
        int imageWidth,
        int imageHeight,
        string? runtime,
        IReadOnlyList<float> rawDistribution)
    {
        if (rawDistribution.Count != 10 ||
            rawDistribution.Any(value => !float.IsFinite(value) || value < 0))
        {
            throw new InvalidDataException("NIMA output does not match the reviewed ten-bin probability contract.");
        }

        var sum = rawDistribution.Sum(value => (double)value);
        if (!double.IsFinite(sum) || sum <= 0)
            throw new InvalidDataException("NIMA output probabilities are empty.");
        var distribution = rawDistribution.Select(value => value / sum).ToArray();
        var mean = distribution.Select((probability, index) => probability * (index + 1)).Sum();
        var variance = distribution.Select((probability, index) =>
            probability * Math.Pow(index + 1 - mean, 2)).Sum();

        return new AestheticScoreResult(
            AestheticScoreStatus.Success,
            null,
            sourcePath,
            imageWidth,
            imageHeight,
            runtime,
            mean,
            Math.Sqrt(variance),
            distribution);
    }

    internal static float[] BuildInputTensor(MagickImage image)
    {
        using var resized = (MagickImage)image.Clone();
        // Match tf.keras.preprocessing.image.load_img(..., target_size=...), whose
        // reviewed NIMA evaluation contract uses nearest-neighbor resizing by default.
        resized.Resize(
            new MagickGeometry(InputSize, InputSize) { IgnoreAspectRatio = true },
            FilterType.Point);
        resized.ColorSpace = ColorSpace.sRGB;
        var tensor = new float[InputSize * InputSize * 3];
        var pixels = resized.GetPixelsUnsafe();
        var quantumToUnit = 1f / (float)Quantum.Max;
        for (var y = 0; y < InputSize; y++)
        for (var x = 0; x < InputSize; x++)
        {
            var channels = pixels.GetPixel(x, y)!.ToArray();
            var offset = (y * InputSize + x) * 3;
            tensor[offset] = channels[0] * quantumToUnit * 2 - 1;
            tensor[offset + 1] = (channels.Length >= 2 ? channels[1] : channels[0]) * quantumToUnit * 2 - 1;
            tensor[offset + 2] = (channels.Length >= 3 ? channels[2] : channels[0]) * quantumToUnit * 2 - 1;
        }
        return tensor;
    }

    private static AestheticScoreResult Failure(
        AestheticScoreStatus status,
        string message,
        string sourcePath) => new(status, message, sourcePath, 0, 0, null, 0, 0, []);
}
