using System.Globalization;
using System.IO;

namespace Images.Services;

public enum CatalogCliMode
{
    Search,
    Near,
    RootAdd,
    RootRemove,
    RootList,
    Rescan,
}

public sealed record CatalogCliRequest(
    CatalogCliMode Mode,
    string? SearchTerms = null,
    double Latitude = 0,
    double Longitude = 0,
    double RadiusKm = 0,
    string? RootPath = null);

/// <summary>
/// Scriptable, window-free consumer for the rebuildable catalog. Standard output contains one
/// matching source path per line; counts and usage errors go to standard error so paths can be
/// piped safely into other tools.
/// </summary>
public static class CatalogCli
{
    public static bool IsCatalogCommand(string[] args) =>
        args.Length > 0 &&
        (string.Equals(args[0], "--catalog-search", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(args[0], "--catalog-near", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(args[0], "--catalog-root-add", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(args[0], "--catalog-root-remove", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(args[0], "--catalog-root-list", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(args[0], "--catalog-rescan", StringComparison.OrdinalIgnoreCase));

    public static bool TryParse(
        string[] args,
        out CatalogCliRequest? request,
        out string? error)
    {
        request = null;
        error = null;
        if (!IsCatalogCommand(args))
            return false;

        if (string.Equals(args[0], "--catalog-search", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                error = "Usage: Images.exe --catalog-search \"<terms>\"";
                return true;
            }

            request = new CatalogCliRequest(CatalogCliMode.Search, SearchTerms: args[1].Trim());
            return true;
        }

        if (string.Equals(args[0], "--catalog-root-add", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args[0], "--catalog-root-remove", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                error = $"Usage: Images.exe {args[0]} <folder>";
                return true;
            }

            request = new CatalogCliRequest(
                string.Equals(args[0], "--catalog-root-add", StringComparison.OrdinalIgnoreCase)
                    ? CatalogCliMode.RootAdd
                    : CatalogCliMode.RootRemove,
                RootPath: args[1].Trim());
            return true;
        }

        if (string.Equals(args[0], "--catalog-root-list", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(args[0], "--catalog-rescan", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 1)
            {
                error = $"Usage: Images.exe {args[0]}";
                return true;
            }

            request = new CatalogCliRequest(
                string.Equals(args[0], "--catalog-root-list", StringComparison.OrdinalIgnoreCase)
                    ? CatalogCliMode.RootList
                    : CatalogCliMode.Rescan);
            return true;
        }

        if (args.Length != 4 ||
            !TryParseFiniteDouble(args[1], out var latitude) || latitude is < -90 or > 90 ||
            !TryParseFiniteDouble(args[2], out var longitude) || longitude is < -180 or > 180 ||
            !TryParseFiniteDouble(args[3], out var radiusKm) || radiusKm <= 0)
        {
            error = "Usage: Images.exe --catalog-near <lat> <lon> <radiusKm>";
            return true;
        }

        request = new CatalogCliRequest(
            CatalogCliMode.Near,
            Latitude: latitude,
            Longitude: longitude,
            RadiusKm: radiusKm);
        return true;
    }

    public static int Run(string[] args)
    {
        CliReport.TryAttachConsole();
        try
        {
            if (!TryParse(args, out var request, out var error) || request is null)
            {
                Console.Error.WriteLine(error ?? "Unknown catalog command.");
                return 64;
            }

            return Execute(request, Console.Out, Console.Error);
        }
        finally
        {
            try { Console.Out.Flush(); } catch { }
            try { Console.Error.Flush(); } catch { }
        }
    }

    public static int Execute(
        CatalogCliRequest request,
        TextWriter output,
        TextWriter error,
        CatalogService? catalog = null,
        int limit = 500)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        catalog ??= new CatalogService();
        if (!catalog.IsAvailable)
        {
            error.WriteLine("Images catalog is unavailable. Open the library and index folders first.");
            return 2;
        }

        if (request.Mode == CatalogCliMode.RootAdd)
        {
            if (!catalog.RegisterRoot(request.RootPath ?? string.Empty))
            {
                error.WriteLine("Catalog root was not added. Confirm the folder exists and is accessible.");
                return 2;
            }

            var rebuildResult = catalog.Rebuild(catalog.GetRoots().Select(root => root.RootPath));
            error.WriteLine($"Catalog root added; indexed {rebuildResult.IndexedCount.ToString(CultureInfo.InvariantCulture)} assets.");
            return 0;
        }

        if (request.Mode == CatalogCliMode.RootRemove)
        {
            if (!catalog.RemoveRoot(request.RootPath ?? string.Empty))
            {
                error.WriteLine("Catalog root was not found.");
                return 2;
            }

            error.WriteLine("Catalog root and its cached assets were removed.");
            return 0;
        }

        if (request.Mode == CatalogCliMode.RootList)
        {
            var roots = catalog.GetRoots();
            foreach (var root in roots)
                output.WriteLine(root.RootPath);
            var offline = roots.Count(root => !root.IsOnline);
            error.WriteLine($"Listed {roots.Count.ToString(CultureInfo.InvariantCulture)} catalog roots; {offline.ToString(CultureInfo.InvariantCulture)} offline (cached assets retained).");
            return 0;
        }

        if (request.Mode == CatalogCliMode.Rescan)
        {
            var roots = catalog.GetRoots();
            var rebuildResult = catalog.Rebuild(roots.Select(root => root.RootPath));
            error.WriteLine($"Catalog rescan indexed {rebuildResult.IndexedCount.ToString(CultureInfo.InvariantCulture)} assets; {rebuildResult.OfflineRoots.Count.ToString(CultureInfo.InvariantCulture)} roots offline (cached assets retained).");
            return 0;
        }

        var query = new CatalogQueryService(catalog);
        var result = request.Mode switch
        {
            CatalogCliMode.Search => query.Search(request.SearchTerms ?? string.Empty, limit),
            CatalogCliMode.Near => query.FindNear(request.Latitude, request.Longitude, request.RadiusKm, limit),
            _ => new CatalogQueryResult([], 0, false),
        };

        foreach (var asset in result.Assets)
            output.WriteLine(asset.SourcePath);

        error.WriteLine(result.Truncated
            ? $"Showing {result.Assets.Count.ToString(CultureInfo.InvariantCulture)} of {result.TotalMatched.ToString(CultureInfo.InvariantCulture)} matching catalog assets."
            : $"Matched {result.TotalMatched.ToString(CultureInfo.InvariantCulture)} catalog assets.");
        return 0;
    }

    private static bool TryParseFiniteDouble(string value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
        double.IsFinite(result);
}
