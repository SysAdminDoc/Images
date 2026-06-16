using System.Globalization;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Xml.Linq;
using ImageMagick;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public sealed record CatalogAssetRecord(
    string SourcePath,
    string Fingerprint,
    long SizeBytes,
    DateTimeOffset CreatedUtc,
    DateTimeOffset ModifiedUtc,
    int Width,
    int Height,
    string Format,
    string Codec,
    int? Rating,
    IReadOnlyList<string> Tags,
    string? SidecarPath,
    DateTimeOffset? SidecarModifiedUtc,
    DateTimeOffset ScannedUtc)
{
    public string FileName => Path.GetFileName(SourcePath);
    public string Folder => Path.GetDirectoryName(SourcePath) ?? "";
    public string DimensionsText => Width > 0 && Height > 0 ? $"{Width} x {Height}" : "Unknown";
}

public sealed record CatalogRebuildResult(
    string? CatalogPath,
    IReadOnlyList<string> Roots,
    IReadOnlyList<CatalogAssetRecord> Assets,
    int FailedCount,
    DateTimeOffset ScannedUtc)
{
    public int IndexedCount => Assets.Count;
}

public sealed class CatalogService
{
    private const int CurrentSchemaVersion = 1;
    private const int SchemaCanaryId = 1;
    private static readonly ILogger Log = Images.Services.Log.Get(nameof(CatalogService));
    private readonly string? _dbPath;
    private readonly string _connectionString;
    private readonly bool _isAvailable;

    public CatalogService()
        : this(CreateDefaultPath())
    {
    }

    public CatalogService(string? dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            _dbPath = null;
            _connectionString = "";
            _isAvailable = false;
            return;
        }

        _dbPath = Path.GetFullPath(dbPath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 5
        }.ToString();
        _isAvailable = TryEnsureSchema(_dbPath);
    }

    public string? CatalogPath => _dbPath;
    public bool IsAvailable => _isAvailable;

    public CatalogRebuildResult Rebuild(
        IEnumerable<string> roots,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roots);
        var scannedUtc = DateTimeOffset.UtcNow;
        var normalizedRoots = NormalizeRoots(roots).ToList();
        if (!_isAvailable || normalizedRoots.Count == 0)
            return new CatalogRebuildResult(_dbPath, normalizedRoots, [], 0, scannedUtc);

        var failed = 0;
        var assets = new List<CatalogAssetRecord>();
        foreach (var path in CollectCandidateFiles(normalizedRoots, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                assets.Add(BuildAsset(path, scannedUtc, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or InvalidOperationException or NotSupportedException or MagickException)
            {
                failed++;
                Log.LogDebug(ex, "Could not catalog asset {Path}", path);
            }
        }

        using var conn = Open();
        using var tx = conn.BeginTransaction();
        ClearCatalog(conn, tx);
        foreach (var asset in assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UpsertAsset(conn, tx, asset);
        }
        foreach (var root in normalizedRoots)
            UpsertRoot(conn, tx, root, scannedUtc, assets.Count, failed);
        tx.Commit();

        return new CatalogRebuildResult(_dbPath, normalizedRoots, assets, failed, scannedUtc);
    }

    public CatalogAssetRecord? GetByPath(string sourcePath)
    {
        if (!_isAvailable || string.IsNullOrWhiteSpace(sourcePath))
            return null;

        try
        {
            var normalizedPath = Path.GetFullPath(sourcePath);
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT id, source_path, fingerprint, size_bytes, created_utc, modified_utc,
                       width, height, format, codec, rating, sidecar_path, sidecar_modified_utc, scanned_utc
                FROM catalog_assets
                WHERE lower(source_path) = lower($path)
                LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("$path", normalizedPath);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return null;

            return ReadAsset(conn, reader);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or InvalidOperationException or NotSupportedException or SqliteException)
        {
            Log.LogWarning(ex, "Could not read catalog asset {Path}", sourcePath);
            return null;
        }
    }

    public IReadOnlyList<CatalogAssetRecord> GetAllAssets(int limit = 1000)
    {
        var assets = new List<CatalogAssetRecord>();
        if (!_isAvailable || limit <= 0)
            return assets;

        limit = Math.Min(limit, 50_000);
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT id, source_path, fingerprint, size_bytes, created_utc, modified_utc,
                       width, height, format, codec, rating, sidecar_path, sidecar_modified_utc, scanned_utc
                FROM catalog_assets
                ORDER BY lower(source_path)
                LIMIT $limit;
                """;
            cmd.Parameters.AddWithValue("$limit", limit);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                assets.Add(ReadAsset(conn, reader));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or InvalidOperationException or NotSupportedException or SqliteException)
        {
            Log.LogWarning(ex, "Could not read catalog assets");
        }

        return assets;
    }

    public void Clear()
    {
        if (!_isAvailable)
            return;

        using var conn = Open();
        using var tx = conn.BeginTransaction();
        ClearCatalog(conn, tx);
        tx.Commit();
    }

    private static string? CreateDefaultPath()
    {
        var directory = AppStorage.TryGetAppDirectory();
        return directory is null ? null : Path.Combine(directory, "catalog.db");
    }

    private bool TryEnsureSchema(string dbPath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            EnsureSchema(dbPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or InvalidOperationException or SqliteException)
        {
            Log.LogWarning(ex, "catalog.db unavailable; catalog cache disabled for this session");
            return false;
        }
    }

    private void EnsureSchema(string dbPath)
    {
        var hadExistingDatabase = File.Exists(dbPath) && new FileInfo(dbPath).Length > 0;
        var current = ReadSchemaVersion();

        if (current > CurrentSchemaVersion)
            throw new InvalidOperationException($"catalog.db schema v{current} is newer than this app supports (v{CurrentSchemaVersion}).");

        for (var version = current; version < CurrentSchemaVersion; version++)
            ApplyGuardedMigration(dbPath, version, version + 1, hadExistingDatabase || version > 0);

        using var conn = Open();
        EnsureSchemaCanary(conn, CurrentSchemaVersion);
        AssertSchemaCanary(conn, CurrentSchemaVersion);
        ValidateIntegrity(conn);
    }

    private int ReadSchemaVersion()
    {
        using var conn = Open();
        ConfigureCatalogPragmas(conn);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
    }

    private void ApplyGuardedMigration(string dbPath, int fromVersion, int toVersion, bool createBackup)
    {
        string? backupPath = null;
        try
        {
            using (var preflight = Open())
            {
                ConfigureCatalogPragmas(preflight);
                ValidateIntegrity(preflight);
                CheckpointWal(preflight);
            }

            SqliteConnection.ClearAllPools();
            if (createBackup)
                backupPath = CreateMigrationBackup(dbPath, fromVersion, toVersion);

            using (var conn = Open())
            {
                ConfigureCatalogPragmas(conn);
                using var tx = conn.BeginTransaction();
                RunMigration(conn, tx, fromVersion, toVersion);
                SetSchemaVersion(conn, tx, toVersion);
                tx.Commit();

                EnsureSchemaCanary(conn, toVersion);
                AssertSchemaCanary(conn, toVersion);
                ValidateIntegrity(conn);
            }
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(backupPath))
                RestoreMigrationBackup(dbPath, backupPath);
            throw;
        }
    }

    private static void RunMigration(SqliteConnection conn, SqliteTransaction tx, int fromVersion, int toVersion)
    {
        if (fromVersion == 0 && toVersion == 1)
        {
            Migrate_0_to_1(conn, tx);
            return;
        }

        throw new InvalidOperationException($"No catalog migration exists for v{fromVersion} to v{toVersion}.");
    }

    private static void Migrate_0_to_1(SqliteConnection conn, SqliteTransaction tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS catalog_assets (
                id                    INTEGER PRIMARY KEY AUTOINCREMENT,
                source_path           TEXT NOT NULL UNIQUE COLLATE NOCASE,
                fingerprint           TEXT NOT NULL,
                size_bytes            INTEGER NOT NULL,
                created_utc           INTEGER NOT NULL,
                modified_utc          INTEGER NOT NULL,
                width                 INTEGER NOT NULL,
                height                INTEGER NOT NULL,
                format                TEXT NOT NULL,
                codec                 TEXT NOT NULL,
                rating                INTEGER NULL,
                sidecar_path          TEXT NULL,
                sidecar_modified_utc  INTEGER NULL,
                scanned_utc           INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_catalog_assets_fingerprint ON catalog_assets(fingerprint);
            CREATE INDEX IF NOT EXISTS ix_catalog_assets_modified ON catalog_assets(modified_utc DESC);

            CREATE TABLE IF NOT EXISTS catalog_tags (
                asset_id INTEGER NOT NULL,
                tag      TEXT NOT NULL,
                PRIMARY KEY(asset_id, tag),
                FOREIGN KEY(asset_id) REFERENCES catalog_assets(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS ix_catalog_tags_tag ON catalog_tags(tag);

            CREATE TABLE IF NOT EXISTS catalog_roots (
                root_path       TEXT PRIMARY KEY NOT NULL COLLATE NOCASE,
                last_scanned_utc INTEGER NOT NULL,
                indexed_count    INTEGER NOT NULL,
                failed_count     INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS catalog_schema_canary (
                id             INTEGER PRIMARY KEY CHECK (id = 1),
                schema_version INTEGER NOT NULL,
                created_utc    INTEGER NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        EnsureSchemaCanary(conn, tx, 1);
    }

    private static void ConfigureCatalogPragmas(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA busy_timeout = 5000;
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;
            """;
        cmd.ExecuteNonQuery();
    }

    private static void SetSchemaVersion(SqliteConnection conn, SqliteTransaction tx, int version)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"PRAGMA user_version = {version};";
        cmd.ExecuteNonQuery();
    }

    private static void EnsureSchemaCanary(SqliteConnection conn, int schemaVersion)
    {
        using var tx = conn.BeginTransaction();
        EnsureSchemaCanary(conn, tx, schemaVersion);
        tx.Commit();
    }

    private static void EnsureSchemaCanary(SqliteConnection conn, SqliteTransaction tx, int schemaVersion)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS catalog_schema_canary (
                id             INTEGER PRIMARY KEY CHECK (id = 1),
                schema_version INTEGER NOT NULL,
                created_utc    INTEGER NOT NULL
            );

            INSERT INTO catalog_schema_canary (id, schema_version, created_utc)
            VALUES (1, $schemaVersion, $createdUtc)
            ON CONFLICT(id) DO UPDATE SET schema_version = excluded.schema_version;
            """;
        cmd.Parameters.AddWithValue("$schemaVersion", schemaVersion);
        cmd.Parameters.AddWithValue("$createdUtc", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        cmd.ExecuteNonQuery();
    }

    private static void AssertSchemaCanary(SqliteConnection conn, int schemaVersion)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT schema_version
            FROM catalog_schema_canary
            WHERE id = $id
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$id", SchemaCanaryId);
        var canaryVersion = Convert.ToInt32(cmd.ExecuteScalar() ?? -1, CultureInfo.InvariantCulture);
        if (canaryVersion != schemaVersion)
            throw new InvalidOperationException($"catalog.db schema canary expected v{schemaVersion} but found v{canaryVersion}.");
    }

    private static void ValidateIntegrity(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check;";
        var result = Convert.ToString(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"catalog.db integrity check failed: {result}");
    }

    private static void CheckpointWal(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        cmd.ExecuteNonQuery();
    }

    private static string CreateMigrationBackup(string dbPath, int fromVersion, int toVersion)
    {
        var backupPath = UniqueBackupPath(dbPath, fromVersion, toVersion);
        File.Copy(dbPath, backupPath);
        return backupPath;
    }

    private static string UniqueBackupPath(string dbPath, int fromVersion, int toVersion)
    {
        var basePath = $"{dbPath}.bak.v{fromVersion}-{toVersion}";
        if (!File.Exists(basePath))
            return basePath;

        for (var i = 2; i < 10_000; i++)
        {
            var candidate = $"{basePath}.{i}";
            if (!File.Exists(candidate))
                return candidate;
        }

        return $"{basePath}.{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    }

    private static void RestoreMigrationBackup(string dbPath, string backupPath)
    {
        try
        {
            SqliteConnection.ClearAllPools();
            TryDeleteSqliteSidecar(dbPath + "-wal");
            TryDeleteSqliteSidecar(dbPath + "-shm");
            File.Copy(backupPath, dbPath, overwrite: true);
            SqliteConnection.ClearAllPools();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            Log.LogError(ex, "Could not restore catalog migration backup {BackupPath}", backupPath);
        }
    }

    private static void TryDeleteSqliteSidecar(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            Log.LogDebug(ex, "Could not delete SQLite sidecar {Path}", path);
        }
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        ConfigureCatalogPragmas(conn);
        return conn;
    }

    private static CatalogAssetRecord BuildAsset(
        string path,
        DateTimeOffset scannedUtc,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length <= 0)
            throw new IOException("Catalog source file does not exist or is empty.");

        var fingerprint = ComputeSha256(info.FullName, cancellationToken);
        using var image = new MagickImage();
        image.Ping(info);
        var sidecar = ReadSidecarState(info.FullName);

        return new CatalogAssetRecord(
            info.FullName,
            fingerprint,
            info.Length,
            ToOffset(info.CreationTimeUtc),
            ToOffset(info.LastWriteTimeUtc),
            checked((int)Math.Min(image.Width, int.MaxValue)),
            checked((int)Math.Min(image.Height, int.MaxValue)),
            Path.GetExtension(info.Name).TrimStart('.').ToUpperInvariant(),
            image.Format.ToString(),
            sidecar.Rating,
            sidecar.Tags,
            sidecar.Path,
            sidecar.ModifiedUtc,
            scannedUtc);
    }

    private static IEnumerable<string> NormalizeRoots(IEnumerable<string> roots)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(root);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                continue;
            }

            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                continue;
            if (seen.Add(fullPath))
                yield return fullPath;
        }
    }

    private static IEnumerable<string> CollectCandidateFiles(
        IEnumerable<string> roots,
        CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(root))
            {
                if (SupportedImageFormats.IsSupported(root) && seen.Add(root))
                    yield return root;
                continue;
            }

            if (!Directory.Exists(root))
                continue;

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
            {
                continue;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (SupportedImageFormats.IsSupported(file) && seen.Add(file))
                    yield return file;
            }
        }
    }

    private static void ClearCatalog(SqliteConnection conn, SqliteTransaction tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            DELETE FROM catalog_tags;
            DELETE FROM catalog_assets;
            DELETE FROM catalog_roots;
            """;
        cmd.ExecuteNonQuery();
    }

    private static void UpsertAsset(SqliteConnection conn, SqliteTransaction tx, CatalogAssetRecord asset)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO catalog_assets (
                source_path, fingerprint, size_bytes, created_utc, modified_utc,
                width, height, format, codec, rating, sidecar_path, sidecar_modified_utc, scanned_utc
            )
            VALUES (
                $path, $fingerprint, $size, $created, $modified,
                $width, $height, $format, $codec, $rating, $sidecar, $sidecarModified, $scanned
            )
            ON CONFLICT(source_path) DO UPDATE SET
                fingerprint = excluded.fingerprint,
                size_bytes = excluded.size_bytes,
                created_utc = excluded.created_utc,
                modified_utc = excluded.modified_utc,
                width = excluded.width,
                height = excluded.height,
                format = excluded.format,
                codec = excluded.codec,
                rating = excluded.rating,
                sidecar_path = excluded.sidecar_path,
                sidecar_modified_utc = excluded.sidecar_modified_utc,
                scanned_utc = excluded.scanned_utc;
            """;
        AddAssetParameters(cmd, asset);
        cmd.ExecuteNonQuery();

        cmd.Parameters.Clear();
        cmd.CommandText = "SELECT id FROM catalog_assets WHERE lower(source_path) = lower($path) LIMIT 1;";
        cmd.Parameters.AddWithValue("$path", asset.SourcePath);
        var assetId = Convert.ToInt64(cmd.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);

        cmd.Parameters.Clear();
        cmd.CommandText = "DELETE FROM catalog_tags WHERE asset_id = $assetId;";
        cmd.Parameters.AddWithValue("$assetId", assetId);
        cmd.ExecuteNonQuery();

        foreach (var tag in asset.Tags)
        {
            cmd.Parameters.Clear();
            cmd.CommandText = """
                INSERT OR IGNORE INTO catalog_tags (asset_id, tag)
                VALUES ($assetId, $tag);
                """;
            cmd.Parameters.AddWithValue("$assetId", assetId);
            cmd.Parameters.AddWithValue("$tag", tag);
            cmd.ExecuteNonQuery();
        }
    }

    private static void UpsertRoot(
        SqliteConnection conn,
        SqliteTransaction tx,
        string root,
        DateTimeOffset scannedUtc,
        int indexedCount,
        int failedCount)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO catalog_roots (root_path, last_scanned_utc, indexed_count, failed_count)
            VALUES ($root, $scanned, $indexed, $failed)
            ON CONFLICT(root_path) DO UPDATE SET
                last_scanned_utc = excluded.last_scanned_utc,
                indexed_count = excluded.indexed_count,
                failed_count = excluded.failed_count;
            """;
        cmd.Parameters.AddWithValue("$root", root);
        cmd.Parameters.AddWithValue("$scanned", scannedUtc.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$indexed", indexedCount);
        cmd.Parameters.AddWithValue("$failed", failedCount);
        cmd.ExecuteNonQuery();
    }

    private static void AddAssetParameters(SqliteCommand cmd, CatalogAssetRecord asset)
    {
        cmd.Parameters.AddWithValue("$path", asset.SourcePath);
        cmd.Parameters.AddWithValue("$fingerprint", asset.Fingerprint);
        cmd.Parameters.AddWithValue("$size", asset.SizeBytes);
        cmd.Parameters.AddWithValue("$created", asset.CreatedUtc.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$modified", asset.ModifiedUtc.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$width", asset.Width);
        cmd.Parameters.AddWithValue("$height", asset.Height);
        cmd.Parameters.AddWithValue("$format", asset.Format);
        cmd.Parameters.AddWithValue("$codec", asset.Codec);
        cmd.Parameters.AddWithValue("$rating", asset.Rating is null ? DBNull.Value : asset.Rating.Value);
        cmd.Parameters.AddWithValue("$sidecar", asset.SidecarPath is null ? DBNull.Value : asset.SidecarPath);
        cmd.Parameters.AddWithValue("$sidecarModified", asset.SidecarModifiedUtc is null ? DBNull.Value : asset.SidecarModifiedUtc.Value.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$scanned", asset.ScannedUtc.ToUnixTimeSeconds());
    }

    private static CatalogAssetRecord ReadAsset(SqliteConnection conn, SqliteDataReader reader)
    {
        var assetId = reader.GetInt64(0);
        return new CatalogAssetRecord(
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt64(3),
            FromUnix(reader.GetInt64(4)),
            FromUnix(reader.GetInt64(5)),
            reader.GetInt32(6),
            reader.GetInt32(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetInt32(10),
            ReadTags(conn, assetId),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.IsDBNull(12) ? null : FromUnix(reader.GetInt64(12)),
            FromUnix(reader.GetInt64(13)));
    }

    private static IReadOnlyList<string> ReadTags(SqliteConnection conn, long assetId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT tag FROM catalog_tags WHERE asset_id = $assetId ORDER BY tag;";
        cmd.Parameters.AddWithValue("$assetId", assetId);
        using var reader = cmd.ExecuteReader();
        var tags = new List<string>();
        while (reader.Read())
            tags.Add(reader.GetString(0));
        return tags;
    }

    private static string ComputeSha256(string path, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var buffer = new byte[1024 * 128];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sha.TransformBlock(buffer, 0, read, null, 0);
        }
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash ?? []).ToLowerInvariant();
    }

    private static SidecarState ReadSidecarState(string imagePath)
    {
        foreach (var sidecarPath in SidecarPaths(imagePath))
        {
            if (!File.Exists(sidecarPath))
                continue;

            try
            {
                var info = new FileInfo(sidecarPath);
                var document = XDocument.Load(sidecarPath, LoadOptions.None);
                return new SidecarState(
                    sidecarPath,
                    ToOffset(info.LastWriteTimeUtc),
                    ReadRating(document),
                    ReadTags(document));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or System.Xml.XmlException or InvalidOperationException)
            {
                Log.LogDebug(ex, "Could not read catalog sidecar {Path}", sidecarPath);
                return new SidecarState(sidecarPath, null, null, []);
            }
        }

        return new SidecarState(null, null, null, []);
    }

    private static IEnumerable<string> SidecarPaths(string imagePath)
    {
        yield return imagePath + ".xmp";

        var directory = Path.GetDirectoryName(imagePath);
        var basename = Path.GetFileNameWithoutExtension(imagePath);
        if (!string.IsNullOrWhiteSpace(directory) && !string.IsNullOrWhiteSpace(basename))
            yield return Path.Combine(directory, basename + ".xmp");
    }

    private static int? ReadRating(XDocument document)
    {
        foreach (var attribute in document.Descendants().Attributes())
        {
            if (attribute.Name.LocalName.Equals("Rating", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rating))
            {
                return Math.Clamp(rating, -1, 5);
            }
        }

        foreach (var element in document.Descendants())
        {
            if (element.Name.LocalName.Equals("Rating", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(element.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rating))
            {
                return Math.Clamp(rating, -1, 5);
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ReadTags(XDocument document)
        => document.Descendants()
            .Where(IsKeywordElement)
            .SelectMany(element => SplitTagValues(element.Value))
            .Select(NormalizeTagOrNull)
            .Where(tag => tag is not null)
            .Select(tag => tag!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool IsKeywordElement(XElement element)
    {
        var name = element.Name.LocalName;
        if (name.Contains("keyword", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("subject", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("tag", StringComparison.OrdinalIgnoreCase))
        {
            return !element.Descendants().Any(child => child.Name.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase));
        }

        return name.Equals("li", StringComparison.OrdinalIgnoreCase) &&
               element.Ancestors().Any(ancestor =>
                   ancestor.Name.LocalName.Contains("keyword", StringComparison.OrdinalIgnoreCase) ||
                   ancestor.Name.LocalName.Contains("subject", StringComparison.OrdinalIgnoreCase) ||
                   ancestor.Name.LocalName.Contains("tag", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> SplitTagValues(string value)
        => (value ?? "")
            .Split(['\r', '\n', ',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item));

    private static string? NormalizeTagOrNull(string value)
    {
        if (!TagGraphService.TryNormalizeTag(value, out var normalized, out _))
            return null;
        return normalized;
    }

    private static DateTimeOffset ToOffset(DateTime utc)
    {
        var normalized = utc.Kind == DateTimeKind.Utc
            ? utc
            : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return new DateTimeOffset(normalized);
    }

    private static DateTimeOffset FromUnix(long seconds)
        => DateTimeOffset.FromUnixTimeSeconds(seconds);

    private sealed record SidecarState(
        string? Path,
        DateTimeOffset? ModifiedUtc,
        int? Rating,
        IReadOnlyList<string> Tags);
}
