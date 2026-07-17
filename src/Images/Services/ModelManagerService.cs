using System.Globalization;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Images.Localization;

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
        ? Strings.ModelManagerNotImported
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
    public string RegistrySummary => Strings.Format(nameof(Strings.ModelRegistrySummaryFormat), ReadyCount, TotalCount);
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

    public static IReadOnlyList<LocalModelDefinition> ApprovedModels =>
    [
        new(
            Id: "opencv-inpainting-lama-2025jan",
            DisplayName: "OpenCV LaMa ONNX",
            Purpose: Strings.ModelOpenCvPurpose,
            StorageGroup: "inpaint",
            SourceUrl: "https://huggingface.co/opencv/inpainting_lama",
            DownloadUrl: "https://huggingface.co/opencv/inpainting_lama/resolve/main/inpainting_lama_2025jan.onnx",
            License: Strings.ModelOpenCvLicense,
            FileName: "inpainting_lama_2025jan.onnx",
            ExpectedSha256: "7df918ac3921d3daf0aae1d219776cf0dc4e4935f035af81841b40adcf74fdf2",
            ExpectedSizeBytes: 92_600_000,
            RuntimeContract: Strings.ModelOpenCvRuntime,
            Notes: Strings.ModelOpenCvNotes),
        new(
            Id: "carve-lama-fp32",
            DisplayName: "Carve LaMa FP32 ONNX",
            Purpose: Strings.ModelCarvePurpose,
            StorageGroup: "inpaint",
            SourceUrl: "https://huggingface.co/Carve/LaMa-ONNX",
            DownloadUrl: "https://huggingface.co/Carve/LaMa-ONNX/resolve/main/lama_fp32.onnx",
            License: Strings.ModelCarveLicense,
            FileName: "lama_fp32.onnx",
            ExpectedSha256: "1faef5301d78db7dda502fe59966957ec4b79dd64e16f03ed96913c7a4eb68d6",
            ExpectedSizeBytes: 208_000_000,
            RuntimeContract: Strings.ModelCarveRuntime,
            Notes: Strings.ModelCarveNotes),
        new(
            Id: "qdrant-clip-vit-b32-text",
            DisplayName: "Qdrant CLIP ViT-B/32 Text ONNX",
            Purpose: Strings.ModelClipTextPurpose,
            StorageGroup: "semantic",
            SourceUrl: "https://huggingface.co/Qdrant/clip-ViT-B-32-text",
            DownloadUrl: "https://huggingface.co/Qdrant/clip-ViT-B-32-text/resolve/main/model.onnx",
            License: Strings.ModelMitLicense,
            FileName: "clip-vit-b32-text.onnx",
            ExpectedSha256: "4dbe762b11e36488304471e439cde89da053ad7acaddbf9e096745d142ec8d8b",
            ExpectedSizeBytes: 254_102_519,
            RuntimeContract: Strings.ModelClipTextRuntime,
            Notes: Strings.ModelClipTextNotes),
        new(
            Id: "qdrant-clip-vit-b32-tokenizer",
            DisplayName: "Qdrant CLIP ViT-B/32 Tokenizer",
            Purpose: Strings.ModelClipTokenizerPurpose,
            StorageGroup: "semantic",
            SourceUrl: "https://huggingface.co/Qdrant/clip-ViT-B-32-text",
            DownloadUrl: "https://huggingface.co/Qdrant/clip-ViT-B-32-text/resolve/main/tokenizer.json",
            License: Strings.ModelMitLicense,
            FileName: "clip-vit-b32-tokenizer.json",
            ExpectedSha256: "b68d571997a1f81bf521fb73806740ddb91e4ed6666cb6e996c066bb289cf55b",
            ExpectedSizeBytes: 2_224_147,
            RuntimeContract: Strings.ModelClipTokenizerRuntime,
            Notes: Strings.ModelClipTokenizerNotes),
        new(
            Id: "qdrant-clip-vit-b32-vision",
            DisplayName: "Qdrant CLIP ViT-B/32 Vision ONNX",
            Purpose: Strings.ModelClipVisionPurpose,
            StorageGroup: "semantic",
            SourceUrl: "https://huggingface.co/Qdrant/clip-ViT-B-32-vision",
            DownloadUrl: "https://huggingface.co/Qdrant/clip-ViT-B-32-vision/resolve/main/model.onnx",
            License: Strings.ModelMitLicense,
            FileName: "clip-vit-b32-vision.onnx",
            ExpectedSha256: "c68d3d9a200ddd2a8c8a5510b576d4c94d1ae383bf8b36dd8c084f94e1fb4d63",
            ExpectedSizeBytes: 352_000_000,
            RuntimeContract: Strings.ModelClipVisionRuntime,
            Notes: Strings.ModelClipVisionNotes),
        new(
            Id: "qdrant-clip-vit-b32-preprocessor",
            DisplayName: "Qdrant CLIP ViT-B/32 Vision Preprocessor",
            Purpose: Strings.ModelClipPreprocessorPurpose,
            StorageGroup: "semantic",
            SourceUrl: "https://huggingface.co/Qdrant/clip-ViT-B-32-vision",
            DownloadUrl: "https://huggingface.co/Qdrant/clip-ViT-B-32-vision/resolve/main/preprocessor_config.json",
            License: Strings.ModelMitLicense,
            FileName: "clip-vit-b32-preprocessor_config.json",
            ExpectedSha256: "ce945ef831c9972c135b5b198a03d8eeb70478cd69c0238f24caf1903a9965e6",
            ExpectedSizeBytes: 780,
            RuntimeContract: Strings.ModelClipPreprocessorRuntime,
            Notes: Strings.ModelClipPreprocessorNotes),
        new(
            Id: "opencv-face-detection-yunet-2023mar",
            DisplayName: "OpenCV YuNet Face Detection",
            Purpose: Strings.ModelFaceDetectionPurpose,
            StorageGroup: "faces",
            SourceUrl: "https://huggingface.co/opencv/face_detection_yunet",
            DownloadUrl: "https://huggingface.co/opencv/face_detection_yunet/resolve/3cc26e7f1014a5ee5d74a42acee58bafc9d0a310/face_detection_yunet_2023mar.onnx",
            License: Strings.ModelMitLicense,
            FileName: "face_detection_yunet_2023mar.onnx",
            ExpectedSha256: "8f2383e4dd3cfbb4553ea8718107fc0423210dc964f9f4280604804ed2552fa4",
            ExpectedSizeBytes: 232_589,
            RuntimeContract: Strings.ModelFaceDetectionRuntime,
            Notes: Strings.ModelFaceDetectionNotes),
        new(
            Id: "opencv-face-recognition-sface-2021dec",
            DisplayName: "OpenCV SFace Recognition",
            Purpose: Strings.ModelFaceRecognitionPurpose,
            StorageGroup: "faces",
            SourceUrl: "https://huggingface.co/opencv/face_recognition_sface",
            DownloadUrl: "https://huggingface.co/opencv/face_recognition_sface/resolve/3d7082438a6e4551e840c9b2bb60b71e8da4b524/face_recognition_sface_2021dec.onnx",
            License: Strings.ModelOpenCvLicense,
            FileName: "face_recognition_sface_2021dec.onnx",
            ExpectedSha256: "0ba9fbfa01b5270c96627c4ef784da859931e02f04419c829e83484087c34e79",
            ExpectedSizeBytes: 38_696_353,
            RuntimeContract: Strings.ModelFaceRecognitionRuntime,
            Notes: Strings.ModelFaceRecognitionNotes),
        new(
            Id: "opencv-object-detection-yolox-2022nov",
            DisplayName: "OpenCV YOLOX-S Object Detection",
            Purpose: Strings.ModelObjectDetectionPurpose,
            StorageGroup: "objects",
            SourceUrl: "https://huggingface.co/opencv/object_detection_yolox",
            DownloadUrl: "https://huggingface.co/opencv/object_detection_yolox/resolve/78c368f74ce73ee28fc7a1be418a598c71b58b52/object_detection_yolox_2022nov.onnx",
            License: Strings.ModelOpenCvLicense,
            FileName: "object_detection_yolox_2022nov.onnx",
            ExpectedSha256: "c5c2d13e59ae883e6af3b45daea64af4833a4951c92d116ec270d9ddbe998063",
            ExpectedSizeBytes: 35_858_002,
            RuntimeContract: Strings.ModelObjectDetectionRuntime,
            Notes: Strings.ModelObjectDetectionNotes),
        new(
            Id: "fachuan-orientation-convnextv2-2026jun",
            DisplayName: "Fachuan ConvNeXtV2 Orientation Classifier",
            Purpose: Strings.ModelOrientationPurpose,
            StorageGroup: "orientation",
            SourceUrl: "https://huggingface.co/Fachuan/orientation-classifier",
            DownloadUrl: "https://huggingface.co/Fachuan/orientation-classifier/resolve/f21ab96006ad10e6388024751d1b829f5b8ab2c9/fachuan-orientation-classifier.onnx",
            License: Strings.ModelMitLicense,
            FileName: "fachuan-orientation-classifier.onnx",
            ExpectedSha256: "50ec8fd24fb08e23aaac8ae657f2756c9251b5f052b00a1e3af8c128e4796b54",
            ExpectedSizeBytes: 13_671_697,
            RuntimeContract: Strings.ModelOrientationRuntime,
            Notes: Strings.ModelOrientationNotes)
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
                Strings.ModelDefinitionNotApproved);
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return new LocalModelImportResult(
                LocalModelImportStatus.SourceMissing,
                null,
                Strings.ModelChooseExistingFile);
        }

        var root = TryGetModelRoot();
        if (string.IsNullOrWhiteSpace(root))
        {
            return new LocalModelImportResult(
                LocalModelImportStatus.StorageUnavailable,
                null,
                Strings.ModelManagerStorageNotAvailable);
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
                var modifiedUtc = ToOffset(info.LastWriteTimeUtc);
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
                    availability,
                    modifiedUtc));

                var status = InspectModel(root, definition);
                return new LocalModelImportResult(
                    availability == LocalModelAvailability.Ready
                        ? LocalModelImportStatus.Imported
                        : LocalModelImportStatus.HashMismatch,
                    status,
                    availability == LocalModelAvailability.Ready
                        ? Strings.Format(nameof(Strings.ModelImportedVerifiedFormat), definition.DisplayName)
                        : Strings.Format(nameof(Strings.ModelImportedHashMismatchFormat), definition.DisplayName));
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
                Strings.Format(nameof(Strings.ModelImportFailedFormat), ex.Message));
        }
    }

    public bool DeleteLocalModel(string modelId, out string message)
    {
        var definition = _definitions.FirstOrDefault(item => item.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase));
        if (definition is null)
        {
            message = Strings.ModelDefinitionNotApproved;
            return false;
        }

        var root = TryGetModelRoot();
        if (string.IsNullOrWhiteSpace(root))
        {
            message = Strings.ModelManagerStorageNotAvailable;
            return false;
        }

        try
        {
            var directory = ModelDirectory(root, definition);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);

            message = Strings.Format(nameof(Strings.ModelRemovedFormat), definition.DisplayName);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
        {
            message = Strings.Format(nameof(Strings.ModelDeleteFailedFormat), ex.Message);
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
            return new ModelValidationResult(false, Strings.ModelClipDefinitionsMissing, steps);

        var textModel = InspectModel(root, textDef);
        steps.Add(new(Strings.ModelValidationTextModel, textModel.IsReady,
            textModel.IsReady ? Strings.ModelValidationReady : Strings.Format(nameof(Strings.ModelValidationNotAvailableFormat), textModel.StatusText)));

        var visionModel = InspectModel(root, visionDef);
        steps.Add(new(Strings.ModelValidationVisionModel, visionModel.IsReady,
            visionModel.IsReady ? Strings.ModelValidationReady : Strings.Format(nameof(Strings.ModelValidationNotAvailableFormat), visionModel.StatusText)));

        var tokenizer = InspectModel(root, tokenizerDef);
        steps.Add(new(Strings.ModelValidationTokenizer, tokenizer.IsReady,
            tokenizer.IsReady ? Strings.ModelValidationReady : Strings.Format(nameof(Strings.ModelValidationNotAvailableFormat), tokenizer.StatusText)));

        var preprocessor = InspectModel(root, preprocessorDef);
        steps.Add(new(Strings.ModelValidationPreprocessor, preprocessor.IsReady,
            preprocessor.IsReady ? Strings.ModelValidationReady : Strings.Format(nameof(Strings.ModelValidationNotAvailableFormat), preprocessor.StatusText)));

        if (steps.Any(s => !s.Passed))
        {
            return new ModelValidationResult(false,
                Strings.Format(nameof(Strings.ModelValidationMissingFilesFormat), steps.Count(s => !s.Passed), 4),
                steps);
        }

        try
        {
            var tokenizerObj = ClipTokenizer.Load(tokenizer.InstalledPath!);
            var tokens = tokenizerObj.Encode("test validation input");
            steps.Add(new(Strings.ModelValidationTokenizerEncode, tokens.Length > 0,
                tokens.Length > 0
                    ? Strings.Format(nameof(Strings.ModelValidationEncodedTokensFormat), tokens.Length)
                    : Strings.ModelValidationTokenizerEmpty));
        }
        catch (Exception ex)
        {
            steps.Add(new(Strings.ModelValidationTokenizerEncode, false, Strings.Format(nameof(Strings.ModelValidationFailedFormat), ex.Message)));
            return new ModelValidationResult(false, Strings.Format(nameof(Strings.ModelValidationTokenizerFailedFormat), ex.Message), steps);
        }

        try
        {
            ClipImagePreprocessor.Load(preprocessor.InstalledPath!);
            steps.Add(new(Strings.ModelValidationPreprocessorLoad, true, Strings.ModelValidationConfigParsed));
        }
        catch (Exception ex)
        {
            steps.Add(new(Strings.ModelValidationPreprocessorLoad, false, Strings.Format(nameof(Strings.ModelValidationFailedFormat), ex.Message)));
            return new ModelValidationResult(false, Strings.Format(nameof(Strings.ModelValidationPreprocessorFailedFormat), ex.Message), steps);
        }

        try
        {
            var provider = ClipEmbeddingProvider.TryCreate(this);
            if (provider is null)
            {
                steps.Add(new(Strings.ModelValidationOnnxSession, false, Strings.ModelValidationProviderNull));
                return new ModelValidationResult(false, Strings.ModelValidationProviderFailed, steps);
            }

            steps.Add(new(Strings.ModelValidationOnnxSession, true, Strings.ModelValidationSessionsCreated));

            var textEmbed = provider.EmbedText("test");
            var textOk = textEmbed.Count > 0 && textEmbed.Any(v => Math.Abs(v) > 0.000001f);
            steps.Add(new(Strings.ModelValidationTextEmbedding, textOk,
                textOk
                    ? Strings.Format(nameof(Strings.ModelValidationVectorFormat), textEmbed.Count)
                    : Strings.ModelValidationEmptyEmbedding));

            provider.Dispose();
        }
        catch (Exception ex)
        {
            steps.Add(new(Strings.ModelValidationOnnxInference, false, Strings.Format(nameof(Strings.ModelValidationFailedFormat), ex.Message)));
            return new ModelValidationResult(false, Strings.Format(nameof(Strings.ModelValidationInferenceFailedFormat), ex.Message), steps);
        }

        return new ModelValidationResult(true,
            Strings.Format(nameof(Strings.ModelValidationSuccessFormat), steps.Count), steps);
    }

    private LocalModelRuntimeStatus InspectRuntime()
    {
        var osVersion = _getOsVersion();
        var windowsMlOsCandidate = OperatingSystem.IsWindows() && osVersion >= new Version(10, 0, 26100);
        // This build has a compile-time package reference; assembly-load timing is not a reliable
        // readiness signal because the WinRT projection is loaded lazily by the catalog probe.
        var windowsMlReferenced = true;
        var onnxDirectMlReferenced = IsAssemblyLoaded("Microsoft.ML.OnnxRuntime");
        var providerLabel = OnnxRuntimeService.ProviderDetail;
        var preferred = OnnxRuntimeService.AvailablePaths.FirstOrDefault()?.DetailLabel ?? Strings.ModelRuntimeUnavailable;
        var status = windowsMlReferenced && OnnxRuntimeService.WindowsMlCatalogAvailable
            ? Strings.ModelRuntimeWindowsMlReferenced
            : Strings.Format(
                windowsMlReferenced
                    ? nameof(Strings.ModelRuntimeActiveProviderWindowsMlFormat)
                    : nameof(Strings.ModelRuntimeActiveProviderOnnxFormat),
                providerLabel);

        var rows = new[]
        {
            new MetadataFact(Strings.ModelRuntimeActiveProvider, providerLabel),
            new MetadataFact(Strings.ModelRuntimePreferred, preferred),
            new MetadataFact(
                Strings.ModelRuntimeWindowsMlOs,
                windowsMlOsCandidate ? Strings.ModelRuntimeWindowsMlOsCandidate : Strings.ModelRuntimeWindowsMlOsNotCandidate),
            new MetadataFact(
                Strings.ModelRuntimeWindowsMlReference,
                windowsMlReferenced ? Strings.ModelRuntimeReferenced : Strings.ModelRuntimeNotReferenced),
            new MetadataFact(
                Strings.ModelRuntimeOnnxDirectMlReference,
                onnxDirectMlReferenced ? Strings.ModelRuntimeReferenced : Strings.ModelRuntimeNotReferenced)
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
                Strings.ModelStorageUnavailable,
                Strings.ModelStorageFixAction,
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
                Strings.ModelNotImportedStatus,
                Strings.ModelImportAction,
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
            var info = new FileInfo(modelPath);
            var modifiedUtc = ToOffset(info.LastWriteTimeUtc);
            var refreshManifest = false;

            // The manifest hash was computed at import time; if the file has
            // drifted since (truncated write, disk error, manual replacement),
            // trusting it would report "SHA-256 verified" for corrupt bytes.
            // Size and mtime probes are near-free; rehash on drift.
            if (!string.IsNullOrWhiteSpace(sha256) && size is not null &&
                (info.Length != size.Value || !SameModifiedUtc(manifest?.ModifiedUtc, modifiedUtc)))
            {
                sha256 = null;
                refreshManifest = true;
            }

            if (string.IsNullOrWhiteSpace(sha256))
            {
                sha256 = ComputeSha256(modelPath);
                info.Refresh();
                size = info.Length;
                modifiedUtc = ToOffset(info.LastWriteTimeUtc);
                refreshManifest = manifest is not null;
            }

            var availability = sha256.Equals(definition.ExpectedSha256, StringComparison.OrdinalIgnoreCase)
                ? LocalModelAvailability.Ready
                : LocalModelAvailability.HashMismatch;
            if (refreshManifest)
            {
                TryWriteManifest(directory, new LocalModelManifest(
                    definition.Id,
                    definition.FileName,
                    modelPath,
                    sha256,
                    size.GetValueOrDefault(info.Length),
                    imported ?? _clock(),
                    availability,
                    modifiedUtc));
            }

            return new LocalModelStatus(
                definition,
                availability,
                availability == LocalModelAvailability.Ready
                    ? Strings.ModelImportedVerifiedStatus
                    : Strings.ModelImportedHashMismatchStatus,
                availability == LocalModelAvailability.Ready
                    ? Strings.ModelReadyAction
                    : Strings.ModelHashMismatchAction,
                modelPath,
                sha256,
                size,
                imported);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or SecurityException or JsonException)
        {
            return new LocalModelStatus(
                definition,
                LocalModelAvailability.ManifestInvalid,
                Strings.ModelMetadataUnreadable,
                Strings.ModelReimportAction,
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

    private static void TryWriteManifest(string directory, LocalModelManifest manifest)
    {
        try
        {
            WriteManifest(directory, manifest);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
        {
            // The model status still reflects the freshly computed hash.
        }
    }

    private static LocalModelManifest? ReadManifest(string path)
    {
        if (!File.Exists(path))
            return null;

        var json = BoundedTextFileReader.ReadUtf8(
            path,
            BoundedTextFileReader.MaxServiceMetadataBytes,
            "Model manifest");
        return JsonSerializer.Deserialize<LocalModelManifest>(json, JsonOptions);
    }

    private static bool IsAssemblyLoaded(string name)
        => AppDomain.CurrentDomain.GetAssemblies()
            .Any(assembly => assembly.GetName().Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true);

    private static string ComputeSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static DateTimeOffset ToOffset(DateTime utc)
    {
        var normalized = utc.Kind == DateTimeKind.Utc
            ? utc
            : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return new DateTimeOffset(normalized);
    }

    private static bool SameModifiedUtc(DateTimeOffset? cached, DateTimeOffset disk)
        => cached is not null && cached.Value.ToUniversalTime().Ticks == disk.ToUniversalTime().Ticks;

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
        LocalModelAvailability Availability,
        DateTimeOffset? ModifiedUtc = null);
}
