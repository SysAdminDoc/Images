using System.IO;
using ImageMagick;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public enum FaceDetectionStatus
{
    Success,
    ModelUnavailable,
    Failed,
}

public sealed record FaceLandmark(double X, double Y);

public sealed record FaceDetection(
    double X,
    double Y,
    double Width,
    double Height,
    double Confidence,
    IReadOnlyList<FaceLandmark> Landmarks)
{
    public double NormalizedCenterX(int imageWidth) => imageWidth <= 0 ? 0 : (X + Width / 2d) / imageWidth;
    public double NormalizedCenterY(int imageHeight) => imageHeight <= 0 ? 0 : (Y + Height / 2d) / imageHeight;
    public double NormalizedWidth(int imageWidth) => imageWidth <= 0 ? 0 : Width / imageWidth;
    public double NormalizedHeight(int imageHeight) => imageHeight <= 0 ? 0 : Height / imageHeight;
}

public sealed record FaceDetectionResult(
    FaceDetectionStatus Status,
    string? ErrorMessage,
    string SourcePath,
    int ImageWidth,
    int ImageHeight,
    string? Runtime,
    IReadOnlyList<FaceDetection> Faces)
{
    public bool Success => Status == FaceDetectionStatus.Success;
}

internal sealed record YuNetCandidate(
    float X,
    float Y,
    float Width,
    float Height,
    float Confidence,
    IReadOnlyList<FaceLandmark> Landmarks);

public static class FaceDetectionService
{
    public const string YuNetModelId = "opencv-face-detection-yunet-2023mar";
    internal const int InputSize = 640;
    private static readonly int[] Strides = [8, 16, 32];
    private static readonly ILogger _log = Log.Get(nameof(FaceDetectionService));

    public static FaceDetectionResult Detect(
        string imagePath,
        ModelManagerService? modelManager = null,
        float confidenceThreshold = 0.9f,
        float nmsThreshold = 0.3f) =>
        DetectMany([imagePath], modelManager, confidenceThreshold, nmsThreshold).Single();

    // Reuses one InferenceSession across the batch (a single detection session for the whole run
    // instead of one per image) and honors cancellation between images.
    public static IReadOnlyList<FaceDetectionResult> DetectMany(
        IReadOnlyList<string> imagePaths,
        ModelManagerService? modelManager = null,
        float confidenceThreshold = 0.9f,
        float nmsThreshold = 0.3f,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imagePaths);
        if (imagePaths.Count == 0)
            return [];

        var manager = modelManager ?? new ModelManagerService();
        var installedPath = manager.GetSnapshot().Models
            .FirstOrDefault(item => item.Definition.Id == YuNetModelId && item.IsReady)?.InstalledPath;
        var session = TryCreateSession(installedPath);
        try
        {
            var results = new List<FaceDetectionResult>(imagePaths.Count);
            foreach (var imagePath in imagePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(DetectOne(imagePath, installedPath, session, confidenceThreshold, nmsThreshold));
            }
            return results;
        }
        finally
        {
            session?.Dispose();
        }
    }

    private static InferenceSession? TryCreateSession(string? installedPath)
    {
        if (installedPath is null)
            return null;
        try
        {
            return OnnxRuntimeService.CreateSession(installedPath);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "The YuNet face-detection model could not be loaded.");
            return null;
        }
    }

    private static FaceDetectionResult DetectOne(
        string imagePath,
        string? installedPath,
        InferenceSession? session,
        float confidenceThreshold,
        float nmsThreshold)
    {
        var normalizedPath = NormalizeExistingPath(imagePath);
        if (normalizedPath is null)
        {
            return new FaceDetectionResult(
                FaceDetectionStatus.Failed,
                "Choose an existing local image file.",
                imagePath ?? string.Empty,
                0, 0, null, []);
        }

        if (installedPath is null)
        {
            return new FaceDetectionResult(
                FaceDetectionStatus.ModelUnavailable,
                "The approved OpenCV YuNet model is not imported. Open Model Manager and import the pinned model file first.",
                normalizedPath,
                0, 0, null, []);
        }

        if (session is null)
        {
            return new FaceDetectionResult(
                FaceDetectionStatus.Failed,
                "The face-detection model could not be loaded. Check diagnostics for technical details.",
                normalizedPath,
                0, 0, null, []);
        }

        try
        {
            return RunInference(normalizedPath, session, confidenceThreshold, nmsThreshold);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Face detection failed for {Path}", normalizedPath);
            return new FaceDetectionResult(
                FaceDetectionStatus.Failed,
                "Face detection could not analyze this image. Check diagnostics for technical details.",
                normalizedPath,
                0, 0, null, []);
        }
    }

    internal static FaceDetectionResult RunInference(
        string imagePath,
        string modelPath,
        float confidenceThreshold = 0.9f,
        float nmsThreshold = 0.3f)
    {
        using var session = OnnxRuntimeService.CreateSession(modelPath);
        return RunInference(imagePath, session, confidenceThreshold, nmsThreshold);
    }

    internal static FaceDetectionResult RunInference(
        string imagePath,
        InferenceSession session,
        float confidenceThreshold = 0.9f,
        float nmsThreshold = 0.3f)
    {
        if (confidenceThreshold is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(confidenceThreshold));
        if (nmsThreshold is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(nmsThreshold));

        using var image = MagickSafeReader.Read(
            imagePath,
            new MagickReadSettings { FrameIndex = 0, FrameCount = 1 });
        image.AutoOrient();
        image.ColorSpace = ColorSpace.sRGB;

        var imageWidth = checked((int)image.Width);
        var imageHeight = checked((int)image.Height);
        if (imageWidth <= 0 || imageHeight <= 0)
            throw new InvalidDataException("Image dimensions are invalid.");

        var scale = Math.Min(InputSize / (double)imageWidth, InputSize / (double)imageHeight);
        var scaledWidth = Math.Clamp((int)Math.Round(imageWidth * scale), 1, InputSize);
        var scaledHeight = Math.Clamp((int)Math.Round(imageHeight * scale), 1, InputSize);
        var tensor = BuildInputTensor(image, scaledWidth, scaledHeight);

        using var results = session.Run(
        [
            NamedOnnxValue.CreateFromTensor(
                session.InputNames.Single(),
                new DenseTensor<float>(tensor, [1, 3, InputSize, InputSize]))
        ]);

        var outputs = results.ToDictionary(
            result => result.Name,
            result => result.AsTensor<float>(),
            StringComparer.Ordinal);
        var candidates = new List<YuNetCandidate>();
        foreach (var stride in Strides)
        {
            var gridSize = InputSize / stride;
            candidates.AddRange(DecodeStride(
                stride,
                gridSize,
                gridSize,
                outputs[$"cls_{stride}"].ToArray(),
                outputs[$"obj_{stride}"].ToArray(),
                outputs[$"bbox_{stride}"].ToArray(),
                outputs[$"kps_{stride}"].ToArray(),
                confidenceThreshold));
        }

        var selected = ApplyNonMaximumSuppression(candidates, nmsThreshold, topK: 5000, maxSelected: 1024);
        var faces = selected
            .Select(candidate => MapToSource(candidate, scale, imageWidth, imageHeight))
            .Where(face => face.Width > 0 && face.Height > 0)
            .ToArray();

        return new FaceDetectionResult(
            FaceDetectionStatus.Success,
            null,
            imagePath,
            imageWidth,
            imageHeight,
            OnnxRuntimeService.ProviderDetail,
            faces);
    }

    internal static IReadOnlyList<YuNetCandidate> DecodeStride(
        int stride,
        int gridWidth,
        int gridHeight,
        IReadOnlyList<float> classScores,
        IReadOnlyList<float> objectScores,
        IReadOnlyList<float> boxes,
        IReadOnlyList<float> keypoints,
        float confidenceThreshold)
    {
        var cells = checked(gridWidth * gridHeight);
        if (stride <= 0 || gridWidth <= 0 || gridHeight <= 0 ||
            classScores.Count != cells || objectScores.Count != cells ||
            boxes.Count != cells * 4 || keypoints.Count != cells * 10)
        {
            throw new InvalidDataException("YuNet output shapes do not match the reviewed model contract.");
        }

        var candidates = new List<YuNetCandidate>();
        for (var row = 0; row < gridHeight; row++)
        {
            for (var column = 0; column < gridWidth; column++)
            {
                var index = row * gridWidth + column;
                var classScore = Math.Clamp(classScores[index], 0f, 1f);
                var objectScore = Math.Clamp(objectScores[index], 0f, 1f);
                var confidence = MathF.Sqrt(classScore * objectScore);
                if (!float.IsFinite(confidence) || confidence < confidenceThreshold)
                    continue;

                var boxOffset = index * 4;
                var centerX = (column + boxes[boxOffset]) * stride;
                var centerY = (row + boxes[boxOffset + 1]) * stride;
                var width = MathF.Exp(boxes[boxOffset + 2]) * stride;
                var height = MathF.Exp(boxes[boxOffset + 3]) * stride;
                if (!float.IsFinite(centerX) || !float.IsFinite(centerY) ||
                    !float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0)
                {
                    continue;
                }

                var landmarks = new FaceLandmark[5];
                var keypointOffset = index * 10;
                for (var point = 0; point < landmarks.Length; point++)
                {
                    landmarks[point] = new FaceLandmark(
                        (keypoints[keypointOffset + point * 2] + column) * stride,
                        (keypoints[keypointOffset + point * 2 + 1] + row) * stride);
                }

                candidates.Add(new YuNetCandidate(
                    centerX - width / 2f,
                    centerY - height / 2f,
                    width,
                    height,
                    confidence,
                    landmarks));
            }
        }

        return candidates;
    }

    internal static IReadOnlyList<YuNetCandidate> ApplyNonMaximumSuppression(
        IEnumerable<YuNetCandidate> candidates,
        float threshold,
        int topK,
        int maxSelected = int.MaxValue)
    {
        if (threshold is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(threshold));
        if (topK <= 0)
            throw new ArgumentOutOfRangeException(nameof(topK));
        if (maxSelected <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSelected));

        var ordered = candidates
            .OrderByDescending(candidate => candidate.Confidence)
            .Take(topK)
            .ToArray();
        // Bound the O(k^2) suppression: a real image never has thousands of faces, so cap the
        // number of kept boxes. Highest-confidence candidates are considered first, so the cap
        // never drops a face that would survive on a normal image.
        var selected = new List<YuNetCandidate>();
        foreach (var candidate in ordered)
        {
            if (selected.Count >= maxSelected)
                break;
            if (selected.All(kept => IntersectionOverUnion(candidate, kept) < threshold))
                selected.Add(candidate);
        }
        return selected;
    }

    internal static float[] BuildInputTensor(MagickImage image, int scaledWidth, int scaledHeight)
    {
        using var resized = (MagickImage)image.Clone();
        resized.Resize(new MagickGeometry((uint)scaledWidth, (uint)scaledHeight)
        {
            IgnoreAspectRatio = true,
        });
        resized.ColorSpace = ColorSpace.sRGB;

        var tensor = new float[3 * InputSize * InputSize];
        var pixels = resized.GetPixelsUnsafe();
        var quantumToByte = 255f / (float)Quantum.Max;
        for (var y = 0; y < scaledHeight; y++)
        {
            for (var x = 0; x < scaledWidth; x++)
            {
                var channels = pixels.GetPixel(x, y)!.ToArray();
                var red = Math.Clamp(channels[0] * quantumToByte, 0f, 255f);
                var green = channels.Length >= 2
                    ? Math.Clamp(channels[1] * quantumToByte, 0f, 255f)
                    : red;
                var blue = channels.Length >= 3
                    ? Math.Clamp(channels[2] * quantumToByte, 0f, 255f)
                    : red;
                var index = y * InputSize + x;

                // OpenCV's reviewed FaceDetectorYN path uses blobFromImage with swapRB=false,
                // so reproduce its BGR planar input from ImageMagick's RGB channel order.
                tensor[index] = blue;
                tensor[InputSize * InputSize + index] = green;
                tensor[2 * InputSize * InputSize + index] = red;
            }
        }
        return tensor;
    }

    private static FaceDetection MapToSource(
        YuNetCandidate candidate,
        double scale,
        int imageWidth,
        int imageHeight)
    {
        var left = Math.Clamp(candidate.X / scale, 0d, imageWidth);
        var top = Math.Clamp(candidate.Y / scale, 0d, imageHeight);
        var right = Math.Clamp((candidate.X + candidate.Width) / scale, 0d, imageWidth);
        var bottom = Math.Clamp((candidate.Y + candidate.Height) / scale, 0d, imageHeight);
        var landmarks = candidate.Landmarks
            .Select(point => new FaceLandmark(
                Math.Clamp(point.X / scale, 0d, imageWidth),
                Math.Clamp(point.Y / scale, 0d, imageHeight)))
            .ToArray();
        return new FaceDetection(
            left,
            top,
            Math.Max(0, right - left),
            Math.Max(0, bottom - top),
            candidate.Confidence,
            landmarks);
    }

    private static double IntersectionOverUnion(YuNetCandidate left, YuNetCandidate right)
    {
        var intersectionLeft = Math.Max(left.X, right.X);
        var intersectionTop = Math.Max(left.Y, right.Y);
        var intersectionRight = Math.Min(left.X + left.Width, right.X + right.Width);
        var intersectionBottom = Math.Min(left.Y + left.Height, right.Y + right.Height);
        var intersectionWidth = Math.Max(0, intersectionRight - intersectionLeft);
        var intersectionHeight = Math.Max(0, intersectionBottom - intersectionTop);
        var intersection = intersectionWidth * intersectionHeight;
        var union = left.Width * left.Height + right.Width * right.Height - intersection;
        return union <= 0 ? 0 : intersection / union;
    }

    private static string? NormalizeExistingPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path.StartsWith("\\\\?\\", StringComparison.Ordinal) ||
            path.StartsWith("\\\\.\\", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            return File.Exists(fullPath) ? fullPath : null;
        }
        catch
        {
            return null;
        }
    }
}
