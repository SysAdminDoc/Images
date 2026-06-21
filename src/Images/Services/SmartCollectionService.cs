using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public sealed record SmartCollectionDefinition(
    string Name,
    SmartCollectionCriteria Criteria)
{
    public string SummaryText => Criteria.ToSummary();
}

public sealed record SmartCollectionCriteria(
    IReadOnlyList<string>? FolderRoots = null,
    int? MinRating = null,
    int? MaxRating = null,
    IReadOnlyList<string>? RequiredTags = null,
    IReadOnlyList<string>? RequiredFormats = null,
    string? Orientation = null,
    string? DimensionBucket = null,
    string? DateBucket = null,
    bool? DuplicatesOnly = null,
    string? SemanticText = null)
{
    public bool IsEmpty =>
        (FolderRoots is null || FolderRoots.Count == 0) &&
        MinRating is null && MaxRating is null &&
        (RequiredTags is null || RequiredTags.Count == 0) &&
        (RequiredFormats is null || RequiredFormats.Count == 0) &&
        Orientation is null && DimensionBucket is null && DateBucket is null &&
        DuplicatesOnly is null && SemanticText is null;

    public string ToSummary()
    {
        var parts = new List<string>();
        if (FolderRoots is { Count: > 0 }) parts.Add($"{FolderRoots.Count} folder(s)");
        if (MinRating is not null || MaxRating is not null)
            parts.Add($"rating {MinRating ?? 0}-{MaxRating ?? 5}");
        if (RequiredTags is { Count: > 0 }) parts.Add($"{RequiredTags.Count} tag(s)");
        if (RequiredFormats is { Count: > 0 }) parts.Add(string.Join("/", RequiredFormats));
        if (Orientation is not null) parts.Add(Orientation);
        if (DimensionBucket is not null) parts.Add(DimensionBucket);
        if (DateBucket is not null) parts.Add(DateBucket);
        if (DuplicatesOnly == true) parts.Add("duplicates");
        if (!string.IsNullOrWhiteSpace(SemanticText)) parts.Add($"\"{SemanticText}\"");
        return parts.Count == 0 ? "All images" : string.Join(", ", parts);
    }

    public bool Matches(AssetSmartFilterItem item)
    {
        if (FolderRoots is { Count: > 0 } &&
            !FolderRoots.Any(root =>
                string.Equals(item.Folder, root, StringComparison.OrdinalIgnoreCase) ||
                item.Folder.StartsWith(
                    root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)))
            return false;

        if (MinRating is not null && (item.Rating ?? 0) < MinRating.Value) return false;
        if (MaxRating is not null && (item.Rating ?? 0) > MaxRating.Value) return false;

        if (RequiredTags is { Count: > 0 } &&
            !RequiredTags.All(tag => item.Tags.Any(t => t.Contains(tag, StringComparison.OrdinalIgnoreCase))))
            return false;

        if (RequiredFormats is { Count: > 0 } &&
            !RequiredFormats.Any(f => f.Equals(item.Format, StringComparison.OrdinalIgnoreCase) ||
                                     f.Equals(item.Extension, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (Orientation is not null &&
            !Orientation.Equals(item.Orientation, StringComparison.OrdinalIgnoreCase))
            return false;

        if (DimensionBucket is not null &&
            !DimensionBucket.Equals(item.DimensionBucket, StringComparison.OrdinalIgnoreCase))
            return false;

        if (DateBucket is not null &&
            !DateBucket.Equals(item.DateBucket, StringComparison.OrdinalIgnoreCase))
            return false;

        if (DuplicatesOnly == true && !item.IsDuplicate) return false;

        return true;
    }
}

public sealed record SmartCollectionStore(
    IReadOnlyList<SmartCollectionDefinition> Collections);

public sealed class SmartCollectionService
{
    private static readonly ILogger _log = Log.Get(nameof(SmartCollectionService));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string? _storagePath;
    private List<SmartCollectionDefinition> _collections = [];

    public SmartCollectionService(string? storagePath = null)
    {
        _storagePath = storagePath ?? DefaultPath();
        Load();
    }

    public IReadOnlyList<SmartCollectionDefinition> Collections => _collections;

    public bool Add(string name, SmartCollectionCriteria criteria)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (_collections.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return false;

        _collections.Add(new SmartCollectionDefinition(name.Trim(), criteria));
        Save();
        return true;
    }

    public bool Remove(string name)
    {
        var index = _collections.FindIndex(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;
        _collections.RemoveAt(index);
        Save();
        return true;
    }

    public bool Rename(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return false;
        var index = _collections.FindIndex(c => c.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;
        if (_collections.Any(c => c.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
            return false;

        _collections[index] = _collections[index] with { Name = newName.Trim() };
        Save();
        return true;
    }

    public void MoveUp(int index)
    {
        if (index <= 0 || index >= _collections.Count) return;
        (_collections[index - 1], _collections[index]) = (_collections[index], _collections[index - 1]);
        Save();
    }

    public void MoveDown(int index)
    {
        if (index < 0 || index >= _collections.Count - 1) return;
        (_collections[index], _collections[index + 1]) = (_collections[index + 1], _collections[index]);
        Save();
    }

    public IReadOnlyList<AssetSmartFilterItem> Apply(
        string name,
        IReadOnlyList<AssetSmartFilterItem> items)
    {
        var definition = _collections.FirstOrDefault(
            c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (definition is null) return items;

        return items.Where(item => definition.Criteria.Matches(item)).ToList();
    }

    private void Load()
    {
        if (_storagePath is null || !File.Exists(_storagePath))
        {
            _collections = [];
            return;
        }

        try
        {
            var json = File.ReadAllText(_storagePath);
            var store = JsonSerializer.Deserialize<SmartCollectionStore>(json, JsonOptions);
            _collections = store?.Collections is not null
                ? new List<SmartCollectionDefinition>(store.Collections)
                : [];
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not load smart collections from {Path}", _storagePath);
            _collections = [];
        }
    }

    private void Save()
    {
        if (_storagePath is null) return;

        try
        {
            var dir = Path.GetDirectoryName(_storagePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var store = new SmartCollectionStore(_collections);
            var json = JsonSerializer.Serialize(store, JsonOptions);
            var tempPath = _storagePath + ".tmp";
            File.WriteAllText(tempPath, json, System.Text.Encoding.UTF8);
            File.Move(tempPath, _storagePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not save smart collections to {Path}", _storagePath);
        }
    }

    private static string? DefaultPath()
    {
        var dir = AppStorage.TryGetAppDirectory();
        return dir is null ? null : Path.Combine(dir, "smart-collections.json");
    }
}
