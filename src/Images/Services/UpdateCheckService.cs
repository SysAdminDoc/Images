using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Images.Services;

/// <summary>
/// P-04: pull-only update check against the GitHub Releases API. No account, no telemetry, no
/// server-side record. The charter line is "network-egress transparency" — every call writes
/// its URL + byte count + duration to the structured log so P-03's visible-pane gets a real
/// event source when it lands.
/// </summary>
public static class UpdateCheckService
{
    private const string ReleasesApiUrl = "https://api.github.com/repos/SysAdminDoc/Images/releases/latest";
    private const string ReleasesHtmlUrl = "https://github.com/SysAdminDoc/Images/releases/latest";

    private static readonly HttpClient _http = CreateHttpClient();
    private static readonly Microsoft.Extensions.Logging.ILogger _log = Log.Get("Images.Updates");

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Images/{AppInfo.Current.DisplayVersion} (+https://github.com/SysAdminDoc/Images)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public sealed record CheckResult(
        bool NewerAvailable,
        string? LatestTag,
        string? LatestHtmlUrl,
        string? Error);

    /// <summary>
    /// Query the latest release tag. Compares semantic-ish strings — returns newer-available if
    /// the tag's "v0.1.N" parses to a strictly greater tuple than our current version. Any
    /// failure (timeout, network, 404) returns <see cref="CheckResult.Error"/> non-null instead
    /// of throwing; caller toasts the error.
    /// </summary>
    public static async Task<CheckResult> CheckAsync(CancellationToken ct = default)
    {
        var startedAt = DateTime.UtcNow;
        try
        {
            _log.LogInformation("update-check: GET {Url}", ReleasesApiUrl);
            using var resp = await _http.GetAsync(ReleasesApiUrl, ct).ConfigureAwait(false);
            var bytes = resp.Content.Headers.ContentLength ?? -1;
            var ms = (int)(DateTime.UtcNow - startedAt).TotalMilliseconds;
            _log.LogInformation("update-check: {Status} {Bytes} bytes in {Ms} ms", (int)resp.StatusCode, bytes, ms);

            if (!resp.IsSuccessStatusCode)
                return new CheckResult(false, null, null, $"HTTP {(int)resp.StatusCode}");

            var release = await resp.Content.ReadFromJsonAsync(ReleaseJsonContext.Default.GitHubRelease, ct).ConfigureAwait(false);
            if (release?.TagName is null)
                return new CheckResult(false, null, null, "no tag in response");

            var latest = ParseVersionTag(release.TagName);
            var current = ParseVersionTag("v" + AppInfo.Current.DisplayVersion);
            if (latest is null || current is null)
                return new CheckResult(false, release.TagName, release.HtmlUrl, "version parse failed");

            var newer = CompareVersion(latest.Value, current.Value) > 0;
            return new CheckResult(newer, release.TagName, release.HtmlUrl ?? ReleasesHtmlUrl, null);
        }
        catch (TaskCanceledException)
        {
            return new CheckResult(false, null, null, "timed out");
        }
        catch (HttpRequestException ex)
        {
            return new CheckResult(false, null, null, $"network: {ex.Message}");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "update-check unexpected failure");
            return new CheckResult(false, null, null, $"unexpected: {ex.Message}");
        }
    }

    private static (int major, int minor, int patch)? ParseVersionTag(string tag)
    {
        var s = tag.TrimStart('v', 'V');
        var parts = s.Split('.');
        if (parts.Length < 3) return null;
        if (!int.TryParse(parts[0], out var major)) return null;
        if (!int.TryParse(parts[1], out var minor)) return null;
        // Patch may have a pre-release suffix — strip anything after the first non-digit.
        var patchStr = new string(parts[2].TakeWhile(char.IsDigit).ToArray());
        if (!int.TryParse(patchStr, out var patch)) return null;
        return (major, minor, patch);
    }

    private static int CompareVersion((int major, int minor, int patch) a, (int major, int minor, int patch) b)
    {
        if (a.major != b.major) return a.major.CompareTo(b.major);
        if (a.minor != b.minor) return a.minor.CompareTo(b.minor);
        return a.patch.CompareTo(b.patch);
    }

    /// <summary>
    /// Persist + check the "last update check" timestamp. Skip the network call if we checked
    /// within the last 24 h. Manual invocations (UI button) bypass this gate.
    /// </summary>
    public static DateTime? LastCheckedUtc
    {
        get
        {
            try
            {
                var path = StateFilePath();
                if (!File.Exists(path)) return null;
                var state = JsonSerializer.Deserialize(File.ReadAllText(path), UpdateStateJsonContext.Default.UpdateState);
                return state?.LastCheckedUtc;
            }
            catch { return null; }
        }
        set
        {
            try
            {
                var path = StateFilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var state = new UpdateState(value);
                File.WriteAllText(path, JsonSerializer.Serialize(state, UpdateStateJsonContext.Default.UpdateState));
            }
            catch { /* non-fatal */ }
        }
    }

    private static string StateFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Images", "update-check.json");

    public static bool IsDueForBackgroundCheck()
    {
        // Respect user opt-out first — a disabled setting silently skips the network call.
        if (!SettingsService.Instance.GetBool(Keys.UpdateCheckEnabled, defaultValue: true))
            return false;

        var last = LastCheckedUtc;
        if (last is null) return true;
        return (DateTime.UtcNow - last.Value) >= TimeSpan.FromHours(24);
    }

    public static bool OptedIn
    {
        get => SettingsService.Instance.GetBool(Keys.UpdateCheckEnabled, defaultValue: true);
        set => SettingsService.Instance.SetBool(Keys.UpdateCheckEnabled, value);
    }
}

internal sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")] public string? TagName { get; set; }
    [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
    [JsonPropertyName("name")]     public string? Name { get; set; }
}

[JsonSerializable(typeof(GitHubRelease))]
internal partial class ReleaseJsonContext : JsonSerializerContext { }

internal sealed record UpdateState(DateTime? LastCheckedUtc);

[JsonSerializable(typeof(UpdateState))]
internal partial class UpdateStateJsonContext : JsonSerializerContext { }
