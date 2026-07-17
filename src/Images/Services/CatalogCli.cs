using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Images.Services;

public enum CatalogCliMode
{
    Search,
    Near,
    RootAdd,
    RootRemove,
    RootList,
    Rescan,
    Stacks,
    Trips,
    Events,
}

public sealed record CatalogCliRequest(
    CatalogCliMode Mode,
    string? SearchTerms = null,
    double Latitude = 0,
    double Longitude = 0,
    double RadiusKm = 0,
    string? RootPath = null,
    int MaxHashDistance = 6,
    double MaxCaptureSeconds = 120,
    double MaxGeoDistanceMeters = 250,
    double HomeLatitude = 0,
    double HomeLongitude = 0,
    double MinTripDistanceKm = 50,
    int MaxTripGapDays = 1,
    double MaxEventGapHours = 6);

/// <summary>
/// Scriptable, window-free consumer for the rebuildable catalog. Search commands write one source
/// path per line; grouped commands write one JSON object per line. Counts and usage errors go to
/// standard error so standard output can be piped safely into other tools.
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
         string.Equals(args[0], "--catalog-rescan", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(args[0], "--catalog-stacks", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(args[0], "--catalog-trips", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(args[0], "--catalog-events", StringComparison.OrdinalIgnoreCase));

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

        if (string.Equals(args[0], "--catalog-stacks", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 1)
            {
                request = new CatalogCliRequest(CatalogCliMode.Stacks);
                return true;
            }

            if (args.Length != 4 ||
                !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hashDistance) || hashDistance is < 0 or > 64 ||
                !TryParseFiniteDouble(args[2], out var seconds) || seconds < 0 ||
                !TryParseFiniteDouble(args[3], out var meters) || meters < 0)
            {
                error = "Usage: Images.exe --catalog-stacks [<maxHashDistance> <maxSeconds> <maxMeters>]";
                return true;
            }

            request = new CatalogCliRequest(
                CatalogCliMode.Stacks,
                MaxHashDistance: hashDistance,
                MaxCaptureSeconds: seconds,
                MaxGeoDistanceMeters: meters);
            return true;
        }

        if (string.Equals(args[0], "--catalog-trips", StringComparison.OrdinalIgnoreCase))
        {
            var distanceKm = 50d;
            var gapDays = 1;
            if (args.Length is not (3 or 5) ||
                !TryParseFiniteDouble(args[1], out var homeLatitude) || homeLatitude is < -90 or > 90 ||
                !TryParseFiniteDouble(args[2], out var homeLongitude) || homeLongitude is < -180 or > 180 ||
                (args.Length == 5 && (!TryParseFiniteDouble(args[3], out distanceKm) || distanceKm <= 0 ||
                                     !int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out gapDays) || gapDays is < 0 or > 31)))
            {
                error = "Usage: Images.exe --catalog-trips <homeLat> <homeLon> [<minDistanceKm> <maxGapDays>]";
                return true;
            }

            request = new CatalogCliRequest(
                CatalogCliMode.Trips,
                HomeLatitude: homeLatitude,
                HomeLongitude: homeLongitude,
                MinTripDistanceKm: distanceKm,
                MaxTripGapDays: gapDays);
            return true;
        }

        if (string.Equals(args[0], "--catalog-events", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 1)
            {
                request = new CatalogCliRequest(CatalogCliMode.Events);
                return true;
            }

            if (args.Length != 2 ||
                !TryParseFiniteDouble(args[1], out var gapHours) || gapHours is < 0 or > 744)
            {
                error = "Usage: Images.exe --catalog-events [<maxGapHours>]";
                return true;
            }

            request = new CatalogCliRequest(CatalogCliMode.Events, MaxEventGapHours: gapHours);
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

        if (request.Mode == CatalogCliMode.Stacks)
        {
            var stacks = new NearDuplicateStackService().Build(
                catalog.GetAllAssets(50_000),
                request.MaxHashDistance,
                TimeSpan.FromSeconds(request.MaxCaptureSeconds),
                request.MaxGeoDistanceMeters,
                limit);
            foreach (var stack in stacks)
            {
                output.WriteLine(JsonSerializer.Serialize(new
                {
                    stackId = stack.StackId,
                    cover = stack.Cover.SourcePath,
                    maxHashDistance = stack.MaxHashDistance,
                    captureSpanSeconds = stack.CaptureSpan.TotalSeconds,
                    maxGeoDistanceMeters = stack.MaxGeoDistanceMeters,
                    assets = stack.Assets.Select(asset => asset.SourcePath)
                }));
            }

            error.WriteLine($"Found {stacks.Count.ToString(CultureInfo.InvariantCulture)} near-duplicate stacks.");
            return 0;
        }

        if (request.Mode == CatalogCliMode.Trips)
        {
            var trips = new TripDetectionService().Build(
                catalog.GetGeoTimedAssets(50_000),
                request.HomeLatitude,
                request.HomeLongitude,
                request.MinTripDistanceKm,
                request.MaxTripGapDays,
                limit);
            foreach (var trip in trips)
            {
                output.WriteLine(JsonSerializer.Serialize(new
                {
                    tripId = trip.TripId,
                    cover = trip.Cover.SourcePath,
                    startedUtc = trip.StartedUtc,
                    endedUtc = trip.EndedUtc,
                    centroid = new { latitude = trip.CentroidLatitude, longitude = trip.CentroidLongitude },
                    maxDistanceFromHomeKm = trip.MaxDistanceFromHomeKm,
                    assets = trip.Assets.Select(asset => asset.SourcePath)
                }));
            }

            error.WriteLine($"Found {trips.Count.ToString(CultureInfo.InvariantCulture)} catalog trips.");
            return 0;
        }

        if (request.Mode == CatalogCliMode.Events)
        {
            var events = new CatalogEventService().Build(
                catalog.GetTimelineAssets(50_000),
                TimeSpan.FromHours(request.MaxEventGapHours),
                limit);
            foreach (var item in events)
            {
                output.WriteLine(JsonSerializer.Serialize(new
                {
                    eventId = item.EventId,
                    keyPhoto = item.KeyPhoto.SourcePath,
                    startedUtc = item.StartedUtc,
                    endedUtc = item.EndedUtc,
                    durationSeconds = item.Duration.TotalSeconds,
                    assets = item.Assets.Select(asset => asset.SourcePath)
                }));
            }

            error.WriteLine($"Found {events.Count.ToString(CultureInfo.InvariantCulture)} catalog events.");
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
