using System.IO;

namespace Images.Services;

public sealed record CatalogQueryResult(
    IReadOnlyList<CatalogAssetRecord> Assets,
    int TotalMatched,
    bool Truncated);

public sealed class CatalogQueryService
{
    private readonly CatalogService _catalog;

    public CatalogQueryService(CatalogService catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public bool IsAvailable => _catalog.IsAvailable;

    public IReadOnlyList<string> ListIndexedFolders(bool redactPaths = false)
    {
        var assets = _catalog.GetAllAssets(50_000);
        var folders = assets
            .Select(a => a.Folder)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!redactPaths) return folders;

        return folders
            .Select(f => RedactPath(f))
            .ToList();
    }

    public CatalogQueryResult QueryByFolder(
        string folder,
        int limit = 500,
        bool redactPaths = false)
    {
        var all = _catalog.GetAllAssets(50_000);
        if (!TryNormalizeFolder(folder, out var normalizedFolder))
            return new CatalogQueryResult([], 0, false);

        var matched = all
            .Where(a => TryNormalizeFolder(a.Folder, out var assetFolder) &&
                assetFolder.Equals(normalizedFolder, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var truncated = matched.Count > limit;
        var results = matched.Take(limit).ToList();

        if (redactPaths)
            results = results.Select(RedactAsset).ToList();

        return new CatalogQueryResult(results, matched.Count, truncated);
    }

    public CatalogQueryResult Search(
        string query,
        int limit = 200,
        bool redactPaths = false)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new CatalogQueryResult([], 0, false);

        var all = _catalog.GetAllAssets(50_000);
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var matched = all.Where(a => MatchesAllTerms(a, terms)).ToList();

        var truncated = matched.Count > limit;
        var results = matched.Take(limit).ToList();

        if (redactPaths)
            results = results.Select(RedactAsset).ToList();

        return new CatalogQueryResult(results, matched.Count, truncated);
    }

    public CatalogAssetRecord? GetByPath(string path, bool redactPaths = false)
    {
        var asset = _catalog.GetByPath(path);
        if (asset is null) return null;
        return redactPaths ? RedactAsset(asset) : asset;
    }

    private static bool MatchesAllTerms(CatalogAssetRecord asset, string[] terms)
    {
        var searchable = string.Join(" ",
            asset.FileName,
            asset.Format,
            asset.Codec,
            string.Join(" ", asset.Tags),
            asset.Rating?.ToString() ?? "");

        return terms.All(t =>
            searchable.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    private static CatalogAssetRecord RedactAsset(CatalogAssetRecord a)
    {
        return a with
        {
            SourcePath = RedactPath(a.SourcePath),
            SidecarPath = a.SidecarPath is not null ? RedactPath(a.SidecarPath) : null,
        };
    }

    private static string RedactPath(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Length <= 2) return path;

        var redacted = new string[parts.Length];
        redacted[0] = parts[0];
        for (var i = 1; i < parts.Length - 1; i++)
            redacted[i] = "***";
        redacted[^1] = parts[^1];
        return string.Join(Path.DirectorySeparatorChar, redacted);
    }

    private static bool TryNormalizeFolder(string folder, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(folder))
            return false;

        try
        {
            normalized = Path.GetFullPath(folder)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return !string.IsNullOrWhiteSpace(normalized);
        }
        catch
        {
            return false;
        }
    }
}
