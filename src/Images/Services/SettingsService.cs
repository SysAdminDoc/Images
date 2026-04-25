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

    public static readonly SettingsService Instance = new(DefaultDbPath());

    private static string DefaultDbPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Images");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "settings.db");
    }

    public SettingsService(string dbPath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

        try
        {
            EnsureSchema();
        }
        catch (SqliteException ex)
        {
            // Corrupt DB — quarantine and retry fresh. SCH-01: sidecars are authoritative;
            // losing the settings cache is recoverable (users just lose prefs and recent folders).
            _log.LogError(ex, "settings.db corrupt or unreadable — quarantining and starting fresh");
            QuarantineAndReset(dbPath);
            EnsureSchema();
        }
    }

    private static void QuarantineAndReset(string dbPath)
    {
        if (!File.Exists(dbPath)) return;
        var quarantinePath = dbPath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        try { File.Move(dbPath, quarantinePath); } catch { /* best-effort */ }
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
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM settings WHERE key = $k LIMIT 1;";
            cmd.Parameters.AddWithValue("$k", key);
            return cmd.ExecuteScalar() as string ?? defaultValue;
        }
        catch (SqliteException ex)
        {
            _log.LogWarning(ex, "GetString({Key}) failed — returning default", key);
            return defaultValue;
        }
    }

    public void SetString(string key, string value)
    {
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
        catch (SqliteException ex)
        {
            _log.LogWarning(ex, "SetString({Key}) failed", key);
        }
    }

    public bool GetBool(string key, bool defaultValue)
    {
        var raw = GetString(key);
        return raw is null ? defaultValue : raw.Equals("true", StringComparison.OrdinalIgnoreCase);
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
        catch (SqliteException ex)
        {
            _log.LogWarning(ex, "TouchRecentFolder failed");
        }
    }

    public IReadOnlyList<string> GetRecentFolders(int max = 10)
    {
        var result = new List<string>();
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
        catch (SqliteException ex)
        {
            _log.LogWarning(ex, "GetRecentFolders failed");
        }
        return result;
    }
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
}
