// This script documents how catalog.v1.db was generated.
// It is NOT executed at test time — the .db file is checked in as a binary fixture.
// Re-run only if you need to regenerate the v1 snapshot from scratch.
//
// Schema: CatalogService.Migrate_0_to_1 (4 tables, 3 indexes, PRAGMA user_version=1)
// Seed: 3 assets, 5 tags, 1 root, 1 canary row.
//
// To regenerate: dotnet-script GenerateV1Fixture.csx
// Requires: dotnet tool install -g dotnet-script && dotnet-script add package Microsoft.Data.Sqlite

#r "nuget: Microsoft.Data.Sqlite, 9.0.0"
using Microsoft.Data.Sqlite;
using System;
using System.IO;

var dbPath = Path.Combine(Path.GetDirectoryName(GetScriptPath()), "catalog.v1.db");
if (File.Exists(dbPath)) File.Delete(dbPath);

var csb = new SqliteConnectionStringBuilder
{
    DataSource = dbPath,
    Mode = SqliteOpenMode.ReadWriteCreate
};

using var conn = new SqliteConnection(csb.ToString());
conn.Open();

using (var pragma = conn.CreateCommand())
{
    pragma.CommandText = "PRAGMA journal_mode = DELETE; PRAGMA foreign_keys = ON;";
    pragma.ExecuteNonQuery();
}

using (var tx = conn.BeginTransaction())
{
    using var cmd = conn.CreateCommand();
    cmd.Transaction = tx;

    // Exact DDL from CatalogService.Migrate_0_to_1
    cmd.CommandText = @"
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
    ";
    cmd.ExecuteNonQuery();

    // Canary
    cmd.CommandText = "INSERT INTO catalog_schema_canary (id, schema_version, created_utc) VALUES (1, 1, 1700000000);";
    cmd.ExecuteNonQuery();

    // PRAGMA user_version = 1
    cmd.CommandText = "PRAGMA user_version = 1;";
    cmd.ExecuteNonQuery();

    // Seed assets (deterministic timestamps)
    cmd.CommandText = @"
        INSERT INTO catalog_assets (source_path, fingerprint, size_bytes, created_utc, modified_utc, width, height, format, codec, rating, sidecar_path, sidecar_modified_utc, scanned_utc)
        VALUES ('C:\Photos\landscape.jpg', 'aabbccdd00112233aabbccdd00112233aabbccdd00112233aabbccdd00112233', 2048000, 1700000000, 1700100000, 4000, 3000, 'JPG', 'Jpeg', 5, 'C:\Photos\landscape.xmp', 1700100000, 1700200000);
    ";
    cmd.ExecuteNonQuery();

    cmd.CommandText = @"
        INSERT INTO catalog_assets (source_path, fingerprint, size_bytes, created_utc, modified_utc, width, height, format, codec, rating, sidecar_path, sidecar_modified_utc, scanned_utc)
        VALUES ('C:\Photos\portrait.png', 'eeff0011eeff0011eeff0011eeff0011eeff0011eeff0011eeff0011eeff0011', 1024000, 1700000100, 1700100100, 2000, 3000, 'PNG', 'Png', NULL, NULL, NULL, 1700200000);
    ";
    cmd.ExecuteNonQuery();

    cmd.CommandText = @"
        INSERT INTO catalog_assets (source_path, fingerprint, size_bytes, created_utc, modified_utc, width, height, format, codec, rating, sidecar_path, sidecar_modified_utc, scanned_utc)
        VALUES ('C:\Photos\raw\sunset.cr3', '1122334411223344112233441122334411223344112233441122334411223344', 30720000, 1700000200, 1700100200, 6720, 4480, 'CR3', 'Cr3', 3, NULL, NULL, 1700200000);
    ";
    cmd.ExecuteNonQuery();

    // Seed tags
    cmd.CommandText = "INSERT INTO catalog_tags (asset_id, tag) VALUES (1, 'landscape');";
    cmd.ExecuteNonQuery();
    cmd.CommandText = "INSERT INTO catalog_tags (asset_id, tag) VALUES (1, 'nature');";
    cmd.ExecuteNonQuery();
    cmd.CommandText = "INSERT INTO catalog_tags (asset_id, tag) VALUES (1, 'sunset');";
    cmd.ExecuteNonQuery();
    cmd.CommandText = "INSERT INTO catalog_tags (asset_id, tag) VALUES (3, 'sunset');";
    cmd.ExecuteNonQuery();
    cmd.CommandText = "INSERT INTO catalog_tags (asset_id, tag) VALUES (3, 'golden-hour');";
    cmd.ExecuteNonQuery();

    // Seed root
    cmd.CommandText = @"
        INSERT INTO catalog_roots (root_path, last_scanned_utc, indexed_count, failed_count)
        VALUES ('C:\Photos', 1700200000, 3, 0);
    ";
    cmd.ExecuteNonQuery();

    tx.Commit();
}

// Force WAL checkpoint and switch to DELETE journal for a single-file fixture
using (var wal = conn.CreateCommand())
{
    wal.CommandText = "PRAGMA wal_checkpoint(TRUNCATE); PRAGMA journal_mode = DELETE;";
    wal.ExecuteNonQuery();
}

conn.Close();
SqliteConnection.ClearAllPools();

// Delete any WAL/SHM sidecars
var walPath = dbPath + "-wal";
var shmPath = dbPath + "-shm";
if (File.Exists(walPath)) File.Delete(walPath);
if (File.Exists(shmPath)) File.Delete(shmPath);

Console.WriteLine($"Generated: {dbPath} ({new FileInfo(dbPath).Length} bytes)");

string GetScriptPath([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;
