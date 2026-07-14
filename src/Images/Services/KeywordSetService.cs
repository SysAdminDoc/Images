using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public sealed record KeywordSetDefinition(
    string Name,
    IReadOnlyList<string> Keywords)
{
    public string KeywordsText => string.Join(", ", Keywords);
}

public sealed record KeywordSetCollection(
    IReadOnlyList<KeywordSetDefinition> Sets);

public sealed class KeywordSetService
{
    private static readonly ILogger _log = Log.Get(nameof(KeywordSetService));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string? _storagePath;
    private List<KeywordSetDefinition> _sets = [];

    public KeywordSetService(string? storagePath = null)
    {
        _storagePath = storagePath ?? DefaultPath();
        Load();
    }

    public IReadOnlyList<KeywordSetDefinition> Sets => _sets;

    public bool Add(string name, IEnumerable<string> keywords)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;

        var normalized = keywords
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0) return false;
        if (_sets.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return false;

        _sets.Add(new KeywordSetDefinition(name.Trim(), normalized));
        Save();
        return true;
    }

    public bool Remove(string name)
    {
        var index = _sets.FindIndex(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;
        _sets.RemoveAt(index);
        Save();
        return true;
    }

    public bool Rename(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return false;
        var index = _sets.FindIndex(s => s.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;
        if (_sets.Any(s => s.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
            return false;

        _sets[index] = _sets[index] with { Name = newName.Trim() };
        Save();
        return true;
    }

    public KeywordSetApplyResult Apply(string setName, string imagePath, TagGraphService tagService)
    {
        var definition = _sets.FirstOrDefault(s => s.Name.Equals(setName, StringComparison.OrdinalIgnoreCase));
        if (definition is null)
            return new KeywordSetApplyResult(false, 0, "Keyword set not found.");

        try
        {
            var result = tagService.ExportSidecarTags(imagePath, definition.Keywords, includeParents: true);
            return new KeywordSetApplyResult(true, definition.Keywords.Count,
                $"Applied {definition.Keywords.Count} keywords from \"{definition.Name}\".");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to apply keyword set {Name} to {Path}", setName, imagePath);
            return new KeywordSetApplyResult(false, 0, $"Failed: {ex.Message}");
        }
    }

    public string ExportJson()
    {
        var collection = new KeywordSetCollection(_sets);
        return JsonSerializer.Serialize(collection, JsonOptions);
    }

    public int ImportJson(string json)
    {
        try
        {
            var collection = JsonSerializer.Deserialize<KeywordSetCollection>(json, JsonOptions);
            if (collection?.Sets is null) return 0;

            var imported = 0;
            foreach (var set in collection.Sets)
            {
                if (Add(set.Name, set.Keywords))
                    imported++;
            }
            return imported;
        }
        catch (JsonException ex)
        {
            _log.LogWarning(ex, "Failed to import keyword sets from JSON");
            return 0;
        }
    }

    private void Load()
    {
        if (_storagePath is null || !File.Exists(_storagePath))
        {
            _sets = [];
            return;
        }

        try
        {
            var json = BoundedTextFileReader.ReadUtf8(
                _storagePath,
                BoundedTextFileReader.MaxServiceStateBytes,
                "Keyword-set store");
            var collection = JsonSerializer.Deserialize<KeywordSetCollection>(json, JsonOptions);
            _sets = collection?.Sets is not null
                ? new List<KeywordSetDefinition>(collection.Sets)
                : [];
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to load keyword sets from {Path}", _storagePath);
            _sets = [];
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

            var json = ExportJson();
            var tempPath = _storagePath + ".tmp";
            File.WriteAllText(tempPath, json, System.Text.Encoding.UTF8);
            File.Move(tempPath, _storagePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to save keyword sets to {Path}", _storagePath);
        }
    }

    private static string? DefaultPath()
    {
        var dir = AppStorage.TryGetAppDirectory();
        return dir is null ? null : Path.Combine(dir, "keyword-sets.json");
    }
}

public sealed record KeywordSetApplyResult(
    bool Success,
    int KeywordsApplied,
    string Message);
