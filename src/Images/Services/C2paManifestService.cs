using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public static class C2paManifestService
{
    private const int ReadTimeoutMilliseconds = 10_000;
    private const string NoRemoteSettingsFileName = "c2patool-no-remote-settings.json";
    internal const string NoRemoteSettingsJson = """
        {
          "version": 1,
          "verify": {
            "remote_manifest_fetch": false,
            "ocsp_fetch": false
          }
        }
        """;
    private static readonly ILogger _log = Log.Get(nameof(C2paManifestService));

    private static readonly HashSet<string> SupportedExtensionSet = new(StringComparer.OrdinalIgnoreCase)
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

    public static bool IsSupportedExtension(string extension)
        => SupportedExtensionSet.Contains(NormalizeExtension(extension));

    public static C2paExportHandoff PlanExportHandoff(string? sourcePath, string targetExtension)
        => PlanExportHandoff(
            sourcePath,
            targetExtension,
            readManifest: null,
            inspectRuntime: null,
            inspectWriter: null);

    internal static C2paExportHandoff PlanExportHandoff(
        string? sourcePath,
        string targetExtension,
        Func<string, C2paInspectionResult>? readManifest,
        Func<C2paToolRuntimeStatus>? inspectRuntime,
        Func<C2paExportWriterRuntimeStatus>? inspectWriter)
    {
        var targetExt = NormalizeExtension(targetExtension);
        if (string.IsNullOrEmpty(targetExt) || !SupportedExtensionSet.Contains(targetExt))
        {
            return C2paExportHandoff.Omitted(
                C2paExportReason.TargetFormatUnsupported,
                "C2PA omitted",
                $"Target {FormatExtension(targetExt)} is not a C2PA-supported export format; Images will not write Content Credentials.");
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return C2paExportHandoff.Omitted(
                C2paExportReason.SourcePathUnavailable,
                "C2PA not written",
                "This export is based on a decoded bitmap without a source file, so Images cannot inspect or write source Content Credentials.");
        }

        string normalizedSourcePath;
        try
        {
            normalizedSourcePath = Path.GetFullPath(sourcePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            _log.LogWarning(ex, "Could not normalize C2PA export source path {Path}", sourcePath);
            return C2paExportHandoff.Omitted(
                C2paExportReason.SourcePathUnavailable,
                "C2PA not written",
                "The source path could not be normalized for C2PA inspection; export will not write Content Credentials.");
        }

        if (!File.Exists(normalizedSourcePath))
        {
            return C2paExportHandoff.Omitted(
                C2paExportReason.SourceFileMissing,
                "C2PA not written",
                "The source file was not available for C2PA inspection; export will not write Content Credentials.");
        }

        var sourceExt = NormalizeExtension(Path.GetExtension(normalizedSourcePath));
        if (!SupportedExtensionSet.Contains(sourceExt))
        {
            return C2paExportHandoff.Omitted(
                C2paExportReason.SourceFormatUnsupported,
                "C2PA not written",
                $"Source {FormatExtension(sourceExt)} is not a C2PA-supported source format; export will not write Content Credentials.");
        }

        var runtime = (inspectRuntime ?? C2paToolRuntime.Inspect)();
        if (!runtime.Available || string.IsNullOrWhiteSpace(runtime.ExecutablePath))
        {
            return C2paExportHandoff.Omitted(
                C2paExportReason.InspectionRuntimeUnavailable,
                "C2PA not written",
                $"{runtime.StatusText} Export will not write Content Credentials.",
                sourceInspection: null,
                inspectionRuntime: runtime);
        }

        var sourceInspection = readManifest is not null
            ? readManifest(normalizedSourcePath)
            : Read(normalizedSourcePath, runtime.ExecutablePath, processRunner: null);

        if (sourceInspection.Status == C2paStatus.NoManifest)
        {
            return C2paExportHandoff.Omitted(
                C2paExportReason.SourceHasNoManifest,
                "C2PA not written",
                "No source Content Credentials were found; export will not create a new C2PA manifest.",
                sourceInspection,
                runtime);
        }

        if (sourceInspection.Status == C2paStatus.RuntimeUnavailable)
        {
            return C2paExportHandoff.Omitted(
                C2paExportReason.InspectionRuntimeUnavailable,
                "C2PA not written",
                $"{sourceInspection.ErrorMessage ?? runtime.StatusText} Export will not write Content Credentials.",
                sourceInspection,
                runtime);
        }

        if (sourceInspection.Status == C2paStatus.Error)
        {
            return C2paExportHandoff.Omitted(
                C2paExportReason.InspectionFailed,
                "C2PA not written",
                $"Source Content Credentials could not be inspected: {sourceInspection.ErrorMessage ?? "unknown C2PA inspection error"}",
                sourceInspection,
                runtime);
        }

        var writer = (inspectWriter ?? C2paExportWriterRuntimeStatus.NotConfigured)();
        if (!writer.Available)
        {
            var reason = writer.Source.Equals("Not configured", StringComparison.OrdinalIgnoreCase)
                ? C2paExportReason.WriterNotConfigured
                : C2paExportReason.WriterRuntimeUnavailable;
            return C2paExportHandoff.Omitted(
                reason,
                "C2PA omitted",
                $"{writer.StatusText} Export will not copy stale source credentials.",
                sourceInspection,
                runtime,
                writer);
        }

        if (!writer.Approved)
        {
            return C2paExportHandoff.Omitted(
                C2paExportReason.WriterNotApproved,
                "C2PA omitted",
                $"{writer.StatusText} Export will not write Content Credentials until the writer is approved.",
                sourceInspection,
                runtime,
                writer);
        }

        return new C2paExportHandoff(
            C2paExportAction.WriteWithRuntime,
            C2paExportReason.ReadyToWrite,
            "C2PA will be written",
            $"Source Content Credentials will be handed to {writer.Source} for an approved C2PA export write.",
            sourceInspection,
            runtime,
            writer);
    }

    internal static C2paInspectionResult Read(
        string path,
        string? executableOverride,
        Func<ProcessStartInfo, (int ExitCode, string Stdout, string Stderr)>? processRunner)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return C2paInspectionResult.NoManifest("File not found.");

        var ext = Path.GetExtension(path);
        if (!SupportedExtensionSet.Contains(ext))
            return C2paInspectionResult.NoManifest("File format does not support C2PA content credentials.");

        var runtime = executableOverride is not null
            ? new C2paToolRuntimeStatus(true, executableOverride, "test", null, null, "test")
            : C2paToolRuntime.Inspect();

        if (!runtime.Available || runtime.ExecutablePath is null)
            return C2paInspectionResult.RuntimeUnavailable(runtime.StatusText);

        try
        {
            var settingsPath = EnsureNoRemoteManifestSettingsFile();
            if (settingsPath is null)
            {
                return C2paInspectionResult.Error(
                    "C2PA inspection skipped because Images could not create the no-network c2patool settings file.");
            }

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
            psi.ArgumentList.Add("--settings");
            psi.ArgumentList.Add(settingsPath);
            EnsureEnvironmentVariable(psi, "C2PATOOL_TRUST_ANCHORS", "");
            EnsureEnvironmentVariable(psi, "C2PATOOL_TRUST_CONFIG", "");

            int exitCode;
            string stdout = "";
            string stderr = "";
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (processRunner is not null)
                {
                    (exitCode, stdout, stderr) = processRunner(psi);
                }
                else
                {
                    var result = BoundedProcessRunner.Run(
                        psi,
                        ReadTimeoutMilliseconds,
                        BoundedProcessRunner.OperationOutputLimitBytes,
                        BoundedProcessRunner.OperationOutputLimitBytes);
                    stdout = result.StandardOutput;
                    stderr = result.StandardError;
                    if (result.Status == BoundedProcessStatus.StartFailed)
                    {
                        _log.LogWarning("Failed to start c2patool process for {Path}", path);
                        return C2paInspectionResult.Error("Failed to start c2patool process.");
                    }

                    if (result.TimedOut)
                    {
                        _log.LogWarning("c2patool timed out reading {Path}", path);
                        return C2paInspectionResult.Error("c2patool timed out reading this file.");
                    }

                    if (result.OutputLimitExceeded)
                    {
                        _log.LogWarning("c2patool exceeded its output limit reading {Path}", path);
                        return C2paInspectionResult.Error("c2patool output exceeded the 4 MiB safety limit.");
                    }

                    exitCode = result.ExitCode ?? -1;
                }
            }
            finally
            {
                stopwatch.Stop();
                NetworkEgressService.Record(
                    "process://c2patool/inspect",
                    "C2PA inspection via c2patool (remote manifest fetch disabled)",
                    Encoding.UTF8.GetByteCount(stdout) + Encoding.UTF8.GetByteCount(stderr),
                    stopwatch.ElapsedMilliseconds);
            }

            if (exitCode != 0)
            {
                if (stderr.Contains("no claim found", StringComparison.OrdinalIgnoreCase) ||
                    stderr.Contains("no manifest", StringComparison.OrdinalIgnoreCase) ||
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

    private static string? EnsureNoRemoteManifestSettingsFile()
    {
        try
        {
            var settingsDirectory = AppStorage.TryGetAppDirectory("c2patool");
            if (settingsDirectory is null)
                return null;

            var settingsPath = Path.Combine(settingsDirectory, NoRemoteSettingsFileName);
            if (!File.Exists(settingsPath) ||
                !string.Equals(File.ReadAllText(settingsPath, Encoding.UTF8), NoRemoteSettingsJson, StringComparison.Ordinal))
            {
                File.WriteAllText(settingsPath, NoRemoteSettingsJson, Encoding.UTF8);
            }

            return settingsPath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or NotSupportedException)
        {
            _log.LogWarning(ex, "Could not create c2patool no-network settings file");
            return null;
        }
    }

    private static void EnsureEnvironmentVariable(ProcessStartInfo psi, string key, string value)
    {
        if (!psi.EnvironmentVariables.ContainsKey(key))
            psi.EnvironmentVariables[key] = value;
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return string.Empty;
        var normalized = RenameService.NormalizeExtension(extension);
        return normalized.ToLowerInvariant();
    }

    private static string FormatExtension(string extension)
        => string.IsNullOrWhiteSpace(extension) ? "format" : extension;
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

public enum C2paExportAction
{
    Preserve,
    WriteWithRuntime,
    Omit,
}

public enum C2paExportReason
{
    PreservedSource,
    ReadyToWrite,
    SourcePathUnavailable,
    SourceFileMissing,
    SourceFormatUnsupported,
    TargetFormatUnsupported,
    SourceHasNoManifest,
    InspectionRuntimeUnavailable,
    InspectionFailed,
    WriterNotConfigured,
    WriterRuntimeUnavailable,
    WriterNotApproved,
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

public sealed record C2paExportHandoff(
    C2paExportAction Action,
    C2paExportReason Reason,
    string Summary,
    string Detail,
    C2paInspectionResult? SourceInspection,
    C2paToolRuntimeStatus? InspectionRuntime,
    C2paExportWriterRuntimeStatus? WriterRuntime)
{
    public bool WillPreserve => Action == C2paExportAction.Preserve;
    public bool WillWrite => Action == C2paExportAction.WriteWithRuntime;
    public bool WillOmit => Action == C2paExportAction.Omit;
    public bool HasSourceCredentials => SourceInspection?.HasCredentials == true;
    public bool RequiresAttention => Action == C2paExportAction.Omit &&
                                     Reason is C2paExportReason.TargetFormatUnsupported
                                         or C2paExportReason.InspectionRuntimeUnavailable
                                         or C2paExportReason.InspectionFailed
                                         or C2paExportReason.WriterNotConfigured
                                         or C2paExportReason.WriterRuntimeUnavailable
                                         or C2paExportReason.WriterNotApproved;

    public static C2paExportHandoff Omitted(
        C2paExportReason reason,
        string summary,
        string detail,
        C2paInspectionResult? sourceInspection = null,
        C2paToolRuntimeStatus? inspectionRuntime = null,
        C2paExportWriterRuntimeStatus? writerRuntime = null)
        => new(
            C2paExportAction.Omit,
            reason,
            summary,
            detail,
            sourceInspection,
            inspectionRuntime,
            writerRuntime);

    public static C2paExportHandoff Preserved(string detail, C2paInspectionResult? sourceInspection = null)
        => new(
            C2paExportAction.Preserve,
            C2paExportReason.PreservedSource,
            "C2PA preserved",
            detail,
            sourceInspection,
            null,
            null);
}

public sealed record C2paExportWriterRuntimeStatus(
    bool Available,
    bool Approved,
    string Source,
    string StatusText)
{
    public static C2paExportWriterRuntimeStatus NotConfigured() => new(
        Available: false,
        Approved: false,
        Source: "Not configured",
        StatusText: "No approved C2PA export writer is configured.");

    public static C2paExportWriterRuntimeStatus ApprovedRuntime(string source, string statusText) => new(
        Available: true,
        Approved: true,
        Source: source,
        StatusText: statusText);
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
