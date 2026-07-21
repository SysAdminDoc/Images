using System.IO;
using System.Reflection;
using ImageMagick;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Images.Services;

public enum SceneClassificationStatus
{
    Success,
    ModelUnavailable,
    ModelLoadFailed,
    Failed
}

public sealed record SceneLabel(int ClassId, string Label, string DisplayLabel, bool IsIndoor);

public sealed record ScenePrediction(
    int ClassId,
    string Label,
    string DisplayLabel,
    double Probability,
    string Environment);

public sealed record SceneClassificationResult(
    SceneClassificationStatus Status,
    string? ErrorMessage,
    string SourcePath,
    int ImageWidth,
    int ImageHeight,
    string? Runtime,
    string? Environment,
    double EnvironmentConfidence,
    IReadOnlyList<ScenePrediction> Predictions,
    IReadOnlyList<string> SuggestedKeywords)
{
    public bool Success => Status == SceneClassificationStatus.Success;
}

public static class SceneClassificationService
{
    public const string ModelId = "csail-places365-resnet18";
    public const string SourceRevision = "8a953ed56438726dc98bdef3796d042e7f1f171e";
    public const string ArtifactSha256 = "449af931452719ff8ae03c5b9afb096d04120d52f3e5cf54e5ddd1c082a3d2c5";
    internal const int ResizeSize = 256;
    internal const int InputSize = 224;
    private static readonly float[] Mean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] StandardDeviation = [0.229f, 0.224f, 0.225f];
    private static readonly Lazy<IReadOnlyList<SceneLabel>> _labels = new(LoadEmbeddedLabels);
    private static readonly ILogger _log = Log.Get(nameof(SceneClassificationService));

    public static IReadOnlyList<SceneLabel> Labels => _labels.Value;

    public static SceneClassificationResult Classify(string imagePath, ModelManagerService? modelManager = null) =>
        ClassifyMany([imagePath], modelManager).Single();

    public static IReadOnlyList<SceneClassificationResult> ClassifyMany(
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
                SceneClassificationStatus.ModelUnavailable,
                "The approved Places365 scene model is not imported. Open Model Manager and import the pinned model file first.",
                path)).ToArray();
        }

        InferenceSession session;
        try
        {
            session = OnnxRuntimeService.CreateSession(model.InstalledPath);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "The Places365 scene model could not be loaded.");
            return imagePaths.Select(path => Failure(
                SceneClassificationStatus.ModelLoadFailed,
                "The scene model could not be loaded. Check diagnostics for technical details.",
                path)).ToArray();
        }

        using (session)
        {
            var results = new List<SceneClassificationResult>(imagePaths.Count);
            foreach (var imagePath in imagePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    results.Add(RunInference(Path.GetFullPath(imagePath), session));
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Scene classification failed for {Path}", imagePath);
                    results.Add(Failure(
                        SceneClassificationStatus.Failed,
                        "This image could not be classified. Check diagnostics for technical details.",
                        imagePath));
                }
            }
            return results;
        }
    }

    internal static SceneClassificationResult RunInference(string imagePath, InferenceSession session)
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
            results.Single().AsTensor<float>().ToArray(),
            Labels);
    }

    internal static SceneClassificationResult Evaluate(
        string sourcePath,
        int imageWidth,
        int imageHeight,
        string? runtime,
        IReadOnlyList<float> logits,
        IReadOnlyList<SceneLabel> labels)
    {
        if (logits.Count != labels.Count || labels.Count != 365 || logits.Any(value => !float.IsFinite(value)))
            throw new InvalidDataException("Places365 output does not match the reviewed 365-class contract.");

        var maximum = logits.Max();
        var exponentials = logits.Select(value => Math.Exp(value - maximum)).ToArray();
        var sum = exponentials.Sum();
        var probabilities = exponentials.Select(value => value / sum).ToArray();
        var top = probabilities.Select((probability, index) => (probability, index))
            .OrderByDescending(item => item.probability)
            .Take(5)
            .Select(item => new ScenePrediction(
                labels[item.index].ClassId,
                labels[item.index].Label,
                labels[item.index].DisplayLabel,
                item.probability,
                labels[item.index].IsIndoor ? "indoor" : "outdoor"))
            .ToArray();
        var indoorProbability = labels.Select((label, index) => label.IsIndoor ? probabilities[index] : 0).Sum();
        var environment = indoorProbability >= 0.5 ? "indoor" : "outdoor";
        var environmentConfidence = Math.Max(indoorProbability, 1 - indoorProbability);
        var sceneThreshold = Math.Max(0.08, top[0].Probability * 0.35);
        var keywords = top.Where(item => item.Probability >= sceneThreshold)
            .Take(3)
            .Select(item => "scene:" + item.DisplayLabel)
            .ToList();
        if (environmentConfidence >= 0.65)
            keywords.Add("environment:" + environment);

        return new SceneClassificationResult(
            SceneClassificationStatus.Success,
            null,
            sourcePath,
            imageWidth,
            imageHeight,
            runtime,
            environment,
            environmentConfidence,
            top,
            keywords);
    }

    internal static float[] BuildInputTensor(MagickImage image)
    {
        using var resized = (MagickImage)image.Clone();
        resized.Resize(
            new MagickGeometry(ResizeSize, ResizeSize) { IgnoreAspectRatio = true },
            FilterType.Triangle);
        resized.Crop(new MagickGeometry(16, 16, InputSize, InputSize));
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

    internal static IReadOnlyList<SceneLabel> ParseLabels(
        IEnumerable<string> categoryLines,
        IEnumerable<string> indoorOutdoorLines)
    {
        var categories = categoryLines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        var environments = indoorOutdoorLines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        if (categories.Length != 365 || environments.Length != 365)
            throw new InvalidDataException("Places365 labels must contain exactly 365 rows.");

        var labels = new SceneLabel[365];
        for (var index = 0; index < labels.Length; index++)
        {
            var categoryParts = categories[index].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var environmentParts = environments[index].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (categoryParts.Length != 2 || environmentParts.Length != 2 ||
                !int.TryParse(categoryParts[1], out var categoryIndex) || categoryIndex != index ||
                environmentParts[0] != categoryParts[0] ||
                !int.TryParse(environmentParts[1], out var environmentValue) || environmentValue is < 1 or > 2)
            {
                throw new InvalidDataException($"Places365 label row {index} is malformed or misaligned.");
            }
            var label = categoryParts[0].Length > 3 &&
                        categoryParts[0][0] == '/' && categoryParts[0][2] == '/'
                ? categoryParts[0][3..]
                : categoryParts[0];
            var display = label.Replace('_', ' ').Replace('/', ' ');
            labels[index] = new SceneLabel(index, label, display, environmentValue == 1);
        }
        return labels;
    }

    private static IReadOnlyList<SceneLabel> LoadEmbeddedLabels()
    {
        var assembly = typeof(SceneClassificationService).Assembly;
        using var categories = OpenResource(assembly, "Images.Models.categories_places365.txt");
        using var environments = OpenResource(assembly, "Images.Models.IO_places365.txt");
        return ParseLabels(ReadLines(categories), ReadLines(environments));
    }

    private static Stream OpenResource(Assembly assembly, string name) =>
        assembly.GetManifestResourceStream(name) ??
        throw new InvalidDataException($"Embedded Places365 resource is missing: {name}");

    private static IEnumerable<string> ReadLines(Stream stream)
    {
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
            yield return line;
    }

    private static SceneClassificationResult Failure(
        SceneClassificationStatus status,
        string message,
        string sourcePath) => new(status, message, sourcePath, 0, 0, null, null, 0, [], []);
}
