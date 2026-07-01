using System.Globalization;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Images.Services;

public enum LocalModelAvailability
{
    Missing,
    Ready,
    HashMismatch,
    ManifestInvalid,
    StorageUnavailable
}

public enum LocalModelImportStatus
{
    Imported,
    HashMismatch,
    UnknownModel,
    SourceMissing,
    StorageUnavailable,
    Failed
}

public sealed record LocalModelDefinition(
    string Id,
    string DisplayName,
    string Purpose,
    string StorageGroup,
    string SourceUrl,
    string DownloadUrl,
    string License,
    string FileName,
    string ExpectedSha256,
    long ExpectedSizeBytes,
    string RuntimeContract,
    string Notes)
{
    [JsonIgnore]
    public string ExpectedSizeText => ModelManagerService.FormatBytes(ExpectedSizeBytes);
}

public sealed record LocalModelStatus(
    LocalModelDefinition Definition,
    LocalModelAvailability Availability,
    string StatusText,
    string ActionText,
    string? InstalledPath,
    string? Sha256,
    long? SizeBytes,
    DateTimeOffset? ImportedUtc)
{
    [JsonIgnore]
    public string DisplayName => Definition.DisplayName;

    [JsonIgnore]
    public string Purpose => Definition.Purpose;

    [JsonIgnore]
    public string SourceUrl => Definition.SourceUrl;

    [JsonIgnore]
    public string ExpectedSha256 => Definition.ExpectedSha256;

    [JsonIgnore]
    public string SizeText => SizeBytes is null ? Definition.ExpectedSizeText : ModelManagerService.FormatBytes(SizeBytes.Value);

    [JsonIgnore]
    public string ImportedText => ImportedUtc is null
        ? "Not imported"
        : ImportedUtc.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    [JsonIgnore]
    public bool CanDelete => !string.IsNullOrWhiteSpace(InstalledPath) || Availability is LocalModelAvailability.HashMismatch or LocalModelAvailability.ManifestInvalid;

    [JsonIgnore]
    public bool IsReady => Availability == LocalModelAvailability.Ready;
}

public sealed record LocalModelRuntimeStatus(
    bool WindowsMlOsCandidate,
    bool WindowsMlReferenced,
    bool OnnxDirectMlReferenced,
    string PreferredBackend,
    string StatusText,
    IReadOnlyList<MetadataFact> Rows);

public sealed record LocalModelManagerSnapshot(
    string? ModelRoot,
    LocalModelRuntimeStatus Runtime,
    IReadOnlyList<LocalModelStatus> Models)
{
    public int ReadyCount => Models.Count(model => model.IsReady);
    public int TotalCount => Models.Count;
    public string RegistrySummary => $"{ReadyCount}/{TotalCount} approved model files ready";
}

public sealed record LocalModelImportResult(
    LocalModelImportStatus Status,
    LocalModelStatus? Model,
    string Message);

public sealed record ModelValidationResult(
    bool Success,
    string Summary,
    IReadOnlyList<ModelValidationStep> Steps)
{
    public string? FirstFailureReason => Steps.FirstOrDefault(s => !s.Passed)?.Reason;
}

public sealed record ModelValidationStep(
    string Name,
    bool Passed,
    string Reason);

public sealed class ModelManagerService
{
    private const string ManifestFileName = "model-manifest.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Func<string?> _getModelRoot;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<Version> _getOsVersion;
    private readonly IReadOnlyList<LocalModelDefinition> _definitions;

    public ModelManagerService(
        Func<string?>? getModelRoot = null,
        Func<DateTimeOffset>? clock = null,
        Func<Version>? getOsVersion = null,
        IReadOnlyList<LocalModelDefinition>? definitions = null)
    {
        _getModelRoot = getModelRoot ?? (() => AppStorage.TryGetAppDirectory("models"));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _getOsVersion = getOsVersion ?? (() => Environment.OSVersion.Version);
        _definitions = definitions ?? ApprovedModels;
    }

    public static IReadOnlyList<LocalModelDefinition> ApprovedModels { get; } =
    [
        new(
            Id: "opencv-inpainting-lama-2025jan",
            DisplayName: "OpenCV LaMa ONNX",
            Purpose: "Content-aware repair",
            StorageGroup: "inpaint",
            SourceUrl: "https://huggingface.co/opencv/inpainting_lama",
            DownloadUrl: "https://huggingface.co/opencv/inpainting_lama/resolve/main/inpainting_lama_2025jan.onnx",
            License: "Apache-2.0 / Apache License per model card",
            FileName: "inpainting_lama_2025jan.onnx",
            ExpectedSha256: "7df918ac3921d3daf0aae1d219776cf0dc4e4935f035af81841b40adcf74fdf2",
            ExpectedSizeBytes: 92_600_000,
            RuntimeContract: "ONNX Runtime DirectML first; CPU fallback with visible slow-path status.",
            Notes: "Primary LaMa candidate from docs/inpaint-runtime-decision.md. No automatic download; import the exact ONNX file and verify SHA-256."),
        new(
            Id: "carve-lama-fp32",
            DisplayName: "Carve LaMa FP32 ONNX",
            Purpose: "Content-aware repair fallback",
            StorageGroup: "inpaint",
            SourceUrl: "https://huggingface.co/Carve/LaMa-ONNX",
            DownloadUrl: "https://huggingface.co/Carve/LaMa-ONNX/resolve/main/lama_fp32.onnx",
            License: "Apache-2.0 per model card",
            FileName: "lama_fp32.onnx",
            ExpectedSha256: "1faef5301d78db7dda502fe59966957ec4b79dd64e16f03ed96913c7a4eb68d6",
            ExpectedSizeBytes: 208_000_000,
            RuntimeContract: "Fixed 512x512 input, opset 17; ONNX Runtime DirectML first, CPU fallback.",
            Notes: "Fallback validation candidate. The alternate Carve lama.onnx export is intentionally not approved because the model card marks it slower/not recommended."),
        new(
            Id: "qdrant-clip-vit-b32-text",
            DisplayName: "Qdrant CLIP ViT-B/32 Text ONNX",
            Purpose: "Semantic search text embeddings",
            StorageGroup: "semantic",
            SourceUrl: "https://huggingface.co/Qdrant/clip-ViT-B-32-text",
            DownloadUrl: "https://huggingface.co/Qdrant/clip-ViT-B-32-text/resolve/main/model.onnx",
            License: "MIT per model card",
            FileName: "clip-vit-b32-text.onnx",
            ExpectedSha256: "4dbe762b11e36488304471e439cde89da053ad7acaddbf9e096745d142ec8d8b",
            ExpectedSizeBytes: 254_102_519,
            RuntimeContract: "Text encoder for CLIP-style semantic search; requires tokenizer validation before runtime use.",
            Notes: "Approved file candidate only. The V7-31 provider must still validate tokenizer files, input names/shapes, normalization, and runtime output compatibility before enabling model-backed search."),
        new(
            Id: "qdrant-clip-vit-b32-tokenizer",
            DisplayName: "Qdrant CLIP ViT-B/32 Tokenizer",
            Purpose: "Semantic search text tokenization",
            StorageGroup: "semantic",
            SourceUrl: "https://huggingface.co/Qdrant/clip-ViT-B-32-text",
            DownloadUrl: "https://huggingface.co/Qdrant/clip-ViT-B-32-text/resolve/main/tokenizer.json",
            License: "MIT per model card",
            FileName: "clip-vit-b32-tokenizer.json",
            ExpectedSha256: "b68d571997a1f81bf521fb73806740ddb91e4ed6666cb6e996c066bb289cf55b",
            ExpectedSizeBytes: 2_224_147,
            RuntimeContract: "Tokenizer metadata for the CLIP text encoder; required before model-backed text search can replace deterministic metadata embeddings.",
            Notes: "Approved tokenizer candidate only. The V7-31 provider must still validate BPE behavior, context length, truncation, and special-token parity before runtime use."),
        new(
            Id: "qdrant-clip-vit-b32-vision",
            DisplayName: "Qdrant CLIP ViT-B/32 Vision ONNX",
            Purpose: "Semantic search image embeddings",
            StorageGroup: "semantic",
            SourceUrl: "https://huggingface.co/Qdrant/clip-ViT-B-32-vision",
            DownloadUrl: "https://huggingface.co/Qdrant/clip-ViT-B-32-vision/resolve/main/model.onnx",
            License: "MIT per model card",
            FileName: "clip-vit-b32-vision.onnx",
            ExpectedSha256: "c68d3d9a200ddd2a8c8a5510b576d4c94d1ae383bf8b36dd8c084f94e1fb4d63",
            ExpectedSizeBytes: 352_000_000,
            RuntimeContract: "Vision encoder for CLIP-style semantic search; requires 224px CLIP preprocessing and runtime validation before use.",
            Notes: "Approved file candidate only. The V7-31 provider must still validate preprocessing, model output shape, and similarity parity before replacing deterministic metadata embeddings."),
        new(
            Id: "qdrant-clip-vit-b32-preprocessor",
            DisplayName: "Qdrant CLIP ViT-B/32 Vision Preprocessor",
            Purpose: "Semantic search image preprocessing",
            StorageGroup: "semantic",
            SourceUrl: "https://huggingface.co/Qdrant/clip-ViT-B-32-vision",
            DownloadUrl: "https://huggingface.co/Qdrant/clip-ViT-B-32-vision/resolve/main/preprocessor_config.json",
            License: "MIT per model card",
            FileName: "clip-vit-b32-preprocessor_config.json",
            ExpectedSha256: "ce945ef831c9972c135b5b198a03d8eeb70478cd69c0238f24caf1903a9965e6",
            ExpectedSizeBytes: 780,
            RuntimeContract: "Preprocessor metadata for the CLIP vision encoder; required before image embeddings can be generated from local files.",
            Notes: "Approved preprocessor candidate only. The V7-31 provider must still validate resize/crop, RGB normalization, tensor layout, and output parity before runtime use.")
    ];

    public LocalModelManagerSnapshot GetSnapshot()
    {
        var root = TryGetModelRoot();
        var runtime = InspectRuntime();
        var models = _definitions
            .Select(definition => InspectModel(root, definition))
            .ToArray();

        return new LocalModelManagerSnapshot(root, runtime, models);
    }

    public LocalModelImportResult ImportLocalModel(string modelId, string sourcePath)
    {
        var definition = _definitions.FirstOrDefault(item => item.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase));
        if (definition is null)
        {
            return new LocalModelImportResult(
                LocalModelImportStatus.UnknownModel,
                null,
                "The selected model definition is not approved by Images.");
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return new LocalModelImportResult(
                LocalModelImportStatus.SourceMissing,
                null,
                "Choose an existing local ONNX model file.");
        }

        var root = TryGetModelRoot();
        if (string.IsNullOrWhiteSpace(root))
        {
            return new LocalModelImportResult(
                LocalModelImportStatus.StorageUnavailable,
                null,
                "Model storage is not available.");
        }

        try
        {
            var directory = ModelDirectory(root, definition);
            Directory.CreateDirectory(directory);
            var destination = Path.Combine(directory, definition.FileName);
            var temp = Path.Combine(directory, ".import-" + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                if (!SamePath(sourcePath, destination))
                {
                    File.Copy(sourcePath, temp, overwrite: false);
                    if (File.Exists(destination))
                        File.Delete(destination);
                    File.Move(temp, destination);
                }

                var sha256 = ComputeSha256(destination);
                var info = new FileInfo(destination);
                var availability = sha256.Equals(definition.ExpectedSha256, StringComparison.OrdinalIgnoreCase)
                    ? LocalModelAvailability.Ready
                    : LocalModelAvailability.HashMismatch;
                WriteManifest(directory, new LocalModelManifest(
                    definition.Id,
                    definition.FileName,
                    destination,
                    sha256,
                    info.Length,
                    _clock(),
                    availability));

                var status = InspectModel(root, definition);
                return new LocalModelImportResult(
                    availability == LocalModelAvailability.Ready
                        ? LocalModelImportStatus.Imported
                        : LocalModelImportStatus.HashMismatch,
                    status,
                    availability == LocalModelAvailability.Ready
                        ? $"{definition.DisplayName} imported and verified."
                        : $"{definition.DisplayName} was copied but its SHA-256 does not match the approved model.");
            }
            finally
            {
                TryDelete(temp);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
        {
            return new LocalModelImportResult(
                LocalModelImportStatus.Failed,
                InspectModel(root, definition),
                "Model import failed: " + ex.Message);
        }
    }

    public bool DeleteLocalModel(string modelId, out string message)
    {
        var definition = _definitions.FirstOrDefault(item => item.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase));
        if (definition is null)
        {
            message = "The selected model definition is not approved by Images.";
            return false;
        }

        var root = TryGetModelRoot();
        if (string.IsNullOrWhiteSpace(root))
        {
            message = "Model storage is not available.";
            return false;
        }

        try
        {
            var directory = ModelDirectory(root, definition);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);

            message = $"{definition.DisplayName} removed from local model storage.";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
        {
            message = "Model delete failed: " + ex.Message;
            return false;
        }
    }

    public string? GetModelRoot() => TryGetModelRoot();

    public ModelValidationResult ValidateClipPipeline()
    {
        var steps = new List<ModelValidationStep>();

        var root = TryGetModelRoot();
        var textDef = _definitions.FirstOrDefault(d => d.Id == "qdrant-clip-vit-b32-text");
        var visionDef = _definitions.FirstOrDefault(d => d.Id == "qdrant-clip-vit-b32-vision");
        var tokenizerDef = _definitions.FirstOrDefault(d => d.Id == "qdrant-clip-vit-b32-tokenizer");
        var preprocessorDef = _definitions.FirstOrDefault(d => d.Id == "qdrant-clip-vit-b32-preprocessor");

        if (textDef is null || visionDef is null || tokenizerDef is null || preprocessorDef is null)
            return new ModelValidationResult(false, "CLIP model definitions not found in approved registry.", steps);

        var textModel = InspectModel(root, textDef);
        steps.Add(new("Text model file", textModel.IsReady,
            textModel.IsReady ? "Ready" : $"Not available: {textModel.StatusText}"));

        var visionModel = InspectModel(root, visionDef);
        steps.Add(new("Vision model file", visionModel.IsReady,
            visionModel.IsReady ? "Ready" : $"Not available: {visionModel.StatusText}"));

        var tokenizer = InspectModel(root, tokenizerDef);
        steps.Add(new("Tokenizer file", tokenizer.IsReady,
            tokenizer.IsReady ? "Ready" : $"Not available: {tokenizer.StatusText}"));

        var preprocessor = InspectModel(root, preprocessorDef);
        steps.Add(new("Preprocessor config", preprocessor.IsReady,
            preprocessor.IsReady ? "Ready" : $"Not available: {preprocessor.StatusText}"));

        if (steps.Any(s => !s.Passed))
        {
            return new ModelValidationResult(false,
                $"Missing model files: {steps.Count(s => !s.Passed)} of 4 unavailable.",
                steps);
        }

        try
        {
            var tokenizerObj = ClipTokenizer.Load(tokenizer.InstalledPath!);
            var tokens = tokenizerObj.Encode("test validation input");
            steps.Add(new("Tokenizer load + encode", tokens.Length > 0,
                tokens.Length > 0 ? $"Encoded to {tokens.Length} tokens" : "Tokenizer returned empty"));
        }
        catch (Exception ex)
        {
            steps.Add(new("Tokenizer load + encode", false, $"Failed: {ex.Message}"));
            return new ModelValidationResult(false, $"Tokenizer validation failed: {ex.Message}", steps);
        }

        try
        {
            ClipImagePreprocessor.Load(preprocessor.InstalledPath!);
            steps.Add(new("Preprocessor config load", true, "Config parsed"));
        }
        catch (Exception ex)
        {
            steps.Add(new("Preprocessor config load", false, $"Failed: {ex.Message}"));
            return new ModelValidationResult(false, $"Preprocessor config failed: {ex.Message}", steps);
        }

        try
        {
            var provider = ClipEmbeddingProvider.TryCreate(this);
            if (provider is null)
            {
                steps.Add(new("ONNX session creation", false, "Provider returned null — ONNX Runtime or model load failure"));
                return new ModelValidationResult(false, "CLIP provider creation failed.", steps);
            }

            steps.Add(new("ONNX session creation", true, "Sessions created"));

            var textEmbed = provider.EmbedText("test");
            var textOk = textEmbed.Count > 0 && textEmbed.Any(v => Math.Abs(v) > 0.000001f);
            steps.Add(new("Text embedding smoke", textOk,
                textOk ? $"{textEmbed.Count}-dim non-zero vector" : "Zero or empty embedding"));

            provider.Dispose();
        }
        catch (Exception ex)
        {
            steps.Add(new("ONNX inference smoke", false, $"Failed: {ex.Message}"));
            return new ModelValidationResult(false, $"Inference failed: {ex.Message}", steps);
        }

        return new ModelValidationResult(true,
            $"CLIP pipeline validated: all {steps.Count} checks passed.", steps);
    }

    private LocalModelRuntimeStatus InspectRuntime()
    {
        var osVersion = _getOsVersion();
        var windowsMlOsCandidate = OperatingSystem.IsWindows() && osVersion >= new Version(10, 0, 26100);
        var windowsMlReferenced = IsAssemblyLoaded("Microsoft.Windows.AI.MachineLearning");
        var onnxDirectMlReferenced = IsAssemblyLoaded("Microsoft.ML.OnnxRuntime.DirectML") ||
                                     IsAssemblyLoaded("Microsoft.ML.OnnxRuntime");

        var probedProvider = OnnxRuntimeService.Provider;
        var providerLabel = OnnxRuntimeService.ProviderLabel;

        var preferred = windowsMlOsCandidate
            ? "Windows ML candidate"
            : probedProvider == OnnxProvider.DirectML
                ? "ONNX Runtime DirectML"
                : probedProvider == OnnxProvider.Cpu
                    ? "ONNX Runtime CPU"
                    : "ONNX Runtime DirectML fallback required";
        var status = windowsMlReferenced
            ? "Windows ML package/runtime is referenced by this build."
            : $"Active provider: {providerLabel}. " + (windowsMlOsCandidate
                ? "Windows 11 24H2+ detected; Windows ML is the preferred future runtime."
                : "This OS uses the ONNX Runtime path for local model features.");

        var rows = new[]
        {
            new MetadataFact("Active provider", providerLabel),
            new MetadataFact("Preferred", preferred),
            new MetadataFact("Windows ML OS", windowsMlOsCandidate ? "Windows 11 24H2+ candidate" : "Not a Windows ML 24H2+ candidate"),
            new MetadataFact("Windows ML reference", windowsMlReferenced ? "Referenced" : "Not referenced"),
            new MetadataFact("ONNX DirectML reference", onnxDirectMlReferenced ? "Referenced" : "Not referenced")
        };

        return new LocalModelRuntimeStatus(
            windowsMlOsCandidate,
            windowsMlReferenced,
            onnxDirectMlReferenced,
            preferred,
            status,
            rows);
    }

    private LocalModelStatus InspectModel(string? root, LocalModelDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return new LocalModelStatus(
                definition,
                LocalModelAvailability.StorageUnavailable,
                "Model storage is unavailable.",
                "Fix app-local storage before importing models.",
                null,
                null,
                null,
                null);
        }

        var directory = ModelDirectory(root, definition);
        var manifestPath = Path.Combine(directory, ManifestFileName);
        var modelPath = Path.Combine(directory, definition.FileName);

        if (!File.Exists(modelPath))
        {
            return new LocalModelStatus(
                definition,
                LocalModelAvailability.Missing,
                "Not imported.",
                "Download manually from the approved source, then import the local ONNX file.",
                null,
                null,
                null,
                null);
        }

        try
        {
            var manifest = ReadManifest(manifestPath);
            var sha256 = manifest?.Sha256;
            var size = manifest?.SizeBytes;
            var imported = manifest?.ImportedUtc;
            if (string.IsNullOrWhiteSpace(sha256))
            {
                sha256 = ComputeSha256(modelPath);
                size = new FileInfo(modelPath).Length;
            }

            var availability = sha256.Equals(definition.ExpectedSha256, StringComparison.OrdinalIgnoreCase)
                ? LocalModelAvailability.Ready
                : LocalModelAvailability.HashMismatch;
            return new LocalModelStatus(
                definition,
                availability,
                availability == LocalModelAvailability.Ready
                    ? "Imported and SHA-256 verified."
                    : "Imported file does not match the approved SHA-256.",
                availability == LocalModelAvailability.Ready
                    ? "Ready for future model-backed tools once runtime code is enabled."
                    : "Delete this file and import the exact approved ONNX artifact.",
                modelPath,
                sha256,
                size,
                imported);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or JsonException)
        {
            return new LocalModelStatus(
                definition,
                LocalModelAvailability.ManifestInvalid,
                "Local model metadata could not be read.",
                "Delete and re-import the model file.",
                modelPath,
                null,
                null,
                null);
        }
    }

    private string? TryGetModelRoot()
    {
        try
        {
            return _getModelRoot();
        }
        catch
        {
            return null;
        }
    }

    private static string ModelDirectory(string root, LocalModelDefinition definition)
        => Path.Combine(root, SafePathSegment(definition.StorageGroup), definition.Id);

    private static string SafePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "general";

        var segment = string.Join(
            "_",
            value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return string.IsNullOrWhiteSpace(segment) ? "general" : segment;
    }

    private static void WriteManifest(string directory, LocalModelManifest manifest)
        => File.WriteAllText(
            Path.Combine(directory, ManifestFileName),
            JsonSerializer.Serialize(manifest, JsonOptions));

    private static LocalModelManifest? ReadManifest(string path)
    {
        if (!File.Exists(path))
            return null;

        return JsonSerializer.Deserialize<LocalModelManifest>(File.ReadAllText(path), JsonOptions);
    }

    private static bool IsAssemblyLoaded(string name)
        => AppDomain.CurrentDomain.GetAssemblies()
            .Any(assembly => assembly.GetName().Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true);

    private static string ComputeSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    internal static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)Math.Max(0, bytes);
        var index = 0;
        while (value >= 1024 && index < units.Length - 1)
        {
            value /= 1024;
            index++;
        }

        return index == 0
            ? $"{bytes} {units[index]}"
            : string.Create(CultureInfo.InvariantCulture, $"{value:0.##} {units[index]}");
    }

    private static bool SamePath(string left, string right)
    {
        try
        {
            return Path.GetFullPath(left).Equals(Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return left.Equals(right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort import-temp cleanup.
        }
    }

    private sealed record LocalModelManifest(
        string ModelId,
        string FileName,
        string InstalledPath,
        string Sha256,
        long SizeBytes,
        DateTimeOffset ImportedUtc,
        LocalModelAvailability Availability);
}
