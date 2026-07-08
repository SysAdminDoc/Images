using System.IO;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public sealed class ClipEmbeddingProvider : ISemanticEmbeddingProvider, IDisposable
{
    public const string TextModelId = "qdrant-clip-vit-b32-text";
    public const string VisionModelId = "qdrant-clip-vit-b32-vision";
    public const string TokenizerId = "qdrant-clip-vit-b32-tokenizer";
    public const string PreprocessorId = "qdrant-clip-vit-b32-preprocessor";

    private const int EmbeddingDimensions = 512;
    private static readonly ILogger _log = Log.Get(nameof(ClipEmbeddingProvider));

    private readonly InferenceSession _textSession;
    private readonly InferenceSession _visionSession;
    private readonly ClipTokenizer _tokenizer;
    private readonly ClipImagePreprocessor _preprocessor;
    private readonly string _statusText;
    private readonly ReaderWriterLockSlim _sessionLock = new();
    private volatile bool _disposed;

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
            _log.LogWarning(
                "CLIP provider unavailable: text={TextReady}, vision={VisionReady}, tokenizer={TokenizerReady}, preprocessor={PreprocessorReady}",
                textModel?.IsReady == true,
                visionModel?.IsReady == true,
                tokenizerModel?.IsReady == true,
                preprocessorModel?.IsReady == true);
            return null;
        }

        try
        {
            var tokenizer = ClipTokenizer.Load(tokenizerModel.InstalledPath!);
            var preprocessor = ClipImagePreprocessor.Load(preprocessorModel.InstalledPath!);

            using var sessionOptions = OnnxRuntimeService.CreateSessionOptions();
            var (textSession, visionSession) = CreateSessionPair(
                sessionOptions,
                options => new InferenceSession(textModel.InstalledPath!, options),
                options => new InferenceSession(visionModel.InstalledPath!, options),
                session => session.Dispose());

            var statusText = $"CLIP ViT-B/32 ready ({OnnxRuntimeService.ProviderLabel})";

            return new ClipEmbeddingProvider(textSession, visionSession, tokenizer, preprocessor, statusText);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "CLIP provider creation failed");
            return null;
        }
    }

    public IReadOnlyList<float> EmbedText(string query)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var tokenIds = _tokenizer.Encode(query);
        var inputTensor = new DenseTensor<long>(tokenIds, [1, ClipTokenizer.ContextLength]);

        _sessionLock.EnterReadLock();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var textInputName = _textSession.InputNames.FirstOrDefault() ?? "input_ids";
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(textInputName, inputTensor)
            };

            if (_textSession.InputNames.Count > 1)
            {
                var attentionMask = BuildAttentionMask(tokenIds);
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
        finally
        {
            _sessionLock.ExitReadLock();
        }
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
        catch (Exception ex)
        {
            _log.LogWarning(ex, "CLIP image preprocessing failed for {Path}", path);
            return new float[EmbeddingDimensions];
        }

        var inputTensor = new DenseTensor<float>(pixelData,
            [1, 3, ClipImagePreprocessor.ImageSize, ClipImagePreprocessor.ImageSize]);

        _sessionLock.EnterReadLock();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

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
        finally
        {
            _sessionLock.ExitReadLock();
        }
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
        _sessionLock.EnterWriteLock();
        try
        {
            if (_disposed) return;
            _disposed = true;
            _textSession.Dispose();
            _visionSession.Dispose();
        }
        finally
        {
            _sessionLock.ExitWriteLock();
        }
    }

    internal static long[] BuildAttentionMask(IReadOnlyList<long> tokenIds)
    {
        var attentionMask = new long[ClipTokenizer.ContextLength];
        var count = Math.Min(tokenIds.Count, ClipTokenizer.ContextLength);
        for (var i = 0; i < count; i++)
        {
            attentionMask[i] = 1;
            if (tokenIds[i] == ClipTokenizer.EndOfTextId)
                break;
        }

        return attentionMask;
    }

    internal static (TSession TextSession, TSession VisionSession) CreateSessionPair<TOptions, TSession>(
        TOptions sessionOptions,
        Func<TOptions, TSession> createTextSession,
        Func<TOptions, TSession> createVisionSession,
        Action<TSession> disposeSession)
        where TSession : class
    {
        ArgumentNullException.ThrowIfNull(createTextSession);
        ArgumentNullException.ThrowIfNull(createVisionSession);
        ArgumentNullException.ThrowIfNull(disposeSession);

        TSession? textSession = null;
        TSession? visionSession = null;
        try
        {
            textSession = createTextSession(sessionOptions);
            visionSession = createVisionSession(sessionOptions);
            return (textSession, visionSession);
        }
        catch
        {
            DisposePartialSession(visionSession, disposeSession);
            DisposePartialSession(textSession, disposeSession);
            throw;
        }
    }

    private static void DisposePartialSession<TSession>(TSession? session, Action<TSession> disposeSession)
        where TSession : class
    {
        if (session is null)
            return;

        try
        {
            disposeSession(session);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "CLIP provider partial session cleanup failed");
        }
    }
}
