using System.IO;
using System.Text.Json;
using Images.Services;

namespace Images.Tests;

public sealed class NetworkEgressServiceTests
{
    [Fact]
    public void ReadPersistedEntriesForDisplay_ReturnsNewestLoadedEntriesFirst()
    {
        var lines = Enumerable.Range(0, 6)
            .Select(index => Serialize(Entry(index)))
            .ToArray();

        var entries = NetworkEgressService.ReadPersistedEntriesForDisplay(lines, maxLines: 3);

        Assert.Collection(
            entries,
            entry => Assert.Equal("https://example.com/5", entry.Url),
            entry => Assert.Equal("https://example.com/4", entry.Url),
            entry => Assert.Equal("https://example.com/3", entry.Url));
    }

    [Fact]
    public void ReadPersistedEntriesForDisplay_SkipsMalformedLines()
    {
        var entries = NetworkEgressService.ReadPersistedEntriesForDisplay(
            ["", "{not-json", Serialize(Entry(1))],
            maxLines: 10);

        var entry = Assert.Single(entries);
        Assert.Equal("https://example.com/1", entry.Url);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TakeLatestLines_WhenLimitIsNotPositive_ReturnsEmpty(int maxLines)
    {
        Assert.Empty(NetworkEgressService.TakeLatestLines(["old", "new"], maxLines));
    }

    [Fact]
    public void RotateIfNeeded_TrimsOnlyWhenPersistedCountExceedsLimit()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "network-egress.jsonl");
        File.WriteAllLines(path, Enumerable.Range(0, NetworkEgressService.MaxPersistedEntries + 1)
            .Select(index => $"line-{index}"));

        NetworkEgressService.RotateIfNeeded(path, NetworkEgressService.MaxPersistedEntries);
        Assert.Equal(NetworkEgressService.MaxPersistedEntries + 1, File.ReadAllLines(path).Length);

        NetworkEgressService.RotateIfNeeded(path, NetworkEgressService.MaxPersistedEntries + 1);

        var lines = File.ReadAllLines(path);
        Assert.Equal(NetworkEgressService.MaxPersistedEntries, lines.Length);
        Assert.Equal("line-1", lines[0]);
    }

    private static NetworkEgressEntry Entry(int index)
        => new(
            Url: $"https://example.com/{index}",
            Purpose: $"test {index}",
            Bytes: index,
            DurationMs: index * 10,
            Timestamp: new DateTimeOffset(2026, 6, 19, 12, index, 0, TimeSpan.Zero),
            Direction: index % 2 == 0 ? "outbound" : "inbound");

    private static string Serialize(NetworkEgressEntry entry)
        => JsonSerializer.Serialize(entry, EgressJsonContext.Default.NetworkEgressEntry);
}
