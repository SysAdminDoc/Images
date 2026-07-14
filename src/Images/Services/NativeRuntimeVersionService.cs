using System.Text.RegularExpressions;
using ImageMagick;

namespace Images.Services;

public sealed record NativeRuntimeVersions(
    string MagickNetVersion,
    string ImageMagickVersion,
    string SqliteVersion)
{
    public bool ImageMagickSupported => NativeRuntimeVersionService.IsImageMagickVersionSupported(ImageMagickVersion);
    public bool SqliteSupported => SqliteConnectionPolicy.IsRuntimeVersionSupported(SqliteVersion);
    public bool AllSupported => ImageMagickSupported && SqliteSupported;
}

/// <summary>
/// Probes the native versions that actually loaded in this process. Package pins alone are not
/// sufficient because both Magick.NET and Microsoft.Data.Sqlite carry native runtime payloads.
/// </summary>
public static partial class NativeRuntimeVersionService
{
    public const string MinimumImageMagickVersion = "7.1.2-2";
    private static readonly Version MinimumImageMagickComparableVersion = new(7, 1, 2, 2);
    private static readonly Lazy<NativeRuntimeVersions> CurrentVersions = new(Probe);

    public static NativeRuntimeVersions Current => CurrentVersions.Value;

    public static bool IsImageMagickVersionSupported(string? value)
        => TryParseImageMagickVersion(value, out var parsed) && parsed >= MinimumImageMagickComparableVersion;

    internal static bool TryParseImageMagickVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var match = ImageMagickVersionPattern().Match(value);
        if (!match.Success ||
            !int.TryParse(match.Groups[1].Value, out var major) ||
            !int.TryParse(match.Groups[2].Value, out var minor) ||
            !int.TryParse(match.Groups[3].Value, out var patch))
        {
            return false;
        }

        var release = match.Groups[4].Success && int.TryParse(match.Groups[4].Value, out var parsedRelease)
            ? parsedRelease
            : 0;
        version = new Version(major, minor, patch, release);
        return true;
    }

    internal static IReadOnlyList<string> BuildStartupWarnings(NativeRuntimeVersions versions)
    {
        ArgumentNullException.ThrowIfNull(versions);
        var warnings = new List<string>(2);
        if (!versions.ImageMagickSupported)
        {
            warnings.Add(
                $"Native ImageMagick {versions.ImageMagickVersion} is below or could not be compared with the reviewed {MinimumImageMagickVersion} security floor.");
        }

        if (!versions.SqliteSupported)
        {
            warnings.Add(
                $"Native SQLite {versions.SqliteVersion} is below or could not be compared with the reviewed {SqliteConnectionPolicy.MinimumRuntimeVersion} security floor.");
        }

        return warnings;
    }

    private static NativeRuntimeVersions Probe()
    {
        var magickNetVersion = TryProbe(() => MagickNET.Version);
        var imageMagickVersion = TryProbe(() => MagickNET.ImageMagickVersion);
        var sqliteVersion = TryProbe(() =>
        {
            using var connection = SqliteConnectionPolicy.Open("Data Source=:memory:");
            return SqliteConnectionPolicy.GetRuntimeVersion(connection);
        });

        return new NativeRuntimeVersions(magickNetVersion, imageMagickVersion, sqliteVersion);
    }

    private static string TryProbe(Func<string> probe)
    {
        try
        {
            var value = probe();
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        }
        catch
        {
            return "unknown";
        }
    }

    [GeneratedRegex(@"(?<!\d)(\d+)\.(\d+)\.(\d+)(?:-(\d+))?", RegexOptions.CultureInvariant)]
    private static partial Regex ImageMagickVersionPattern();
}
