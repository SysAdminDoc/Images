using System.Globalization;
using Images.Services;

namespace Images.Tests;

public sealed class SqliteConnectionPolicyTests
{
    [Fact]
    public void Open_DisablesTrustedSchemaAndMeetsRuntimeFloor()
    {
        using var connection = SqliteConnectionPolicy.Open("Data Source=:memory:");

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA trusted_schema;";
        Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture));

        var runtimeVersion = SqliteConnectionPolicy.GetRuntimeVersion(connection);
        Assert.True(
            SqliteConnectionPolicy.IsRuntimeVersionSupported(runtimeVersion),
            $"SQLite {runtimeVersion} is below {SqliteConnectionPolicy.MinimumRuntimeVersion}.");
    }

    [Theory]
    [InlineData("3.53.1", false)]
    [InlineData("3.53.2", true)]
    [InlineData("3.53.3", true)]
    [InlineData("invalid", false)]
    [InlineData(null, false)]
    public void IsRuntimeVersionSupported_EnforcesReviewedFloor(string? version, bool expected)
        => Assert.Equal(expected, SqliteConnectionPolicy.IsRuntimeVersionSupported(version));
}
