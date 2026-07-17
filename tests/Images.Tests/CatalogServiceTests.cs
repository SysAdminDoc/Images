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
    public void Rebuild_StoresPerceptualHashAndBackfillsAnUnattemptedV3Row()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImage(temp.Path, "hash.png", 16, 8);
        var dbPath = Path.Combine(temp.Path, "catalog.db");
        var service = new CatalogService(dbPath);
        var first = service.Rebuild([temp.Path]);
        Assert.NotNull(Assert.Single(first.Assets).PerceptualHash);

        ExecuteSql(dbPath, "UPDATE catalog_assets SET perceptual_hash = NULL, perceptual_hash_state = 0;");
        var backfilled = service.Rebuild([temp.Path]);

        Assert.Equal(1, backfilled.UpdatedCount);
        Assert.NotNull(service.GetByPath(source)!.PerceptualHash);
        Assert.Equal(1, ReadInt(dbPath, "SELECT perceptual_hash_state FROM catalog_assets LIMIT 1;"));
    }

    [Fact]
    public void Open_UsesPrivateCacheSoConcurrentWritesDoNotLockReaders()
    {
        using var temp = TestDirectory.Create();
        var service = new CatalogService(Path.Combine(temp.Path, "catalog.db"));

        // Private cache under WAL is the contract that keeps a concurrent writer from raising
        // shared-cache SQLITE_LOCKED on a UI-thread read (which GetAllAssets would swallow to empty).
        Assert.Contains("Cache=Private", service.ConnectionStringForTests, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cache=Shared", service.ConnectionStringForTests, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetAllAssets_ReturnsCommittedRowsWhileAnotherWriteTransactionIsOpen()
    {
        using var temp = TestDirectory.Create();
        WriteImage(temp.Path, "a.png", 16, 8);
        WriteImage(temp.Path, "b.png", 16, 8);
        var service = new CatalogService(Path.Combine(temp.Path, "catalog.db"));
        var rebuilt = service.Rebuild([temp.Path]);
        Assert.Equal(2, rebuilt.Assets.Count);

        // Hold an uncommitted write transaction on the same database through the service's own
        // connection string (same cache semantics), then read. Under the old shared cache this
        // could throw SQLITE_LOCKED and blank the catalog; under WAL + private cache the reader
        // still observes the last committed snapshot.
        using var writer = new SqliteConnection(service.ConnectionStringForTests);
        writer.Open();
        using (var tx = writer.BeginTransaction())
        {
            using var write = writer.CreateCommand();
            write.Transaction = tx;
            // Acquire and hold the WAL write lock for the duration of the transaction.
            write.CommandText = "CREATE TABLE _concurrency_probe(x); INSERT INTO _concurrency_probe VALUES (1);";
            write.ExecuteNonQuery();

            var assets = service.GetAllAssets();
            Assert.Equal(2, assets.Count);

            tx.Rollback();
        }
    }

    [Fact]
    public void Rebuild_MapsMicrosoftPhotoRatingScale()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImage(temp.Path, "source.png", 16, 8);
        File.WriteAllText(
            source + ".xmp",
            """
            <x:xmpmeta xmlns:x="adobe:ns:meta/" xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#" xmlns:MicrosoftPhoto="http://ns.microsoft.com/photo/1.0/">
              <rdf:RDF>
                <rdf:Description MicrosoftPhoto:Rating="50" />
              </rdf:RDF>
            </x:xmpmeta>
            """);
        var service = new CatalogService(Path.Combine(temp.Path, "catalog.db"));

        var result = service.Rebuild([temp.Path]);

        Assert.Equal(3, Assert.Single(result.Assets).Rating);
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
    public void Rebuild_StoresPerRootIndexedAndFailedCounts()
    {
        using var temp = TestDirectory.Create();
        var root1 = Directory.CreateDirectory(Path.Combine(temp.Path, "root1")).FullName;
        var root2 = Directory.CreateDirectory(Path.Combine(temp.Path, "root2")).FullName;
        WriteImage(root1, "first.png", 8, 8);
        WriteImage(root1, "second.png", 8, 8);
        WriteImage(root2, "third.png", 8, 8);
        File.WriteAllText(Path.Combine(root2, "broken.png"), "not an image");
        var dbPath = Path.Combine(temp.Path, "catalog.db");
        var service = new CatalogService(dbPath);

        var result = service.Rebuild([root1, root2]);

        Assert.Equal(3, result.IndexedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(2, ReadRootInt(dbPath, root1, "indexed_count"));
        Assert.Equal(0, ReadRootInt(dbPath, root1, "failed_count"));
        Assert.Equal(1, ReadRootInt(dbPath, root2, "indexed_count"));
        Assert.Equal(1, ReadRootInt(dbPath, root2, "failed_count"));
    }

    [Fact]
    public void Rebuild_PreservesCachedAssetsWhenOneRegisteredRootIsOffline()
    {
        using var temp = TestDirectory.Create();
        var onlineRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "online")).FullName;
        var offlineRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "removable")).FullName;
        var onlineAsset = WriteImage(onlineRoot, "online.png", 8, 8);
        var offlineAsset = WriteImage(offlineRoot, "offline.png", 8, 8);
        var service = new CatalogService(Path.Combine(temp.Path, "catalog.db"));
        service.Rebuild([onlineRoot, offlineRoot]);
        Directory.Move(offlineRoot, offlineRoot + "-disconnected");

        var result = service.Rebuild([onlineRoot, offlineRoot]);

        Assert.Equal(2, result.Assets.Count);
        Assert.Contains(result.Assets, asset => asset.SourcePath == onlineAsset);
        Assert.Contains(result.Assets, asset => asset.SourcePath == offlineAsset);
        Assert.Equal(offlineRoot, Assert.Single(result.OfflineRoots));
        Assert.False(service.GetRoots().Single(root => root.RootPath == offlineRoot).IsOnline);
    }

    [Fact]
    public void RegisterAndRemoveRoot_PersistsLifecycleAndDeletesOnlyOwnedAssets()
    {
        using var temp = TestDirectory.Create();
        var firstRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "first")).FullName;
        var secondRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "second")).FullName;
        var firstAsset = WriteImage(firstRoot, "first.png", 8, 8);
        var secondAsset = WriteImage(secondRoot, "second.png", 8, 8);
        var service = new CatalogService(Path.Combine(temp.Path, "catalog.db"));

        Assert.True(service.RegisterRoot(firstRoot));
        Assert.True(service.RegisterRoot(secondRoot));
        service.Rebuild(service.GetRoots().Select(root => root.RootPath));
        Assert.Equal(2, service.GetRoots().Count);

        Assert.True(service.RemoveRoot(firstRoot));

        Assert.Null(service.GetByPath(firstAsset));
        Assert.NotNull(service.GetByPath(secondAsset));
        Assert.Equal(secondRoot, Assert.Single(service.GetRoots()).RootPath);
    }

    [Fact]
    public void Rebuild_SkipsReparsePointDirectories()
    {
        using var temp = TestDirectory.Create();
        var root = Directory.CreateDirectory(Path.Combine(temp.Path, "root")).FullName;
        var linkedTarget = Directory.CreateDirectory(Path.Combine(temp.Path, "linked-target")).FullName;
        var source = WriteImage(root, "source.png", 8, 8);
        WriteImage(linkedTarget, "linked.png", 8, 8);
        ReparsePointTestHelper.CreateDirectoryLinkOrSkip(Path.Combine(root, "linked"), linkedTarget);
        var service = new CatalogService(Path.Combine(temp.Path, "catalog.db"));

        var result = service.Rebuild([root]);

        var asset = Assert.Single(result.Assets);
        Assert.Equal(Path.GetFullPath(source), asset.SourcePath);
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
        Assert.Equal(4, ReadInt(dbPath, "PRAGMA user_version;"));
        Assert.Equal(4, ReadInt(dbPath, "SELECT schema_version FROM catalog_schema_canary WHERE id = 1;"));
        Assert.Equal(1, ReadInt(dbPath, "SELECT COUNT(*) FROM pragma_table_info('catalog_assets') WHERE name = 'perceptual_hash';"));
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

    [Fact]
    public void Rebuild_IndexesGpsAndCameraExifIntoCatalog()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImageWithExif(temp.Path, "geotagged.jpg");
        var service = new CatalogService(Path.Combine(temp.Path, "catalog.db"));

        var result = service.Rebuild([temp.Path]);

        var asset = Assert.Single(result.Assets);
        var exif = asset.ExifFacts;
        Assert.True(exif.HasGeo);
        Assert.Equal(48.8583, exif.Latitude!.Value, 2);
        Assert.Equal(2.2945, exif.Longitude!.Value, 2);
        Assert.Equal("TestCam", exif.CameraMake);
        Assert.Equal(200, exif.Iso);
        Assert.Equal(new DateTimeOffset(2021, 7, 15, 14, 30, 0, TimeSpan.Zero), exif.CapturedUtc!.Value);
    }

    [Fact]
    public void GetByPath_RoundTripsPersistedExifFacts()
    {
        using var temp = TestDirectory.Create();
        var source = WriteImageWithExif(temp.Path, "geotagged.jpg");
        var service = new CatalogService(Path.Combine(temp.Path, "catalog.db"));
        service.Rebuild([temp.Path]);

        var reloaded = service.GetByPath(source);

        Assert.NotNull(reloaded);
        Assert.True(reloaded.ExifFacts.HasGeo);
        Assert.Equal(48.8583, reloaded.ExifFacts.Latitude!.Value, 2);
        Assert.Equal("TestCam", reloaded.ExifFacts.CameraMake);
    }

    [Fact]
    public void FindWithinBounds_ReturnsOnlyAssetsInsideBox()
    {
        using var temp = TestDirectory.Create();
        WriteImageWithExif(temp.Path, "paris.jpg");        // 48.8583, 2.2945
        WriteImage(temp.Path, "no-gps.png", 8, 8);          // no EXIF at all
        var service = new CatalogService(Path.Combine(temp.Path, "catalog.db"));
        service.Rebuild([temp.Path]);

        var inside = service.FindWithinBounds(48.0, 49.0, 2.0, 3.0);
        Assert.Single(inside);
        Assert.EndsWith("paris.jpg", inside[0].SourcePath);

        var outside = service.FindWithinBounds(-10.0, 10.0, -10.0, 10.0);
        Assert.Empty(outside);
    }

    [Fact]
    public void FindWithinBounds_AntimeridianBoxWrapsLongitude()
    {
        using var temp = TestDirectory.Create();
        WriteImageWithExif(temp.Path, "paris.jpg");        // lon 2.29, should be excluded
        var service = new CatalogService(Path.Combine(temp.Path, "catalog.db"));
        service.Rebuild([temp.Path]);

        // Box crossing the antimeridian: lon >= 170 OR lon <= -170. Paris (2.29) is not in it.
        var wrapped = service.FindWithinBounds(40.0, 50.0, 170.0, -170.0);
        Assert.Empty(wrapped);
    }

    private static string WriteImageWithExif(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(MagickColors.Blue, 16, 12)
        {
            Format = MagickFormat.Jpeg
        };

        var exif = new ExifProfile();
        // Eiffel Tower: 48°51'30"N, 2°17'40"E
        exif.SetValue(ExifTag.GPSLatitude, [new Rational(48, 1), new Rational(51, 1), new Rational(30, 1)]);
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        exif.SetValue(ExifTag.GPSLongitude, [new Rational(2, 1), new Rational(17, 1), new Rational(40, 1)]);
        exif.SetValue(ExifTag.GPSLongitudeRef, "E");
        exif.SetValue(ExifTag.Make, "TestCam");
        exif.SetValue(ExifTag.Model, "Model-X");
        exif.SetValue(ExifTag.ISOSpeedRatings, new ushort[] { 200 });
        exif.SetValue(ExifTag.DateTimeOriginal, "2021:07:15 14:30:00");
        exif.SetValue(ExifTag.OffsetTimeOriginal, "+00:00");
        image.SetProfile(exif);

        image.Write(path);
        return path;
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

    private static int ReadRootInt(string dbPath, string rootPath, string columnName)
    {
        using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {columnName} FROM catalog_roots WHERE root_path = $root;";
        cmd.Parameters.AddWithValue("$root", rootPath);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

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
    public void Rebuild_IncrementalUpdatesWhenSidecarAppearsOrDisappears()
    {
        using var temp = TestDirectory.Create();
        var image = WriteImage(temp.Path, "rated.png", 8, 8);
        var sidecar = image + ".xmp";
        var dbPath = Path.Combine(temp.Path, "catalog.db");
        var service = new CatalogService(dbPath);

        var first = service.Rebuild([temp.Path]);
        Assert.Equal(1, first.UpdatedCount);
        Assert.Null(Assert.Single(first.Assets).Rating);

        WriteRatingSidecar(sidecar, 4);

        var withSidecar = service.Rebuild([temp.Path]);
        Assert.Equal(0, withSidecar.ReusedCount);
        Assert.Equal(1, withSidecar.UpdatedCount);
        var withSidecarAsset = Assert.Single(withSidecar.Assets);
        Assert.Equal(4, withSidecarAsset.Rating);
        Assert.Equal(sidecar, withSidecarAsset.SidecarPath);

        File.Delete(sidecar);

        var withoutSidecar = service.Rebuild([temp.Path]);
        Assert.Equal(0, withoutSidecar.ReusedCount);
        Assert.Equal(1, withoutSidecar.UpdatedCount);
        var withoutSidecarAsset = Assert.Single(withoutSidecar.Assets);
        Assert.Null(withoutSidecarAsset.Rating);
        Assert.Null(withoutSidecarAsset.SidecarPath);
    }

    [Fact]
    public void Rebuild_TransientSidecarProbeDefersHashAndRetriesLater()
    {
        using var temp = TestDirectory.Create();
        var image = WriteImage(temp.Path, "rated.png", 8, 8);
        var sidecar = image + ".xmp";
        WriteRatingSidecar(sidecar, 2);
        var originalSidecarMtime = File.GetLastWriteTimeUtc(sidecar);
        var probeFails = false;
        var hashCalls = 0;
        var service = new CatalogService(
            Path.Combine(temp.Path, "catalog.db"),
            _ =>
            {
                if (probeFails)
                    throw new IOException("Simulated transient sidecar metadata failure.");

                return new CatalogService.CatalogSidecarFileSummary(
                    sidecar,
                    new DateTimeOffset(File.GetLastWriteTimeUtc(sidecar)));
            },
            (_, _) => Interlocked.Increment(ref hashCalls).ToString("x64"));

        var first = service.Rebuild([temp.Path]);
        Assert.Equal(1, first.UpdatedCount);
        Assert.Equal(2, Assert.Single(first.Assets).Rating);
        Assert.Equal(1, hashCalls);

        WriteRatingSidecar(sidecar, 5);
        File.SetLastWriteTimeUtc(sidecar, originalSidecarMtime.AddSeconds(5));
        probeFails = true;

        var deferred = service.Rebuild([temp.Path]);
        Assert.Equal(1, deferred.ReusedCount);
        Assert.Equal(0, deferred.UpdatedCount);
        Assert.Equal(0, deferred.FailedCount);
        Assert.Equal(2, Assert.Single(deferred.Assets).Rating);
        Assert.Equal(1, hashCalls);

        probeFails = false;

        var retried = service.Rebuild([temp.Path]);
        Assert.Equal(0, retried.ReusedCount);
        Assert.Equal(1, retried.UpdatedCount);
        Assert.Equal(5, Assert.Single(retried.Assets).Rating);
        Assert.Equal(2, hashCalls);
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

    private static void WriteRatingSidecar(string path, int rating)
    {
        File.WriteAllText(
            path,
            $$"""
            <x:xmpmeta xmlns:x="adobe:ns:meta/" xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#" xmlns:xmp="http://ns.adobe.com/xap/1.0/">
              <rdf:RDF>
                <rdf:Description xmp:Rating="{{rating}}" />
              </rdf:RDF>
            </x:xmpmeta>
            """);
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
