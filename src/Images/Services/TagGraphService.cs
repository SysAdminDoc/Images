using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public sealed record TagNamespaceRule(string Prefix, string Label);
public sealed record TagAliasRule(string Alias, string Target);
public sealed record TagParentRule(string Tag, string Parent);

public sealed record TagGraphSnapshot(
    IReadOnlyList<TagNamespaceRule> Namespaces,
    IReadOnlyList<TagAliasRule> Aliases,
    IReadOnlyList<TagParentRule> Parents);

public sealed record TagGraphMutationResult(bool Success, string Message);

public sealed record TagExpansion(
    string Original,
    string Canonical,
    IReadOnlyList<string> Parents,
    IReadOnlyList<string> AllTags);

public sealed record TagSidecarImportResult(
    bool Success,
    string SidecarPath,
    IReadOnlyList<string> Tags,
    string Message);

public sealed record TagSidecarExportResult(
    bool Success,
    string SidecarPath,
    int TagCount,
    string Message);

public sealed class TagGraphService
{
    private const int MaxGraphDepth = 64;
    private static readonly ILogger Log = Images.Services.Log.Get(nameof(TagGraphService));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly object _gate = new();
    private readonly string? _storePath;
    private TagGraphDocument _document;

    public TagGraphService()
        : this(CreateDefaultStorePath())
    {
    }

    public TagGraphService(string? storePath)
    {
        _storePath = string.IsNullOrWhiteSpace(storePath) ? null : storePath;
        _document = LoadOrCreate(_storePath);
    }

    public bool IsPersistent => _storePath is not null;

    public TagGraphSnapshot Snapshot
    {
        get
        {
            lock (_gate)
                return CreateSnapshot(_document);
        }
    }

    public TagGraphMutationResult AddNamespace(string prefix, string? label)
    {
        if (!TryNormalizeNamespace(prefix, out var normalizedPrefix, out var error))
            return new TagGraphMutationResult(false, error);

        var normalizedLabel = string.IsNullOrWhiteSpace(label)
            ? normalizedPrefix
            : label.Trim();

        lock (_gate)
        {
            var existing = _document.Namespaces.FirstOrDefault(
                item => string.Equals(item.Prefix, normalizedPrefix, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                existing.Label = normalizedLabel;
                return PersistMutation($"Updated namespace {normalizedPrefix}:.");
            }

            _document.Namespaces.Add(new TagNamespaceDocument
            {
                Prefix = normalizedPrefix,
                Label = normalizedLabel
            });
            SortDocument(_document);
            return PersistMutation($"Added namespace {normalizedPrefix}:.");
        }
    }

    public TagGraphMutationResult AddAlias(string alias, string target)
    {
        if (!TryNormalizeTag(alias, out var normalizedAlias, out var aliasError))
            return new TagGraphMutationResult(false, aliasError);
        if (!TryNormalizeTag(target, out var normalizedTarget, out var targetError))
            return new TagGraphMutationResult(false, targetError);

        lock (_gate)
        {
            normalizedTarget = ResolveCanonicalCore(normalizedTarget, _document).Canonical;
            if (string.Equals(normalizedAlias, normalizedTarget, StringComparison.OrdinalIgnoreCase))
                return new TagGraphMutationResult(false, "Alias and target already resolve to the same tag.");

            if (WouldCreateAliasCycle(normalizedAlias, normalizedTarget, _document))
                return new TagGraphMutationResult(false, "Alias would create a cycle.");

            _document.Aliases.RemoveAll(
                item => string.Equals(item.Alias, normalizedAlias, StringComparison.OrdinalIgnoreCase));
            _document.Aliases.Add(new TagAliasDocument
            {
                Alias = normalizedAlias,
                Target = normalizedTarget
            });
            SortDocument(_document);
            return PersistMutation($"Added alias {normalizedAlias} -> {normalizedTarget}.");
        }
    }

    public TagGraphMutationResult RemoveAlias(string alias)
    {
        if (!TryNormalizeTag(alias, out var normalizedAlias, out var error))
            return new TagGraphMutationResult(false, error);

        lock (_gate)
        {
            var removed = _document.Aliases.RemoveAll(
                item => string.Equals(item.Alias, normalizedAlias, StringComparison.OrdinalIgnoreCase));
            return removed == 0
                ? new TagGraphMutationResult(false, "Alias was not found.")
                : PersistMutation($"Removed alias {normalizedAlias}.");
        }
    }

    public TagGraphMutationResult AddParent(string tag, string parent)
    {
        if (!TryNormalizeTag(tag, out var normalizedTag, out var tagError))
            return new TagGraphMutationResult(false, tagError);
        if (!TryNormalizeTag(parent, out var normalizedParent, out var parentError))
            return new TagGraphMutationResult(false, parentError);

        lock (_gate)
        {
            normalizedTag = ResolveCanonicalCore(normalizedTag, _document).Canonical;
            normalizedParent = ResolveCanonicalCore(normalizedParent, _document).Canonical;

            if (string.Equals(normalizedTag, normalizedParent, StringComparison.OrdinalIgnoreCase))
                return new TagGraphMutationResult(false, "A tag cannot be its own parent.");
            if (WouldCreateParentCycle(normalizedTag, normalizedParent, _document))
                return new TagGraphMutationResult(false, "Parent relationship would create a cycle.");

            var exists = _document.Parents.Any(item =>
                string.Equals(item.Tag, normalizedTag, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Parent, normalizedParent, StringComparison.OrdinalIgnoreCase));
            if (exists)
                return new TagGraphMutationResult(true, "Parent relationship already exists.");

            _document.Parents.Add(new TagParentDocument
            {
                Tag = normalizedTag,
                Parent = normalizedParent
            });
            SortDocument(_document);
            return PersistMutation($"Added parent {normalizedTag} -> {normalizedParent}.");
        }
    }

    public TagGraphMutationResult RemoveParent(string tag, string parent)
    {
        if (!TryNormalizeTag(tag, out var normalizedTag, out var tagError))
            return new TagGraphMutationResult(false, tagError);
        if (!TryNormalizeTag(parent, out var normalizedParent, out var parentError))
            return new TagGraphMutationResult(false, parentError);

        lock (_gate)
        {
            var removed = _document.Parents.RemoveAll(item =>
                string.Equals(item.Tag, normalizedTag, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Parent, normalizedParent, StringComparison.OrdinalIgnoreCase));
            return removed == 0
                ? new TagGraphMutationResult(false, "Parent relationship was not found.")
                : PersistMutation($"Removed parent {normalizedTag} -> {normalizedParent}.");
        }
    }

    public TagExpansion Expand(string tag)
    {
        if (!TryNormalizeTag(tag, out var normalized, out _))
            return new TagExpansion(tag, "", [], []);

        lock (_gate)
        {
            var canonical = ResolveCanonicalCore(normalized, _document).Canonical;
            var parents = ExpandParentsCore(canonical, _document);
            var allTags = new List<string> { canonical };
            allTags.AddRange(parents);
            return new TagExpansion(tag, canonical, parents, allTags);
        }
    }

    public IReadOnlyList<TagExpansion> ExpandMany(IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        return tags
            .Select(Expand)
            .Where(expansion => !string.IsNullOrWhiteSpace(expansion.Canonical))
            .DistinctBy(expansion => expansion.Canonical, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public TagSidecarImportResult ImportSidecarTags(string imagePath)
    {
        if (!TryNormalizeImagePath(imagePath, requireExistingImage: false, out var normalizedPath, out var pathError))
        {
            return new TagSidecarImportResult(false, "", [], pathError);
        }

        foreach (var sidecarPath in SidecarPaths(normalizedPath))
        {
            if (!File.Exists(sidecarPath))
                continue;

            try
            {
                var document = XDocument.Load(sidecarPath, LoadOptions.None);
                var tags = ExtractTagElements(document)
                    .SelectMany(SplitTagValues)
                    .Select(NormalizeTagOrNull)
                    .Where(tag => tag is not null)
                    .Select(tag => tag!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new TagSidecarImportResult(
                    true,
                    sidecarPath,
                    tags,
                    tags.Count == 0
                        ? "Sidecar has no keyword tags."
                        : $"Imported {tags.Count} tag{Plural(tags.Count)} from sidecar.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.Xml.XmlException)
            {
                Log.LogWarning(ex, "Could not import tag sidecar {Path}", sidecarPath);
                return new TagSidecarImportResult(false, sidecarPath, [], "Sidecar could not be read.");
            }
        }

        return new TagSidecarImportResult(
            false,
            SidecarPaths(normalizedPath).First(),
            [],
            "No XMP sidecar was found for this image.");
    }

    public TagSidecarExportResult ExportSidecarTags(
        string imagePath,
        IEnumerable<string> tags,
        bool includeParents)
    {
        ArgumentNullException.ThrowIfNull(tags);

        if (!TryNormalizeImagePath(imagePath, requireExistingImage: true, out var normalizedPath, out var pathError))
        {
            return new TagSidecarExportResult(false, "", 0, pathError);
        }

        var expandedTags = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            if (!TryNormalizeTag(tag, out var normalizedTag, out _))
                continue;

            var expansion = Expand(normalizedTag);
            if (string.IsNullOrWhiteSpace(expansion.Canonical))
                continue;

            expandedTags.Add(expansion.Canonical);
            if (includeParents)
            {
                foreach (var parent in expansion.Parents)
                    expandedTags.Add(parent);
            }
        }

        if (expandedTags.Count == 0)
        {
            return new TagSidecarExportResult(
                false,
                SidecarPaths(normalizedPath).First(),
                0,
                "Add at least one valid tag before exporting.");
        }

        var sidecarPath = SidecarPaths(normalizedPath).First();
        try
        {
            var document = File.Exists(sidecarPath)
                ? XDocument.Load(sidecarPath, LoadOptions.None)
                : CreateEmptySidecar();

            ReplaceSubjectTags(document, expandedTags);
            Directory.CreateDirectory(Path.GetDirectoryName(sidecarPath)!);
            document.Save(sidecarPath);

            return new TagSidecarExportResult(
                true,
                sidecarPath,
                expandedTags.Count,
                $"Exported {expandedTags.Count} tag{Plural(expandedTags.Count)} to sidecar.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.Xml.XmlException or InvalidOperationException)
        {
            Log.LogWarning(ex, "Could not export tag sidecar {Path}", sidecarPath);
            return new TagSidecarExportResult(false, sidecarPath, 0, "Sidecar could not be written.");
        }
    }

    public static IReadOnlyList<string> ParseTagInput(string input)
        => SplitTagValues(input ?? "")
            .Select(NormalizeTagOrNull)
            .Where(tag => tag is not null)
            .Select(tag => tag!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static bool TryNormalizeTag(string input, out string normalized, out string error)
    {
        normalized = "";
        error = "";

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Tag cannot be empty.";
            return false;
        }

        var trimmed = input.Trim().TrimStart('#').Trim();
        var separator = trimmed.IndexOf(':', StringComparison.Ordinal);
        if (separator >= 0)
        {
            if (separator == 0 || separator == trimmed.Length - 1)
            {
                error = "Namespaced tags must look like person:name.";
                return false;
            }

            if (!TryNormalizeNamespace(trimmed[..separator], out var prefix, out error))
                return false;

            var body = NormalizeTagSegment(trimmed[(separator + 1)..], allowDot: true);
            if (string.IsNullOrWhiteSpace(body))
            {
                error = "Namespaced tags need a value after the colon.";
                return false;
            }

            normalized = $"{prefix}:{body}";
            return true;
        }

        normalized = NormalizeTagSegment(trimmed, allowDot: true);
        if (!string.IsNullOrWhiteSpace(normalized))
            return true;

        error = "Tag contains no searchable text.";
        return false;
    }

    public static bool TryNormalizeNamespace(string input, out string normalized, out string error)
    {
        normalized = "";
        error = "";

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Namespace prefix cannot be empty.";
            return false;
        }

        normalized = NormalizeTagSegment(input.Trim().TrimEnd(':'), allowDot: false);
        if (!string.IsNullOrWhiteSpace(normalized))
            return true;

        error = "Namespace prefix contains no searchable text.";
        return false;
    }

    private TagGraphMutationResult PersistMutation(string successMessage)
        => TrySave()
            ? new TagGraphMutationResult(true, successMessage)
            : new TagGraphMutationResult(true, successMessage + " Persistent storage is unavailable.");

    private bool TrySave()
    {
        if (_storePath is null)
            return false;

        try
        {
            var directory = Path.GetDirectoryName(_storePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var tempPath = _storePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(_document, JsonOptions), Encoding.UTF8);
            File.Move(tempPath, _storePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or NotSupportedException)
        {
            Log.LogWarning(ex, "Could not persist local tag graph {Path}", _storePath);
            return false;
        }
    }

    private static string? CreateDefaultStorePath()
    {
        try
        {
            var directory = AppStorage.TryGetAppDirectory();
            return directory is null ? null : Path.Combine(directory, "tag-graph.json");
        }
        catch
        {
            return null;
        }
    }

    private static TagGraphDocument LoadOrCreate(string? storePath)
    {
        var document = CreateDefaultDocument();
        if (string.IsNullOrWhiteSpace(storePath) || !File.Exists(storePath))
            return document;

        try
        {
            var loaded = JsonSerializer.Deserialize<TagGraphDocument>(
                File.ReadAllText(storePath, Encoding.UTF8),
                JsonOptions);
            if (loaded is null)
                return document;

            MergeDefaults(loaded);
            NormalizeDocument(loaded);
            return loaded;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            Log.LogWarning(ex, "Could not load local tag graph {Path}", storePath);
            TryQuarantineCorruptStore(storePath);
            return document;
        }
    }

    private static void TryQuarantineCorruptStore(string storePath)
    {
        try
        {
            if (!File.Exists(storePath))
                return;

            var quarantinePath = storePath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
            File.Move(storePath, quarantinePath);
        }
        catch
        {
            // Best effort only. A fresh in-memory graph is still usable for this session.
        }
    }

    private static TagGraphSnapshot CreateSnapshot(TagGraphDocument document)
        => new(
            document.Namespaces
                .Select(item => new TagNamespaceRule(item.Prefix, item.Label))
                .OrderBy(item => item.Prefix, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            document.Aliases
                .Select(item => new TagAliasRule(item.Alias, item.Target))
                .OrderBy(item => item.Alias, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            document.Parents
                .Select(item => new TagParentRule(item.Tag, item.Parent))
                .OrderBy(item => item.Tag, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Parent, StringComparer.OrdinalIgnoreCase)
                .ToList());

    private static TagGraphDocument CreateDefaultDocument()
    {
        var document = new TagGraphDocument();
        document.Namespaces.Add(new TagNamespaceDocument { Prefix = "person", Label = "People" });
        document.Namespaces.Add(new TagNamespaceDocument { Prefix = "place", Label = "Places" });
        document.Namespaces.Add(new TagNamespaceDocument { Prefix = "project", Label = "Projects" });
        return document;
    }

    private static void MergeDefaults(TagGraphDocument document)
    {
        foreach (var item in CreateDefaultDocument().Namespaces)
        {
            if (!document.Namespaces.Any(existing => string.Equals(existing.Prefix, item.Prefix, StringComparison.OrdinalIgnoreCase)))
                document.Namespaces.Add(item);
        }
    }

    private static void NormalizeDocument(TagGraphDocument document)
    {
        document.Namespaces = document.Namespaces
            .Where(item => TryNormalizeNamespace(item.Prefix, out _, out _))
            .Select(item =>
            {
                TryNormalizeNamespace(item.Prefix, out var prefix, out _);
                return new TagNamespaceDocument
                {
                    Prefix = prefix,
                    Label = string.IsNullOrWhiteSpace(item.Label) ? prefix : item.Label.Trim()
                };
            })
            .DistinctBy(item => item.Prefix, StringComparer.OrdinalIgnoreCase)
            .ToList();

        document.Aliases = document.Aliases
            .Where(item =>
                TryNormalizeTag(item.Alias, out _, out _) &&
                TryNormalizeTag(item.Target, out _, out _))
            .Select(item =>
            {
                TryNormalizeTag(item.Alias, out var alias, out _);
                TryNormalizeTag(item.Target, out var target, out _);
                return new TagAliasDocument { Alias = alias, Target = target };
            })
            .Where(item => !string.Equals(item.Alias, item.Target, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(item => item.Alias, StringComparer.OrdinalIgnoreCase)
            .ToList();

        document.Parents = document.Parents
            .Where(item =>
                TryNormalizeTag(item.Tag, out _, out _) &&
                TryNormalizeTag(item.Parent, out _, out _))
            .Select(item =>
            {
                TryNormalizeTag(item.Tag, out var tag, out _);
                TryNormalizeTag(item.Parent, out var parent, out _);
                return new TagParentDocument { Tag = tag, Parent = parent };
            })
            .Where(item => !string.Equals(item.Tag, item.Parent, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(item => item.Tag + "\u001f" + item.Parent, StringComparer.OrdinalIgnoreCase)
            .ToList();

        SortDocument(document);
    }

    private static void SortDocument(TagGraphDocument document)
    {
        document.Namespaces = document.Namespaces
            .OrderBy(item => item.Prefix, StringComparer.OrdinalIgnoreCase)
            .ToList();
        document.Aliases = document.Aliases
            .OrderBy(item => item.Alias, StringComparer.OrdinalIgnoreCase)
            .ToList();
        document.Parents = document.Parents
            .OrderBy(item => item.Tag, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Parent, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool WouldCreateAliasCycle(string alias, string target, TagGraphDocument document)
    {
        var map = document.Aliases.ToDictionary(
            item => item.Alias,
            item => item.Target,
            StringComparer.OrdinalIgnoreCase);

        var current = target;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var depth = 0; depth < MaxGraphDepth; depth++)
        {
            if (string.Equals(current, alias, StringComparison.OrdinalIgnoreCase))
                return true;
            if (!visited.Add(current) || !map.TryGetValue(current, out current!))
                return false;
        }

        return true;
    }

    private static bool WouldCreateParentCycle(string tag, string parent, TagGraphDocument document)
    {
        var parents = document.Parents
            .GroupBy(item => item.Tag, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Parent).ToList(),
                StringComparer.OrdinalIgnoreCase);

        return Reaches(parent, tag, parents);
    }

    private static bool Reaches(
        string start,
        string target,
        IReadOnlyDictionary<string, List<string>> parents)
    {
        var stack = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        stack.Push(start);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
                continue;
            if (string.Equals(current, target, StringComparison.OrdinalIgnoreCase))
                return true;
            if (!parents.TryGetValue(current, out var next))
                continue;

            foreach (var parent in next)
                stack.Push(parent);
        }

        return false;
    }

    private static (string Canonical, IReadOnlyList<string> Chain) ResolveCanonicalCore(
        string normalizedTag,
        TagGraphDocument document)
    {
        var map = document.Aliases.ToDictionary(
            item => item.Alias,
            item => item.Target,
            StringComparer.OrdinalIgnoreCase);

        var current = normalizedTag;
        var chain = new List<string> { current };
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { current };
        for (var depth = 0; depth < MaxGraphDepth; depth++)
        {
            if (!map.TryGetValue(current, out var target))
                break;
            if (!visited.Add(target))
                break;

            current = target;
            chain.Add(current);
        }

        return (current, chain);
    }

    private static IReadOnlyList<string> ExpandParentsCore(string canonicalTag, TagGraphDocument document)
    {
        var byTag = document.Parents
            .GroupBy(item => item.Tag, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Parent).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var result = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        if (byTag.TryGetValue(canonicalTag, out var directParents))
        {
            foreach (var parent in directParents)
                stack.Push(parent);
        }

        while (stack.Count > 0)
        {
            var parent = ResolveCanonicalCore(stack.Pop(), document).Canonical;
            if (!result.Add(parent))
                continue;
            if (!byTag.TryGetValue(parent, out var nextParents))
                continue;

            foreach (var nextParent in nextParents)
                stack.Push(nextParent);
        }

        return result.ToList();
    }

    private static string? NormalizeTagOrNull(string value)
        => TryNormalizeTag(value, out var normalized, out _) ? normalized : null;

    private static string NormalizeTagSegment(string value, bool allowDot)
    {
        var builder = new StringBuilder(value.Length);
        var previousSeparator = false;

        foreach (var raw in value.Trim().ToLowerInvariant())
        {
            var ch = raw;
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                previousSeparator = false;
                continue;
            }

            if (ch == '_' || (allowDot && ch == '.'))
            {
                builder.Append(ch);
                previousSeparator = false;
                continue;
            }

            if (ch is '-' or ' ' or '\t' or '\r' or '\n' or '/' or '\\' or ',' or ';' or '|' or ':')
            {
                if (!previousSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                    previousSeparator = true;
                }
            }
        }

        return builder.ToString().Trim('-', '.', '_');
    }

    private static bool TryNormalizeImagePath(
        string imagePath,
        bool requireExistingImage,
        out string normalizedPath,
        out string error)
    {
        normalizedPath = "";
        error = "";

        if (string.IsNullOrWhiteSpace(imagePath))
        {
            error = "No image is selected.";
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(imagePath);
            if (requireExistingImage && !File.Exists(normalizedPath))
            {
                error = "Selected image does not exist.";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            error = "Selected image path is not valid.";
            return false;
        }
    }

    private static IEnumerable<string> SidecarPaths(string imagePath)
    {
        yield return imagePath + ".xmp";

        var directory = Path.GetDirectoryName(imagePath);
        var basename = Path.GetFileNameWithoutExtension(imagePath);
        if (!string.IsNullOrWhiteSpace(directory) && !string.IsNullOrWhiteSpace(basename))
            yield return Path.Combine(directory, basename + ".xmp");
    }

    private static IEnumerable<string> SplitTagValues(string value)
        => (value ?? "")
            .Split(['\r', '\n', ',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item));

    private static IEnumerable<string> ExtractTagElements(XDocument document)
    {
        foreach (var element in document.Descendants())
        {
            if (!IsKeywordElement(element))
                continue;
            if (element.Elements().Any(child => child.Name.LocalName.Equals("Bag", StringComparison.OrdinalIgnoreCase)) ||
                element.Descendants().Any(child => child.Name.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var value = (element.Value ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(value))
                yield return value;
        }
    }

    private static bool IsKeywordElement(XElement element)
    {
        var localName = element.Name.LocalName;
        if (localName.Contains("keyword", StringComparison.OrdinalIgnoreCase) ||
            localName.Contains("subject", StringComparison.OrdinalIgnoreCase) ||
            localName.Contains("tag", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!localName.Equals("li", StringComparison.OrdinalIgnoreCase))
            return false;

        return element.Ancestors().Any(ancestor =>
        {
            var name = ancestor.Name.LocalName;
            return name.Contains("keyword", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("subject", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("tag", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static XDocument CreateEmptySidecar()
    {
        XNamespace x = "adobe:ns:meta/";
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace dc = "http://purl.org/dc/elements/1.1/";

        return new XDocument(
            new XElement(
                x + "xmpmeta",
                new XAttribute(XNamespace.Xmlns + "x", x.NamespaceName),
                new XElement(
                    rdf + "RDF",
                    new XAttribute(XNamespace.Xmlns + "rdf", rdf.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "dc", dc.NamespaceName),
                    new XElement(rdf + "Description"))));
    }

    private static void ReplaceSubjectTags(XDocument document, IEnumerable<string> tags)
    {
        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace dc = "http://purl.org/dc/elements/1.1/";

        var rdfRoot = document.Descendants().FirstOrDefault(
            element => element.Name.LocalName.Equals("RDF", StringComparison.OrdinalIgnoreCase));
        if (rdfRoot is null)
        {
            var root = document.Root ?? CreateEmptySidecar().Root!;
            if (document.Root is null)
                document.Add(root);

            rdfRoot = new XElement(
                rdf + "RDF",
                new XAttribute(XNamespace.Xmlns + "rdf", rdf.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "dc", dc.NamespaceName));
            root.Add(rdfRoot);
        }

        var description = rdfRoot.Elements().FirstOrDefault(
            element => element.Name.LocalName.Equals("Description", StringComparison.OrdinalIgnoreCase));
        if (description is null)
        {
            description = new XElement(rdf + "Description");
            rdfRoot.Add(description);
        }

        description.Elements()
            .Where(element => element.Name.LocalName.Equals("subject", StringComparison.OrdinalIgnoreCase))
            .Remove();

        description.Add(
            new XElement(
                dc + "subject",
                new XElement(
                    rdf + "Bag",
                    tags.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                        .Select(tag => new XElement(rdf + "li", tag)))));
    }

    private static string Plural(int count) => count == 1 ? "" : "s";

    private sealed class TagGraphDocument
    {
        public List<TagNamespaceDocument> Namespaces { get; set; } = [];
        public List<TagAliasDocument> Aliases { get; set; } = [];
        public List<TagParentDocument> Parents { get; set; } = [];
    }

    private sealed class TagNamespaceDocument
    {
        public string Prefix { get; set; } = "";
        public string Label { get; set; } = "";
    }

    private sealed class TagAliasDocument
    {
        public string Alias { get; set; } = "";
        public string Target { get; set; } = "";
    }

    private sealed class TagParentDocument
    {
        public string Tag { get; set; } = "";
        public string Parent { get; set; } = "";
    }
}
