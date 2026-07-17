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
        var folders = _catalog.GetIndexedFolders();

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
        var page = _catalog.QueryByFolder(folder, limit);
        var results = page.Assets.ToList();

        if (redactPaths)
            results = results.Select(RedactAsset).ToList();

        return new CatalogQueryResult(results, page.TotalMatched, page.TotalMatched > results.Count);
    }

    public CatalogQueryResult Search(
        string query,
        int limit = 200,
        bool redactPaths = false)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new CatalogQueryResult([], 0, false);

        var page = _catalog.Search(query, limit);
        var results = page.Assets.ToList();

        if (redactPaths)
            results = results.Select(RedactAsset).ToList();

        return new CatalogQueryResult(results, page.TotalMatched, page.TotalMatched > results.Count);
    }

    public CatalogQueryResult FindNear(
        double latitude,
        double longitude,
        double radiusKm,
        int limit = 200,
        bool redactPaths = false)
    {
        var page = _catalog.FindNear(latitude, longitude, radiusKm, limit);
        var results = page.Assets.ToList();

        if (redactPaths)
            results = results.Select(RedactAsset).ToList();

        return new CatalogQueryResult(results, page.TotalMatched, page.TotalMatched > results.Count);
    }

    public CatalogAssetRecord? GetByPath(string path, bool redactPaths = false)
    {
        var asset = _catalog.GetByPath(path);
        if (asset is null) return null;
        return redactPaths ? RedactAsset(asset) : asset;
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

}
