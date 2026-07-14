using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Images.Services;

/// <summary>
/// Opens every app-owned SQLite store with the same defensive connection policy. The native
/// runtime is pinned separately in the project file; this boundary makes its effective version
/// and the per-connection trusted-schema setting observable to tests and diagnostics.
/// </summary>
internal static class SqliteConnectionPolicy
{
    internal static readonly Version MinimumRuntimeVersion = new(3, 53, 2);

    public static SqliteConnection Open(string connectionString)
    {
        var connection = new SqliteConnection(connectionString);
        try
        {
            connection.Open();
            ConfigureAndVerify(connection);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    internal static string GetRuntimeVersion(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sqlite_version();";
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    internal static bool IsRuntimeVersionSupported(string? version)
        => Version.TryParse(version, out var parsed) && parsed >= MinimumRuntimeVersion;

    private static void ConfigureAndVerify(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA trusted_schema = OFF;
            PRAGMA busy_timeout = 5000;
            """;
        command.ExecuteNonQuery();

        command.CommandText = "PRAGMA trusted_schema;";
        var trustedSchema = Convert.ToInt32(command.ExecuteScalar() ?? 1, CultureInfo.InvariantCulture);
        if (trustedSchema != 0)
            throw new InvalidOperationException("SQLite trusted-schema hardening could not be enabled.");
    }
}
