using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public static class C2paToolRuntime
{
    public const string EnvironmentVariable = "IMAGES_C2PATOOL_EXE";

    private const int VersionTimeoutMilliseconds = 3000;
    private static readonly ILogger _log = Log.Get(nameof(C2paToolRuntime));

    public static C2paToolRuntimeStatus Inspect()
        => Inspect(AppContext.BaseDirectory, Environment.GetEnvironmentVariable(EnvironmentVariable));

    internal static C2paToolRuntimeStatus Inspect(
        string baseDirectory,
        string? environmentExecutablePath,
        Func<string, string?>? versionReader = null)
    {
        var location = ResolveExecutable(baseDirectory, environmentExecutablePath);
        if (location is null)
        {
            if (!string.IsNullOrWhiteSpace(environmentExecutablePath))
            {
                return C2paToolRuntimeStatus.Missing(
                    $"{EnvironmentVariable} points to a missing c2patool: {NormalizePath(environmentExecutablePath)}");
            }

            return C2paToolRuntimeStatus.Missing(
                $"c2patool not found; install from https://github.com/contentauth/c2patool, " +
                $"place under Codecs\\C2paTool, or set {EnvironmentVariable}");
        }

        var sha256 = GetSha256(location.ExecutablePath);
        var version = (versionReader ?? TryReadVersion)(location.ExecutablePath);
        return new C2paToolRuntimeStatus(
            Available: true,
            ExecutablePath: location.ExecutablePath,
            Source: location.Source,
            Version: version,
            Sha256: sha256,
            StatusText: $"c2patool available via {location.Source}");
    }

    internal static C2paToolRuntimeLocation? ResolveExecutable(
        string baseDirectory,
        string? environmentExecutablePath)
    {
        if (!string.IsNullOrWhiteSpace(environmentExecutablePath))
        {
            var overridePath = NormalizePath(environmentExecutablePath);
            return File.Exists(overridePath)
                ? new C2paToolRuntimeLocation(overridePath, EnvironmentVariable)
                : null;
        }

        foreach (var candidate in AppLocalCandidates(baseDirectory))
        {
            if (File.Exists(candidate))
                return new C2paToolRuntimeLocation(candidate, "app-local Codecs\\C2paTool");
        }

        var pathExe = FindOnPath("c2patool.exe") ?? FindOnPath("c2patool");
        if (pathExe is not null)
            return new C2paToolRuntimeLocation(pathExe, "system PATH");

        return null;
    }

    private static string? FindOnPath(string executableName)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVar)) return null;

        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), executableName);
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                _log.LogWarning(ex, "Could not inspect PATH entry for c2patool: {Directory}", dir);
            }
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or NotSupportedException)
        {
            _log.LogWarning(ex, "Could not compute SHA-256 for c2patool runtime {Path}", executablePath);
            return null;
        }
    }

    private static IEnumerable<string> AppLocalCandidates(string baseDirectory)
    {
        var root = string.IsNullOrWhiteSpace(baseDirectory)
            ? AppContext.BaseDirectory
            : baseDirectory;

        yield return Path.Combine(root, "Codecs", "C2paTool", "c2patool.exe");
        yield return Path.Combine(root, "codecs", "c2patool", "c2patool.exe");
        yield return Path.Combine(root, "C2paTool", "c2patool.exe");
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path.Trim().Trim('"'));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            _log.LogWarning(ex, "Could not normalize c2patool path {Path}", path);
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
            psi.ArgumentList.Add("--version");

            using var process = Process.Start(psi);
            if (process is null)
            {
                _log.LogWarning("Could not start c2patool to read version: {Path}", executablePath);
                return null;
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(VersionTimeoutMilliseconds))
            {
                _log.LogWarning("c2patool version probe timed out: {Path}", executablePath);
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Could not kill timed-out c2patool version probe: {Path}", executablePath);
                }

                return null;
            }

            var output = (stdoutTask.GetAwaiter().GetResult() + Environment.NewLine +
                          stderrTask.GetAwaiter().GetResult()).Trim();
            if (string.IsNullOrWhiteSpace(output)) return null;

            var firstLine = output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.Length > 0);
            return firstLine;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not read c2patool version: {Path}", executablePath);
            return null;
        }
    }
}

internal sealed record C2paToolRuntimeLocation(string ExecutablePath, string Source);

public sealed record C2paToolRuntimeStatus(
    bool Available,
    string? ExecutablePath,
    string Source,
    string? Version,
    string? Sha256,
    string StatusText)
{
    public static C2paToolRuntimeStatus Missing(string statusText) => new(
        Available: false,
        ExecutablePath: null,
        Source: "Not found",
        Version: null,
        Sha256: null,
        StatusText: statusText);
}
