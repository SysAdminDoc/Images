using System.Diagnostics;
using System.Globalization;
using System.IO;
using ImageMagick;

namespace Images.Services;

internal delegate JpegTranProcessResult JpegTranProcessRunner(
    string executablePath,
    IReadOnlyList<string> arguments,
    int timeoutMilliseconds);

internal sealed record JpegTranProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut = false);

internal sealed record JpegTranWriteResult(
    bool Attempted,
    bool Applied,
    string Message)
{
    public static JpegTranWriteResult NotAttempted(string message) => new(false, false, message);
    public static JpegTranWriteResult AppliedResult(string message) => new(true, true, message);
    public static JpegTranWriteResult Failed(string message) => new(true, false, message);
}

internal static class JpegTranTransformService
{
    private const int TransformTimeoutMilliseconds = 30000;

    private static readonly HashSet<string> JpegExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jpe", ".jfif", ".jif"
    };

    public static JpegTranWriteResult TryApplyExactCrop(
        string sourcePath,
        PixelSelection requestedSelection,
        int imageWidth,
        int imageHeight,
        JpegTranRuntimeStatus runtime,
        JpegTranProcessRunner? processRunner = null,
        bool allowTrim = false)
    {
        var normalizedSourcePath = Path.GetFullPath(sourcePath);
        if (!JpegExtensions.Contains(Path.GetExtension(normalizedSourcePath)))
            return JpegTranWriteResult.NotAttempted("jpegtran crop writeback is JPEG-only.");

        if (!runtime.Available || string.IsNullOrWhiteSpace(runtime.ExecutablePath))
            return JpegTranWriteResult.NotAttempted(runtime.StatusText);

        var plan = LosslessJpegTransformPolicy.PlanCrop(
            requestedSelection,
            imageWidth,
            imageHeight,
            JpegMcuSize.Conservative420);
        if (!plan.CanApplyLosslessly || plan.AlignedSelection is not { } crop)
            return JpegTranWriteResult.NotAttempted(plan.UserMessage);

        if (plan.RequiresTrimConfirmation && !allowTrim)
            return JpegTranWriteResult.NotAttempted(plan.UserMessage);

        var directory = Path.GetDirectoryName(normalizedSourcePath);
        if (string.IsNullOrWhiteSpace(directory))
            return JpegTranWriteResult.Failed("JPEG crop failed: source file has no directory.");

        Directory.CreateDirectory(directory);
        var outputPath = Path.Combine(directory, $".images-jpegtran-{Guid.NewGuid():N}.jpg");
        var backupPath = Path.Combine(directory, $".images-jpegtran-backup-{Guid.NewGuid():N}{Path.GetExtension(normalizedSourcePath)}");

        try
        {
            var arguments = BuildCropArguments(crop, outputPath, normalizedSourcePath);
            var runner = processRunner ?? RunProcess;
            var result = runner(runtime.ExecutablePath, arguments, TransformTimeoutMilliseconds);
            if (result.TimedOut)
                return JpegTranWriteResult.Failed("JPEG crop failed: jpegtran timed out.");

            if (result.ExitCode != 0)
            {
                var details = FirstNonEmptyLine(result.StandardError, result.StandardOutput);
                return JpegTranWriteResult.Failed(
                    string.IsNullOrWhiteSpace(details)
                        ? $"JPEG crop failed: jpegtran exited with code {result.ExitCode}."
                        : $"JPEG crop failed: jpegtran exited with code {result.ExitCode}: {details}");
            }

            if (!ValidateJpegOutput(outputPath, crop.Width, crop.Height, out var validationError))
                return JpegTranWriteResult.Failed($"JPEG crop failed: {validationError}");

            ReplaceAtomically(outputPath, normalizedSourcePath, backupPath);
            return JpegTranWriteResult.AppliedResult(
                plan.RequiresTrimConfirmation
                    ? "JPEG crop applied losslessly with jpegtran after confirmed MCU trim."
                    : "JPEG crop applied losslessly with jpegtran.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException or NotSupportedException)
        {
            return JpegTranWriteResult.Failed($"JPEG crop failed: {ex.Message}");
        }
        finally
        {
            TryDeleteFile(outputPath);
            TryDeleteFile(backupPath);
        }
    }

    internal static IReadOnlyList<string> BuildCropArguments(
        PixelSelection crop,
        string outputPath,
        string sourcePath)
        =>
        [
            "-copy",
            "icc",
            "-crop",
            FormattableString.Invariant($"{crop.Width}x{crop.Height}+{crop.X}+{crop.Y}"),
            "-outfile",
            outputPath,
            sourcePath
        ];

    public static JpegTranWriteResult TryApplyExactRotation(
        string sourcePath,
        LosslessJpegRotation rotation,
        int imageWidth,
        int imageHeight,
        JpegTranRuntimeStatus runtime,
        JpegTranProcessRunner? processRunner = null,
        bool allowTrim = false)
    {
        var normalizedSourcePath = Path.GetFullPath(sourcePath);
        if (!JpegExtensions.Contains(Path.GetExtension(normalizedSourcePath)))
            return JpegTranWriteResult.NotAttempted("jpegtran rotation writeback is JPEG-only.");

        if (!runtime.Available || string.IsNullOrWhiteSpace(runtime.ExecutablePath))
            return JpegTranWriteResult.NotAttempted(runtime.StatusText);

        var plan = LosslessJpegTransformPolicy.PlanRotation(
            imageWidth,
            imageHeight,
            rotation,
            JpegMcuSize.Conservative420);
        if (!plan.CanApplyLosslessly || plan.PreservedSourceBounds is not { } preservedSourceBounds)
            return JpegTranWriteResult.NotAttempted(plan.UserMessage);

        if (plan.RequiresTrimConfirmation && !allowTrim)
            return JpegTranWriteResult.NotAttempted(plan.UserMessage);

        var directory = Path.GetDirectoryName(normalizedSourcePath);
        if (string.IsNullOrWhiteSpace(directory))
            return JpegTranWriteResult.Failed("JPEG rotation failed: source file has no directory.");

        Directory.CreateDirectory(directory);
        var outputPath = Path.Combine(directory, $".images-jpegtran-{Guid.NewGuid():N}.jpg");
        var backupPath = Path.Combine(directory, $".images-jpegtran-backup-{Guid.NewGuid():N}{Path.GetExtension(normalizedSourcePath)}");
        var expectedSize = ExpectedRotatedSize(preservedSourceBounds.Width, preservedSourceBounds.Height, rotation);

        try
        {
            var arguments = BuildRotateArguments(
                rotation,
                outputPath,
                normalizedSourcePath,
                trimIncompleteEdgeBlocks: plan.RequiresTrimConfirmation);
            var runner = processRunner ?? RunProcess;
            var result = runner(runtime.ExecutablePath, arguments, TransformTimeoutMilliseconds);
            if (result.TimedOut)
                return JpegTranWriteResult.Failed("JPEG rotation failed: jpegtran timed out.");

            if (result.ExitCode != 0)
            {
                var details = FirstNonEmptyLine(result.StandardError, result.StandardOutput);
                return JpegTranWriteResult.Failed(
                    string.IsNullOrWhiteSpace(details)
                        ? $"JPEG rotation failed: jpegtran exited with code {result.ExitCode}."
                        : $"JPEG rotation failed: jpegtran exited with code {result.ExitCode}: {details}");
            }

            if (!ValidateJpegOutput(outputPath, expectedSize.Width, expectedSize.Height, out var validationError))
                return JpegTranWriteResult.Failed($"JPEG rotation failed: {validationError}");

            ReplaceAtomically(outputPath, normalizedSourcePath, backupPath);
            return JpegTranWriteResult.AppliedResult(
                plan.RequiresTrimConfirmation
                    ? "JPEG rotation applied losslessly with jpegtran after confirmed MCU trim."
                    : "JPEG rotation applied losslessly with jpegtran.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException or NotSupportedException)
        {
            return JpegTranWriteResult.Failed($"JPEG rotation failed: {ex.Message}");
        }
        finally
        {
            TryDeleteFile(outputPath);
            TryDeleteFile(backupPath);
        }
    }

    internal static IReadOnlyList<string> BuildRotateArguments(
        LosslessJpegRotation rotation,
        string outputPath,
        string sourcePath,
        bool trimIncompleteEdgeBlocks = false)
    {
        var args = new List<string>
        {
            "-copy",
            "icc"
        };

        if (trimIncompleteEdgeBlocks)
            args.Add("-trim");

        args.AddRange(
        [
            "-rotate",
            ((int)rotation).ToString(CultureInfo.InvariantCulture),
            "-outfile",
            outputPath,
            sourcePath
        ]);

        return args;
    }

    private static JpegTranProcessResult RunProcess(
        string executablePath,
        IReadOnlyList<string> arguments,
        int timeoutMilliseconds)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        using var process = Process.Start(psi);
        if (process is null)
            throw new InvalidOperationException("jpegtran did not start.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return new JpegTranProcessResult(
                ExitCode: -1,
                StandardOutput: ReadCompletedOutput(stdoutTask),
                StandardError: ReadCompletedOutput(stderrTask),
                TimedOut: true);
        }

        return new JpegTranProcessResult(
            process.ExitCode,
            stdoutTask.GetAwaiter().GetResult(),
            stderrTask.GetAwaiter().GetResult());
    }

    private static void ReplaceAtomically(string outputPath, string targetPath, string backupPath)
    {
        var replaced = false;
        try
        {
            if (File.Exists(targetPath))
            {
                File.Replace(outputPath, targetPath, backupPath, ignoreMetadataErrors: true);
                replaced = true;
            }
            else
            {
                File.Move(outputPath, targetPath);
            }

            File.SetLastWriteTimeUtc(targetPath, DateTime.UtcNow);
        }
        catch
        {
            if (replaced && File.Exists(backupPath))
                TryRestoreBackup(backupPath, targetPath);
            throw;
        }
    }

    private static bool ValidateJpegOutput(string outputPath, int expectedWidth, int expectedHeight, out string error)
    {
        error = "";
        var file = new FileInfo(outputPath);
        if (!file.Exists)
        {
            error = "jpegtran did not create an output file.";
            return false;
        }

        if (file.Length == 0)
        {
            error = "jpegtran created an empty output file.";
            return false;
        }

        try
        {
            using var stream = File.OpenRead(outputPath);
            Span<byte> header = stackalloc byte[3];
            if (stream.Read(header) < 3 || header[0] != 0xFF || header[1] != 0xD8 || header[2] != 0xFF)
            {
                error = "jpegtran output is not a JPEG file.";
                return false;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            error = ex.Message;
            return false;
        }

        try
        {
            using var image = new MagickImage(outputPath);
            if (image.Width != expectedWidth || image.Height != expectedHeight)
            {
                error = string.Create(
                    CultureInfo.InvariantCulture,
                    $"jpegtran output size was {image.Width}x{image.Height}, expected {expectedWidth}x{expectedHeight}.");
                return false;
            }
        }
        catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            error = $"jpegtran output could not be decoded: {ex.Message}";
            return false;
        }

        return true;
    }

    private static (int Width, int Height) ExpectedRotatedSize(
        int imageWidth,
        int imageHeight,
        LosslessJpegRotation rotation)
        => rotation is LosslessJpegRotation.Rotate90 or LosslessJpegRotation.Rotate270
            ? (imageHeight, imageWidth)
            : (imageWidth, imageHeight);

    private static string FirstNonEmptyLine(params string[] values)
        => values
            .SelectMany(value => (value ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0) ?? "";

    private static string ReadCompletedOutput(Task<string> outputTask)
        => outputTask.IsCompletedSuccessfully ? outputTask.Result : "";

    private static void TryRestoreBackup(string backupPath, string targetPath)
    {
        try
        {
            File.Copy(backupPath, targetPath, overwrite: true);
        }
        catch
        {
            // If rollback fails, surface the original replace failure to the caller.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup; rollback/temp files are intentionally hidden and unique.
        }
    }
}
