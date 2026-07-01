using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using ImageMagick;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public static class BatchOperationKinds
{
    public const string Resize = "resize";
    public const string Rotate = "rotate";
    public const string FlipHorizontal = "flip-horizontal";
    public const string FlipVertical = "flip-vertical";
    public const string StripMetadata = "strip-metadata";
    public const string RenamePattern = "rename-pattern";
    public const string ExportCopy = "export-copy";
}

public sealed record BatchOperationStep(
    string Kind,
    IReadOnlyDictionary<string, string> Parameters);

public sealed record BatchProcessorPreset(
    string Name,
    string Extension,
    int Quality,
    int MaxWidth,
    int MaxHeight,
    IReadOnlyList<BatchOperationStep>? Operations = null)
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
    string EstimatedOutputSizeText,
    string DeltaText,
    string WarningsText,
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

public sealed record BatchProgressUpdate(
    int CompletedCount,
    int TotalCount,
    string FileName,
    bool Success,
    string? Error);

public sealed class BatchProcessorService
{
    private static readonly ILogger Log = Images.Services.Log.Get(nameof(BatchProcessorService));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string SerializePreset(BatchProcessorPreset preset)
        => JsonSerializer.Serialize(NormalizePreset(preset), JsonOptions);

    public static BatchProcessorPreset ParsePreset(string json)
        => NormalizePreset(
            JsonSerializer.Deserialize<BatchProcessorPreset>(json, JsonOptions)
            ?? throw new JsonException("Preset JSON did not contain a batch preset."));

    public static BatchProcessorPreset NormalizePreset(BatchProcessorPreset preset)
    {
        var extension = RenameService.NormalizeExtension(preset.Extension);
        if (string.IsNullOrWhiteSpace(extension) || ImageExportService.TryResolveFormat(extension) is null)
            extension = ".png";

        var quality = Math.Clamp(preset.Quality, 1, 100);
        var maxWidth = Math.Max(0, preset.MaxWidth);
        var maxHeight = Math.Max(0, preset.MaxHeight);

        return preset with
        {
            Name = string.IsNullOrWhiteSpace(preset.Name) ? "Batch preset" : preset.Name.Trim(),
            Extension = extension,
            Quality = quality,
            MaxWidth = maxWidth,
            MaxHeight = maxHeight,
            Operations = NormalizeOperations(preset.Operations, extension, quality, maxWidth, maxHeight)
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
        var paths = NormalizeSourcePaths(sourcePaths);
        for (var index = 0; index < paths.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = paths[index];
            try
            {
                var item = BuildPreviewItem(path, index + 1, preset, outputFolder, cancellationToken);
                items.Add(item);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or InvalidOperationException or NotSupportedException or MagickException)
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

        var paths = NormalizeSourcePaths(sourcePaths);
        var results = new List<MacroRunItemResult>(paths.Count);
        for (var index = 0; index < paths.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(RunOne(paths[index], index + 1, preset, outputFolder, dryRun, cancellationToken));
        }

        return new BatchRunResult(results);
    }

    public async Task<BatchRunResult> RunAsync(
        IEnumerable<string> sourcePaths,
        BatchProcessorPreset preset,
        string? outputFolder,
        bool dryRun,
        int maxConcurrency = 0,
        IProgress<BatchProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        preset = NormalizePreset(preset);

        var paths = NormalizeSourcePaths(sourcePaths);
        if (paths.Count == 0)
            return new BatchRunResult([]);

        var concurrency = maxConcurrency > 0
            ? maxConcurrency
            : Math.Max(1, Environment.ProcessorCount - 1);

        var results = new MacroRunItemResult[paths.Count];
        var completed = 0;
        var reservedNames = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        using var semaphore = new SemaphoreSlim(concurrency, concurrency);

        var tasks = new Task[paths.Count];
        for (var i = 0; i < paths.Count; i++)
        {
            var index = i;
            var path = paths[index];

            tasks[index] = Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var result = RunOne(path, index + 1, preset, outputFolder, dryRun, cancellationToken, reservedNames);
                    results[index] = result;
                    var done = Interlocked.Increment(ref completed);
                    progress?.Report(new BatchProgressUpdate(
                        done,
                        paths.Count,
                        Path.GetFileName(path),
                        result.Success,
                        result.Error));
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);
        }

        await Task.WhenAll(tasks);
        return new BatchRunResult(results);
    }

    private static BatchPreviewItem BuildPreviewItem(
        string sourcePath,
        int sourceIndex,
        BatchProcessorPreset preset,
        string? outputFolder,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(sourcePath);
        if (!info.Exists)
            throw new IOException("Source file no longer exists.");

        using var image = new MagickImage(info.FullName);
        var originalWidth = image.Width;
        var originalHeight = image.Height;
        var messages = new List<string>();
        var outputName = BuildOutputFileName(info.FullName, sourceIndex, preset);
        ApplyPipelineOperations(image, info.FullName, preset, messages, cancellationToken);

        var request = ExportRequestFromPreset(preset);
        var extension = request.Extension;
        var format = ImageExportService.TryResolveFormat(extension) ?? MagickFormat.Png;
        using var encodedImage = (MagickImage)image.Clone();
        ImageExportService.PrepareForExport(encodedImage, format, (uint)request.Quality);
        var encodedBytes = encodedImage.ToByteArray(format);

        var outputPath = ResolveUniqueDestination(
            ResolveOutputFolder(info.FullName, outputFolder),
            Path.ChangeExtension(outputName, extension));
        var warnings = ExportCapabilityWarningService.BuildWarnings(image, info.FullName, extension, format);
        var status = BuildStatusText(preset, messages);

        return new BatchPreviewItem(
            info.FullName,
            info.Name,
            outputPath,
            extension.TrimStart('.').ToUpperInvariant(),
            FormatDimensions(originalWidth, originalHeight),
            FormatDimensions(encodedImage.Width, encodedImage.Height),
            FormatBytes(info.Length),
            FormatBytes(encodedBytes.LongLength),
            FormatDelta(encodedBytes.LongLength - info.Length),
            warnings.Count == 0 ? "No format warnings." : string.Join(" ", warnings),
            status);
    }

    private static MacroRunItemResult RunOne(
        string sourcePath,
        int sourceIndex,
        BatchProcessorPreset preset,
        string? outputFolder,
        bool dryRun,
        CancellationToken cancellationToken,
        ConcurrentDictionary<string, byte>? reservedNames = null)
    {
        var messages = new List<string>();
        var currentPath = sourcePath;

        try
        {
            if (!File.Exists(sourcePath))
                return new MacroRunItemResult(sourcePath, sourcePath, messages, "Source file no longer exists.");

            var outputName = BuildOutputFileName(sourcePath, sourceIndex, preset);
            var request = ExportRequestFromPreset(preset);
            var outputPath = ResolveUniqueDestination(
                ResolveOutputFolder(sourcePath, outputFolder),
                Path.ChangeExtension(outputName, request.Extension),
                reservedNames);
            currentPath = outputPath;

            if (dryRun)
            {
                var preview = BuildPreviewItem(sourcePath, sourceIndex, preset, outputFolder, cancellationToken);
                messages.Add($"Would run {preset.Operations?.Count ?? 0} operation{Plural(preset.Operations?.Count ?? 0)} on {Path.GetFileName(sourcePath)}.");
                messages.Add($"Would write {preview.OutputDimensions} {preview.Format} to {preview.OutputPath}.");
                return new MacroRunItemResult(sourcePath, outputPath, messages, null);
            }

            using var image = new MagickImage(sourcePath);
            ApplyPipelineOperations(image, sourcePath, preset, messages, cancellationToken);
            var format = ImageExportService.TryResolveFormat(request.Extension) ?? MagickFormat.Png;
            ImageExportService.PrepareForExport(image, format, (uint)request.Quality);
            WriteAtomically(image, outputPath);
            messages.Add($"Wrote {Path.GetFileName(outputPath)}.");
            return new MacroRunItemResult(sourcePath, outputPath, messages, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or InvalidOperationException or NotSupportedException or MagickException)
        {
            Log.LogWarning(ex, "Batch operation chain failed for {Path}", sourcePath);
            return new MacroRunItemResult(sourcePath, currentPath, messages, ex.Message);
        }
    }

    private static void ApplyPipelineOperations(
        MagickImage image,
        string sourcePath,
        BatchProcessorPreset preset,
        List<string> messages,
        CancellationToken cancellationToken)
    {
        foreach (var operation in preset.Operations ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (NormalizeKind(operation.Kind))
            {
                case BatchOperationKinds.Resize:
                    ApplyResize(image, operation.Parameters);
                    messages.Add(DescribeOperation(operation));
                    break;
                case BatchOperationKinds.Rotate:
                    var degrees = ParseDouble(GetParameter(operation.Parameters, "degrees", "90"), 0);
                    if (Math.Abs(degrees) > double.Epsilon)
                        image.Rotate(degrees);
                    messages.Add(DescribeOperation(operation));
                    break;
                case BatchOperationKinds.FlipHorizontal:
                    image.Flop();
                    messages.Add(DescribeOperation(operation));
                    break;
                case BatchOperationKinds.FlipVertical:
                    image.Flip();
                    messages.Add(DescribeOperation(operation));
                    break;
                case BatchOperationKinds.StripMetadata:
                    var categories = ParseCategories(GetParameter(operation.Parameters, "categories", "all"));
                    var result = MetadataEditService.StripMetadata(image, categories);
                    messages.Add(result.RemovedCount == 0
                        ? $"No {MetadataEditService.CategoryLabel(categories)} metadata to strip"
                        : $"Strip {result.RemovedCount} {MetadataEditService.CategoryLabel(categories)} field{Plural(result.RemovedCount)}");
                    break;
                case BatchOperationKinds.RenamePattern:
                case BatchOperationKinds.ExportCopy:
                    messages.Add(DescribeOperation(operation));
                    break;
                default:
                    Log.LogDebug("Skipping unsupported batch operation {Kind} for {Path}", operation.Kind, sourcePath);
                    break;
            }
        }
    }

    private static void ApplyResize(MagickImage image, IReadOnlyDictionary<string, string> parameters)
    {
        var maxWidth = ParseInt(GetParameter(parameters, "maxWidth", ""), 0, int.MaxValue, 0);
        var maxHeight = ParseInt(GetParameter(parameters, "maxHeight", ""), 0, int.MaxValue, 0);
        if (maxWidth <= 0 && maxHeight <= 0)
            return;

        image.Resize(new MagickGeometry(
            maxWidth > 0 ? (uint)maxWidth : image.Width,
            maxHeight > 0 ? (uint)maxHeight : image.Height)
        {
            IgnoreAspectRatio = false
        });
    }

    private static IReadOnlyList<BatchOperationStep> NormalizeOperations(
        IReadOnlyList<BatchOperationStep>? operations,
        string extension,
        int quality,
        int maxWidth,
        int maxHeight)
    {
        var normalized = new List<BatchOperationStep>();
        if (operations is not null)
        {
            foreach (var operation in operations)
            {
                var step = NormalizeOperation(operation);
                if (step is not null)
                    normalized.Add(step);
            }
        }

        if (normalized.Count == 0)
        {
            if (maxWidth > 0 || maxHeight > 0)
            {
                normalized.Add(new BatchOperationStep(
                    BatchOperationKinds.Resize,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["maxWidth"] = maxWidth.ToString(CultureInfo.InvariantCulture),
                        ["maxHeight"] = maxHeight.ToString(CultureInfo.InvariantCulture)
                    }));
            }

            normalized.Add(new BatchOperationStep(
                BatchOperationKinds.ExportCopy,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["extension"] = extension,
                    ["quality"] = quality.ToString(CultureInfo.InvariantCulture)
                }));
        }

        if (!normalized.Any(operation => NormalizeKind(operation.Kind) == BatchOperationKinds.ExportCopy))
        {
            normalized.Add(new BatchOperationStep(
                BatchOperationKinds.ExportCopy,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["extension"] = extension,
                    ["quality"] = quality.ToString(CultureInfo.InvariantCulture)
                }));
        }

        return normalized;
    }

    private static BatchOperationStep? NormalizeOperation(BatchOperationStep operation)
    {
        var kind = NormalizeKind(operation.Kind);
        if (kind is not (
            BatchOperationKinds.Resize or
            BatchOperationKinds.Rotate or
            BatchOperationKinds.FlipHorizontal or
            BatchOperationKinds.FlipVertical or
            BatchOperationKinds.StripMetadata or
            BatchOperationKinds.RenamePattern or
            BatchOperationKinds.ExportCopy))
        {
            return null;
        }

        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in operation.Parameters ?? new Dictionary<string, string>())
        {
            if (!string.IsNullOrWhiteSpace(item.Key))
                parameters[item.Key.Trim()] = item.Value?.Trim() ?? "";
        }

        if (kind == BatchOperationKinds.ExportCopy)
        {
            var extension = RenameService.NormalizeExtension(GetParameter(parameters, "extension", ".png"));
            if (ImageExportService.TryResolveFormat(extension) is null)
                extension = ".png";
            parameters["extension"] = extension;
            parameters["quality"] = ParseInt(GetParameter(parameters, "quality", "92"), 1, 100, 92).ToString(CultureInfo.InvariantCulture);
        }

        if (kind == BatchOperationKinds.Resize)
        {
            parameters["maxWidth"] = ParseInt(GetParameter(parameters, "maxWidth", "0"), 0, int.MaxValue, 0).ToString(CultureInfo.InvariantCulture);
            parameters["maxHeight"] = ParseInt(GetParameter(parameters, "maxHeight", "0"), 0, int.MaxValue, 0).ToString(CultureInfo.InvariantCulture);
        }

        if (kind == BatchOperationKinds.Rotate)
            parameters["degrees"] = ParseDouble(GetParameter(parameters, "degrees", "90"), 90).ToString(CultureInfo.InvariantCulture);

        if (kind == BatchOperationKinds.StripMetadata)
            parameters["categories"] = GetParameter(parameters, "categories", "all").Trim();

        if (kind == BatchOperationKinds.RenamePattern)
            parameters["pattern"] = string.IsNullOrWhiteSpace(GetParameter(parameters, "pattern", "")) ? "{name}" : GetParameter(parameters, "pattern", "{name}");

        return new BatchOperationStep(kind, parameters);
    }

    private static ExportPreviewRequest ExportRequestFromPreset(BatchProcessorPreset preset)
    {
        var export = preset.Operations?
            .LastOrDefault(operation => NormalizeKind(operation.Kind) == BatchOperationKinds.ExportCopy);
        if (export is null)
            return ExportPreviewService.NormalizeRequest(new ExportPreviewRequest(preset.Extension, preset.Quality));

        return ExportPreviewService.NormalizeRequest(new ExportPreviewRequest(
            GetParameter(export.Parameters, "extension", preset.Extension),
            ParseInt(GetParameter(export.Parameters, "quality", preset.Quality.ToString(CultureInfo.InvariantCulture)), 1, 100, preset.Quality)));
    }

    private static string BuildOutputFileName(string sourcePath, int sourceIndex, BatchProcessorPreset preset)
    {
        var stem = Path.GetFileNameWithoutExtension(sourcePath);
        foreach (var operation in preset.Operations ?? [])
        {
            if (NormalizeKind(operation.Kind) == BatchOperationKinds.RenamePattern)
                stem = ApplyPattern(GetParameter(operation.Parameters, "pattern", "{name}"), sourcePath, sourceIndex, stem);
        }

        return SanitizeFileNameStem(stem) + ExportRequestFromPreset(preset).Extension;
    }

    private static string ApplyPattern(string pattern, string path, int index, string currentStem)
    {
        var info = new FileInfo(path);
        var date = info.Exists
            ? info.LastWriteTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
            : DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        return pattern
            .Replace("{name}", currentStem, StringComparison.OrdinalIgnoreCase)
            .Replace("{sourceName}", Path.GetFileNameWithoutExtension(path), StringComparison.OrdinalIgnoreCase)
            .Replace("{index}", index.ToString("000", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{date}", date, StringComparison.OrdinalIgnoreCase)
            .Replace("{ext}", Path.GetExtension(path).TrimStart('.'), StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeFileNameStem(string stem)
    {
        if (string.IsNullOrWhiteSpace(stem))
            return "image";

        var invalid = Path.GetInvalidFileNameChars();
        var chars = stem.Trim()
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray();
        var sanitized = new string(chars).Trim('.', ' ');
        return string.IsNullOrWhiteSpace(sanitized) ? "image" : sanitized;
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

    private static string ResolveUniqueDestination(
        string folder,
        string fileName,
        ConcurrentDictionary<string, byte>? reservedNames = null)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var attempt = 0; attempt < 10_000; attempt++)
        {
            var candidateName = attempt == 0
                ? stem + extension
                : $"{stem} ({attempt + 1}){extension}";
            var candidate = Path.Combine(folder, candidateName);
            if (!File.Exists(candidate) &&
                (reservedNames is null || reservedNames.TryAdd(candidate, 0)))
                return candidate;
        }

        var fallback = Path.Combine(folder, stem + "-" + Guid.NewGuid().ToString("N")[..8] + extension);
        reservedNames?.TryAdd(fallback, 0);
        return fallback;
    }

    private static void WriteAtomically(MagickImage image, string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new IOException("Batch export destination has no directory.");

        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".images-batch-{Guid.NewGuid():N}{Path.GetExtension(targetPath)}.tmp");
        try
        {
            image.Write(tempPath);
            File.Move(tempPath, targetPath, overwrite: true);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private static string BuildStatusText(BatchProcessorPreset preset, IReadOnlyList<string> messages)
    {
        if (messages.Count > 0)
            return string.Join(" -> ", messages.Distinct(StringComparer.OrdinalIgnoreCase));

        return $"{preset.Operations?.Count ?? 0} operation{Plural(preset.Operations?.Count ?? 0)}";
    }

    public static string DescribeOperation(BatchOperationStep operation)
        => NormalizeKind(operation.Kind) switch
        {
            BatchOperationKinds.Resize => $"Resize max {GetParameter(operation.Parameters, "maxWidth", "0")} x {GetParameter(operation.Parameters, "maxHeight", "0")}",
            BatchOperationKinds.Rotate => $"Rotate {GetParameter(operation.Parameters, "degrees", "90")} degrees",
            BatchOperationKinds.FlipHorizontal => "Flip horizontal",
            BatchOperationKinds.FlipVertical => "Flip vertical",
            BatchOperationKinds.StripMetadata => $"Strip {GetParameter(operation.Parameters, "categories", "all")} metadata",
            BatchOperationKinds.RenamePattern => $"Rename pattern {GetParameter(operation.Parameters, "pattern", "{name}")}",
            BatchOperationKinds.ExportCopy => $"Export {GetParameter(operation.Parameters, "extension", ".png").TrimStart('.').ToUpperInvariant()} q{GetParameter(operation.Parameters, "quality", "92")}",
            _ => operation.Kind
        };

    private static MetadataStripCategory ParseCategories(string value)
    {
        var result = MetadataStripCategory.None;
        foreach (var part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            result |= part.ToLowerInvariant() switch
            {
                "gps" => MetadataStripCategory.Gps,
                "device" or "deviceinfo" => MetadataStripCategory.DeviceInfo,
                "timestamps" or "time" => MetadataStripCategory.Timestamps,
                "software" => MetadataStripCategory.Software,
                "all" => MetadataStripCategory.All,
                _ => MetadataStripCategory.None
            };
        }

        return result == MetadataStripCategory.None ? MetadataStripCategory.All : result;
    }

    private static string FormatDimensions(uint width, uint height)
        => width == 0 || height == 0
            ? "Unknown"
            : $"{width} x {height}";

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(0, bytes);
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

    private static string FormatDelta(long bytes)
    {
        if (bytes == 0)
            return "same size";

        var sign = bytes > 0 ? "+" : "-";
        return sign + FormatBytes(Math.Abs(bytes));
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

    private static double ParseDouble(string value, double fallback)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static string NormalizeKind(string kind)
        => (kind ?? "").Trim().ToLowerInvariant().Replace("_", "-", StringComparison.OrdinalIgnoreCase);

    private static string Plural(int count) => count == 1 ? "" : "s";
}
