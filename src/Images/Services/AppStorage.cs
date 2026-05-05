using System.IO;

namespace Images.Services;

/// <summary>
/// Best-effort app-local storage roots. Persistent caches should prefer LocalAppData, then
/// degrade to Temp instead of failing static initialization before the viewer can open.
/// </summary>
public static class AppStorage
{
    public static string? TryGetAppDirectory(params string[] relativeSegments)
        => TryCreateUnder(GetCandidateRoot(), relativeSegments);

    internal static string? TryGetAppDirectoryForRoots(
        IEnumerable<string> candidateRoots,
        params string[] relativeSegments)
    {
        ArgumentNullException.ThrowIfNull(candidateRoots);
        return TryCreateUnder(candidateRoots, relativeSegments);
    }

    private static string? TryCreateUnder(IEnumerable<string> roots, IReadOnlyList<string> relativeSegments)
    {
        if (!AreSafeRelativeSegments(relativeSegments))
            return null;

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;

            try
            {
                var parts = new string[relativeSegments.Count + 2];
                parts[0] = root;
                parts[1] = "Images";
                for (var i = 0; i < relativeSegments.Count; i++)
                    parts[i + 2] = relativeSegments[i];

                var path = Path.Combine(parts);
                Directory.CreateDirectory(path);
                return path;
            }
            catch
            {
                // Try the next writable root. Callers decide whether null disables a feature.
            }
        }

        return null;
    }

    private static bool AreSafeRelativeSegments(IReadOnlyList<string> relativeSegments)
    {
        foreach (var segment in relativeSegments)
        {
            if (string.IsNullOrWhiteSpace(segment)) return false;
            if (Path.IsPathRooted(segment)) return false;
            if (segment.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return false;
            if (segment.Contains(Path.DirectorySeparatorChar) || segment.Contains(Path.AltDirectorySeparatorChar)) return false;
            if (segment is "." or "..") return false;
        }

        return true;
    }

    private static IEnumerable<string> GetCandidateRoot()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.GetTempPath();
    }
}
