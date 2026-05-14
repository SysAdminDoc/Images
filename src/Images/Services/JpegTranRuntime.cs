using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace Images.Services;

/// <summary>
/// Resolves the optional libjpeg-turbo jpegtran.exe sidecar used by future lossless
/// JPEG writeback paths. This class intentionally does not search PATH or download
/// anything; only an explicit developer override or an app-local runtime is allowed.
/// </summary>
public static class JpegTranRuntime
{
    public const string EnvironmentVariable = "IMAGES_JPEGTRAN_EXE";

    private const int VersionTimeoutMilliseconds = 1500;

    public static JpegTranRuntimeStatus Inspect()
        => Inspect(AppContext.BaseDirectory, Environment.GetEnvironmentVariable(EnvironmentVariable));

    internal static JpegTranRuntimeStatus Inspect(
        string baseDirectory,
        string? environmentExecutablePath,
        Func<string, string?>? versionReader = null)
    {
        var location = ResolveExecutable(baseDirectory, environmentExecutablePath);
        if (location is null)
        {
            if (!string.IsNullOrWhiteSpace(environmentExecutablePath))
            {
                return JpegTranRuntimeStatus.Missing(
                    $"{EnvironmentVariable} points to a missing jpegtran.exe: {NormalizePath(environmentExecutablePath)}");
            }

            return JpegTranRuntimeStatus.Missing(
                $"jpegtran not found; place an approved libjpeg-turbo jpegtran.exe under Codecs\\JpegTran or set {EnvironmentVariable}");
        }

        var sha256 = GetSha256(location.ExecutablePath);
        var version = (versionReader ?? TryReadVersion)(location.ExecutablePath);
        return new JpegTranRuntimeStatus(
            Available: true,
            ExecutablePath: location.ExecutablePath,
            Source: location.Source,
            Version: version,
            Sha256: sha256,
            StatusText: $"jpegtran available via {location.Source}");
    }

    internal static JpegTranRuntimeLocation? ResolveExecutable(string baseDirectory, string? environmentExecutablePath)
    {
        if (!string.IsNullOrWhiteSpace(environmentExecutablePath))
        {
            var overridePath = NormalizePath(environmentExecutablePath);
            return File.Exists(overridePath)
                ? new JpegTranRuntimeLocation(overridePath, EnvironmentVariable)
                : null;
        }

        foreach (var candidate in AppLocalCandidates(baseDirectory))
        {
            if (File.Exists(candidate))
                return new JpegTranRuntimeLocation(candidate, "app-local Codecs\\JpegTran");
        }

        return null;
    }

    public static string? GetSha256(string executablePath)
    {
        try
        {
            using var stream = File.OpenRead(executablePath);
            return Convert.ToHexStringLower(SHA256.HashData(stream));
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> AppLocalCandidates(string baseDirectory)
    {
        var root = string.IsNullOrWhiteSpace(baseDirectory)
            ? AppContext.BaseDirectory
            : baseDirectory;

        yield return Path.Combine(root, "Codecs", "JpegTran", "jpegtran.exe");
        yield return Path.Combine(root, "codecs", "jpegtran", "jpegtran.exe");
        yield return Path.Combine(root, "JpegTran", "jpegtran.exe");
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path.Trim().Trim('"'));
        }
        catch
        {
            return path.Trim().Trim('"');
        }
    }

    private static string? TryReadVersion(string executablePath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = executablePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-version");

            using var process = Process.Start(psi);
            if (process is null) return null;
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(VersionTimeoutMilliseconds))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return null;
            }

            var output = (stdoutTask.GetAwaiter().GetResult() + Environment.NewLine + stderrTask.GetAwaiter().GetResult()).Trim();
            if (string.IsNullOrWhiteSpace(output)) return null;

            var firstLine = output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.Length > 0);
            return firstLine;
        }
        catch
        {
            return null;
        }
    }
}

internal sealed record JpegTranRuntimeLocation(string ExecutablePath, string Source);

public sealed record JpegTranRuntimeStatus(
    bool Available,
    string? ExecutablePath,
    string Source,
    string? Version,
    string? Sha256,
    string StatusText)
{
    public static JpegTranRuntimeStatus Missing(string statusText) => new(
        Available: false,
        ExecutablePath: null,
        Source: "Not found",
        Version: null,
        Sha256: null,
        StatusText: statusText);
}
