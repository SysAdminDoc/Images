using System.Globalization;
using System.IO;
using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageMagick;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public sealed record MacroActionPlan(
    string Name,
    IReadOnlyList<MacroActionStep> Actions)
{
    public static MacroActionPlan Default { get; } = new(
        "Images macro",
        [
            new MacroActionStep(
                "export-copy",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["extension"] = ".png",
                    ["quality"] = "92"
                })
        ]);
}

public sealed record MacroActionStep(
    string Kind,
    IReadOnlyDictionary<string, string> Parameters);

public sealed record MacroRunOptions(
    string? OutputFolder,
    bool DryRun);

public sealed record MacroRunItemResult(
    string SourcePath,
    string FinalPath,
    IReadOnlyList<string> Messages,
    string? Error)
{
    public bool Success => Error is null;
}

public sealed record MacroRunResult(
    IReadOnlyList<MacroRunItemResult> Items)
{
    public int SuccessCount => Items.Count(item => item.Success);
    public int FailedCount => Items.Count - SuccessCount;
}

public sealed class MacroActionService
{
    private static readonly ILogger Log = Images.Services.Log.Get(nameof(MacroActionService));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(MacroActionPlan plan)
        => JsonSerializer.Serialize(ToDocument(plan), JsonOptions);

    public static MacroActionPlan Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Macro JSON is empty.", nameof(json));

        var document = JsonSerializer.Deserialize<MacroActionDocument>(json, JsonOptions)
            ?? throw new JsonException("Macro JSON did not contain a plan.");

        var actions = document.Actions
            .Where(action => !string.IsNullOrWhiteSpace(action.Kind))
            .Select(action => new MacroActionStep(
                NormalizeKind(action.Kind),
                new Dictionary<string, string>(action.Parameters, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        if (actions.Count == 0)
            throw new JsonException("Macro plan must contain at least one action.");

        return new MacroActionPlan(
            string.IsNullOrWhiteSpace(document.Name) ? "Images macro" : document.Name.Trim(),
            actions);
    }

    public static bool TryParse(string json, out MacroActionPlan plan, out string error)
    {
        try
        {
            plan = Parse(json);
            error = "";
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException or NotSupportedException)
        {
            plan = MacroActionPlan.Default;
            error = ex.Message;
            return false;
        }
    }

    public MacroRunResult Run(
        MacroActionPlan plan,
        IEnumerable<string> sourcePaths,
        MacroRunOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentNullException.ThrowIfNull(options);

        var paths = sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var results = new List<MacroRunItemResult>(paths.Count);
        for (var index = 0; index < paths.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(RunOne(plan, paths[index], index + 1, options, cancellationToken));
        }

        return new MacroRunResult(results);
    }

    private MacroRunItemResult RunOne(
        MacroActionPlan plan,
        string sourcePath,
        int index,
        MacroRunOptions options,
        CancellationToken cancellationToken)
    {
        var messages = new List<string>();
        var currentPath = sourcePath;

        try
        {
            if (!File.Exists(sourcePath))
                return new MacroRunItemResult(sourcePath, sourcePath, messages, "Source file no longer exists.");

            foreach (var action in plan.Actions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                switch (NormalizeKind(action.Kind))
                {
                    case "strip-gps":
                        messages.Add(StripGps(currentPath, options.DryRun));
                        break;
                    case "export-copy":
                        currentPath = ExportCopy(currentPath, action.Parameters, options, messages);
                        break;
                    case "rename-pattern":
                        currentPath = RenamePattern(currentPath, action.Parameters, index, options.DryRun, messages);
                        break;
                    default:
                        messages.Add($"Skipped unknown action: {action.Kind}");
                        break;
                }
            }

            return new MacroRunItemResult(sourcePath, currentPath, messages, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or InvalidOperationException or NotSupportedException or MagickException)
        {
            Log.LogWarning(ex, "Macro action failed for {Path}", sourcePath);
            return new MacroRunItemResult(sourcePath, currentPath, messages, ex.Message);
        }
    }

    private static string StripGps(string path, bool dryRun)
    {
        if (!SupportsGpsStrip(path))
            return $"GPS strip skipped for {Path.GetFileName(path)}.";
        if (dryRun)
            return $"Would strip GPS from {Path.GetFileName(path)}.";

        var removed = MetadataEditService.StripGpsMetadata(path);
        return removed == 0
            ? $"No GPS metadata found in {Path.GetFileName(path)}."
            : $"Removed {removed} GPS field{Plural(removed)} from {Path.GetFileName(path)}.";
    }

    private static string ExportCopy(
        string path,
        IReadOnlyDictionary<string, string> parameters,
        MacroRunOptions options,
        List<string> messages)
    {
        var extension = GetParameter(parameters, "extension", ".png");
        extension = RenameService.NormalizeExtension(extension);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".png";

        var outputFolder = ResolveOutputFolder(path, options.OutputFolder);
        var targetName = Path.GetFileNameWithoutExtension(path) + extension;
        var targetPath = ResolveUniqueDestination(outputFolder, targetName);
        if (options.DryRun)
        {
            messages.Add($"Would export {Path.GetFileName(path)} to {targetPath}.");
            return targetPath;
        }

        using var image = new MagickImage(path);
        ApplyResize(image, parameters);

        var format = ImageExportService.TryResolveFormat(extension) ?? MagickFormat.Png;
        image.Format = format;
        image.Quality = (uint)ParseInt(GetParameter(parameters, "quality", "92"), 1, 100, 92);
        if (RequiresOpaqueBackground(format))
        {
            image.BackgroundColor = MagickColors.White;
            image.Alpha(AlphaOption.Remove);
        }

        WriteAtomically(image, targetPath);
        messages.Add($"Exported {Path.GetFileName(targetPath)}.");
        return targetPath;
    }

    private static string RenamePattern(
        string path,
        IReadOnlyDictionary<string, string> parameters,
        int index,
        bool dryRun,
        List<string> messages)
    {
        var pattern = GetParameter(parameters, "pattern", "{name}-{index}");
        var folder = Path.GetDirectoryName(path) ?? "";
        var extension = Path.GetExtension(path);
        var stem = ApplyPattern(pattern, path, index);
        var target = RenameService.ResolveTargetPath(folder, stem, extension, path);

        if (string.Equals(target, path, StringComparison.OrdinalIgnoreCase))
        {
            messages.Add($"Rename skipped for {Path.GetFileName(path)}.");
            return path;
        }

        if (dryRun)
        {
            messages.Add($"Would rename {Path.GetFileName(path)} to {Path.GetFileName(target)}.");
            return target;
        }

        File.Move(path, target);
        messages.Add($"Renamed to {Path.GetFileName(target)}.");
        return target;
    }

    private static void ApplyResize(MagickImage image, IReadOnlyDictionary<string, string> parameters)
    {
        var maxWidth = ParseInt(GetParameter(parameters, "maxWidth", ""), 0, int.MaxValue, 0);
        var maxHeight = ParseInt(GetParameter(parameters, "maxHeight", ""), 0, int.MaxValue, 0);
        if (maxWidth <= 0 && maxHeight <= 0)
            return;

        var width = maxWidth > 0 ? (uint)maxWidth : image.Width;
        var height = maxHeight > 0 ? (uint)maxHeight : image.Height;
        image.Resize(new MagickGeometry(width, height)
        {
            IgnoreAspectRatio = false
        });
    }

    private static string ApplyPattern(string pattern, string path, int index)
    {
        var info = new FileInfo(path);
        var date = info.LastWriteTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return pattern
            .Replace("{name}", Path.GetFileNameWithoutExtension(path), StringComparison.OrdinalIgnoreCase)
            .Replace("{index}", index.ToString("000", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{date}", date, StringComparison.OrdinalIgnoreCase)
            .Replace("{ext}", Path.GetExtension(path).TrimStart('.'), StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveOutputFolder(string sourcePath, string? configuredOutputFolder)
    {
        var folder = string.IsNullOrWhiteSpace(configuredOutputFolder)
            ? Path.Combine(Path.GetDirectoryName(sourcePath) ?? Path.GetTempPath(), "Images macro output")
            : Path.GetFullPath(configuredOutputFolder);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string ResolveUniqueDestination(string folder, string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var attempt = 0; attempt < 10_000; attempt++)
        {
            var candidateName = attempt == 0
                ? stem + extension
                : $"{stem} ({attempt + 1}){extension}";
            var candidate = Path.Combine(folder, candidateName);
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(folder, stem + "-" + Guid.NewGuid().ToString("N")[..8] + extension);
    }

    private static void WriteAtomically(MagickImage image, string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new IOException("Macro export destination has no directory.");

        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".images-macro-{Guid.NewGuid():N}{Path.GetExtension(targetPath)}.tmp");
        try
        {
            image.Write(tempPath);
            if (File.Exists(targetPath))
                File.Replace(tempPath, targetPath, null);
            else
                File.Move(tempPath, targetPath);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private static MacroActionDocument ToDocument(MacroActionPlan plan)
        => new()
        {
            Name = plan.Name,
            Actions = plan.Actions
                .Select(action => new MacroActionStepDocument
                {
                    Kind = NormalizeKind(action.Kind),
                    Parameters = new Dictionary<string, string>(action.Parameters, StringComparer.OrdinalIgnoreCase)
                })
                .ToList()
        };

    private static bool SupportsGpsStrip(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresOpaqueBackground(MagickFormat format) => format is
        MagickFormat.Jpeg or
        MagickFormat.Bmp or
        MagickFormat.Ppm or
        MagickFormat.Pgm or
        MagickFormat.Pbm;

    private static string GetParameter(
        IReadOnlyDictionary<string, string> parameters,
        string key,
        string fallback)
        => parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;

    private static int ParseInt(string value, int minimum, int maximum, int fallback)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, minimum, maximum)
            : fallback;

    private static string NormalizeKind(string kind)
        => kind.Trim().ToLowerInvariant().Replace("_", "-", StringComparison.OrdinalIgnoreCase);

    private static string Plural(int count) => count == 1 ? "" : "s";

    private sealed class MacroActionDocument
    {
        public string Name { get; set; } = "Images macro";
        public List<MacroActionStepDocument> Actions { get; set; } = [];
    }

    private sealed class MacroActionStepDocument
    {
        public string Kind { get; set; } = "";
        public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
