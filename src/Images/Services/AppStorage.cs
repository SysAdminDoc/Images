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

    private static string? TryCreateUnder(IEnumerable<string> roots, IReadOnlyList<string> relativeSegments)
    {
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

    private static IEnumerable<string> GetCandidateRoot()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.GetTempPath();
    }
}
