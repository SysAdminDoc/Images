using System.IO;
using ImageMagick;

namespace Images.Services;

public sealed record FaceCullingHint(
    double LocalSharpness,
    double? LeftEyeTextureRatio,
    double? RightEyeTextureRatio,
    bool PossibleLocalBlur,
    bool PossibleClosedEyes,
    IReadOnlyList<string> ReviewHints);

public static class FaceCullingHintService
{
    internal const int FaceSampleSize = 128;
    internal const int EyeSampleWidth = 48;
    internal const int EyeSampleHeight = 24;
    internal const double LocalBlurThreshold = 0.0025;
    internal const double ClosedEyeTextureRatioThreshold = 0.72;

    public static IReadOnlyDictionary<FaceReviewKey, FaceCullingHint> Analyze(
        string imagePath,
        IReadOnlyList<FaceEmbedding> faces)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        ArgumentNullException.ThrowIfNull(faces);
        using var image = MagickSafeReader.Read(
            imagePath,
            new MagickReadSettings { FrameIndex = 0, FrameCount = 1 });
        image.AutoOrient();
        image.ColorSpace = ColorSpace.sRGB;
        return faces.ToDictionary(
            face => new FaceReviewKey(face.SourcePath, face.FaceIndex),
            face => Evaluate(image, face.Detection));
    }

    internal static FaceCullingHint Evaluate(MagickImage image, FaceDetection face)
    {
        using var faceCrop = Crop(image, face.X, face.Y, face.Width, face.Height);
        faceCrop.Resize(
            new MagickGeometry(FaceSampleSize, FaceSampleSize) { IgnoreAspectRatio = true },
            FilterType.Lanczos);
        var sharpness = ComputeLaplacianVariance(ReadGray(faceCrop));
        var possibleBlur = sharpness < LocalBlurThreshold;

        double? leftRatio = null;
        double? rightRatio = null;
        if (face.Landmarks.Count >= 2)
        {
            var interocular = Distance(face.Landmarks[0], face.Landmarks[1]);
            if (interocular >= 8)
            {
                leftRatio = EyeTextureRatio(image, face.Landmarks[0], interocular);
                rightRatio = EyeTextureRatio(image, face.Landmarks[1], interocular);
            }
        }

        var possibleClosedEyes = !possibleBlur &&
                                 leftRatio is < ClosedEyeTextureRatioThreshold &&
                                 rightRatio is < ClosedEyeTextureRatioThreshold;
        var hints = new List<string>(2);
        if (possibleBlur)
            hints.Add("Possible local face blur");
        if (possibleClosedEyes)
            hints.Add("Possible closed eyes");

        return new FaceCullingHint(
            sharpness,
            leftRatio,
            rightRatio,
            possibleBlur,
            possibleClosedEyes,
            hints);
    }

    internal static double ComputeLaplacianVariance(float[,] pixels)
    {
        var height = pixels.GetLength(0);
        var width = pixels.GetLength(1);
        if (height < 3 || width < 3) return 0;
        double sum = 0;
        double sumSquares = 0;
        var count = 0;
        for (var y = 1; y < height - 1; y++)
        for (var x = 1; x < width - 1; x++)
        {
            var value = pixels[y - 1, x] + pixels[y + 1, x] + pixels[y, x - 1] + pixels[y, x + 1] -
                        4 * pixels[y, x];
            sum += value;
            sumSquares += value * value;
            count++;
        }
        var mean = sum / count;
        return Math.Max(0, sumSquares / count - mean * mean);
    }

    internal static double ComputeDirectionalTextureRatio(float[,] pixels)
    {
        var height = pixels.GetLength(0);
        var width = pixels.GetLength(1);
        if (height < 2 || width < 2) return 0;
        double horizontal = 0;
        double vertical = 0;
        for (var y = 1; y < height; y++)
        for (var x = 1; x < width; x++)
        {
            horizontal += Math.Abs(pixels[y, x] - pixels[y, x - 1]);
            vertical += Math.Abs(pixels[y, x] - pixels[y - 1, x]);
        }
        return horizontal / Math.Max(vertical, 1e-8);
    }

    private static double EyeTextureRatio(MagickImage image, FaceLandmark eye, double interocular)
    {
        var width = Math.Max(8, interocular * 0.42);
        var height = Math.Max(6, interocular * 0.24);
        using var crop = Crop(image, eye.X - width / 2, eye.Y - height / 2, width, height);
        crop.Resize(
            new MagickGeometry(EyeSampleWidth, EyeSampleHeight) { IgnoreAspectRatio = true },
            FilterType.Lanczos);
        return ComputeDirectionalTextureRatio(ReadGray(crop));
    }

    private static MagickImage Crop(
        MagickImage image,
        double x,
        double y,
        double width,
        double height)
    {
        var left = Math.Clamp((int)Math.Floor(x), 0, checked((int)image.Width) - 1);
        var top = Math.Clamp((int)Math.Floor(y), 0, checked((int)image.Height) - 1);
        var right = Math.Clamp((int)Math.Ceiling(x + Math.Max(1, width)), left + 1, checked((int)image.Width));
        var bottom = Math.Clamp((int)Math.Ceiling(y + Math.Max(1, height)), top + 1, checked((int)image.Height));
        var crop = (MagickImage)image.Clone();
        crop.Crop(new MagickGeometry(left, top, (uint)(right - left), (uint)(bottom - top)));
        crop.ColorSpace = ColorSpace.sRGB;
        return crop;
    }

    private static float[,] ReadGray(MagickImage image)
    {
        var width = checked((int)image.Width);
        var height = checked((int)image.Height);
        var values = new float[height, width];
        var pixels = image.GetPixelsUnsafe();
        var scale = 1f / (float)Quantum.Max;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var channels = pixels.GetPixel(x, y)!.ToArray();
            var red = channels[0] * scale;
            var green = (channels.Length >= 2 ? channels[1] : channels[0]) * scale;
            var blue = (channels.Length >= 3 ? channels[2] : channels[0]) * scale;
            values[y, x] = 0.2126f * red + 0.7152f * green + 0.0722f * blue;
        }
        return values;
    }

    private static double Distance(FaceLandmark left, FaceLandmark right) =>
        Math.Sqrt(Math.Pow(left.X - right.X, 2) + Math.Pow(left.Y - right.Y, 2));
}
