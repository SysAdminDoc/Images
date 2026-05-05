using System.Globalization;
using System.IO;
using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageMagick;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public sealed record BatchProcessorPreset(
    string Name,
    string Extension,
    int Quality,
    int MaxWidth,
    int MaxHeight)
{
    public static IReadOnlyList<BatchProcessorPreset> Defaults { get; } =
    [
        new("JPEG web copy", ".jpg", 88, 2400, 2400),
        new("PNG archive copy", ".png", 92, 0, 0),
        new("WebP balanced", ".webp", 82, 2400, 2400)
    ];
}

public sealed record BatchPreviewItem(
    string SourcePath,
    string FileName,
    string OutputPath,
    string Format,
    string OriginalDimensions,
    string OutputDimensions,
    string SizeText,
    string StatusText);

public sealed record BatchPreviewResult(
    IReadOnlyList<BatchPreviewItem> Items,
    int FailedCount);

public sealed record BatchRunResult(
    IReadOnlyList<MacroRunItemResult> Items)
{
    public int SuccessCount => Items.Count(item => item.Success);
    public int FailedCount => Items.Count - SuccessCount;
}

public sealed class BatchProcessorService
{
    private static readonly ILogger Log = Images.Services.Log.Get(nameof(BatchProcessorService));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly MacroActionService _macroActionService;

    public BatchProcessorService(MacroActionService? macroActionService = null)
    {
        _macroActionService = macroActionService ?? new MacroActionService();
    }

    public static string SerializePreset(BatchProcessorPreset preset)
        => JsonSerializer.Serialize(preset, JsonOptions);

    public static BatchProcessorPreset ParsePreset(string json)
        => NormalizePreset(
            JsonSerializer.Deserialize<BatchProcessorPreset>(json, JsonOptions)
            ?? throw new JsonException("Preset JSON did not contain a batch preset."));

    public static BatchProcessorPreset NormalizePreset(BatchProcessorPreset preset)
    {
        var extension = RenameService.NormalizeExtension(preset.Extension);
        if (string.IsNullOrWhiteSpace(extension) || ImageExportService.TryResolveFormat(extension) is null)
            extension = ".png";

        return preset with
        {
            Name = string.IsNullOrWhiteSpace(preset.Name) ? "Batch preset" : preset.Name.Trim(),
            Extension = extension,
            Quality = Math.Clamp(preset.Quality, 1, 100),
            MaxWidth = Math.Max(0, preset.MaxWidth),
            MaxHeight = Math.Max(0, preset.MaxHeight)
        };
    }

    public BatchPreviewResult BuildPreview(
        IEnumerable<string> sourcePaths,
        BatchProcessorPreset preset,
        string? outputFolder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        preset = NormalizePreset(preset);

        var failed = 0;
        var items = new List<BatchPreviewItem>();
        foreach (var path in NormalizeSourcePaths(sourcePaths))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    failed++;
                    continue;
                }

                using var image = new MagickImage();
                image.Ping(info);
                var outputDimensions = OutputDimensions(image.Width, image.Height, preset);
                items.Add(new BatchPreviewItem(
                    info.FullName,
                    info.Name,
                    ResolveUniqueDestination(ResolveOutputFolder(info.FullName, outputFolder), Path.GetFileNameWithoutExtension(info.Name) + preset.Extension),
                    preset.Extension.TrimStart('.').ToUpperInvariant(),
                    FormatDimensions(image.Width, image.Height),
                    FormatDimensions(outputDimensions.Width, outputDimensions.Height),
                    FormatBytes(info.Length),
                    StatusFor(image.Width, image.Height, outputDimensions.Width, outputDimensions.Height)));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException or MagickException)
            {
                failed++;
                Log.LogDebug(ex, "Could not preview batch item {Path}", path);
            }
        }

        return new BatchPreviewResult(items, failed);
    }

    public BatchRunResult Run(
        IEnumerable<string> sourcePaths,
        BatchProcessorPreset preset,
        string? outputFolder,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        preset = NormalizePreset(preset);

        var plan = new MacroActionPlan(
            "Batch export",
            [
                new MacroActionStep(
                    "export-copy",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["extension"] = preset.Extension,
                        ["quality"] = preset.Quality.ToString(CultureInfo.InvariantCulture),
                        ["maxWidth"] = preset.MaxWidth.ToString(CultureInfo.InvariantCulture),
                        ["maxHeight"] = preset.MaxHeight.ToString(CultureInfo.InvariantCulture)
                    })
            ]);

        return new BatchRunResult(
            _macroActionService.Run(
                plan,
                NormalizeSourcePaths(sourcePaths),
                new MacroRunOptions(outputFolder, dryRun),
                cancellationToken).Items);
    }

    private static IReadOnlyList<string> NormalizeSourcePaths(IEnumerable<string> sourcePaths)
        => sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path))
            .Where(File.Exists)
            .Where(SupportedImageFormats.IsSupported)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string ResolveOutputFolder(string sourcePath, string? configuredOutputFolder)
    {
        var folder = string.IsNullOrWhiteSpace(configuredOutputFolder)
            ? Path.Combine(Path.GetDirectoryName(sourcePath) ?? Path.GetTempPath(), "Images batch output")
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

    private static (uint Width, uint Height) OutputDimensions(uint width, uint height, BatchProcessorPreset preset)
    {
        if (width == 0 || height == 0)
            return (width, height);

        var ratio = 1d;
        if (preset.MaxWidth > 0 && width > preset.MaxWidth)
            ratio = Math.Min(ratio, preset.MaxWidth / (double)width);
        if (preset.MaxHeight > 0 && height > preset.MaxHeight)
            ratio = Math.Min(ratio, preset.MaxHeight / (double)height);

        if (ratio >= 1d)
            return (width, height);

        return (
            Math.Max(1, (uint)Math.Round(width * ratio)),
            Math.Max(1, (uint)Math.Round(height * ratio)));
    }

    private static string StatusFor(uint width, uint height, uint outputWidth, uint outputHeight)
        => width == outputWidth && height == outputHeight
            ? "Convert/export only"
            : $"Resize to {FormatDimensions(outputWidth, outputHeight)}";

    private static string FormatDimensions(uint width, uint height)
        => width == 0 || height == 0
            ? "Unknown"
            : $"{width} x {height}";

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes} B"
            : $"{value:0.#} {units[unit]}";
    }
}
