using System.IO;
using System.Windows.Media.Imaging;

namespace Images.Services;

public static class AnimationWorkbenchService
{
    public const double MinPlaybackSpeed = 0.25;
    public const double MaxPlaybackSpeed = 4.0;
    private static readonly TimeSpan MinTimerInterval = TimeSpan.FromMilliseconds(16);

    public static int ClampFrameIndex(AnimationSequence? sequence, int index)
    {
        if (sequence is null || sequence.Frames.Count == 0)
            return 0;

        return Math.Clamp(index, 0, sequence.Frames.Count - 1);
    }

    public static double ClampPlaybackSpeed(double speed)
    {
        if (double.IsNaN(speed) || double.IsInfinity(speed))
            return 1.0;

        return Math.Clamp(speed, MinPlaybackSpeed, MaxPlaybackSpeed);
    }

    public static TimeSpan DelayForSpeed(TimeSpan delay, double speed)
    {
        var safeSpeed = ClampPlaybackSpeed(speed);
        var milliseconds = Math.Max(MinTimerInterval.TotalMilliseconds, delay.TotalMilliseconds / safeSpeed);
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    public static string FormatFramePosition(int index, int frameCount)
        => frameCount <= 0 ? "No frames" : $"Frame {Math.Clamp(index, 0, frameCount - 1) + 1} of {frameCount}";

    public static string FormatDelay(TimeSpan delay)
        => delay.TotalSeconds >= 1
            ? $"{delay.TotalSeconds:0.##} s"
            : $"{Math.Max(1, (int)Math.Round(delay.TotalMilliseconds))} ms";

    public static string FormatTimestamp(IReadOnlyList<TimeSpan> delays, int index)
    {
        if (delays.Count == 0)
            return "00:00.000";

        var clamped = Math.Clamp(index, 0, delays.Count - 1);
        var elapsed = TimeSpan.Zero;
        for (var i = 0; i < clamped; i++)
            elapsed += delays[i];

        return $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds:000}";
    }

    public static string FormatSpeed(double speed)
    {
        var clamped = ClampPlaybackSpeed(speed);
        return $"{clamped:0.##}x";
    }

    public static string CreateDefaultFrameExportFileName(string? sourcePath, int frameIndex)
    {
        var stem = string.IsNullOrWhiteSpace(sourcePath)
            ? "animation"
            : Path.GetFileNameWithoutExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(stem))
            stem = "animation";

        return $"{SanitizeFileName(stem)}-frame-{frameIndex + 1:000}.png";
    }

    public static void SaveFramePng(BitmapSource frame, string path)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Export path is required.", nameof(path));

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(frame));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    public static string SaveFrameToTemp(BitmapSource frame, string? sourcePath, int frameIndex)
    {
        var directory = AppStorage.TryGetAppDirectory("animation-frames")
                        ?? Path.Combine(Path.GetTempPath(), "Images", "animation-frames");
        Directory.CreateDirectory(directory);

        var baseName = Path.GetFileNameWithoutExtension(CreateDefaultFrameExportFileName(sourcePath, frameIndex));
        var path = Path.Combine(directory, $"{baseName}-{Guid.NewGuid():N}.png");
        SaveFramePng(frame, path);
        return path;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (invalid.Contains(chars[i]))
                chars[i] = '-';
        }

        return new string(chars).Trim('.', ' ');
    }
}
