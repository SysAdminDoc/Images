using System.IO;
using ImageMagick;
using Images.Services;
using Microsoft.Data.Sqlite;

namespace Images.Tests;

public sealed class CatalogServiceTests
{
    [Fact]
    public void Rebuild_IndexesImageMetadataFingerprintAndSidecarState()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImage(temp.Path, "source.png", 16, 8);
        File.WriteAllText(
            source + ".xmp",
            """
            <x:xmpmeta xmlns:x="adobe:ns:meta/" xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#" xmlns:xmp="http://ns.adobe.com/xap/1.0/" xmlns:dc="http://purl.org/dc/elements/1.1/">
              <rdf:RDF>
                <rdf:Description xmp:Rating="4">
                  <dc:subject>
                    <rdf:Bag>
                      <rdf:li>project:Images</rdf:li>
                      <rdf:li>Needs Review</rdf:li>
                    </rdf:Bag>
                  </dc:subject>
                </rdf:Description>
              </rdf:RDF>
            </x:xmpmeta>
            """);
        var service = new CatalogService(Path.Combine(temp.Path, "catalog.db"));

        var result = service.Rebuild([temp.Path]);

        var asset = Assert.Single(result.Assets);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(Path.GetFullPath(source), asset.SourcePath);
        Assert.Equal(64, asset.Fingerprint.Length);
        Assert.Equal(16, asset.Width);
        Assert.Equal(8, asset.Height);
        Assert.Equal("PNG", asset.Format);
        Assert.Equal(4, asset.Rating);
        Assert.Contains("project:images", asset.Tags);
        Assert.Contains("needs-review", asset.Tags);
        Assert.Equal(source + ".xmp", asset.SidecarPath);

        var stored = service.GetByPath(source);
        Assert.NotNull(stored);
        Assert.Equal(asset.Fingerprint, stored.Fingerprint);
        Assert.Equal(asset.Tags, stored.Tags);
    }

    [Fact]
    public void Rebuild_ClearsPreviousRowsBecauseCatalogIsRebuildableCache()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImage(temp.Path, "source.png", 8, 8);
        var service = new CatalogService(Path.Combine(temp.Path, "catalog.db"));
        service.Rebuild([temp.Path]);
        File.Delete(source);

        var result = service.Rebuild([temp.Path]);

        Assert.Empty(result.Assets);
        Assert.Empty(service.GetAllAssets());
    }

    [Fact]
    public void Rebuild_SkipsUnsupportedFiles()
    {
        using var temp = TestDirectory.Create();
        temp.WriteFile("notes.txt", "not an image");
        var service = new CatalogService(Path.Combine(temp.Path, "catalog.db"));

        var result = service.Rebuild([temp.Path]);

        Assert.Empty(result.Assets);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public void EnsureSchema_BackupsExistingVersionZeroCatalogBeforeMigration()
    {
        using var temp = TestDirectory.Create();
        var dbPath = Path.Combine(temp.Path, "catalog.db");
        ExecuteSql(
            dbPath,
            """
            CREATE TABLE legacy_marker (
                value TEXT NOT NULL
            );
            INSERT INTO legacy_marker (value) VALUES ('kept-before-migration');
            PRAGMA user_version = 0;
            """);

        var service = new CatalogService(dbPath);

        Assert.True(service.IsAvailable);
        Assert.Equal(1, ReadInt(dbPath, "PRAGMA user_version;"));
        Assert.Equal(1, ReadInt(dbPath, "SELECT schema_version FROM catalog_schema_canary WHERE id = 1;"));
        var backupPath = dbPath + ".bak.v0-1";
        Assert.True(File.Exists(backupPath));
        Assert.Equal("kept-before-migration", ReadString(backupPath, "SELECT value FROM legacy_marker LIMIT 1;"));
    }

    [Fact]
    public void EnsureSchema_RestoresBackupWhenMigrationFails()
    {
        using var temp = TestDirectory.Create();
        var dbPath = Path.Combine(temp.Path, "catalog.db");
        ExecuteSql(
            dbPath,
            """
            CREATE TABLE catalog_schema_canary (
                id INTEGER PRIMARY KEY
            );
            PRAGMA user_version = 0;
            """);

        var service = new CatalogService(dbPath);

        Assert.False(service.IsAvailable);
        Assert.Equal(0, ReadInt(dbPath, "PRAGMA user_version;"));
        Assert.Equal(0, ReadInt(dbPath, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'catalog_assets';"));
        Assert.Equal(1, ReadInt(dbPath, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'catalog_schema_canary';"));
        Assert.True(File.Exists(dbPath + ".bak.v0-1"));
    }

    [Fact]
    public void EnsureSchema_RejectsNewerSchemaWithoutDowngrading()
    {
        using var temp = TestDirectory.Create();
        var dbPath = Path.Combine(temp.Path, "catalog.db");
        ExecuteSql(dbPath, "PRAGMA user_version = 99;");

        var service = new CatalogService(dbPath);

        Assert.False(service.IsAvailable);
        Assert.Equal(99, ReadInt(dbPath, "PRAGMA user_version;"));
        Assert.False(File.Exists(dbPath + ".bak.v99-100"));
    }

    private static string WriteImage(string folder, string name, uint width, uint height)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(MagickColors.Red, width, height)
        {
            Format = MagickFormat.Png
        };
        image.Write(path);
        return path;
    }

    private static void ExecuteSql(string dbPath, string sql)
    {
        using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static int ReadInt(string dbPath, string sql)
        => Convert.ToInt32(ReadScalar(dbPath, sql));

    [Fact]
    public void Rebuild_IncrementalReusesUnchangedRows()
    {
        using var temp = TestDirectory.Create();
        var img1 = WriteImage(temp.Path, "stable.png", 8, 8);
        var img2 = WriteImage(temp.Path, "updated.png", 8, 8);
        var dbPath = Path.Combine(temp.Path, "catalog.db");
        var service = new CatalogService(dbPath);

        var first = service.Rebuild([temp.Path]);
        Assert.Equal(2, first.IndexedCount);
        Assert.Equal(0, first.ReusedCount);
        Assert.Equal(2, first.UpdatedCount);

        File.WriteAllBytes(img2, File.ReadAllBytes(WriteImage(temp.Path, "temp-replace.png", 16, 16)));
        File.Delete(Path.Combine(temp.Path, "temp-replace.png"));

        var second = service.Rebuild([temp.Path]);
        Assert.Equal(2, second.IndexedCount);
        Assert.Equal(1, second.ReusedCount);
        Assert.Equal(1, second.UpdatedCount);
        Assert.Equal(0, second.RemovedCount);
    }

    [Fact]
    public void Rebuild_IncrementalRemovesDeletedFiles()
    {
        using var temp = TestDirectory.Create();
        var img1 = WriteImage(temp.Path, "keep.png", 8, 8);
        var img2 = WriteImage(temp.Path, "delete.png", 8, 8);
        var dbPath = Path.Combine(temp.Path, "catalog.db");
        var service = new CatalogService(dbPath);

        service.Rebuild([temp.Path]);
        File.Delete(img2);

        var result = service.Rebuild([temp.Path]);
        Assert.Equal(1, result.IndexedCount);
        Assert.Equal(1, result.ReusedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.RemovedCount);
    }

    private static string ReadString(string dbPath, string sql)
        => Convert.ToString(ReadScalar(dbPath, sql)) ?? "";

    private static object? ReadScalar(string dbPath, string sql)
    {
        using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }
}
