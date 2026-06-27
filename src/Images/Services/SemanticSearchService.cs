using System.Globalization;
using System.IO;
using System.Security;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public sealed record SemanticAssetEmbeddingInput(CatalogAssetRecord Asset);

public interface ISemanticEmbeddingProvider
{
    string ProviderId { get; }
    string ModelId { get; }
    int Dimensions { get; }
    string StatusText { get; }
    IReadOnlyList<float> EmbedImage(SemanticAssetEmbeddingInput input);
    IReadOnlyList<float> EmbedText(string query);
    string DescribeAsset(CatalogAssetRecord asset);
}

public sealed record SemanticSearchIndexResult(
    string? IndexPath,
    IReadOnlyList<string> Roots,
    int CatalogedCount,
    int IndexedCount,
    int FailedCount,
    DateTimeOffset IndexedUtc);

public sealed record SemanticSearchResult(
    string SourcePath,
    string FileName,
    string Folder,
    double Score,
    string MatchedText,
    string Fingerprint,
    string ModelId,
    string ProviderId,
    DateTimeOffset IndexedUtc);

public sealed record SemanticSearchStatus(
    string? IndexPath,
    bool IsAvailable,
    int IndexedCount,
    DateTimeOffset? LastIndexedUtc,
    string ProviderId,
    string ModelId,
    int Dimensions,
    string ProviderStatus,
    string? ProviderFallbackReason);

public sealed class SemanticSearchService
{
    private const int CurrentSchemaVersion = 1;
    private static readonly ILogger Log = Images.Services.Log.Get(nameof(SemanticSearchService));
    private readonly string? _dbPath;
    private readonly string _connectionString;
    private readonly CatalogService _catalog;
    private readonly ISemanticEmbeddingProvider _provider;
    private readonly Func<DateTimeOffset> _clock;
    private readonly bool _isAvailable;

    public SemanticSearchService()
        : this(null)
    {
    }

    public SemanticSearchService(
        string? dbPath,
        CatalogService? catalog = null,
        ISemanticEmbeddingProvider? provider = null,
        Func<DateTimeOffset>? clock = null,
        Func<(ISemanticEmbeddingProvider? Provider, string? FallbackReason)>? clipProviderFactory = null)
    {
        _dbPath = string.IsNullOrWhiteSpace(dbPath)
            ? CreateDefaultPath()
            : Path.GetFullPath(dbPath);
        _catalog = catalog ?? new CatalogService();
        var clipProvider = provider is null
            ? (clipProviderFactory?.Invoke() ?? TryCreateClipProvider())
            : (provider, null);
        _provider = clipProvider.Provider ?? new DeterministicSemanticEmbeddingProvider();
        ClipFallbackReason = _provider is DeterministicSemanticEmbeddingProvider
            ? (clipProvider.FallbackReason ?? "CLIP models not available; using deterministic metadata embeddings.")
            : null;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);

        if (string.IsNullOrWhiteSpace(_dbPath))
        {
            _connectionString = "";
            _isAvailable = false;
            return;
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 5
        }.ToString();
        _isAvailable = TryEnsureSchema(_dbPath);
    }

    public string? IndexPath => _dbPath;
    public bool IsAvailable => _isAvailable;
    public ISemanticEmbeddingProvider Provider => _provider;

    public string? ClipFallbackReason { get; }

    public SemanticSearchIndexResult Rebuild(
        IEnumerable<string> roots,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roots);
        var indexedUtc = _clock();
        var rootList = NormalizeRoots(roots).ToArray();
        if (!_isAvailable || rootList.Length == 0)
            return new SemanticSearchIndexResult(_dbPath, rootList, 0, 0, 0, indexedUtc);

        var catalogResult = _catalog.Rebuild(rootList, cancellationToken);
        var rows = new List<SemanticIndexRow>();
        foreach (var asset in catalogResult.Assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var vector = Normalize(_provider.EmbedImage(new SemanticAssetEmbeddingInput(asset)));
                if (vector.Length != _provider.Dimensions || IsZeroVector(vector))
                    continue;

                rows.Add(new SemanticIndexRow(
                    asset.SourcePath,
                    asset.Fingerprint,
                    _provider.ModelId,
                    _provider.ProviderId,
                    _provider.Dimensions,
                    vector,
                    _provider.DescribeAsset(asset),
                    indexedUtc));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or InvalidOperationException or NotSupportedException)
            {
                Log.LogDebug(ex, "Could not embed semantic asset {Path}", asset.SourcePath);
            }
        }

        using var conn = Open();
        using var tx = conn.BeginTransaction();
        ClearIndex(conn, tx);
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UpsertRow(conn, tx, row);
        }
        foreach (var root in rootList)
            UpsertRoot(conn, tx, root, indexedUtc, rows.Count, catalogResult.FailedCount);
        tx.Commit();

        return new SemanticSearchIndexResult(
            _dbPath,
            rootList,
            catalogResult.IndexedCount,
            rows.Count,
            catalogResult.FailedCount,
            indexedUtc);
    }

    public IReadOnlyList<SemanticSearchResult> Search(
        string query,
        int limit = 50,
        string? folderFilter = null,
        CancellationToken cancellationToken = default)
    {
        if (!_isAvailable || string.IsNullOrWhiteSpace(query))
            return [];

        var queryVector = Normalize(_provider.EmbedText(query));
        if (queryVector.Length != _provider.Dimensions || IsZeroVector(queryVector))
            return [];

        var normalizedFilter = NormalizeFolderFilter(folderFilter);
        limit = Math.Clamp(limit, 1, 500);
        var results = new List<SemanticSearchResult>();

        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT source_path, fingerprint, model_id, provider_id, dimensions,
                       embedding, matched_text, indexed_utc
                FROM semantic_assets
                WHERE model_id = $model AND provider_id = $provider
                ORDER BY lower(source_path);
                """;
            cmd.Parameters.AddWithValue("$model", _provider.ModelId);
            cmd.Parameters.AddWithValue("$provider", _provider.ProviderId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourcePath = reader.GetString(0);
                if (!IsWithinFolderFilter(sourcePath, normalizedFilter))
                    continue;

                var vector = ReadVector((byte[])reader["embedding"], reader.GetInt32(4));
                if (vector.Length != queryVector.Length)
                    continue;

                var score = Dot(queryVector, vector);
                if (score <= 0)
                    continue;

                results.Add(new SemanticSearchResult(
                    sourcePath,
                    Path.GetFileName(sourcePath),
                    Path.GetDirectoryName(sourcePath) ?? "",
                    score,
                    reader.GetString(6),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    FromUnix(reader.GetInt64(7))));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or InvalidOperationException or NotSupportedException or SqliteException)
        {
            Log.LogWarning(ex, "Semantic search failed");
            return [];
        }

        return results
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.FileName, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    public SemanticSearchStatus GetStatus()
    {
        if (!_isAvailable)
        {
            return new SemanticSearchStatus(
                _dbPath,
                false,
                0,
                null,
                _provider.ProviderId,
                _provider.ModelId,
                _provider.Dimensions,
                _provider.StatusText,
                ClipFallbackReason);
        }

        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*), MAX(indexed_utc) FROM semantic_assets WHERE model_id = $model AND provider_id = $provider;";
            cmd.Parameters.AddWithValue("$model", _provider.ModelId);
            cmd.Parameters.AddWithValue("$provider", _provider.ProviderId);
            using var reader = cmd.ExecuteReader();
            var count = 0;
            DateTimeOffset? indexedUtc = null;
            if (reader.Read())
            {
                count = reader.GetInt32(0);
                if (!reader.IsDBNull(1))
                    indexedUtc = FromUnix(reader.GetInt64(1));
            }

            return new SemanticSearchStatus(
                _dbPath,
                true,
                count,
                indexedUtc,
                _provider.ProviderId,
                _provider.ModelId,
                _provider.Dimensions,
                _provider.StatusText,
                ClipFallbackReason);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or InvalidOperationException or NotSupportedException or SqliteException)
        {
            Log.LogWarning(ex, "Semantic search status failed");
            return new SemanticSearchStatus(_dbPath, false, 0, null, _provider.ProviderId, _provider.ModelId, _provider.Dimensions, _provider.StatusText, ClipFallbackReason);
        }
    }

    public void Clear()
    {
        if (!_isAvailable)
            return;

        using var conn = Open();
        using var tx = conn.BeginTransaction();
        ClearIndex(conn, tx);
        tx.Commit();
    }

    private static string? CreateDefaultPath()
    {
        var directory = AppStorage.TryGetAppDirectory();
        return directory is null ? null : Path.Combine(directory, "semantic-index.db");
    }

    private bool TryEnsureSchema(string dbPath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            EnsureSchema();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or InvalidOperationException or SqliteException)
        {
            Log.LogWarning(ex, "semantic-index.db unavailable; semantic search disabled for this session");
            return false;
        }
    }

    private void EnsureSchema()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA busy_timeout = 5000;
            PRAGMA journal_mode = WAL;
            """;
        cmd.ExecuteNonQuery();

        cmd.CommandText = "PRAGMA user_version;";
        var current = Convert.ToInt32(cmd.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
        if (current < 1)
            Migrate_0_to_1(conn);

        cmd.CommandText = $"PRAGMA user_version = {CurrentSchemaVersion};";
        cmd.ExecuteNonQuery();
    }

    private static void Migrate_0_to_1(SqliteConnection conn)
    {
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS semantic_assets (
                source_path  TEXT PRIMARY KEY NOT NULL COLLATE NOCASE,
                fingerprint  TEXT NOT NULL,
                model_id     TEXT NOT NULL,
                provider_id  TEXT NOT NULL,
                dimensions   INTEGER NOT NULL,
                embedding    BLOB NOT NULL,
                matched_text TEXT NOT NULL,
                indexed_utc  INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_semantic_assets_model ON semantic_assets(model_id, provider_id);
            CREATE INDEX IF NOT EXISTS ix_semantic_assets_indexed ON semantic_assets(indexed_utc DESC);

            CREATE TABLE IF NOT EXISTS semantic_roots (
                root_path        TEXT PRIMARY KEY NOT NULL COLLATE NOCASE,
                last_indexed_utc INTEGER NOT NULL,
                indexed_count    INTEGER NOT NULL,
                failed_count     INTEGER NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA busy_timeout = 5000;";
        cmd.ExecuteNonQuery();
        return conn;
    }

    private static void ClearIndex(SqliteConnection conn, SqliteTransaction tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            DELETE FROM semantic_assets;
            DELETE FROM semantic_roots;
            """;
        cmd.ExecuteNonQuery();
    }

    private static void UpsertRow(SqliteConnection conn, SqliteTransaction tx, SemanticIndexRow row)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO semantic_assets (
                source_path, fingerprint, model_id, provider_id, dimensions,
                embedding, matched_text, indexed_utc
            )
            VALUES (
                $path, $fingerprint, $model, $provider, $dimensions,
                $embedding, $matched, $indexed
            )
            ON CONFLICT(source_path) DO UPDATE SET
                fingerprint = excluded.fingerprint,
                model_id = excluded.model_id,
                provider_id = excluded.provider_id,
                dimensions = excluded.dimensions,
                embedding = excluded.embedding,
                matched_text = excluded.matched_text,
                indexed_utc = excluded.indexed_utc;
            """;
        cmd.Parameters.AddWithValue("$path", row.SourcePath);
        cmd.Parameters.AddWithValue("$fingerprint", row.Fingerprint);
        cmd.Parameters.AddWithValue("$model", row.ModelId);
        cmd.Parameters.AddWithValue("$provider", row.ProviderId);
        cmd.Parameters.AddWithValue("$dimensions", row.Dimensions);
        cmd.Parameters.Add("$embedding", SqliteType.Blob).Value = WriteVector(row.Vector);
        cmd.Parameters.AddWithValue("$matched", row.MatchedText);
        cmd.Parameters.AddWithValue("$indexed", row.IndexedUtc.ToUnixTimeSeconds());
        cmd.ExecuteNonQuery();
    }

    private static void UpsertRoot(
        SqliteConnection conn,
        SqliteTransaction tx,
        string root,
        DateTimeOffset indexedUtc,
        int indexedCount,
        int failedCount)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO semantic_roots (root_path, last_indexed_utc, indexed_count, failed_count)
            VALUES ($root, $indexed, $count, $failed)
            ON CONFLICT(root_path) DO UPDATE SET
                last_indexed_utc = excluded.last_indexed_utc,
                indexed_count = excluded.indexed_count,
                failed_count = excluded.failed_count;
            """;
        cmd.Parameters.AddWithValue("$root", root);
        cmd.Parameters.AddWithValue("$indexed", indexedUtc.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$count", indexedCount);
        cmd.Parameters.AddWithValue("$failed", failedCount);
        cmd.ExecuteNonQuery();
    }

    private static IReadOnlyList<string> NormalizeRoots(IEnumerable<string> roots)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;

            try
            {
                var fullPath = Path.GetFullPath(root);
                if ((File.Exists(fullPath) || Directory.Exists(fullPath)) && seen.Add(fullPath))
                    normalized.Add(fullPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
            }
        }

        return normalized;
    }

    private static string? NormalizeFolderFilter(string? folderFilter)
    {
        if (string.IsNullOrWhiteSpace(folderFilter))
            return null;

        try
        {
            return Path.GetFullPath(folderFilter);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool IsWithinFolderFilter(string sourcePath, string? normalizedFilter)
    {
        if (string.IsNullOrWhiteSpace(normalizedFilter))
            return true;

        try
        {
            var fullPath = Path.GetFullPath(sourcePath);
            return fullPath.Equals(normalizedFilter, StringComparison.OrdinalIgnoreCase) ||
                   fullPath.StartsWith(
                       normalizedFilter.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static float[] Normalize(IReadOnlyList<float> vector)
    {
        if (vector.Count == 0)
            return [];

        double sum = 0;
        foreach (var value in vector)
            sum += value * value;

        if (sum <= 0)
            return vector.ToArray();

        var scale = 1.0 / Math.Sqrt(sum);
        var normalized = new float[vector.Count];
        for (var i = 0; i < vector.Count; i++)
            normalized[i] = (float)(vector[i] * scale);
        return normalized;
    }

    private static bool IsZeroVector(IReadOnlyList<float> vector)
        => vector.All(value => Math.Abs(value) < 0.000001f);

    private static double Dot(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        var sum = 0.0;
        var count = Math.Min(left.Count, right.Count);
        for (var i = 0; i < count; i++)
            sum += left[i] * right[i];
        return sum;
    }

    private static byte[] WriteVector(IReadOnlyList<float> vector)
    {
        var bytes = new byte[vector.Count * sizeof(float)];
        var copy = vector.ToArray();
        Buffer.BlockCopy(copy, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] ReadVector(byte[] bytes, int dimensions)
    {
        if (bytes.Length != dimensions * sizeof(float))
            return [];

        var vector = new float[dimensions];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }

    private static DateTimeOffset FromUnix(long seconds)
        => DateTimeOffset.FromUnixTimeSeconds(seconds);

    private sealed record SemanticIndexRow(
        string SourcePath,
        string Fingerprint,
        string ModelId,
        string ProviderId,
        int Dimensions,
        IReadOnlyList<float> Vector,
        string MatchedText,
        DateTimeOffset IndexedUtc);

    private static (ISemanticEmbeddingProvider? Provider, string? FallbackReason) TryCreateClipProvider()
    {
        try
        {
            var provider = ClipEmbeddingProvider.TryCreate();
            if (provider is null)
            {
                var fallbackReason = "CLIP model files not ready or ONNX session creation failed.";
                Log.LogWarning("Semantic search falling back to deterministic embeddings: {Reason}", fallbackReason);
                return (null, fallbackReason);
            }

            return (provider, null);
        }
        catch (Exception ex)
        {
            var fallbackReason = $"CLIP provider creation failed: {ex.Message}";
            Log.LogWarning(ex, "Semantic search falling back to deterministic embeddings: {Reason}", fallbackReason);
            return (null, fallbackReason);
        }
    }
}

public sealed class DeterministicSemanticEmbeddingProvider : ISemanticEmbeddingProvider
{
    private const int VectorDimensions = 64;
    private static readonly IReadOnlyDictionary<string, string[]> Synonyms = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["photo"] = ["image", "picture", "pic"],
        ["image"] = ["photo", "picture", "pic"],
        ["landscape"] = ["wide", "horizontal"],
        ["horizontal"] = ["wide", "landscape"],
        ["portrait"] = ["vertical", "tall"],
        ["vertical"] = ["portrait", "tall"],
        ["square"] = ["even"],
        ["receipt"] = ["invoice", "paper", "document"],
        ["document"] = ["paper", "receipt", "scan"],
        ["transparent"] = ["alpha"],
        ["png"] = ["transparent", "lossless"],
        ["jpg"] = ["jpeg", "photo"],
        ["jpeg"] = ["jpg", "photo"],
        ["webp"] = ["web"],
        ["gif"] = ["animation", "animated"],
    };

    public string ProviderId => "deterministic-local-metadata";
    public string ModelId => "deterministic-local-v1";
    public int Dimensions => VectorDimensions;
    public string StatusText => "Deterministic local metadata embeddings; no model download or inference runtime is used.";

    public IReadOnlyList<float> EmbedImage(SemanticAssetEmbeddingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var asset = input.Asset;
        var tokens = new List<WeightedToken>();
        AddTextTokens(tokens, Path.GetFileNameWithoutExtension(asset.SourcePath), 3.0f);
        AddTextTokens(tokens, Path.GetDirectoryName(asset.SourcePath) ?? "", 1.0f);
        AddToken(tokens, asset.Format.ToLowerInvariant(), 2.5f);
        AddToken(tokens, asset.Codec.ToLowerInvariant(), 1.5f);
        AddToken(tokens, asset.Width > asset.Height ? "landscape" : asset.Height > asset.Width ? "portrait" : "square", 2.0f);
        AddToken(tokens, $"rating-{asset.Rating}", asset.Rating is null ? 0.0f : 1.5f);
        foreach (var tag in asset.Tags)
            AddTextTokens(tokens, tag, 3.0f);

        return BuildVector(tokens);
    }

    public IReadOnlyList<float> EmbedText(string query)
    {
        var tokens = new List<WeightedToken>();
        AddTextTokens(tokens, query, 3.0f);
        return BuildVector(tokens);
    }

    public string DescribeAsset(CatalogAssetRecord asset)
    {
        var parts = new List<string>
        {
            Path.GetFileNameWithoutExtension(asset.SourcePath),
            asset.Format,
            asset.Width > asset.Height ? "landscape" : asset.Height > asset.Width ? "portrait" : "square"
        };
        parts.AddRange(asset.Tags);
        if (asset.Rating is not null)
            parts.Add($"rating-{asset.Rating}");
        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static void AddTextTokens(List<WeightedToken> tokens, string text, float weight)
    {
        foreach (var token in Tokenize(text))
            AddToken(tokens, token, weight);
    }

    private static void AddToken(List<WeightedToken> tokens, string token, float weight)
    {
        if (string.IsNullOrWhiteSpace(token) || weight <= 0)
            return;

        tokens.Add(new WeightedToken(token, weight));
        if (Synonyms.TryGetValue(token, out var synonyms))
        {
            foreach (var synonym in synonyms)
                tokens.Add(new WeightedToken(synonym, weight * 0.65f));
        }
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        var buffer = new List<char>();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer.Add(char.ToLowerInvariant(ch));
                continue;
            }

            if (buffer.Count > 0)
            {
                yield return new string(buffer.ToArray());
                buffer.Clear();
            }
        }

        if (buffer.Count > 0)
            yield return new string(buffer.ToArray());
    }

    private static IReadOnlyList<float> BuildVector(IEnumerable<WeightedToken> tokens)
    {
        var vector = new float[VectorDimensions];
        foreach (var token in tokens)
        {
            var hash = StableHash(token.Token);
            var index = (int)(hash % VectorDimensions);
            var sign = (hash & 0x80000000) == 0 ? 1.0f : -1.0f;
            vector[index] += token.Weight * sign;
        }

        return vector;
    }

    private static uint StableHash(string value)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        foreach (var ch in value)
        {
            hash ^= ch;
            hash *= prime;
        }

        return hash;
    }

    private readonly record struct WeightedToken(string Token, float Weight);
}
