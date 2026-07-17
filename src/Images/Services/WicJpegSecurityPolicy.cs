using System.Diagnostics;
using System.IO;

namespace Images.Services;

internal sealed record WicJpegSecurityStatus(
    Version? FileVersion,
    bool IsPatched,
    string? Error = null);

/// <summary>
/// Guards WIC JPEG decode against CVE-2025-50165. Microsoft serviced the supported Windows
/// client branches in the August 2025 cumulative updates; unknown branches fail closed to the
/// existing Magick.NET fallback rather than guessing that an older codec is safe.
/// </summary>
internal static class WicJpegSecurityPolicy
{
    private static readonly IReadOnlyDictionary<int, int> MinimumPatchedRevisions =
        new Dictionary<int, int>
        {
            [10240] = 21100,
            [14393] = 8330,
            [17763] = 7678,
            [19041] = 6216,
            [19044] = 6216,
            [19045] = 6216,
            [22621] = 5768,
            [22631] = 5768,
            [26100] = 4946,
        };

    private static readonly HashSet<string> JpegExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jpe", ".jfif", ".jif"
    };

    private static readonly Lazy<WicJpegSecurityStatus> CurrentStatus = new(Inspect);

    internal static WicJpegSecurityStatus Current => CurrentStatus.Value;

    internal static bool ShouldBypassWic(string pathOrExtension, WicJpegSecurityStatus? status = null)
    {
        var extension = pathOrExtension.StartsWith(".", StringComparison.Ordinal)
            ? pathOrExtension
            : Path.GetExtension(pathOrExtension);
        return JpegExtensions.Contains(extension) && !(status ?? Current).IsPatched;
    }

    internal static WicJpegSecurityStatus Evaluate(Version? fileVersion, string? error = null)
        => new(fileVersion, IsPatched(fileVersion), error);

    internal static bool IsPatched(Version? fileVersion)
    {
        if (fileVersion is null)
            return false;

        if (fileVersion.Major > 10)
            return true;
        if (fileVersion.Major < 10 || fileVersion.Minor != 0)
            return false;

        // Later Windows client branches were released after the August 2025 servicing floor.
        if (fileVersion.Build > 26100)
            return true;

        return fileVersion.Revision >= 0 &&
               MinimumPatchedRevisions.TryGetValue(fileVersion.Build, out var minimumRevision) &&
               fileVersion.Revision >= minimumRevision;
    }

    private static WicJpegSecurityStatus Inspect()
    {
        try
        {
            var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (string.IsNullOrWhiteSpace(windowsDirectory))
                return Evaluate(null, "Windows directory is unavailable.");

            var path = Path.Combine(windowsDirectory, "System32", "WindowsCodecs.dll");
            var info = FileVersionInfo.GetVersionInfo(path);
            var version = new Version(
                info.FileMajorPart,
                info.FileMinorPart,
                info.FileBuildPart,
                info.FilePrivatePart);
            return Evaluate(version);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Evaluate(null, ex.Message);
        }
    }
}
