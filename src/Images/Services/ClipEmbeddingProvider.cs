using System.IO;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Images.Services;

public sealed class ClipEmbeddingProvider : ISemanticEmbeddingProvider, IDisposable
{
    public const string TextModelId = "qdrant-clip-vit-b32-text";
    public const string VisionModelId = "qdrant-clip-vit-b32-vision";
    public const string TokenizerId = "qdrant-clip-vit-b32-tokenizer";
    public const string PreprocessorId = "qdrant-clip-vit-b32-preprocessor";

    private const int EmbeddingDimensions = 512;

    private readonly InferenceSession _textSession;
    private readonly InferenceSession _visionSession;
    private readonly ClipTokenizer _tokenizer;
    private readonly ClipImagePreprocessor _preprocessor;
    private readonly string _statusText;
    private bool _disposed;

    private ClipEmbeddingProvider(
        InferenceSession textSession,
        InferenceSession visionSession,
        ClipTokenizer tokenizer,
        ClipImagePreprocessor preprocessor,
        string statusText)
    {
        _textSession = textSession;
        _visionSession = visionSession;
        _tokenizer = tokenizer;
        _preprocessor = preprocessor;
        _statusText = statusText;
    }

    public string ProviderId => "clip-onnx-local";
    public string ModelId => "qdrant-clip-vit-b32";
    public int Dimensions => EmbeddingDimensions;
    public string StatusText => _statusText;

    public static ClipEmbeddingProvider? TryCreate(ModelManagerService? modelManager = null)
    {
        var manager = modelManager ?? new ModelManagerService();
        var snapshot = manager.GetSnapshot();

        var textModel = snapshot.Models.FirstOrDefault(m => m.Definition.Id == TextModelId);
        var visionModel = snapshot.Models.FirstOrDefault(m => m.Definition.Id == VisionModelId);
        var tokenizerModel = snapshot.Models.FirstOrDefault(m => m.Definition.Id == TokenizerId);
        var preprocessorModel = snapshot.Models.FirstOrDefault(m => m.Definition.Id == PreprocessorId);

        if (textModel?.IsReady != true ||
            visionModel?.IsReady != true ||
            tokenizerModel?.IsReady != true ||
            preprocessorModel?.IsReady != true)
        {
            return null;
        }

        try
        {
            var tokenizer = ClipTokenizer.Load(tokenizerModel.InstalledPath!);
            var preprocessor = ClipImagePreprocessor.Load(preprocessorModel.InstalledPath!);

            var sessionOptions = OnnxRuntimeService.CreateSessionOptions();
            var textSession = new InferenceSession(textModel.InstalledPath!, sessionOptions);
            var visionSession = new InferenceSession(visionModel.InstalledPath!, sessionOptions);

            var statusText = $"CLIP ViT-B/32 ready ({OnnxRuntimeService.ProviderLabel})";

            return new ClipEmbeddingProvider(textSession, visionSession, tokenizer, preprocessor, statusText);
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyList<float> EmbedText(string query)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var tokenIds = _tokenizer.Encode(query);
        var inputTensor = new DenseTensor<long>(tokenIds, [1, ClipTokenizer.ContextLength]);

        var textInputName = _textSession.InputNames.FirstOrDefault() ?? "input_ids";
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(textInputName, inputTensor)
        };

        if (_textSession.InputNames.Count > 1)
        {
            var attentionMask = new long[ClipTokenizer.ContextLength];
            for (var i = 0; i < tokenIds.Length && tokenIds[i] != 0; i++)
                attentionMask[i] = 1;
            var maskTensor = new DenseTensor<long>(attentionMask, [1, ClipTokenizer.ContextLength]);
            inputs.Add(NamedOnnxValue.CreateFromTensor(
                _textSession.InputNames[1], maskTensor));
        }

        using var results = _textSession.Run(inputs);
        var outputName = _textSession.OutputNames.FirstOrDefault() ?? "output";
        var output = results.First(r => r.Name == outputName);
        var tensor = output.AsTensor<float>();

        var embedding = new float[EmbeddingDimensions];
        for (var i = 0; i < EmbeddingDimensions; i++)
            embedding[i] = tensor[0, i];

        return embedding;
    }

    public IReadOnlyList<float> EmbedImage(SemanticAssetEmbeddingInput input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var path = input.Asset.SourcePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return new float[EmbeddingDimensions];

        float[] pixelData;
        try
        {
            pixelData = _preprocessor.Preprocess(path);
        }
        catch
        {
            return new float[EmbeddingDimensions];
        }

        var inputTensor = new DenseTensor<float>(pixelData,
            [1, 3, ClipImagePreprocessor.ImageSize, ClipImagePreprocessor.ImageSize]);

        var visionInputName = _visionSession.InputNames.FirstOrDefault() ?? "pixel_values";
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(visionInputName, inputTensor)
        };

        using var results = _visionSession.Run(inputs);
        var outputName = _visionSession.OutputNames.FirstOrDefault() ?? "output";
        var output = results.First(r => r.Name == outputName);
        var tensor = output.AsTensor<float>();

        var embedding = new float[EmbeddingDimensions];
        for (var i = 0; i < EmbeddingDimensions; i++)
            embedding[i] = tensor[0, i];

        return embedding;
    }

    public string DescribeAsset(CatalogAssetRecord asset)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(asset.SourcePath))
            parts.Add(Path.GetFileNameWithoutExtension(asset.SourcePath));
        if (!string.IsNullOrWhiteSpace(asset.Format))
            parts.Add(asset.Format);
        return string.Join(" | ", parts);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _textSession.Dispose();
        _visionSession.Dispose();
    }
}
