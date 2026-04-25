using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Images.Services;

/// <summary>
/// V20-02: persistent settings + recent-folders MRU + hotkey overrides backed by SQLite at
/// <c>%LOCALAPPDATA%\Images\settings.db</c>. Per SCH-01 / SCH-03 the DB is a cache, not a source
/// of truth — on corruption we quarantine and start fresh. Schema migrations hop via
/// <c>PRAGMA user_version</c>; each bump adds one migration method.
///
/// Deliberately NO EF Core here — single-assembly viewer, a key/value table + two small
/// relations don't warrant the dep. Direct ADO.NET via Microsoft.Data.Sqlite is enough.
/// </summary>
public sealed class SettingsService
{
    private const int CurrentSchemaVersion = 1;
    private readonly string _connectionString;
    private readonly ILogger _log = Log.For<SettingsService>();
    private bool _isAvailable;

    public static readonly SettingsService Instance = CreateDefault();

    private SettingsService()
    {
        _connectionString = string.Empty;
        _isAvailable = false;
    }

    private static SettingsService CreateDefault()
    {
        try
        {
            var dir = AppStorage.TryGetAppDirectory();
            return dir is null
                ? new SettingsService()
                : new SettingsService(Path.Combine(dir, "settings.db"));
        }
        catch
        {
            return new SettingsService();
        }
    }

    public SettingsService(string dbPath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

        _isAvailable = TryEnsureSchema(dbPath);
    }

    private bool TryEnsureSchema(string dbPath)
    {
        try
        {
            EnsureSchema();
            return true;
        }
        catch (Exception ex) when (IsRecoverableStorageFailure(ex))
        {
            // Corrupt / locked / unreadable DB — quarantine and retry fresh. SCH-01: sidecars
            // are authoritative; losing the settings cache is recoverable.
            _log.LogError(ex, "settings.db unavailable — quarantining and starting fresh");
            if (!TryQuarantineAndReset(dbPath))
                return false;
        }

        try
        {
            EnsureSchema();
            return true;
        }
        catch (Exception ex) when (IsRecoverableStorageFailure(ex))
        {
            _log.LogError(ex, "settings.db unavailable after reset — disabling persistent settings for this session");
            return false;
        }
    }

    private static bool TryQuarantineAndReset(string dbPath)
    {
        SqliteConnection.ClearAllPools();

        if (!File.Exists(dbPath)) return true;
        var quarantinePath = dbPath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        try
        {
            File.Move(dbPath, quarantinePath);
            TryDeleteSqliteSidecar(dbPath + "-wal");
            TryDeleteSqliteSidecar(dbPath + "-shm");
            SqliteConnection.ClearAllPools();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryDeleteSqliteSidecar(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup; a later successful open will reconcile SQLite sidecars.
        }
    }

    private void EnsureSchema()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var current = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);

        // Hop migrations, never jump — SCH-04.
        if (current < 1) Migrate_0_to_1(conn);

        cmd.CommandText = $"PRAGMA user_version = {CurrentSchemaVersion};";
        cmd.ExecuteNonQuery();
    }

    private static void Migrate_0_to_1(SqliteConnection conn)
    {
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS settings (
                key   TEXT PRIMARY KEY NOT NULL,
                value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS recent_folders (
                path        TEXT PRIMARY KEY NOT NULL,
                last_opened INTEGER NOT NULL  -- unix seconds
            );
            CREATE INDEX IF NOT EXISTS ix_recent_folders_opened ON recent_folders(last_opened DESC);
            CREATE TABLE IF NOT EXISTS hotkeys (
                action    TEXT PRIMARY KEY NOT NULL,
                key       TEXT NOT NULL,
                modifiers TEXT NOT NULL DEFAULT ''
            );
            """;
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    // ---------------- Settings (generic string key/value) ----------------

    public string? GetString(string key, string? defaultValue = null)
    {
        if (!_isAvailable) return defaultValue;

        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM settings WHERE key = $k LIMIT 1;";
            cmd.Parameters.AddWithValue("$k", key);
            return cmd.ExecuteScalar() as string ?? defaultValue;
        }
        catch (Exception ex) when (IsRecoverableStorageFailure(ex))
        {
            _log.LogWarning(ex, "GetString({Key}) failed — returning default", key);
            return defaultValue;
        }
    }

    public void SetString(string key, string value)
    {
        if (!_isAvailable) return;

        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO settings (key, value) VALUES ($k, $v)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """;
            cmd.Parameters.AddWithValue("$k", key);
            cmd.Parameters.AddWithValue("$v", value);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (IsRecoverableStorageFailure(ex))
        {
            _log.LogWarning(ex, "SetString({Key}) failed", key);
        }
    }

    public bool GetBool(string key, bool defaultValue)
    {
        var raw = GetString(key);
        return bool.TryParse(raw, out var value) ? value : defaultValue;
    }

    public void SetBool(string key, bool value) => SetString(key, value ? "true" : "false");

    public int GetInt(string key, int defaultValue)
    {
        var raw = GetString(key);
        return int.TryParse(raw, out var v) ? v : defaultValue;
    }

    public void SetInt(string key, int value) => SetString(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public double GetDouble(string key, double defaultValue)
    {
        var raw = GetString(key);
        return double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : defaultValue;
    }

    public void SetDouble(string key, double value)
        => SetString(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    // ---------------- Recent folders MRU ----------------

    public void TouchRecentFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!_isAvailable) return;

        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO recent_folders (path, last_opened) VALUES ($p, $t)
                ON CONFLICT(path) DO UPDATE SET last_opened = excluded.last_opened;
                DELETE FROM recent_folders WHERE path NOT IN (
                    SELECT path FROM recent_folders ORDER BY last_opened DESC LIMIT 10
                );
                """;
            cmd.Parameters.AddWithValue("$p", path);
            cmd.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (IsRecoverableStorageFailure(ex))
        {
            _log.LogWarning(ex, "TouchRecentFolder failed");
        }
    }

    public IReadOnlyList<string> GetRecentFolders(int max = 10)
    {
        var result = new List<string>();
        if (!_isAvailable) return result;

        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT path FROM recent_folders ORDER BY last_opened DESC LIMIT $n;";
            cmd.Parameters.AddWithValue("$n", max);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var path = reader.GetString(0);
                // Skip folders that no longer exist — users rearrange drives, USB sticks come and go.
                if (Directory.Exists(path)) result.Add(path);
            }
        }
        catch (Exception ex) when (IsRecoverableStorageFailure(ex))
        {
            _log.LogWarning(ex, "GetRecentFolders failed");
        }
        return result;
    }

    private static bool IsRecoverableStorageFailure(Exception ex)
        => ex is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException;
}

/// <summary>
/// Strongly-typed setting keys so callers don't pass raw strings. Add keys here as new
/// settings surface — the compiler catches typos.
/// </summary>
public static class Keys
{
    public const string UpdateCheckEnabled = "update-check.enabled";
    public const string TelemetryEnabled   = "telemetry.enabled";

    // Window state
    public const string WindowLeft   = "window.left";
    public const string WindowTop    = "window.top";
    public const string WindowWidth  = "window.width";
    public const string WindowHeight = "window.height";
    public const string WindowMaximized = "window.maximized";

    // Viewer chrome
    public const string FilmstripVisible = "viewer.filmstrip.visible";
}
