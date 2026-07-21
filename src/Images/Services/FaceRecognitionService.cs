using System.IO;
using ImageMagick;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Images.Services;

public enum FaceEmbeddingQuality
{
    Accepted,
    FaceTooSmall,
    InvalidLandmarks,
    InferenceFailed,
}

public sealed record FaceEmbedding(
    string SourcePath,
    int FaceIndex,
    FaceDetection Detection,
    FaceEmbeddingQuality Quality,
    string? RejectionReason,
    IReadOnlyList<float>? Vector)
{
    public bool IsAccepted => Quality == FaceEmbeddingQuality.Accepted && Vector is { Count: > 0 };
}

public sealed record FaceRecognitionResult(
    bool Success,
    string? ErrorMessage,
    string SourcePath,
    string? Runtime,
    IReadOnlyList<FaceEmbedding> Faces);

internal readonly record struct SimilarityTransform(double A, double B, double TranslateX, double TranslateY)
{
    public double Determinant => A * A + B * B;
}

public static class FaceRecognitionService
{
    public const string SFaceModelId = "opencv-face-recognition-sface-2021dec";
    public const int EmbeddingDimensions = 128;
    internal const int AlignedSize = 112;
    internal const double MinimumFacePixels = 40;

    private static readonly FaceLandmark[] AlignmentTemplate =
    [
        new(38.2946, 51.6963),
        new(73.5318, 51.5014),
        new(56.0252, 71.7366),
        new(41.5493, 92.3655),
        new(70.7299, 92.2041),
    ];

    private static readonly ILogger _log = Log.Get(nameof(FaceRecognitionService));

    public static FaceRecognitionResult Analyze(
        string imagePath,
        ModelManagerService? modelManager = null) =>
        AnalyzeMany([imagePath], modelManager).Single();

    // Reuses one detection session and one recognition session across the whole batch
    // (previously each image loaded both models). Honors cancellation between images.
    public static IReadOnlyList<FaceRecognitionResult> AnalyzeMany(
        IReadOnlyList<string> imagePaths,
        ModelManagerService? modelManager = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imagePaths);
        if (imagePaths.Count == 0)
            return [];

        var manager = modelManager ?? new ModelManagerService();
        var detections = FaceDetectionService.DetectMany(imagePaths, manager, cancellationToken: cancellationToken);
        var installedPath = manager.GetSnapshot().Models
            .FirstOrDefault(item => item.Definition.Id == SFaceModelId && item.IsReady)?.InstalledPath;

        // The official 2021 SFace export exposes legacy initializers as overridable inputs.
        // ORT emits one graph warning per initializer even though the reviewed data input and
        // fc1 output are valid; retain errors while keeping CLI output usable.
        var session = TryCreateRecognitionSession(installedPath);
        try
        {
            var results = new List<FaceRecognitionResult>(imagePaths.Count);
            foreach (var detection in detections)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!detection.Success)
                {
                    results.Add(new FaceRecognitionResult(
                        false, detection.ErrorMessage, detection.SourcePath, detection.Runtime, []));
                    continue;
                }
                if (installedPath is null)
                {
                    results.Add(new FaceRecognitionResult(
                        false,
                        "The approved OpenCV SFace model is not imported. Open Model Manager and import the pinned model file first.",
                        detection.SourcePath, detection.Runtime, []));
                    continue;
                }
                if (session is null)
                {
                    results.Add(new FaceRecognitionResult(
                        false,
                        "The face-recognition model could not be loaded. Check diagnostics for technical details.",
                        detection.SourcePath, detection.Runtime, []));
                    continue;
                }
                try
                {
                    results.Add(RunInference(detection, session));
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Face recognition failed for {Path}", detection.SourcePath);
                    results.Add(new FaceRecognitionResult(
                        false,
                        "Face recognition could not analyze this image. Check diagnostics for technical details.",
                        detection.SourcePath, detection.Runtime, []));
                }
            }
            return results;
        }
        finally
        {
            session?.Dispose();
        }
    }

    private static InferenceSession? TryCreateRecognitionSession(string? installedPath)
    {
        if (installedPath is null)
            return null;
        try
        {
            return OnnxRuntimeService.CreateSession(installedPath, OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "The SFace recognition model could not be loaded.");
            return null;
        }
    }

    internal static FaceRecognitionResult RunInference(
        FaceDetectionResult detection,
        string modelPath)
    {
        using var session = OnnxRuntimeService.CreateSession(
            modelPath,
            OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR);
        return RunInference(detection, session);
    }

    internal static FaceRecognitionResult RunInference(
        FaceDetectionResult detection,
        InferenceSession session)
    {
        using var image = MagickSafeReader.Read(
            detection.SourcePath,
            new MagickReadSettings { FrameIndex = 0, FrameCount = 1 });
        image.AutoOrient();
        image.ColorSpace = ColorSpace.sRGB;

        var faces = new List<FaceEmbedding>(detection.Faces.Count);
        for (var index = 0; index < detection.Faces.Count; index++)
        {
            var face = detection.Faces[index];
            if (Math.Min(face.Width, face.Height) < MinimumFacePixels)
            {
                faces.Add(new FaceEmbedding(
                    detection.SourcePath,
                    index,
                    face,
                    FaceEmbeddingQuality.FaceTooSmall,
                    $"Face is smaller than {MinimumFacePixels:0} pixels on its shortest side.",
                    null));
                continue;
            }

            if (!TryBuildAlignedTensor(image, face.Landmarks, out var inputTensor))
            {
                faces.Add(new FaceEmbedding(
                    detection.SourcePath,
                    index,
                    face,
                    FaceEmbeddingQuality.InvalidLandmarks,
                    "Five finite, non-degenerate landmarks are required for recognition.",
                    null));
                continue;
            }

            try
            {
                using var results = session.Run(
                [
                    NamedOnnxValue.CreateFromTensor(
                        session.InputNames.Single(),
                        new DenseTensor<float>(inputTensor, [1, 3, AlignedSize, AlignedSize]))
                ]);
                var raw = results.Single().AsTensor<float>().ToArray();
                if (raw.Length != EmbeddingDimensions || !TryNormalize(raw))
                    throw new InvalidDataException("SFace output does not match the reviewed 128-value embedding contract.");

                faces.Add(new FaceEmbedding(
                    detection.SourcePath,
                    index,
                    face,
                    FaceEmbeddingQuality.Accepted,
                    null,
                    raw));
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "SFace inference failed for face {FaceIndex} in {Path}", index, detection.SourcePath);
                faces.Add(new FaceEmbedding(
                    detection.SourcePath,
                    index,
                    face,
                    FaceEmbeddingQuality.InferenceFailed,
                    "The local recognition model could not embed this face.",
                    null));
            }
        }

        return new FaceRecognitionResult(
            true,
            null,
            detection.SourcePath,
            OnnxRuntimeService.ProviderDetail,
            faces);
    }

    public static double CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Count == 0 || left.Count != right.Count)
            throw new ArgumentException("Embedding vectors must be non-empty and have equal dimensions.");

        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;
        for (var index = 0; index < left.Count; index++)
        {
            dot += left[index] * right[index];
            leftNorm += left[index] * left[index];
            rightNorm += right[index] * right[index];
        }
        var denominator = Math.Sqrt(leftNorm * rightNorm);
        return denominator <= double.Epsilon ? 0 : Math.Clamp(dot / denominator, -1, 1);
    }

    internal static SimilarityTransform ComputeSimilarityTransform(
        IReadOnlyList<FaceLandmark> source,
        IReadOnlyList<FaceLandmark>? destination = null)
    {
        destination ??= AlignmentTemplate;
        if (source.Count != 5 || destination.Count != 5 ||
            source.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)) ||
            destination.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
        {
            throw new ArgumentException("Exactly five finite landmarks are required.");
        }

        var sourceMeanX = source.Average(point => point.X);
        var sourceMeanY = source.Average(point => point.Y);
        var destinationMeanX = destination.Average(point => point.X);
        var destinationMeanY = destination.Average(point => point.Y);
        double denominator = 0;
        double aNumerator = 0;
        double bNumerator = 0;
        for (var index = 0; index < source.Count; index++)
        {
            var sourceX = source[index].X - sourceMeanX;
            var sourceY = source[index].Y - sourceMeanY;
            var destinationX = destination[index].X - destinationMeanX;
            var destinationY = destination[index].Y - destinationMeanY;
            denominator += sourceX * sourceX + sourceY * sourceY;
            aNumerator += sourceX * destinationX + sourceY * destinationY;
            bNumerator += sourceX * destinationY - sourceY * destinationX;
        }
        if (denominator <= double.Epsilon)
            throw new ArgumentException("Landmarks are degenerate.");

        var a = aNumerator / denominator;
        var b = bNumerator / denominator;
        var translateX = destinationMeanX - a * sourceMeanX + b * sourceMeanY;
        var translateY = destinationMeanY - b * sourceMeanX - a * sourceMeanY;
        var transform = new SimilarityTransform(a, b, translateX, translateY);
        if (!double.IsFinite(transform.Determinant) || transform.Determinant <= double.Epsilon)
            throw new ArgumentException("Landmark alignment is not invertible.");
        return transform;
    }

    internal static bool TryBuildAlignedTensor(
        MagickImage image,
        IReadOnlyList<FaceLandmark> landmarks,
        out float[] tensor)
    {
        tensor = [];
        SimilarityTransform transform;
        try
        {
            transform = ComputeSimilarityTransform(landmarks);
        }
        catch (ArgumentException)
        {
            return false;
        }

        tensor = new float[3 * AlignedSize * AlignedSize];
        var pixels = image.GetPixelsUnsafe();
        var quantumToByte = 255f / (float)Quantum.Max;
        for (var y = 0; y < AlignedSize; y++)
        {
            for (var x = 0; x < AlignedSize; x++)
            {
                var sourceX = (transform.A * (x - transform.TranslateX) +
                               transform.B * (y - transform.TranslateY)) / transform.Determinant;
                var sourceY = (-transform.B * (x - transform.TranslateX) +
                               transform.A * (y - transform.TranslateY)) / transform.Determinant;
                var (red, green, blue) = BilinearSample(pixels, sourceX, sourceY, image.Width, image.Height);
                var offset = y * AlignedSize + x;

                // SFace's reviewed OpenCV path uses blobFromImage(..., swapRB=true), yielding
                // planar RGB values in the original 0..255 range.
                tensor[offset] = red * quantumToByte;
                tensor[AlignedSize * AlignedSize + offset] = green * quantumToByte;
                tensor[2 * AlignedSize * AlignedSize + offset] = blue * quantumToByte;
            }
        }
        return true;
    }

    private static (float Red, float Green, float Blue) BilinearSample(
        IUnsafePixelCollection<float> pixels,
        double x,
        double y,
        uint width,
        uint height)
    {
        if (x < 0 || y < 0 || x > width - 1 || y > height - 1)
            return (0, 0, 0);

        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var x1 = Math.Min(x0 + 1, checked((int)width) - 1);
        var y1 = Math.Min(y0 + 1, checked((int)height) - 1);
        var xWeight = (float)(x - x0);
        var yWeight = (float)(y - y0);
        var top = SampleRow(pixels, x0, x1, y0, xWeight);
        var bottom = SampleRow(pixels, x0, x1, y1, xWeight);
        return (
            top.Red + (bottom.Red - top.Red) * yWeight,
            top.Green + (bottom.Green - top.Green) * yWeight,
            top.Blue + (bottom.Blue - top.Blue) * yWeight);
    }

    private static (float Red, float Green, float Blue) SampleRow(
        IUnsafePixelCollection<float> pixels,
        int x0,
        int x1,
        int y,
        float weight)
    {
        var left = pixels.GetPixel(x0, y)!.ToArray();
        var right = pixels.GetPixel(x1, y)!.ToArray();
        var leftRed = left[0];
        var leftGreen = left.Length >= 2 ? left[1] : leftRed;
        var leftBlue = left.Length >= 3 ? left[2] : leftRed;
        var rightRed = right[0];
        var rightGreen = right.Length >= 2 ? right[1] : rightRed;
        var rightBlue = right.Length >= 3 ? right[2] : rightRed;
        return (
            leftRed + (rightRed - leftRed) * weight,
            leftGreen + (rightGreen - leftGreen) * weight,
            leftBlue + (rightBlue - leftBlue) * weight);
    }

    private static bool TryNormalize(float[] vector)
    {
        double sum = 0;
        foreach (var value in vector)
        {
            if (!float.IsFinite(value))
                return false;
            sum += value * value;
        }
        var norm = Math.Sqrt(sum);
        if (norm <= double.Epsilon)
            return false;
        for (var index = 0; index < vector.Length; index++)
            vector[index] = (float)(vector[index] / norm);
        return true;
    }
}
