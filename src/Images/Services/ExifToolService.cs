using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Images.Services;

internal delegate ExifToolProcessResult ExifToolProcessRunner(ProcessStartInfo startInfo, int timeoutMilliseconds);

internal sealed record ExifToolProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut = false,
    bool OutputLimitExceeded = false);

internal sealed record ExifToolRunResult(
    bool Succeeded,
    string Message,
    string StandardOutput,
    string StandardError);

/// <summary>
/// Safe process boundary for future ExifTool metadata writes. Arguments and target paths are
/// passed through a UTF-8 argfile so untrusted filenames never flow through shell quoting.
/// </summary>
internal static class ExifToolService
{
    public const int DefaultTimeoutMilliseconds = 30000;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly ILogger _log = Log.Get(nameof(ExifToolService));

    public static ExifToolRunResult Run(
        string executablePath,
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> targetPaths,
        string? tempDirectory = null,
        ExifToolProcessRunner? processRunner = null,
        int timeoutMilliseconds = DefaultTimeoutMilliseconds)
    {
        if (!TryNormalizeExecutable(executablePath, out var normalizedExecutable, out var executableError))
            return Failed(executableError);

        if (arguments.Count == 0)
            return Failed("ExifTool arguments are required.");

        if (targetPaths.Count == 0)
            return Failed("ExifTool target paths are required.");

        var lines = new List<string>(arguments.Count + targetPaths.Count + 2);

        // Windows ExifTool interprets filename arguments in the system code page
        // unless told otherwise; without this a non-ANSI target path (CJK,
        // Cyrillic, emoji) resolves to "file not found" or the wrong file.
        lines.Add("-charset");
        lines.Add("filename=UTF8");

        foreach (var argument in arguments)
        {
            if (!TryValidateArgfileLine(argument, "argument", out var error))
                return Failed(error);
            lines.Add(argument);
        }

        foreach (var path in targetPaths)
        {
            if (!TryNormalizeTargetPath(path, out var normalizedPath, out var error))
                return Failed(error);
            lines.Add(normalizedPath);
        }

        var argFile = CreateArgFilePath(tempDirectory);
        try
        {
            File.WriteAllLines(argFile, lines, Utf8NoBom);
            var startInfo = BuildStartInfo(normalizedExecutable, argFile);
            var result = (processRunner ?? RunProcess)(startInfo, timeoutMilliseconds);

            if (result.TimedOut)
                return Failed("ExifTool timed out.", result);

            if (result.OutputLimitExceeded)
                return Failed("ExifTool output exceeded the 4 MiB safety limit.", result);

            if (result.ExitCode != 0)
            {
                var detail = FirstNonEmptyLine(result.StandardError, result.StandardOutput);
                return Failed(
                    string.IsNullOrWhiteSpace(detail)
                        ? $"ExifTool exited with code {result.ExitCode}."
                        : $"ExifTool exited with code {result.ExitCode}: {detail}",
                    result);
            }

            return new ExifToolRunResult(true, "ExifTool completed.", result.StandardOutput, result.StandardError);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException or NotSupportedException)
        {
            return Failed($"ExifTool failed: {ex.Message}");
        }
        finally
        {
            TryDelete(argFile);
        }
    }

    internal static ProcessStartInfo BuildStartInfo(string executablePath, string argFilePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("-@");
        startInfo.ArgumentList.Add(argFilePath);
        return startInfo;
    }

    internal static ExifToolProcessResult RunProcess(ProcessStartInfo startInfo, int timeoutMilliseconds)
    {
        var result = BoundedProcessRunner.Run(
            startInfo,
            timeoutMilliseconds,
            BoundedProcessRunner.OperationOutputLimitBytes,
            BoundedProcessRunner.OperationOutputLimitBytes);
        return new ExifToolProcessResult(
            result.ExitCode ?? -1,
            result.StandardOutput,
            result.StandardError,
            TimedOut: result.TimedOut,
            OutputLimitExceeded: result.OutputLimitExceeded);
    }

    private static bool TryNormalizeExecutable(string path, out string normalizedPath, out string error)
    {
        normalizedPath = "";
        error = "";
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "ExifTool executable path is required.";
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(path.Trim().Trim('"'));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"ExifTool executable path is invalid: {ex.Message}";
            return false;
        }

        if (!File.Exists(normalizedPath))
        {
            error = $"ExifTool executable not found: {normalizedPath}";
            return false;
        }

        var fileName = Path.GetFileNameWithoutExtension(normalizedPath);
        if (!fileName.Equals("exiftool", StringComparison.OrdinalIgnoreCase))
        {
            error = $"ExifTool executable must be named exiftool (got '{Path.GetFileName(normalizedPath)}').";
            return false;
        }

        var ext = Path.GetExtension(normalizedPath);
        if (!string.IsNullOrEmpty(ext)
            && !ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            && !ext.Equals(".pl", StringComparison.OrdinalIgnoreCase))
        {
            error = $"ExifTool executable has unexpected extension '{ext}'.";
            return false;
        }

        return true;
    }

    private static bool TryNormalizeTargetPath(string path, out string normalizedPath, out string error)
    {
        normalizedPath = "";
        error = "";
        if (!TryValidateArgfileLine(path, "target path", out error))
            return false;

        if (path.IndexOfAny(['<', '>', '|']) >= 0)
        {
            error = "Unsafe ExifTool target path contains a shell metacharacter.";
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"ExifTool target path is invalid: {ex.Message}";
            return false;
        }

        if (!File.Exists(normalizedPath))
        {
            error = $"ExifTool target path not found: {normalizedPath}";
            return false;
        }

        return true;
    }

    private static bool TryValidateArgfileLine(string value, string label, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(value))
        {
            error = $"ExifTool {label} is required.";
            return false;
        }

        if (value.Contains('\0') || value.Contains('\r') || value.Contains('\n'))
        {
            error = $"Unsafe ExifTool {label} contains a line break or null character.";
            return false;
        }

        return true;
    }

    private static string CreateArgFilePath(string? tempDirectory)
    {
        var root = string.IsNullOrWhiteSpace(tempDirectory) ? Path.GetTempPath() : tempDirectory;
        Directory.CreateDirectory(root);
        return Path.Combine(root, $".images-exiftool-{Guid.NewGuid():N}.args.txt");
    }

    private static ExifToolRunResult Failed(string message, ExifToolProcessResult? result = null)
        => new(false, message, result?.StandardOutput ?? "", result?.StandardError ?? "");

    private static string FirstNonEmptyLine(params string[] values)
        => values
            .SelectMany(value => (value ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0) ?? "";

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _log.LogDebug(ex, "Could not delete temporary ExifTool argfile {Path}", path);
        }
    }
}
