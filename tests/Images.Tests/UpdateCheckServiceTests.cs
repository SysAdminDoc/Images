using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using Images.Services;

namespace Images.Tests;

public sealed class UpdateCheckServiceTests
{
    [Fact]
    public async Task CheckAsync_WhenNewerTrustedReleaseExists_ReturnsUpdate()
    {
        using var http = ClientReturningJson("""
            {
              "tag_name": "v99.0.0",
              "html_url": "https://github.com/SysAdminDoc/Images/releases/tag/v99.0.0"
            }
            """);

        var result = await UpdateCheckService.CheckAsync(http, FixedClock());

        Assert.True(result.NewerAvailable);
        Assert.Equal("v99.0.0", result.LatestTag);
        Assert.Equal("https://github.com/SysAdminDoc/Images/releases/tag/v99.0.0", result.LatestHtmlUrl);
        Assert.Null(result.Error);
        Assert.True(result.ShouldUpdateLastChecked);
    }

    [Fact]
    public async Task CheckAsync_WhenReleaseIsCurrent_ReturnsNoUpdate()
    {
        using var http = ClientReturningJson($$"""
            {
              "tag_name": "v{{AppInfo.Current.DisplayVersion}}",
              "html_url": "https://github.com/SysAdminDoc/Images/releases/latest"
            }
            """);

        var result = await UpdateCheckService.CheckAsync(http, FixedClock());

        Assert.False(result.NewerAvailable);
        Assert.Equal($"v{AppInfo.Current.DisplayVersion}", result.LatestTag);
        Assert.Null(result.Error);
        Assert.True(result.ShouldUpdateLastChecked);
    }

    [Fact]
    public async Task CheckAsync_WhenReleaseUrlIsUntrusted_FallsBackToLatestReleaseUrl()
    {
        using var http = ClientReturningJson("""
            {
              "tag_name": "v99.0.0",
              "html_url": "https://example.com/malware"
            }
            """);

        var result = await UpdateCheckService.CheckAsync(http, FixedClock());

        Assert.True(result.NewerAvailable);
        Assert.Equal("https://github.com/SysAdminDoc/Images/releases/latest", result.LatestHtmlUrl);
    }

    [Fact]
    public async Task CheckAsync_WhenHttpFails_ReturnsCompletedErrorForThrottle()
    {
        using var http = ClientReturning(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await UpdateCheckService.CheckAsync(http, FixedClock());

        Assert.False(result.NewerAvailable);
        Assert.Equal("HTTP 500", result.Error);
        Assert.True(result.ShouldUpdateLastChecked);
    }

    [Fact]
    public async Task CheckAsync_WhenPayloadHasNoTag_ReturnsCompletedErrorForThrottle()
    {
        using var http = ClientReturningJson("{}");

        var result = await UpdateCheckService.CheckAsync(http, FixedClock());

        Assert.Equal("no tag in response", result.Error);
        Assert.True(result.ShouldUpdateLastChecked);
    }

    [Fact]
    public async Task CheckAsync_WhenNetworkFails_DoesNotUpdateLastChecked()
    {
        using var http = ClientThrowing(new HttpRequestException("offline"));

        var result = await UpdateCheckService.CheckAsync(http, FixedClock());

        Assert.StartsWith("network: offline", result.Error);
        Assert.False(result.ShouldUpdateLastChecked);
    }

    [Fact]
    public async Task CheckAsync_WhenTimedOut_DoesNotUpdateLastChecked()
    {
        using var http = ClientThrowing(new TaskCanceledException("slow"));

        var result = await UpdateCheckService.CheckAsync(http, FixedClock());

        Assert.Equal("timed out", result.Error);
        Assert.False(result.ShouldUpdateLastChecked);
    }

    [Fact]
    public void RecordLastCheckedIfAppropriate_WritesOnlyForCompletedChecks()
    {
        var recorded = new List<DateTime?>();
        var now = new DateTime(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc);
        var completed = new UpdateCheckService.CheckResult(false, null, null, null, true);
        var transient = new UpdateCheckService.CheckResult(false, null, null, "timed out", false);

        UpdateCheckService.RecordLastCheckedIfAppropriate(completed, recorded.Add, () => now);
        UpdateCheckService.RecordLastCheckedIfAppropriate(transient, recorded.Add, () => now.AddHours(1));

        Assert.Equal([now], recorded);
    }

    [Fact]
    public void IsDueForBackgroundCheck_UsesOptInAndTwentyFourHourWindow()
    {
        var now = new DateTime(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(UpdateCheckService.IsDueForBackgroundCheck(false, null, now));
        Assert.True(UpdateCheckService.IsDueForBackgroundCheck(true, null, now));
        Assert.False(UpdateCheckService.IsDueForBackgroundCheck(true, now.AddHours(-2), now));
        Assert.True(UpdateCheckService.IsDueForBackgroundCheck(true, now.AddHours(-25), now));
        Assert.True(UpdateCheckService.IsDueForBackgroundCheck(true, now.AddMinutes(10), now));
    }

    [Fact]
    public void LastCheckedStateFile_RoundTripsAndRejectsOversizedState()
    {
        using var temp = TestDirectory.Create();
        var statePath = Path.Combine(temp.Path, "update-check.json");
        var expected = new DateTime(2026, 5, 5, 12, 30, 0, DateTimeKind.Utc);

        UpdateCheckService.WriteLastCheckedUtcToFile(statePath, expected);

        Assert.Equal(expected, UpdateCheckService.ReadLastCheckedUtcFromFile(statePath));

        File.WriteAllText(statePath, new string('x', 16 * 1024 + 1));
        Assert.Null(UpdateCheckService.ReadLastCheckedUtcFromFile(statePath));
    }

    private static Func<DateTime> FixedClock()
        => () => new DateTime(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc);

    private static HttpClient ClientReturningJson(string json)
        => ClientReturning(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

    private static HttpClient ClientReturning(HttpResponseMessage response)
        => new(new StubHandler((_, _) => Task.FromResult(response)));

    private static HttpClient ClientThrowing(Exception exception)
        => new(new StubHandler((_, _) => Task.FromException<HttpResponseMessage>(exception)));

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        {
            _send = send;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _send(request, cancellationToken);
    }
}
