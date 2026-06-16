using System.IO;
using Images.Services;
using Microsoft.Data.Sqlite;

namespace Images.Tests;

/// <summary>
/// T-04 + SCH-05: catalog v1 snapshot fixture tests.
/// Verifies the checked-in <c>Fixtures/catalog.v1.db</c> matches the expected v1 schema
/// and that <see cref="CatalogService"/> can open/migrate it without data loss.
/// Every future schema bump adds a new <c>catalog.vN.db</c> and a matching test that
/// rolls all prior snapshots forward.
/// </summary>
public sealed class CatalogSchemaSnapshotTests
{
    private static readonly string FixtureDir = Path.Combine(
        AppContext.BaseDirectory, "Fixtures");

    private static readonly string V1FixturePath = Path.Combine(
        FixtureDir, "catalog.v1.db");

    // ── Schema shape assertions ──────────────────────────────────────

    [Fact]
    public void V1Fixture_Exists()
    {
        Assert.True(File.Exists(V1FixturePath),
            $"catalog.v1.db fixture missing at {V1FixturePath}");
    }

    [Fact]
    public void V1Fixture_HasCorrectUserVersion()
    {
        Assert.Equal(1, ReadInt(V1FixturePath, "PRAGMA user_version;"));
    }

    [Fact]
    public void V1Fixture_HasSchemaCanaryVersion1()
    {
        Assert.Equal(1, ReadInt(V1FixturePath,
            "SELECT schema_version FROM catalog_schema_canary WHERE id = 1;"));
    }

    [Fact]
    public void V1Fixture_HasExpectedTables()
    {
        var tables = ReadStrings(V1FixturePath,
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;");

        Assert.Equal(4, tables.Count);
        Assert.Contains("catalog_assets", tables);
        Assert.Contains("catalog_tags", tables);
        Assert.Contains("catalog_roots", tables);
        Assert.Contains("catalog_schema_canary", tables);
    }

    [Fact]
    public void V1Fixture_HasExpectedIndexes()
    {
        var indexes = ReadStrings(V1FixturePath,
            "SELECT name FROM sqlite_master WHERE type = 'index' AND name NOT LIKE 'sqlite_%' ORDER BY name;");

        Assert.Contains("ix_catalog_assets_fingerprint", indexes);
        Assert.Contains("ix_catalog_assets_modified", indexes);
        Assert.Contains("ix_catalog_tags_tag", indexes);
    }

    [Theory]
    [InlineData("catalog_assets", "id", "INTEGER")]
    [InlineData("catalog_assets", "source_path", "TEXT")]
    [InlineData("catalog_assets", "fingerprint", "TEXT")]
    [InlineData("catalog_assets", "size_bytes", "INTEGER")]
    [InlineData("catalog_assets", "created_utc", "INTEGER")]
    [InlineData("catalog_assets", "modified_utc", "INTEGER")]
    [InlineData("catalog_assets", "width", "INTEGER")]
    [InlineData("catalog_assets", "height", "INTEGER")]
    [InlineData("catalog_assets", "format", "TEXT")]
    [InlineData("catalog_assets", "codec", "TEXT")]
    [InlineData("catalog_assets", "rating", "INTEGER")]
    [InlineData("catalog_assets", "sidecar_path", "TEXT")]
    [InlineData("catalog_assets", "sidecar_modified_utc", "INTEGER")]
    [InlineData("catalog_assets", "scanned_utc", "INTEGER")]
    [InlineData("catalog_tags", "asset_id", "INTEGER")]
    [InlineData("catalog_tags", "tag", "TEXT")]
    [InlineData("catalog_roots", "root_path", "TEXT")]
    [InlineData("catalog_roots", "last_scanned_utc", "INTEGER")]
    [InlineData("catalog_roots", "indexed_count", "INTEGER")]
    [InlineData("catalog_roots", "failed_count", "INTEGER")]
    [InlineData("catalog_schema_canary", "id", "INTEGER")]
    [InlineData("catalog_schema_canary", "schema_version", "INTEGER")]
    [InlineData("catalog_schema_canary", "created_utc", "INTEGER")]
    public void V1Fixture_ColumnTypesMatchSchema(string table, string column, string expectedType)
    {
        var columns = ReadColumnTypes(V1FixturePath, table);
        Assert.True(columns.TryGetValue(column, out var actualType),
            $"Column '{column}' not found in table '{table}'");
        Assert.Equal(expectedType, actualType);
    }

    [Fact]
    public void V1Fixture_PassesIntegrityCheck()
    {
        var result = ReadString(V1FixturePath, "PRAGMA integrity_check;");
        Assert.Equal("ok", result, StringComparer.OrdinalIgnoreCase);
    }

    // ── Seed data assertions ─────────────────────────────────────────

    [Fact]
    public void V1Fixture_Contains3Assets()
    {
        Assert.Equal(3, ReadInt(V1FixturePath, "SELECT COUNT(*) FROM catalog_assets;"));
    }

    [Fact]
    public void V1Fixture_Contains5Tags()
    {
        Assert.Equal(5, ReadInt(V1FixturePath, "SELECT COUNT(*) FROM catalog_tags;"));
    }

    [Fact]
    public void V1Fixture_Contains1Root()
    {
        Assert.Equal(1, ReadInt(V1FixturePath, "SELECT COUNT(*) FROM catalog_roots;"));
    }

    [Fact]
    public void V1Fixture_Asset1HasRatingAndSidecar()
    {
        Assert.Equal(5, ReadInt(V1FixturePath,
            @"SELECT rating FROM catalog_assets WHERE source_path = 'C:\Photos\landscape.jpg';"));
        Assert.Equal(@"C:\Photos\landscape.xmp", ReadString(V1FixturePath,
            @"SELECT sidecar_path FROM catalog_assets WHERE source_path = 'C:\Photos\landscape.jpg';"));
    }

    [Fact]
    public void V1Fixture_Asset2HasNullRatingAndNoSidecar()
    {
        Assert.True(ReadIsNull(V1FixturePath,
            @"SELECT rating FROM catalog_assets WHERE source_path = 'C:\Photos\portrait.png';"));
        Assert.True(ReadIsNull(V1FixturePath,
            @"SELECT sidecar_path FROM catalog_assets WHERE source_path = 'C:\Photos\portrait.png';"));
    }

    [Fact]
    public void V1Fixture_TagsAssociatedCorrectly()
    {
        var asset1Tags = ReadStrings(V1FixturePath,
            "SELECT tag FROM catalog_tags WHERE asset_id = 1 ORDER BY tag;");
        Assert.Equal(3, asset1Tags.Count);
        Assert.Contains("landscape", asset1Tags);
        Assert.Contains("nature", asset1Tags);
        Assert.Contains("sunset", asset1Tags);

        var asset2Tags = ReadStrings(V1FixturePath,
            "SELECT tag FROM catalog_tags WHERE asset_id = 2 ORDER BY tag;");
        Assert.Empty(asset2Tags);

        var asset3Tags = ReadStrings(V1FixturePath,
            "SELECT tag FROM catalog_tags WHERE asset_id = 3 ORDER BY tag;");
        Assert.Equal(2, asset3Tags.Count);
        Assert.Contains("golden-hour", asset3Tags);
        Assert.Contains("sunset", asset3Tags);
    }

    // ── Forward migration: v1 snapshot opened by current CatalogService ──

    [Fact]
    public void V1Fixture_OpensSuccessfullyViaCatalogService()
    {
        using var temp = TestDirectory.Create();
        var dbCopy = CopyFixtureToTemp(temp.Path);

        var service = new CatalogService(dbCopy);

        Assert.True(service.IsAvailable);
    }

    [Fact]
    public void V1Fixture_MigrationPreservesAllAssets()
    {
        using var temp = TestDirectory.Create();
        var dbCopy = CopyFixtureToTemp(temp.Path);

        var service = new CatalogService(dbCopy);
        var assets = service.GetAllAssets();

        Assert.Equal(3, assets.Count);
    }

    [Fact]
    public void V1Fixture_MigrationPreservesAssetDetails()
    {
        using var temp = TestDirectory.Create();
        var dbCopy = CopyFixtureToTemp(temp.Path);

        var service = new CatalogService(dbCopy);
        var landscape = service.GetByPath(@"C:\Photos\landscape.jpg");

        Assert.NotNull(landscape);
        Assert.Equal("aabbccdd00112233aabbccdd00112233aabbccdd00112233aabbccdd00112233", landscape.Fingerprint);
        Assert.Equal(2048000, landscape.SizeBytes);
        Assert.Equal(4000, landscape.Width);
        Assert.Equal(3000, landscape.Height);
        Assert.Equal("JPG", landscape.Format);
        Assert.Equal("Jpeg", landscape.Codec);
        Assert.Equal(5, landscape.Rating);
        Assert.Equal(@"C:\Photos\landscape.xmp", landscape.SidecarPath);
    }

    [Fact]
    public void V1Fixture_MigrationPreservesTags()
    {
        using var temp = TestDirectory.Create();
        var dbCopy = CopyFixtureToTemp(temp.Path);

        var service = new CatalogService(dbCopy);
        var landscape = service.GetByPath(@"C:\Photos\landscape.jpg");

        Assert.NotNull(landscape);
        Assert.Equal(3, landscape.Tags.Count);
        Assert.Contains("landscape", landscape.Tags);
        Assert.Contains("nature", landscape.Tags);
        Assert.Contains("sunset", landscape.Tags);
    }

    [Fact]
    public void V1Fixture_MigrationPreservesNullableFields()
    {
        using var temp = TestDirectory.Create();
        var dbCopy = CopyFixtureToTemp(temp.Path);

        var service = new CatalogService(dbCopy);
        var portrait = service.GetByPath(@"C:\Photos\portrait.png");

        Assert.NotNull(portrait);
        Assert.Null(portrait.Rating);
        Assert.Null(portrait.SidecarPath);
        Assert.Null(portrait.SidecarModifiedUtc);
        Assert.Empty(portrait.Tags);
    }

    [Fact]
    public void V1Fixture_MigrationPreservesTimestamps()
    {
        using var temp = TestDirectory.Create();
        var dbCopy = CopyFixtureToTemp(temp.Path);

        var service = new CatalogService(dbCopy);
        var landscape = service.GetByPath(@"C:\Photos\landscape.jpg");

        Assert.NotNull(landscape);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000), landscape.CreatedUtc);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700100000), landscape.ModifiedUtc);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700200000), landscape.ScannedUtc);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700100000), landscape.SidecarModifiedUtc);
    }

    [Fact]
    public void V1Fixture_MigrationKeepsSchemaVersionAtCurrent()
    {
        using var temp = TestDirectory.Create();
        var dbCopy = CopyFixtureToTemp(temp.Path);

        _ = new CatalogService(dbCopy);

        // After CatalogService opens it, user_version should still be 1
        // (or upgraded to current if a v2 migration exists in the future).
        var version = ReadInt(dbCopy, "PRAGMA user_version;");
        Assert.True(version >= 1, $"user_version should be >= 1 after migration, got {version}");

        var canary = ReadInt(dbCopy, "SELECT schema_version FROM catalog_schema_canary WHERE id = 1;");
        Assert.True(canary >= 1, $"canary schema_version should be >= 1 after migration, got {canary}");
    }

    [Fact]
    public void V1Fixture_MigrationPassesIntegrityCheck()
    {
        using var temp = TestDirectory.Create();
        var dbCopy = CopyFixtureToTemp(temp.Path);

        _ = new CatalogService(dbCopy);

        var result = ReadString(dbCopy, "PRAGMA integrity_check;");
        Assert.Equal("ok", result, StringComparer.OrdinalIgnoreCase);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static string CopyFixtureToTemp(string tempDir)
    {
        var dest = Path.Combine(tempDir, "catalog.v1.db");
        File.Copy(V1FixturePath, dest);
        return dest;
    }

    private static int ReadInt(string dbPath, string sql)
        => Convert.ToInt32(ReadScalar(dbPath, sql));

    private static string ReadString(string dbPath, string sql)
        => Convert.ToString(ReadScalar(dbPath, sql)) ?? "";

    private static bool ReadIsNull(string dbPath, string sql)
        => ReadScalar(dbPath, sql) is null or DBNull;

    private static List<string> ReadStrings(string dbPath, string sql)
    {
        using var conn = OpenReadOnly(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        var results = new List<string>();
        while (reader.Read())
            results.Add(reader.GetString(0));
        return results;
    }

    private static Dictionary<string, string> ReadColumnTypes(string dbPath, string table)
    {
        using var conn = OpenReadOnly(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        using var reader = cmd.ExecuteReader();
        var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
            columns[reader.GetString(1)] = reader.GetString(2);
        return columns;
    }

    private static object? ReadScalar(string dbPath, string sql)
    {
        using var conn = OpenReadOnly(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    private static SqliteConnection OpenReadOnly(string dbPath)
    {
        var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        conn.Open();
        return conn;
    }
}
