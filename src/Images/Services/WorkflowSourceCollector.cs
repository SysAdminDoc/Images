using System.IO;
using System.Security;

namespace Images.Services;

public sealed record WorkflowSourceCollection(
    IReadOnlyList<string> Files,
    IReadOnlyList<string> SkippedSources);

/// <summary>
/// Resolves files and folders selected for batch workflows without following nested junctions
/// or symbolic links into unrelated trees.
/// </summary>
public static class WorkflowSourceCollector
{
    private static readonly EnumerationOptions RecursiveOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    public static WorkflowSourceCollection Collect(IEnumerable<string> sourceRows)
    {
        ArgumentNullException.ThrowIfNull(sourceRows);

        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skipped = new List<string>();
        foreach (var source in sourceRows.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            try
            {
                if (File.Exists(source))
                {
                    AddIfSupported(files, source);
                    continue;
                }

                if (!Directory.Exists(source))
                {
                    skipped.Add(source);
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(source, "*", RecursiveOptions))
                    AddIfSupported(files, file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
            {
                skipped.Add(source);
            }
        }

        return new WorkflowSourceCollection(
            files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray(),
            skipped.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static void AddIfSupported(ISet<string> files, string path)
    {
        if (SupportedImageFormats.IsSupported(path))
            files.Add(Path.GetFullPath(path));
    }
}
