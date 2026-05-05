using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using ImageMagick;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public sealed record EditOperation(
    string Id,
    string Kind,
    DateTimeOffset CreatedUtc,
    bool Enabled,
    IReadOnlyDictionary<string, string> Parameters,
    string? Label = null);

public sealed record VirtualEditCopy(
    string Id,
    string Name,
    DateTimeOffset CreatedUtc,
    IReadOnlyList<EditOperation> Operations);

public sealed record EditStackSnapshot(
    int SchemaVersion,
    string SidecarPath,
    string SourceFileName,
    DateTimeOffset UpdatedUtc,
    IReadOnlyList<EditOperation> Operations,
    IReadOnlyList<VirtualEditCopy> VirtualCopies)
{
    public int EnabledOperationCount => Operations.Count(operation => operation.Enabled);
}

public sealed record EditStackMutationResult(
    bool Success,
    string SidecarPath,
    string Message,
    EditOperation? Operation = null,
    VirtualEditCopy? VirtualCopy = null);

public sealed record EditExportResult(
    bool Success,
    string OutputPath,
    string ProvenanceSidecarPath,
    int AppliedOperationCount,
    string Message);

public sealed class NonDestructiveEditService
{
    public const string MasterCopyId = "master";

    private const int CurrentSchemaVersion = 1;
    private static readonly ILogger Log = Images.Services.Log.Get(nameof(NonDestructiveEditService));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly XNamespace Xmp = "adobe:ns:meta/";
    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace ImagesEdit = "https://github.com/SysAdminDoc/Images/ns/edit/1.0/";

    public EditStackSnapshot LoadSnapshot(string imagePath)
    {
        var normalizedPath = NormalizeImagePath(imagePath, requireExistingImage: false);
        var sidecarPath = SidecarPaths(normalizedPath).First();
        var document = LoadDocumentOrDefault(normalizedPath);
        return ToSnapshot(document, sidecarPath, normalizedPath);
    }

    public bool HasEnabledOperations(string imagePath, string? virtualCopyId = null)
    {
        var snapshot = LoadSnapshot(imagePath);
        return GetOperations(snapshot, virtualCopyId).Any(operation => operation.Enabled);
    }

    public EditStackMutationResult AppendOperation(
        string imagePath,
        string kind,
        IReadOnlyDictionary<string, string>? parameters = null,
        string? label = null,
        string? virtualCopyId = null)
    {
        if (!TryNormalizeKind(kind, out var normalizedKind, out var kindError))
            return new EditStackMutationResult(false, "", kindError);

        try
        {
            var normalizedPath = NormalizeImagePath(imagePath, requireExistingImage: true);
            var operation = new EditOperation(
                Guid.NewGuid().ToString("N"),
                normalizedKind,
                DateTimeOffset.UtcNow,
                Enabled: true,
                NormalizeParameters(parameters),
                string.IsNullOrWhiteSpace(label) ? DefaultOperationLabel(normalizedKind) : label.Trim());

            var document = LoadDocumentOrDefault(normalizedPath);
            if (IsMasterCopy(virtualCopyId))
            {
                document.Operations.Add(ToDocument(operation));
            }
            else if (FindVirtualCopy(document, virtualCopyId!) is { } copy)
            {
                copy.Operations.Add(ToDocument(operation));
            }
            else
            {
                return new EditStackMutationResult(false, PrimarySidecarPath(normalizedPath), "Virtual copy was not found.");
            }

            Persist(normalizedPath, document);
            return new EditStackMutationResult(
                true,
                PrimarySidecarPath(normalizedPath),
                $"Added {operation.Kind} to the non-destructive edit stack.",
                operation);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            Log.LogWarning(ex, "Could not append edit operation for {Path}", imagePath);
            return new EditStackMutationResult(false, "", ex.Message);
        }
    }

    public EditStackMutationResult SetOperationEnabled(
        string imagePath,
        string operationId,
        bool enabled,
        string? virtualCopyId = null)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            return new EditStackMutationResult(false, "", "Select an edit operation first.");

        try
        {
            var normalizedPath = NormalizeImagePath(imagePath, requireExistingImage: true);
            var document = LoadDocumentOrDefault(normalizedPath);
            var operations = IsMasterCopy(virtualCopyId)
                ? document.Operations
                : FindVirtualCopy(document, virtualCopyId!)?.Operations;

            var operation = operations?.FirstOrDefault(item =>
                string.Equals(item.Id, operationId, StringComparison.OrdinalIgnoreCase));
            if (operation is null)
                return new EditStackMutationResult(false, PrimarySidecarPath(normalizedPath), "Edit operation was not found.");

            operation.Enabled = enabled;
            Persist(normalizedPath, document);
            return new EditStackMutationResult(
                true,
                PrimarySidecarPath(normalizedPath),
                enabled ? "Edit operation enabled." : "Edit operation disabled.",
                FromDocument(operation));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            Log.LogWarning(ex, "Could not update edit operation for {Path}", imagePath);
            return new EditStackMutationResult(false, "", ex.Message);
        }
    }

    public EditStackMutationResult CreateVirtualCopy(string imagePath, string? name = null, string? seedCopyId = null)
    {
        try
        {
            var normalizedPath = NormalizeImagePath(imagePath, requireExistingImage: true);
            var document = LoadDocumentOrDefault(normalizedPath);
            var sourceOperations = GetOperationDocuments(document, seedCopyId).Select(CloneOperation).ToList();
            var copyName = string.IsNullOrWhiteSpace(name)
                ? $"Virtual copy {document.VirtualCopies.Count + 1}"
                : name.Trim();
            var copy = new VirtualEditCopyDocument
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = copyName,
                CreatedUtc = DateTimeOffset.UtcNow,
                Operations = sourceOperations
            };

            document.VirtualCopies.Add(copy);
            Persist(normalizedPath, document);
            return new EditStackMutationResult(
                true,
                PrimarySidecarPath(normalizedPath),
                $"Created {copyName} without duplicating pixels.",
                VirtualCopy: FromDocument(copy));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            Log.LogWarning(ex, "Could not create virtual copy for {Path}", imagePath);
            return new EditStackMutationResult(false, "", ex.Message);
        }
    }

    public EditExportResult Export(
        string imagePath,
        string targetPath,
        string? virtualCopyId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedPath = NormalizeImagePath(imagePath, requireExistingImage: true);
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = LoadSnapshot(normalizedPath);
            var operations = GetOperations(snapshot, virtualCopyId)
                .Where(operation => operation.Enabled)
                .ToList();

            cancellationToken.ThrowIfCancellationRequested();
            var outputPath = ImageExportService.Save(normalizedPath, targetPath, operations);

            cancellationToken.ThrowIfCancellationRequested();
            var sidecarPath = WriteExportProvenance(outputPath, CreateProvenance(normalizedPath, snapshot, operations, virtualCopyId));
            return new EditExportResult(
                true,
                outputPath,
                sidecarPath,
                operations.Count,
                operations.Count == 0
                    ? "Saved copy and wrote export provenance."
                    : $"Applied {operations.Count} edit operation{Plural(operations.Count)} and wrote export provenance.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or InvalidOperationException or NotSupportedException or MagickException)
        {
            Log.LogWarning(ex, "Could not export edit stack for {Path}", imagePath);
            return new EditExportResult(false, "", "", 0, ex.Message);
        }
    }

    internal static void ApplyOperations(MagickImage image, IEnumerable<EditOperation> operations)
    {
        foreach (var operation in operations)
        {
            if (!operation.Enabled)
                continue;

            switch (NormalizeKind(operation.Kind))
            {
                case "crop":
                    ApplyCrop(image, operation.Parameters);
                    break;
                case "resize":
                    ApplyResize(image, operation.Parameters);
                    break;
                case "adjust":
                    ImageAdjustmentService.Apply(image, ImageAdjustmentService.FromParameters(operation.Parameters));
                    break;
                case "local-exposure":
                    LocalExposureBrushService.Apply(image, LocalExposureBrushService.FromParameters(operation.Parameters));
                    break;
                case "red-eye":
                    RedEyeCorrectionService.Apply(image, RedEyeCorrectionService.FromParameters(operation.Parameters));
                    break;
                case "rotate":
                    ApplyRotate(image, operation.Parameters);
                    break;
                case "flip-horizontal":
                    image.Flop();
                    break;
                case "flip-vertical":
                    image.Flip();
                    break;
                default:
                    Log.LogDebug("Skipping unsupported non-destructive edit operation {Kind}", operation.Kind);
                    break;
            }
        }
    }

    public static IReadOnlyList<EditOperation> GetOperations(EditStackSnapshot snapshot, string? virtualCopyId = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (IsMasterCopy(virtualCopyId))
            return snapshot.Operations;

        return snapshot.VirtualCopies.FirstOrDefault(copy =>
                string.Equals(copy.Id, virtualCopyId, StringComparison.OrdinalIgnoreCase))
            ?.Operations ?? [];
    }

    public static string FormatOperation(EditOperation operation)
    {
        var label = string.IsNullOrWhiteSpace(operation.Label) ? operation.Kind : operation.Label;
        var state = operation.Enabled ? "enabled" : "disabled";
        var parameters = operation.Parameters.Count == 0
            ? ""
            : " · " + string.Join(", ", operation.Parameters.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(item => $"{item.Key}={item.Value}"));
        return $"{label} ({state}){parameters}";
    }

    private static void ApplyCrop(MagickImage image, IReadOnlyDictionary<string, string> parameters)
    {
        var x = ParseUInt(parameters, "x", 0);
        var y = ParseUInt(parameters, "y", 0);
        var width = ParseUInt(parameters, "width", image.Width);
        var height = ParseUInt(parameters, "height", image.Height);

        if (x >= image.Width || y >= image.Height || width == 0 || height == 0)
            throw new InvalidOperationException("Crop is outside the image bounds.");

        width = Math.Min(width, image.Width - x);
        height = Math.Min(height, image.Height - y);
        image.Crop(new MagickGeometry((int)x, (int)y, width, height));
    }

    private static void ApplyResize(MagickImage image, IReadOnlyDictionary<string, string> parameters)
    {
        var width = ParseUInt(parameters, "width", 0);
        var height = ParseUInt(parameters, "height", 0);
        var maxWidth = ParseUInt(parameters, "maxWidth", 0);
        var maxHeight = ParseUInt(parameters, "maxHeight", 0);
        var filter = ParseResizeFilter(parameters);

        if (width > 0 || height > 0)
        {
            image.Resize(new MagickGeometry(width == 0 ? image.Width : width, height == 0 ? image.Height : height)
            {
                IgnoreAspectRatio = ParseBool(parameters, "ignoreAspectRatio", false)
            }, filter);
            return;
        }

        if (maxWidth > 0 || maxHeight > 0)
        {
            image.Resize(new MagickGeometry(maxWidth == 0 ? image.Width : maxWidth, maxHeight == 0 ? image.Height : maxHeight)
            {
                IgnoreAspectRatio = false
            }, filter);
        }
    }

    private static FilterType ParseResizeFilter(IReadOnlyDictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("filter", out var filter) || string.IsNullOrWhiteSpace(filter))
            return FilterType.Lanczos;

        return filter.Trim().ToLowerInvariant() switch
        {
            "lanczos" or "lanczos3" => FilterType.Lanczos,
            "mitchell" => FilterType.Mitchell,
            "bicubic" or "cubic" => FilterType.Cubic,
            _ => FilterType.Lanczos
        };
    }

    private static void ApplyRotate(MagickImage image, IReadOnlyDictionary<string, string> parameters)
    {
        var degrees = ParseDouble(parameters, "degrees", 0);
        if (Math.Abs(degrees) < double.Epsilon)
            return;
        image.Rotate(degrees);
    }

    private static string WriteExportProvenance(string outputPath, ExportProvenanceDocument provenance)
    {
        var sidecarPath = PrimarySidecarPath(outputPath);
        var document = File.Exists(sidecarPath)
            ? XDocument.Load(sidecarPath, LoadOptions.PreserveWhitespace)
            : CreateEmptySidecar();

        ReplaceJsonElement(document, "ExportProvenance", provenance);
        SaveAtomically(document, sidecarPath);
        return sidecarPath;
    }

    private static ExportProvenanceDocument CreateProvenance(
        string sourcePath,
        EditStackSnapshot snapshot,
        IReadOnlyList<EditOperation> operations,
        string? virtualCopyId)
    {
        var source = new FileInfo(sourcePath);
        var virtualCopy = IsMasterCopy(virtualCopyId)
            ? null
            : snapshot.VirtualCopies.FirstOrDefault(copy => string.Equals(copy.Id, virtualCopyId, StringComparison.OrdinalIgnoreCase));

        return new ExportProvenanceDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            App = "Images",
            AppVersion = AppInfo.Current.DisplayVersion,
            ExportedUtc = DateTimeOffset.UtcNow,
            SourceFileName = source.Name,
            SourceLength = source.Exists ? source.Length : 0,
            SourceLastWriteUtc = source.Exists ? source.LastWriteTimeUtc : null,
            SourceSha256 = source.Exists ? ComputeSha256(source.FullName) : "",
            VirtualCopyId = virtualCopy?.Id ?? MasterCopyId,
            VirtualCopyName = virtualCopy?.Name ?? "Master",
            Operations = operations.Select(ToProvenanceOperation).ToList()
        };
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static EditOperationProvenanceDocument ToProvenanceOperation(EditOperation operation)
        => new()
        {
            Id = operation.Id,
            Kind = operation.Kind,
            Label = operation.Label,
            CreatedUtc = operation.CreatedUtc,
            Parameters = new Dictionary<string, string>(operation.Parameters, StringComparer.OrdinalIgnoreCase)
        };

    private static EditStackDocument LoadDocumentOrDefault(string imagePath)
    {
        foreach (var sidecarPath in SidecarPaths(imagePath))
        {
            if (!File.Exists(sidecarPath))
                continue;

            try
            {
                var xmpDocument = XDocument.Load(sidecarPath, LoadOptions.PreserveWhitespace);
                var stackJson = FindJsonElement(xmpDocument, "EditStack")?.Value;
                if (string.IsNullOrWhiteSpace(stackJson))
                    return CreateEmptyDocument(imagePath);

                var loaded = JsonSerializer.Deserialize<EditStackDocument>(stackJson, JsonOptions);
                if (loaded is null)
                    return CreateEmptyDocument(imagePath);

                NormalizeDocument(loaded, imagePath);
                return loaded;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.Xml.XmlException or JsonException or NotSupportedException)
            {
                Log.LogWarning(ex, "Could not read edit sidecar {Path}", sidecarPath);
                return CreateEmptyDocument(imagePath);
            }
        }

        return CreateEmptyDocument(imagePath);
    }

    private static void Persist(string imagePath, EditStackDocument editDocument)
    {
        NormalizeDocument(editDocument, imagePath);
        editDocument.UpdatedUtc = DateTimeOffset.UtcNow;

        var sidecarPath = PrimarySidecarPath(imagePath);
        var xmpDocument = File.Exists(sidecarPath)
            ? XDocument.Load(sidecarPath, LoadOptions.PreserveWhitespace)
            : CreateEmptySidecar();

        ReplaceJsonElement(xmpDocument, "EditStack", editDocument);
        SaveAtomically(xmpDocument, sidecarPath);
    }

    private static void ReplaceJsonElement<T>(XDocument document, string localName, T value)
    {
        var description = EnsureDescription(document);
        description.Elements()
            .Where(element => element.Name.Namespace == ImagesEdit && element.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))
            .Remove();

        description.Add(new XElement(ImagesEdit + localName, JsonSerializer.Serialize(value, JsonOptions)));
    }

    private static XElement? FindJsonElement(XDocument document, string localName)
        => document.Descendants()
            .FirstOrDefault(element =>
                element.Name.Namespace == ImagesEdit &&
                element.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));

    private static XDocument CreateEmptySidecar()
        => new(
            new XElement(
                Xmp + "xmpmeta",
                new XAttribute(XNamespace.Xmlns + "x", Xmp.NamespaceName),
                new XElement(
                    Rdf + "RDF",
                    new XAttribute(XNamespace.Xmlns + "rdf", Rdf.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "images", ImagesEdit.NamespaceName),
                    new XElement(Rdf + "Description"))));

    private static XElement EnsureDescription(XDocument document)
    {
        if (document.Root is null)
        {
            document.Add(CreateEmptySidecar().Root!);
        }

        var root = document.Root!;
        if (root.GetNamespaceOfPrefix("images") is null)
            root.Add(new XAttribute(XNamespace.Xmlns + "images", ImagesEdit.NamespaceName));

        var rdfRoot = document.Descendants().FirstOrDefault(
            element => element.Name.LocalName.Equals("RDF", StringComparison.OrdinalIgnoreCase));
        if (rdfRoot is null)
        {
            rdfRoot = new XElement(
                Rdf + "RDF",
                new XAttribute(XNamespace.Xmlns + "rdf", Rdf.NamespaceName));
            root.Add(rdfRoot);
        }

        var description = rdfRoot.Elements().FirstOrDefault(
            element => element.Name.LocalName.Equals("Description", StringComparison.OrdinalIgnoreCase));
        if (description is null)
        {
            description = new XElement(Rdf + "Description");
            rdfRoot.Add(description);
        }

        return description;
    }

    private static void SaveAtomically(XDocument document, string sidecarPath)
    {
        var directory = Path.GetDirectoryName(sidecarPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new IOException("Sidecar destination has no directory.");

        Directory.CreateDirectory(directory);
        var tempPath = sidecarPath + ".tmp";
        try
        {
            document.Save(tempPath);
            File.Move(tempPath, sidecarPath, overwrite: true);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private static EditStackSnapshot ToSnapshot(EditStackDocument document, string sidecarPath, string sourcePath)
    {
        NormalizeDocument(document, sourcePath);
        return new EditStackSnapshot(
            document.SchemaVersion,
            sidecarPath,
            document.SourceFileName,
            document.UpdatedUtc,
            document.Operations.Select(FromDocument).ToList(),
            document.VirtualCopies.Select(FromDocument).ToList());
    }

    private static EditStackDocument CreateEmptyDocument(string imagePath)
        => new()
        {
            SchemaVersion = CurrentSchemaVersion,
            SourceFileName = Path.GetFileName(imagePath),
            UpdatedUtc = DateTimeOffset.UtcNow
        };

    private static void NormalizeDocument(EditStackDocument document, string imagePath)
    {
        document.SchemaVersion = document.SchemaVersion <= 0 ? CurrentSchemaVersion : document.SchemaVersion;
        document.SourceFileName = string.IsNullOrWhiteSpace(document.SourceFileName)
            ? Path.GetFileName(imagePath)
            : document.SourceFileName.Trim();
        if (document.UpdatedUtc == default)
            document.UpdatedUtc = DateTimeOffset.UtcNow;

        document.Operations = document.Operations
            .Where(operation => TryNormalizeKind(operation.Kind, out _, out _))
            .Select(NormalizeOperationDocument)
            .ToList();

        document.VirtualCopies = document.VirtualCopies
            .Where(copy => !string.IsNullOrWhiteSpace(copy.Id))
            .Select(copy =>
            {
                copy.Id = copy.Id.Trim();
                copy.Name = string.IsNullOrWhiteSpace(copy.Name) ? "Virtual copy" : copy.Name.Trim();
                if (copy.CreatedUtc == default)
                    copy.CreatedUtc = DateTimeOffset.UtcNow;
                copy.Operations = copy.Operations
                    .Where(operation => TryNormalizeKind(operation.Kind, out _, out _))
                    .Select(NormalizeOperationDocument)
                    .ToList();
                return copy;
            })
            .DistinctBy(copy => copy.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static EditOperationDocument NormalizeOperationDocument(EditOperationDocument operation)
    {
        operation.Id = string.IsNullOrWhiteSpace(operation.Id) ? Guid.NewGuid().ToString("N") : operation.Id.Trim();
        operation.Kind = NormalizeKind(operation.Kind);
        if (operation.CreatedUtc == default)
            operation.CreatedUtc = DateTimeOffset.UtcNow;
        operation.Parameters = operation.Parameters
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(
                item => item.Key.Trim(),
                item => item.Value?.Trim() ?? "",
                StringComparer.OrdinalIgnoreCase);
        operation.Label = string.IsNullOrWhiteSpace(operation.Label) ? DefaultOperationLabel(operation.Kind) : operation.Label.Trim();
        return operation;
    }

    private static IReadOnlyList<EditOperationDocument> GetOperationDocuments(EditStackDocument document, string? copyId)
    {
        if (IsMasterCopy(copyId))
            return document.Operations;
        return FindVirtualCopy(document, copyId!)?.Operations ?? [];
    }

    private static VirtualEditCopyDocument? FindVirtualCopy(EditStackDocument document, string copyId)
        => document.VirtualCopies.FirstOrDefault(copy =>
            string.Equals(copy.Id, copyId, StringComparison.OrdinalIgnoreCase));

    private static EditOperationDocument ToDocument(EditOperation operation)
        => new()
        {
            Id = operation.Id,
            Kind = NormalizeKind(operation.Kind),
            CreatedUtc = operation.CreatedUtc,
            Enabled = operation.Enabled,
            Label = operation.Label,
            Parameters = new Dictionary<string, string>(operation.Parameters, StringComparer.OrdinalIgnoreCase)
        };

    private static EditOperation FromDocument(EditOperationDocument document)
        => new(
            document.Id,
            NormalizeKind(document.Kind),
            document.CreatedUtc,
            document.Enabled,
            new Dictionary<string, string>(document.Parameters, StringComparer.OrdinalIgnoreCase),
            document.Label);

    private static VirtualEditCopy FromDocument(VirtualEditCopyDocument document)
        => new(
            document.Id,
            document.Name,
            document.CreatedUtc,
            document.Operations.Select(FromDocument).ToList());

    private static EditOperationDocument CloneOperation(EditOperationDocument operation)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = operation.Kind,
            CreatedUtc = DateTimeOffset.UtcNow,
            Enabled = operation.Enabled,
            Label = operation.Label,
            Parameters = new Dictionary<string, string>(operation.Parameters, StringComparer.OrdinalIgnoreCase)
        };

    private static IReadOnlyDictionary<string, string> NormalizeParameters(IReadOnlyDictionary<string, string>? parameters)
        => parameters is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : parameters
                .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                .ToDictionary(
                    item => item.Key.Trim(),
                    item => item.Value?.Trim() ?? "",
                    StringComparer.OrdinalIgnoreCase);

    private static bool TryNormalizeKind(string kind, out string normalized, out string error)
    {
        normalized = NormalizeKind(kind);
        error = "";

        if (string.IsNullOrWhiteSpace(normalized))
        {
            error = "Edit operation kind is required.";
            return false;
        }

        if (normalized is "crop" or "resize" or "adjust" or "local-exposure" or "red-eye" or "rotate" or "flip-horizontal" or "flip-vertical")
            return true;

        error = $"Unsupported edit operation: {kind}.";
        return false;
    }

    private static string NormalizeKind(string kind)
        => (kind ?? "").Trim().ToLowerInvariant().Replace("_", "-", StringComparison.OrdinalIgnoreCase);

    private static string DefaultOperationLabel(string kind)
        => kind switch
        {
            "crop" => "Crop",
            "resize" => "Resize",
            "adjust" => "Adjust",
            "local-exposure" => "Local exposure",
            "red-eye" => "Red-eye correction",
            "rotate" => "Rotate",
            "flip-horizontal" => "Flip horizontal",
            "flip-vertical" => "Flip vertical",
            _ => kind
        };

    private static string NormalizeImagePath(string imagePath, bool requireExistingImage)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            throw new ArgumentException("No image is selected.", nameof(imagePath));

        var normalizedPath = Path.GetFullPath(imagePath);
        if (requireExistingImage && !File.Exists(normalizedPath))
            throw new IOException("Selected image does not exist.");

        return normalizedPath;
    }

    private static IEnumerable<string> SidecarPaths(string imagePath)
    {
        yield return PrimarySidecarPath(imagePath);

        var directory = Path.GetDirectoryName(imagePath);
        var basename = Path.GetFileNameWithoutExtension(imagePath);
        if (!string.IsNullOrWhiteSpace(directory) && !string.IsNullOrWhiteSpace(basename))
            yield return Path.Combine(directory, basename + ".xmp");
    }

    private static string PrimarySidecarPath(string imagePath)
        => imagePath + ".xmp";

    private static bool IsMasterCopy(string? copyId)
        => string.IsNullOrWhiteSpace(copyId) || string.Equals(copyId, MasterCopyId, StringComparison.OrdinalIgnoreCase);

    private static uint ParseUInt(IReadOnlyDictionary<string, string> parameters, string key, uint fallback)
        => parameters.TryGetValue(key, out var raw) &&
           uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static double ParseDouble(IReadOnlyDictionary<string, string> parameters, string key, double fallback)
        => parameters.TryGetValue(key, out var raw) &&
           double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static bool ParseBool(IReadOnlyDictionary<string, string> parameters, string key, bool fallback)
        => parameters.TryGetValue(key, out var raw) && bool.TryParse(raw, out var parsed)
            ? parsed
            : fallback;

    private static string Plural(int count) => count == 1 ? "" : "s";

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private sealed class EditStackDocument
    {
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public string SourceFileName { get; set; } = "";
        public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public List<EditOperationDocument> Operations { get; set; } = [];
        public List<VirtualEditCopyDocument> VirtualCopies { get; set; } = [];
    }

    private sealed class EditOperationDocument
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = "";
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public bool Enabled { get; set; } = true;
        public string? Label { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class VirtualEditCopyDocument
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public List<EditOperationDocument> Operations { get; set; } = [];
    }

    private sealed class ExportProvenanceDocument
    {
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public string App { get; set; } = "Images";
        public string AppVersion { get; set; } = "";
        public DateTimeOffset ExportedUtc { get; set; } = DateTimeOffset.UtcNow;
        public string SourceFileName { get; set; } = "";
        public long SourceLength { get; set; }
        public DateTime? SourceLastWriteUtc { get; set; }
        public string SourceSha256 { get; set; } = "";
        public string VirtualCopyId { get; set; } = MasterCopyId;
        public string VirtualCopyName { get; set; } = "Master";
        public List<EditOperationProvenanceDocument> Operations { get; set; } = [];
    }

    private sealed class EditOperationProvenanceDocument
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = "";
        public string? Label { get; set; }
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
