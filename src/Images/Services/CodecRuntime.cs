using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using ImageMagick;

namespace Images.Services;

/// <summary>
/// Configures optional decoder/runtime components that are not part of WIC or Magick.NET itself.
/// Ghostscript may be shipped beside the app when licensing permits, or discovered from a system
/// install as a convenience for local/developer machines.
/// </summary>
public static class CodecRuntime
{
    private static readonly object Sync = new();
    private static bool _configured;
    private static CodecRuntimeStatus _status = CodecRuntimeStatus.Unconfigured;

    public static CodecRuntimeStatus Status
    {
        get
        {
            Configure();
            return _status;
        }
    }

    public static CodecRuntimeStatus Configure()
    {
        lock (Sync)
        {
            if (_configured) return _status;

            string? ghostscriptDirectory = null;
            string ghostscriptSource = "Not found";

            foreach (var candidate in GhostscriptCandidates())
            {
                var resolved = ResolveGhostscriptDirectory(candidate.Path);
                if (resolved is null) continue;

                ghostscriptDirectory = resolved;
                ghostscriptSource = candidate.Source;
                break;
            }

            if (ghostscriptDirectory is not null)
            {
                MagickNET.SetGhostscriptDirectory(ghostscriptDirectory);
            }

            _status = new CodecRuntimeStatus(
                GhostscriptAvailable: ghostscriptDirectory is not null,
                GhostscriptDirectory: ghostscriptDirectory,
                GhostscriptSource: ghostscriptSource,
                MagickStatus: $"Magick.NET {GetMagickAssemblyVersion()} configured",
                DocumentStatus: ghostscriptDirectory is null
                    ? "EPS/PDF/PS/AI previews need bundled or installed Ghostscript"
                    : $"EPS/PDF/PS/AI previews enabled via {ghostscriptSource}");

            _configured = true;
            return _status;
        }
    }

    /// <summary>
    /// Returns the assembly-informational version of Magick.NET, falling back to the file
    /// version on failure. Used by both runtime status and CLI/About reports so the version
    /// shown to users always matches what's actually loaded into the process.
    /// </summary>
    public static string GetMagickAssemblyVersion()
    {
        try
        {
            var asm = typeof(MagickNET).Assembly;
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info)) return info;
            return asm.GetName().Version?.ToString() ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Returns the on-disk path of the loaded Magick.NET managed assembly, when discoverable.
    /// Used in provenance reports (About + <c>--system-info</c>) so users can verify they
    /// are running an app-local copy and not something shimmed in from elsewhere.
    /// </summary>
    public static string? GetMagickAssemblyPath()
    {
        try
        {
            var loc = typeof(MagickNET).Assembly.Location;
            return string.IsNullOrEmpty(loc) ? null : loc;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the path to the Ghostscript shared library actually configured for Magick.NET,
    /// or <c>null</c> when Ghostscript is not available. The path is also where the SHA-256
    /// fingerprint in <see cref="GetGhostscriptDllSha256"/> is computed from.
    /// </summary>
    public static string? GetGhostscriptDllPath()
    {
        var status = Status;
        if (status.GhostscriptDirectory is null) return null;

        var dll64 = Path.Combine(status.GhostscriptDirectory, "gsdll64.dll");
        if (File.Exists(dll64)) return dll64;
        var dll32 = Path.Combine(status.GhostscriptDirectory, "gsdll32.dll");
        if (File.Exists(dll32)) return dll32;
        return null;
    }

    /// <summary>
    /// Hex-encoded SHA-256 of the configured Ghostscript shared library, or <c>null</c> when
    /// the library is missing/unreadable. Provenance: lets the user (or release maintainer)
    /// verify the bundled DLL matches the approved redistributable that was reviewed.
    /// </summary>
    public static string? GetGhostscriptDllSha256()
    {
        var path = GetGhostscriptDllPath();
        if (path is null) return null;

        try
        {
            using var stream = File.OpenRead(path);
            var hash = SHA256.HashData(stream);
            return Convert.ToHexStringLower(hash);
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<(string Path, string Source)> GhostscriptCandidates()
    {
        var baseDir = AppContext.BaseDirectory;

        var env = Environment.GetEnvironmentVariable("IMAGES_GHOSTSCRIPT_DIR");
        if (!string.IsNullOrWhiteSpace(env))
            yield return (env, "IMAGES_GHOSTSCRIPT_DIR");

        yield return (Path.Combine(baseDir, "codecs", "ghostscript"), "bundled Ghostscript");
        yield return (Path.Combine(baseDir, "Codecs", "Ghostscript"), "bundled Ghostscript");
        yield return (Path.Combine(baseDir, "ghostscript"), "bundled Ghostscript");
        yield return (Path.Combine(baseDir, "gs"), "bundled Ghostscript");

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        foreach (var dir in EnumerateGhostscriptInstalls(programFiles))
            yield return (dir, "installed Ghostscript");

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        foreach (var dir in EnumerateGhostscriptInstalls(programFilesX86))
            yield return (dir, "installed Ghostscript");
    }

    private static IEnumerable<string> EnumerateGhostscriptInstalls(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) yield break;

        var gsRoot = Path.Combine(root, "gs");
        if (!Directory.Exists(gsRoot)) yield break;

        IEnumerable<string> dirs;
        try
        {
            dirs = Directory.EnumerateDirectories(gsRoot, "gs*", SearchOption.TopDirectoryOnly)
                .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            yield break;
        }

        foreach (var dir in dirs)
        {
            yield return dir;
            yield return Path.Combine(dir, "bin");
        }
    }

    private static string? ResolveGhostscriptDirectory(string directory)
    {
        if (HasGhostscriptBinaries(directory)) return directory;

        var bin = Path.Combine(directory, "bin");
        if (HasGhostscriptBinaries(bin)) return bin;

        return null;
    }

    private static bool HasGhostscriptBinaries(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return false;

        var hasDll =
            File.Exists(Path.Combine(directory, "gsdll64.dll")) ||
            File.Exists(Path.Combine(directory, "gsdll32.dll"));

        return hasDll;
    }

    public static string? GetGhostscriptVersion()
    {
        var status = Status;
        if (status.GhostscriptDirectory is null) return null;

        var exe = File.Exists(Path.Combine(status.GhostscriptDirectory, "gswin64c.exe"))
            ? Path.Combine(status.GhostscriptDirectory, "gswin64c.exe")
            : Path.Combine(status.GhostscriptDirectory, "gswin32c.exe");

        if (!File.Exists(exe)) return null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("--version");

            using var process = Process.Start(psi);
            if (process is null) return null;
            if (!process.WaitForExit(1500))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return null;
            }

            var version = process.StandardOutput.ReadToEnd().Trim();
            return string.IsNullOrWhiteSpace(version) ? null : version;
        }
        catch
        {
            return null;
        }
    }
}

public sealed record CodecRuntimeStatus(
    bool GhostscriptAvailable,
    string? GhostscriptDirectory,
    string GhostscriptSource,
    string MagickStatus,
    string DocumentStatus)
{
    public static CodecRuntimeStatus Unconfigured { get; } = new(
        GhostscriptAvailable: false,
        GhostscriptDirectory: null,
        GhostscriptSource: "Not checked",
        MagickStatus: "Magick.NET not configured",
        DocumentStatus: "Document preview support not checked");
}
