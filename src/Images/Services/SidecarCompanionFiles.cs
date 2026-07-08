using System.IO;
using System.Security;

namespace Images.Services;

public static class SidecarCompanionFiles
{
    public static IReadOnlyList<RecoverySidecarMove> TryMoveAlongside(string oldImagePath, string newImagePath)
    {
        var moved = new List<RecoverySidecarMove>();
        foreach (var plan in EnumerateMovePlans(oldImagePath, newImagePath))
        {
            try
            {
                if (!File.Exists(plan.SourcePath))
                    continue;

                if (SamePathOrdinal(plan.SourcePath, plan.DestinationPath))
                    continue;

                if (DestinationExistsForDifferentPath(plan.SourcePath, plan.DestinationPath))
                    continue;

                var folder = Path.GetDirectoryName(plan.DestinationPath);
                if (string.IsNullOrWhiteSpace(folder))
                    continue;

                Directory.CreateDirectory(folder);
                File.Move(plan.SourcePath, plan.DestinationPath, overwrite: false);
                moved.Add(new RecoverySidecarMove(plan.SourcePath, plan.DestinationPath));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
            {
                // Sidecars are metadata companions. Primary file operations have
                // already succeeded, so sidecar carry is strictly best-effort.
            }
        }

        return moved;
    }

    public static bool WouldOverwriteDestination(string oldImagePath, string newImagePath)
    {
        foreach (var plan in EnumerateMovePlans(oldImagePath, newImagePath))
        {
            try
            {
                if (File.Exists(plan.SourcePath) &&
                    DestinationExistsForDifferentPath(plan.SourcePath, plan.DestinationPath))
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
            {
                continue;
            }
        }

        return false;
    }

    private static IReadOnlyList<SidecarMovePlan> EnumerateMovePlans(string oldImagePath, string newImagePath)
    {
        var plans = new List<SidecarMovePlan>();
        var seenSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Add(oldImagePath + ".xmp", newImagePath + ".xmp");

        var oldFolder = Path.GetDirectoryName(oldImagePath);
        var oldStem = Path.GetFileNameWithoutExtension(oldImagePath);
        var newFolder = Path.GetDirectoryName(newImagePath);
        var newStem = Path.GetFileNameWithoutExtension(newImagePath);
        if (!string.IsNullOrWhiteSpace(oldFolder) &&
            !string.IsNullOrWhiteSpace(oldStem) &&
            !string.IsNullOrWhiteSpace(newFolder) &&
            !string.IsNullOrWhiteSpace(newStem))
        {
            Add(
                Path.Combine(oldFolder, oldStem + ".xmp"),
                Path.Combine(newFolder, newStem + ".xmp"));
        }

        return plans;

        void Add(string sourcePath, string destinationPath)
        {
            var normalizedSource = NormalizePath(sourcePath);
            if (!seenSources.Add(normalizedSource))
                return;

            plans.Add(new SidecarMovePlan(normalizedSource, NormalizePath(destinationPath)));
        }
    }

    private static bool DestinationExistsForDifferentPath(string sourcePath, string destinationPath)
    {
        if (!File.Exists(destinationPath) && !Directory.Exists(destinationPath))
            return false;

        return !SamePathIgnoreCase(sourcePath, destinationPath);
    }

    private static bool SamePathIgnoreCase(string left, string right)
        => string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);

    private static bool SamePathOrdinal(string left, string right)
        => string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.Ordinal);

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
        {
            return path;
        }
    }

    private sealed record SidecarMovePlan(string SourcePath, string DestinationPath);
}
