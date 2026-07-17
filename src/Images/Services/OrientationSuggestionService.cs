using System.IO;
using ImageMagick;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Images.Services;

public enum OrientationSuggestionStatus
{
    Success,
    ModelUnavailable,
    Failed
}

public sealed record OrientationSuggestionResult(
    OrientationSuggestionStatus Status,
    string? ErrorMessage,
    string SourcePath,
    int ImageWidth,
    int ImageHeight,
    string? Runtime,
    int PredictedClass,
    int CorrectionDegreesClockwise,
    double Confidence,
    double Margin,
    IReadOnlyList<double> Probabilities,
    bool IsConfident)
{
    public bool Success => Status == OrientationSuggestionStatus.Success;
    public int? SuggestedCorrectionDegreesClockwise => IsConfident ? CorrectionDegreesClockwise : null;
    public string Assessment => !IsConfident
        ? "uncertain"
        : CorrectionDegreesClockwise == 0 ? "upright" : "rotate";
}

public static class OrientationSuggestionService
{
    public const string ModelId = "fachuan-orientation-convnextv2-2026jun";
    internal const int InputSize = 384;
    internal const double DefaultConfidenceThreshold = 0.80;
    internal const double DefaultMarginThreshold = 0.20;
    private static readonly float[] Mean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] StandardDeviation = [0.229f, 0.224f, 0.225f];
    private static readonly ILogger _log = Log.Get(nameof(OrientationSuggestionService));

    public static OrientationSuggestionResult Suggest(
        string imagePath,
        ModelManagerService? modelManager = null,
        double confidenceThreshold = DefaultConfidenceThreshold,
        double marginThreshold = DefaultMarginThreshold)
    {
        var manager = modelManager ?? new ModelManagerService();
        var model = manager.GetSnapshot().Models.FirstOrDefault(item =>
            item.Definition.Id == ModelId && item.IsReady);
        if (model?.InstalledPath is null)
        {
            return Failure(
                OrientationSuggestionStatus.ModelUnavailable,
                "The approved orientation model is not imported. Open Model Manager and import the pinned model file first.",
                imagePath);
        }

        try
        {
            return RunInference(
                Path.GetFullPath(imagePath),
                model.InstalledPath,
                confidenceThreshold,
                marginThreshold);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Orientation suggestion failed for {Path}", imagePath);
            return Failure(
                OrientationSuggestionStatus.Failed,
                "Orientation analysis could not inspect this image. Check diagnostics for technical details.",
                imagePath);
        }
    }

    internal static OrientationSuggestionResult RunInference(
        string imagePath,
        string modelPath,
        double confidenceThreshold = DefaultConfidenceThreshold,
        double marginThreshold = DefaultMarginThreshold)
    {
        ValidateThresholds(confidenceThreshold, marginThreshold);
        using var image = MagickSafeReader.Read(imagePath, new MagickReadSettings { FrameIndex = 0, FrameCount = 1 });
        image.AutoOrient();
        image.ColorSpace = ColorSpace.sRGB;
        var width = checked((int)image.Width);
        var height = checked((int)image.Height);
        var input = BuildInputTensor(image);

        using var session = OnnxRuntimeService.CreateSession(modelPath);
        using var results = session.Run(
        [
            NamedOnnxValue.CreateFromTensor(
                session.InputNames.Single(),
                new DenseTensor<float>(input, [1, 3, InputSize, InputSize]))
        ]);
        var logits = results.Single().AsTensor<float>().ToArray();
        return Evaluate(imagePath, width, height, OnnxRuntimeService.ProviderDetail, logits, confidenceThreshold, marginThreshold);
    }

    internal static OrientationSuggestionResult Evaluate(
        string sourcePath,
        int imageWidth,
        int imageHeight,
        string? runtime,
        IReadOnlyList<float> logits,
        double confidenceThreshold = DefaultConfidenceThreshold,
        double marginThreshold = DefaultMarginThreshold)
    {
        ValidateThresholds(confidenceThreshold, marginThreshold);
        if (logits.Count != 4 || logits.Any(value => !float.IsFinite(value)))
            throw new InvalidDataException("Orientation output does not match the reviewed four-class contract.");

        var maximum = logits.Max();
        var exponentials = logits.Select(value => Math.Exp(value - maximum)).ToArray();
        var sum = exponentials.Sum();
        var probabilities = exponentials.Select(value => value / sum).ToArray();
        var ranked = probabilities
            .Select((probability, index) => (probability, index))
            .OrderByDescending(item => item.probability)
            .ToArray();
        var predictedClass = ranked[0].index;
        var confidence = ranked[0].probability;
        var margin = confidence - ranked[1].probability;
        var correction = predictedClass switch
        {
            0 => 0,
            1 => 180,
            2 => 270,
            3 => 90,
            _ => throw new InvalidDataException("Unexpected orientation class.")
        };

        return new OrientationSuggestionResult(
            OrientationSuggestionStatus.Success,
            null,
            sourcePath,
            imageWidth,
            imageHeight,
            runtime,
            predictedClass,
            correction,
            confidence,
            margin,
            probabilities,
            confidence >= confidenceThreshold && margin >= marginThreshold);
    }

    internal static float[] BuildInputTensor(MagickImage image)
    {
        using var resized = (MagickImage)image.Clone();
        resized.Resize(new MagickGeometry(InputSize, InputSize) { IgnoreAspectRatio = true });
        resized.ColorSpace = ColorSpace.sRGB;
        var tensor = new float[3 * InputSize * InputSize];
        var pixels = resized.GetPixelsUnsafe();
        var quantumToUnit = 1f / (float)Quantum.Max;
        var plane = InputSize * InputSize;
        for (var y = 0; y < InputSize; y++)
        for (var x = 0; x < InputSize; x++)
        {
            var channels = pixels.GetPixel(x, y)!.ToArray();
            var values = new[]
            {
                channels[0] * quantumToUnit,
                (channels.Length >= 2 ? channels[1] : channels[0]) * quantumToUnit,
                (channels.Length >= 3 ? channels[2] : channels[0]) * quantumToUnit
            };
            var index = y * InputSize + x;
            for (var channel = 0; channel < 3; channel++)
                tensor[channel * plane + index] = (values[channel] - Mean[channel]) / StandardDeviation[channel];
        }
        return tensor;
    }

    private static void ValidateThresholds(double confidenceThreshold, double marginThreshold)
    {
        if (confidenceThreshold is < 0 or > 1 || marginThreshold is < 0 or > 1)
            throw new ArgumentOutOfRangeException();
    }

    private static OrientationSuggestionResult Failure(
        OrientationSuggestionStatus status,
        string message,
        string sourcePath) => new(
            status, message, sourcePath, 0, 0, null, -1, 0, 0, 0, [], false);
}
