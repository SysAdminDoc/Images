using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public static class C2paManifestService
{
    private const int ReadTimeoutMilliseconds = 10_000;
    private static readonly ILogger _log = Log.Get(nameof(C2paManifestService));

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jpe", ".jfif",
        ".png",
        ".webp",
        ".avif",
        ".heic", ".heif",
        ".tiff", ".tif",
        ".gif",
        ".svg",
        ".mp4", ".mov",
        ".pdf",
    };

    public static C2paInspectionResult Read(string path)
        => Read(path, null, null);

    internal static C2paInspectionResult Read(
        string path,
        string? executableOverride,
        Func<ProcessStartInfo, (int ExitCode, string Stdout, string Stderr)>? processRunner)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return C2paInspectionResult.NoManifest("File not found.");

        var ext = Path.GetExtension(path);
        if (!SupportedExtensions.Contains(ext))
            return C2paInspectionResult.NoManifest("File format does not support C2PA content credentials.");

        var runtime = executableOverride is not null
            ? new C2paToolRuntimeStatus(true, executableOverride, "test", null, null, "test")
            : C2paToolRuntime.Inspect();

        if (!runtime.Available || runtime.ExecutablePath is null)
            return C2paInspectionResult.RuntimeUnavailable(runtime.StatusText);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = runtime.ExecutablePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
            };
            psi.ArgumentList.Add(NormalizePath(path));
            psi.EnvironmentVariables["C2PATOOL_TRUST_ANCHORS"] ??= "";
            psi.EnvironmentVariables["C2PATOOL_TRUST_CONFIG"] ??= "";

            int exitCode;
            string stdout, stderr;

            if (processRunner is not null)
            {
                (exitCode, stdout, stderr) = processRunner(psi);
            }
            else
            {
                using var process = Process.Start(psi);
                if (process is null)
                {
                    _log.LogWarning("Failed to start c2patool process for {Path}", path);
                    return C2paInspectionResult.Error("Failed to start c2patool process.");
                }

                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(ReadTimeoutMilliseconds))
                {
                    _log.LogWarning("c2patool timed out reading {Path}", path);
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "Could not kill timed-out c2patool process for {Path}", path);
                    }

                    return C2paInspectionResult.Error("c2patool timed out reading this file.");
                }

                exitCode = process.ExitCode;
                stdout = stdoutTask.GetAwaiter().GetResult();
                stderr = stderrTask.GetAwaiter().GetResult();
            }

            if (exitCode != 0)
            {
                if (stderr.Contains("no claim found", StringComparison.OrdinalIgnoreCase) ||
                    stderr.Contains("no manifest", StringComparison.OrdinalIgnoreCase) ||
                    stderr.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                    stdout.Contains("no claim found", StringComparison.OrdinalIgnoreCase))
                {
                    return C2paInspectionResult.NoManifest("No C2PA content credentials found in this file.");
                }

                _log.LogWarning(
                    "c2patool exited with code {ExitCode} for {Path}: {Error}",
                    exitCode,
                    path,
                    TrimToFirstLine(stderr));
                return C2paInspectionResult.Error(
                    $"c2patool exited with code {exitCode}: {TrimToFirstLine(stderr)}");
            }

            if (string.IsNullOrWhiteSpace(stdout))
                return C2paInspectionResult.NoManifest("No C2PA content credentials found in this file.");

            return ParseManifestJson(stdout);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to inspect C2PA manifest for {Path}", path);
            return C2paInspectionResult.Error($"Failed to inspect C2PA manifest: {ex.Message}");
        }
    }

    internal static C2paInspectionResult ParseManifestJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });

            var root = doc.RootElement;
            var manifests = new List<C2paClaim>();
            var activeManifestLabel = TryGetString(root, "active_manifest");

            if (root.TryGetProperty("manifests", out var manifestsObj) &&
                manifestsObj.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in manifestsObj.EnumerateObject())
                {
                    var claim = ParseClaim(prop.Name, prop.Value);
                    if (claim is not null) manifests.Add(claim);
                }
            }
            else
            {
                var singleClaim = ParseClaim(activeManifestLabel ?? "unknown", root);
                if (singleClaim is not null) manifests.Add(singleClaim);
            }

            if (manifests.Count == 0)
                return C2paInspectionResult.NoManifest("C2PA manifest structure present but contained no readable claims.");

            var activeClaim = manifests.FirstOrDefault(m => m.Label == activeManifestLabel) ?? manifests[^1];

            var trustLevel = DetermineTrustLevel(root, activeClaim);

            return new C2paInspectionResult(
                Status: C2paStatus.Found,
                TrustLevel: trustLevel,
                ActiveManifestLabel: activeManifestLabel,
                Claims: manifests,
                ErrorMessage: null,
                RawJson: json);
        }
        catch (JsonException ex)
        {
            _log.LogWarning(ex, "Failed to parse C2PA manifest JSON");
            return C2paInspectionResult.Error($"Failed to parse C2PA manifest JSON: {ex.Message}");
        }
    }

    private static C2paClaim? ParseClaim(string label, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;

        var title = TryGetString(element, "title") ?? TryGetString(element, "dc:title");
        var format = TryGetString(element, "format") ?? TryGetString(element, "dc:format");
        var instanceId = TryGetString(element, "instance_id") ?? TryGetString(element, "instanceID");
        var claimGenerator = TryGetString(element, "claim_generator") ??
                             TryGetString(element, "claim_generator_info", "name");

        string? signatureDate = null;
        if (element.TryGetProperty("signature_info", out var sigInfo))
        {
            signatureDate = TryGetString(sigInfo, "time") ??
                            TryGetString(sigInfo, "date");
        }

        string? signatureIssuer = null;
        if (element.TryGetProperty("signature_info", out var sigInfo2))
        {
            signatureIssuer = TryGetString(sigInfo2, "issuer") ??
                              TryGetString(sigInfo2, "cert_serial_number");
        }

        var assertions = new List<C2paAssertion>();
        if (element.TryGetProperty("assertions", out var assertionsArray) &&
            assertionsArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var assertionElem in assertionsArray.EnumerateArray())
            {
                var assertionLabel = TryGetString(assertionElem, "label") ?? "unknown";
                var summary = SummarizeAssertion(assertionLabel, assertionElem);
                assertions.Add(new C2paAssertion(assertionLabel, summary));
            }
        }

        var ingredients = new List<C2paIngredient>();
        if (element.TryGetProperty("ingredients", out var ingredientsArray) &&
            ingredientsArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var ingredientElem in ingredientsArray.EnumerateArray())
            {
                var ingredientTitle = TryGetString(ingredientElem, "title") ?? "unknown";
                var ingredientFormat = TryGetString(ingredientElem, "format");
                var relationship = TryGetString(ingredientElem, "relationship") ?? "unknown";
                ingredients.Add(new C2paIngredient(ingredientTitle, ingredientFormat, relationship));
            }
        }

        return new C2paClaim(
            Label: label,
            Title: title,
            Format: format,
            InstanceId: instanceId,
            ClaimGenerator: claimGenerator,
            SignatureDate: signatureDate,
            SignatureIssuer: signatureIssuer,
            Assertions: assertions,
            Ingredients: ingredients);
    }

    private static string SummarizeAssertion(string label, JsonElement element)
    {
        if (label.StartsWith("c2pa.actions", StringComparison.OrdinalIgnoreCase))
        {
            if (element.TryGetProperty("data", out var data) &&
                data.TryGetProperty("actions", out var actions) &&
                actions.ValueKind == JsonValueKind.Array)
            {
                var actionNames = new List<string>();
                foreach (var action in actions.EnumerateArray())
                {
                    var name = TryGetString(action, "action");
                    if (name is not null) actionNames.Add(name);
                }
                return actionNames.Count > 0
                    ? string.Join(", ", actionNames)
                    : "actions recorded";
            }
        }

        if (label.StartsWith("c2pa.hash", StringComparison.OrdinalIgnoreCase))
        {
            if (element.TryGetProperty("data", out var data))
            {
                var alg = TryGetString(data, "name") ?? TryGetString(data, "alg");
                return alg is not null ? $"hash: {alg}" : "content hash";
            }
        }

        if (label.StartsWith("stds.schema", StringComparison.OrdinalIgnoreCase) ||
            label.StartsWith("stds.exif", StringComparison.OrdinalIgnoreCase))
        {
            return "embedded metadata";
        }

        return label;
    }

    private static C2paTrustLevel DetermineTrustLevel(JsonElement root, C2paClaim activeClaim)
    {
        if (root.TryGetProperty("validation_status", out var validation) &&
            validation.ValueKind == JsonValueKind.Array)
        {
            var hasFailure = false;
            foreach (var status in validation.EnumerateArray())
            {
                var code = TryGetString(status, "code") ?? "";
                if (code.Contains("failure", StringComparison.OrdinalIgnoreCase) ||
                    code.Contains("error", StringComparison.OrdinalIgnoreCase))
                {
                    hasFailure = true;
                    break;
                }
            }

            return hasFailure ? C2paTrustLevel.Invalid : C2paTrustLevel.Signed;
        }

        return activeClaim.SignatureIssuer is not null
            ? C2paTrustLevel.Signed
            : C2paTrustLevel.Present;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }
        return null;
    }

    private static string? TryGetString(JsonElement element, string property1, string property2)
    {
        if (element.TryGetProperty(property1, out var nested) &&
            nested.ValueKind == JsonValueKind.Object)
        {
            return TryGetString(nested, property2);
        }
        return null;
    }

    private static string TrimToFirstLine(string text)
    {
        var trimmed = text.Trim();
        var newline = trimmed.IndexOfAny(['\r', '\n']);
        return newline >= 0 ? trimmed[..newline] : trimmed;
    }

    private static string NormalizePath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            _log.LogWarning(ex, "Could not normalize C2PA target path {Path}", path);
            return path;
        }
    }
}

public enum C2paStatus
{
    Found,
    NoManifest,
    RuntimeUnavailable,
    Error,
}

public enum C2paTrustLevel
{
    Present,
    Signed,
    Invalid,
}

public sealed record C2paInspectionResult(
    C2paStatus Status,
    C2paTrustLevel TrustLevel,
    string? ActiveManifestLabel,
    IReadOnlyList<C2paClaim> Claims,
    string? ErrorMessage,
    string? RawJson)
{
    public bool HasCredentials => Status == C2paStatus.Found && Claims.Count > 0;

    public static C2paInspectionResult NoManifest(string message) => new(
        Status: C2paStatus.NoManifest,
        TrustLevel: C2paTrustLevel.Present,
        ActiveManifestLabel: null,
        Claims: [],
        ErrorMessage: message,
        RawJson: null);

    public static C2paInspectionResult RuntimeUnavailable(string message) => new(
        Status: C2paStatus.RuntimeUnavailable,
        TrustLevel: C2paTrustLevel.Present,
        ActiveManifestLabel: null,
        Claims: [],
        ErrorMessage: message,
        RawJson: null);

    public static C2paInspectionResult Error(string message) => new(
        Status: C2paStatus.Error,
        TrustLevel: C2paTrustLevel.Present,
        ActiveManifestLabel: null,
        Claims: [],
        ErrorMessage: message,
        RawJson: null);
}

public sealed record C2paClaim(
    string Label,
    string? Title,
    string? Format,
    string? InstanceId,
    string? ClaimGenerator,
    string? SignatureDate,
    string? SignatureIssuer,
    IReadOnlyList<C2paAssertion> Assertions,
    IReadOnlyList<C2paIngredient> Ingredients);

public sealed record C2paAssertion(string Label, string Summary);

public sealed record C2paIngredient(string Title, string? Format, string Relationship);
