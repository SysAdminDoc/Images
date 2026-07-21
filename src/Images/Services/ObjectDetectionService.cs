using System.IO;
using ImageMagick;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Images.Services;

public sealed record ObjectDetection(
    string Label,
    int ClassId,
    double Confidence,
    double X,
    double Y,
    double Width,
    double Height);

public sealed record ObjectDetectionResult(
    bool Success,
    string? ErrorMessage,
    string SourcePath,
    int ImageWidth,
    int ImageHeight,
    string? Runtime,
    IReadOnlyList<ObjectDetection> Detections)
{
    public IReadOnlyList<string> SuggestedKeywords => Detections
        .OrderByDescending(item => item.Confidence)
        .Select(item => "object:" + item.Label)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

internal sealed record YoloXCandidate(
    int ClassId,
    float Confidence,
    float Left,
    float Top,
    float Right,
    float Bottom);

public static class ObjectDetectionService
{
    public const string ModelId = "opencv-object-detection-yolox-2022nov";
    internal const int InputSize = 640;
    private static readonly int[] Strides = [8, 16, 32];
    private static readonly ILogger _log = Log.Get(nameof(ObjectDetectionService));

    public static IReadOnlyList<string> CocoLabels { get; } =
    [
        "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat", "traffic light",
        "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow",
        "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee",
        "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle",
        "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange",
        "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch", "potted plant", "bed",
        "dining table", "toilet", "tv", "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", "oven",
        "toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush",
    ];

    public static ObjectDetectionResult Detect(
        string imagePath,
        ModelManagerService? modelManager = null,
        float confidenceThreshold = 0.35f,
        float nmsThreshold = 0.5f) =>
        DetectMany([imagePath], modelManager, confidenceThreshold, nmsThreshold).Single();

    // Reuses one InferenceSession across the whole batch; a single --object-detect over N images
    // no longer reloads the YOLOX model per image. Honors cancellation between images.
    public static IReadOnlyList<ObjectDetectionResult> DetectMany(
        IReadOnlyList<string> imagePaths,
        ModelManagerService? modelManager = null,
        float confidenceThreshold = 0.35f,
        float nmsThreshold = 0.5f,
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
            return imagePaths.Select(path => new ObjectDetectionResult(
                false,
                "The approved OpenCV YOLOX-S model is not imported. Open Model Manager and import the pinned model file first.",
                path, 0, 0, null, [])).ToArray();
        }

        InferenceSession session;
        try
        {
            session = OnnxRuntimeService.CreateSession(model.InstalledPath);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "The YOLOX object-detection model could not be loaded.");
            return imagePaths.Select(path => new ObjectDetectionResult(
                false,
                "The object-detection model could not be loaded. Check diagnostics for technical details.",
                path, 0, 0, null, [])).ToArray();
        }

        using (session)
        {
            var results = new List<ObjectDetectionResult>(imagePaths.Count);
            foreach (var imagePath in imagePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    results.Add(RunInference(Path.GetFullPath(imagePath), session, confidenceThreshold, nmsThreshold));
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Object detection failed for {Path}", imagePath);
                    results.Add(new ObjectDetectionResult(
                        false,
                        "Object detection could not analyze this image. Check diagnostics for technical details.",
                        imagePath, 0, 0, null, []));
                }
            }
            return results;
        }
    }

    internal static ObjectDetectionResult RunInference(
        string imagePath,
        string modelPath,
        float confidenceThreshold = 0.35f,
        float nmsThreshold = 0.5f)
    {
        using var session = OnnxRuntimeService.CreateSession(modelPath);
        return RunInference(imagePath, session, confidenceThreshold, nmsThreshold);
    }

    internal static ObjectDetectionResult RunInference(
        string imagePath,
        InferenceSession session,
        float confidenceThreshold = 0.35f,
        float nmsThreshold = 0.5f)
    {
        if (confidenceThreshold is < 0 or > 1 || nmsThreshold is < 0 or > 1)
            throw new ArgumentOutOfRangeException();

        using var image = MagickSafeReader.Read(imagePath, new MagickReadSettings { FrameIndex = 0, FrameCount = 1 });
        image.AutoOrient();
        image.ColorSpace = ColorSpace.sRGB;
        var width = checked((int)image.Width);
        var height = checked((int)image.Height);
        var scale = Math.Min(InputSize / (double)width, InputSize / (double)height);
        var scaledWidth = Math.Clamp((int)(width * scale), 1, InputSize);
        var scaledHeight = Math.Clamp((int)(height * scale), 1, InputSize);
        var input = BuildInputTensor(image, scaledWidth, scaledHeight);

        using var results = session.Run(
        [
            NamedOnnxValue.CreateFromTensor(
                session.InputNames.Single(),
                new DenseTensor<float>(input, [1, 3, InputSize, InputSize]))
        ]);
        var output = results.Single().AsTensor<float>().ToArray();
        var candidates = Decode(output, confidenceThreshold);
        var selected = ApplyClassAwareNms(candidates, nmsThreshold);
        var detections = selected.Select(candidate =>
        {
            var left = Math.Clamp(candidate.Left / scale, 0, width);
            var top = Math.Clamp(candidate.Top / scale, 0, height);
            var right = Math.Clamp(candidate.Right / scale, 0, width);
            var bottom = Math.Clamp(candidate.Bottom / scale, 0, height);
            return new ObjectDetection(
                CocoLabels[candidate.ClassId],
                candidate.ClassId,
                candidate.Confidence,
                left,
                top,
                Math.Max(0, right - left),
                Math.Max(0, bottom - top));
        }).Where(item => item.Width > 0 && item.Height > 0).ToArray();

        return new ObjectDetectionResult(
            true,
            null,
            imagePath,
            width,
            height,
            OnnxRuntimeService.ProviderDetail,
            detections);
    }

    internal static IReadOnlyList<YoloXCandidate> Decode(
        IReadOnlyList<float> output,
        float confidenceThreshold)
    {
        const int attributes = 85;
        const int cells = 8400;
        if (output.Count != cells * attributes)
            throw new InvalidDataException("YOLOX output does not match the reviewed 8400x85 contract.");

        var candidates = new List<YoloXCandidate>();
        var cellIndex = 0;
        foreach (var stride in Strides)
        {
            var gridSize = InputSize / stride;
            var strideCells = gridSize * gridSize;
            for (var localIndex = 0; localIndex < strideCells; localIndex++, cellIndex++)
            {
                var offset = cellIndex * attributes;
                var objectness = output[offset + 4];
                var classId = 0;
                var classScore = output[offset + 5];
                for (var candidateClass = 1; candidateClass < CocoLabels.Count; candidateClass++)
                {
                    if (output[offset + 5 + candidateClass] > classScore)
                    {
                        classScore = output[offset + 5 + candidateClass];
                        classId = candidateClass;
                    }
                }
                var confidence = objectness * classScore;
                if (!float.IsFinite(confidence) || confidence < confidenceThreshold)
                    continue;

                var gridX = localIndex % gridSize;
                var gridY = localIndex / gridSize;
                var centerX = (output[offset] + gridX) * stride;
                var centerY = (output[offset + 1] + gridY) * stride;
                var boxWidth = MathF.Exp(output[offset + 2]) * stride;
                var boxHeight = MathF.Exp(output[offset + 3]) * stride;
                if (!float.IsFinite(centerX) || !float.IsFinite(centerY) ||
                    !float.IsFinite(boxWidth) || !float.IsFinite(boxHeight))
                    continue;
                candidates.Add(new YoloXCandidate(
                    classId,
                    confidence,
                    centerX - boxWidth / 2,
                    centerY - boxHeight / 2,
                    centerX + boxWidth / 2,
                    centerY + boxHeight / 2));
            }
        }
        return candidates;
    }

    internal static IReadOnlyList<YoloXCandidate> ApplyClassAwareNms(
        IEnumerable<YoloXCandidate> candidates,
        float threshold)
    {
        var selected = new List<YoloXCandidate>();
        foreach (var classGroup in candidates.GroupBy(item => item.ClassId))
        {
            foreach (var candidate in classGroup.OrderByDescending(item => item.Confidence))
            {
                if (selected.Where(item => item.ClassId == candidate.ClassId)
                    .All(item => IntersectionOverUnion(item, candidate) < threshold))
                    selected.Add(candidate);
            }
        }
        return selected.OrderByDescending(item => item.Confidence).Take(300).ToArray();
    }

    internal static float[] BuildInputTensor(MagickImage image, int scaledWidth, int scaledHeight)
    {
        using var resized = (MagickImage)image.Clone();
        resized.Resize(new MagickGeometry((uint)scaledWidth, (uint)scaledHeight) { IgnoreAspectRatio = true });
        resized.ColorSpace = ColorSpace.sRGB;
        var tensor = Enumerable.Repeat(114f, 3 * InputSize * InputSize).ToArray();
        var pixels = resized.GetPixelsUnsafe();
        var quantumToByte = 255f / (float)Quantum.Max;
        for (var y = 0; y < scaledHeight; y++)
        for (var x = 0; x < scaledWidth; x++)
        {
            var channels = pixels.GetPixel(x, y)!.ToArray();
            var red = channels[0] * quantumToByte;
            var green = (channels.Length >= 2 ? channels[1] : channels[0]) * quantumToByte;
            var blue = (channels.Length >= 3 ? channels[2] : channels[0]) * quantumToByte;
            var index = y * InputSize + x;
            tensor[index] = red;
            tensor[InputSize * InputSize + index] = green;
            tensor[2 * InputSize * InputSize + index] = blue;
        }
        return tensor;
    }

    private static double IntersectionOverUnion(YoloXCandidate left, YoloXCandidate right)
    {
        var width = Math.Max(0, Math.Min(left.Right, right.Right) - Math.Max(left.Left, right.Left));
        var height = Math.Max(0, Math.Min(left.Bottom, right.Bottom) - Math.Max(left.Top, right.Top));
        var intersection = width * height;
        var leftArea = Math.Max(0, left.Right - left.Left) * Math.Max(0, left.Bottom - left.Top);
        var rightArea = Math.Max(0, right.Right - right.Left) * Math.Max(0, right.Bottom - right.Top);
        var union = leftArea + rightArea - intersection;
        return union <= 0 ? 0 : intersection / union;
    }
}
