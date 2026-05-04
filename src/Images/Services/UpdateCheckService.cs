using System.IO;
using System.Buffers;
using System.Net.Http;
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
    private const int MaxReleaseResponseBytes = 64 * 1024;

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
            using var resp = await _http.GetAsync(ReleasesApiUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            var bytes = resp.Content.Headers.ContentLength ?? -1;
            var ms = (int)(DateTime.UtcNow - startedAt).TotalMilliseconds;
            _log.LogInformation("update-check: {Status} {Bytes} bytes in {Ms} ms", (int)resp.StatusCode, bytes, ms);

            if (!resp.IsSuccessStatusCode)
                return new CheckResult(false, null, null, $"HTTP {(int)resp.StatusCode}");

            var release = await ReadReleaseJsonAsync(resp.Content, ct).ConfigureAwait(false);
            if (release?.TagName is null)
                return new CheckResult(false, null, null, "no tag in response");

            var latest = ParseVersionTag(release.TagName);
            var current = ParseVersionTag("v" + AppInfo.Current.DisplayVersion);
            if (latest is null || current is null)
                return new CheckResult(false, release.TagName, release.HtmlUrl, "version parse failed");

            var newer = CompareVersion(latest.Value, current.Value) > 0;
            return new CheckResult(newer, release.TagName, NormalizeTrustedReleaseUrl(release.HtmlUrl), null);
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

    private static async Task<GitHubRelease?> ReadReleaseJsonAsync(HttpContent content, CancellationToken ct)
    {
        if (content.Headers.ContentLength is > MaxReleaseResponseBytes)
            throw new InvalidOperationException("update response too large");

        await using var input = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var bounded = new MemoryStream(capacity: Math.Min(MaxReleaseResponseBytes, 4096));
        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            while (true)
            {
                var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
                if (read == 0) break;
                if (bounded.Length + read > MaxReleaseResponseBytes)
                    throw new InvalidOperationException("update response too large");
                bounded.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        bounded.Position = 0;
        return await JsonSerializer.DeserializeAsync(bounded, ReleaseJsonContext.Default.GitHubRelease, ct).ConfigureAwait(false);
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

    private static string NormalizeTrustedReleaseUrl(string? htmlUrl)
    {
        if (!Uri.TryCreate(htmlUrl, UriKind.Absolute, out var uri))
            return ReleasesHtmlUrl;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return ReleasesHtmlUrl;

        if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            return ReleasesHtmlUrl;

        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.Equals("/SysAdminDoc/Images/releases/latest", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/SysAdminDoc/Images/releases/tag/", StringComparison.OrdinalIgnoreCase))
            return uri.AbsoluteUri;

        return ReleasesHtmlUrl;
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
                if (path is null) return null;
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
                if (path is null) return;
                var state = new UpdateState(value);
                File.WriteAllText(path, JsonSerializer.Serialize(state, UpdateStateJsonContext.Default.UpdateState));
            }
            catch { /* non-fatal */ }
        }
    }

    private static string? StateFilePath()
    {
        var dir = AppStorage.TryGetAppDirectory();
        return dir is null ? null : Path.Combine(dir, "update-check.json");
    }

    public static bool IsDueForBackgroundCheck()
    {
        // Respect user opt-out first — a disabled setting silently skips the network call.
        if (!SettingsService.Instance.GetBool(Keys.UpdateCheckEnabled, defaultValue: false))
            return false;

        var last = LastCheckedUtc;
        if (last is null) return true;
        return (DateTime.UtcNow - last.Value) >= TimeSpan.FromHours(24);
    }

    public static bool OptedIn
    {
        get => SettingsService.Instance.GetBool(Keys.UpdateCheckEnabled, defaultValue: false);
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
