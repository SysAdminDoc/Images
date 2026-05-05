using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Xml.Linq;
using ImageMagick;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public sealed record AssetSmartFilterItem(
    string Path,
    string FileName,
    string Folder,
    string Extension,
    string Format,
    long Length,
    DateTimeOffset ModifiedUtc,
    int? Width,
    int? Height,
    string Orientation,
    string DimensionBucket,
    string DateBucket,
    bool IsDuplicate,
    int? Rating,
    IReadOnlyList<string> Tags,
    string Palette)
{
    public string DuplicateStatus => IsDuplicate ? "duplicate" : "unique";
}

public sealed record AssetSmartFilterResult(
    IReadOnlyList<AssetSmartFilterItem> Items,
    string Summary,
    bool HasStructuredFilters,
    IReadOnlyList<string> ActiveFilters);

public static class AssetSmartFilterService
{
    private static readonly ILogger Log = Images.Services.Log.Get(nameof(AssetSmartFilterService));

    public static IReadOnlyList<AssetSmartFilterItem> BuildIndex(
        IEnumerable<string> paths,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var snapshots = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(TryCreateSnapshot)
            .Where(snapshot => snapshot is not null)
            .Select(snapshot => snapshot!)
            .DistinctBy(snapshot => snapshot.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var duplicatePaths = FindDuplicatePaths(snapshots);
        var timestamp = now ?? DateTimeOffset.Now;

        return snapshots
            .Select(snapshot => BuildItem(snapshot, duplicatePaths.Contains(snapshot.Path), timestamp))
            .ToList();
    }

    public static AssetSmartFilterResult Filter(
        IReadOnlyList<AssetSmartFilterItem> index,
        string query)
    {
        ArgumentNullException.ThrowIfNull(index);

        var parsed = ParsedSmartFilterQuery.Parse(query);
        if (parsed.IsEmpty)
            return new AssetSmartFilterResult(index, "", false, []);

        var items = index
            .Where(parsed.Matches)
            .ToList();
        return new AssetSmartFilterResult(
            items,
            parsed.Summary,
            parsed.HasStructuredFilters,
            parsed.ActiveFilters);
    }

    private static FileSnapshot? TryCreateSnapshot(string path)
    {
        try
        {
            var fullPath = System.IO.Path.GetFullPath(path);
            if (!File.Exists(fullPath))
                return null;

            var info = new FileInfo(fullPath);
            return new FileSnapshot(
                fullPath,
                info.Name,
                info.Directory?.FullName ?? "",
                NormalizeToken(info.Extension.TrimStart('.')),
                info.Length,
                new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.LogDebug(ex, "Could not index asset smart-filter path {Path}", path);
            return null;
        }
    }

    private static AssetSmartFilterItem BuildItem(
        FileSnapshot snapshot,
        bool isDuplicate,
        DateTimeOffset now)
    {
        var signals = TryReadImageSignals(snapshot.Path);
        var sidecar = ReadSidecarFacts(snapshot.Path);

        return new AssetSmartFilterItem(
            snapshot.Path,
            snapshot.FileName,
            snapshot.Folder,
            snapshot.Extension,
            FormatForExtension(snapshot.Extension),
            snapshot.Length,
            snapshot.ModifiedUtc,
            signals.Width,
            signals.Height,
            OrientationFor(signals.Width, signals.Height),
            DimensionBucketFor(signals.Width, signals.Height),
            DateBucketFor(snapshot.ModifiedUtc, now),
            isDuplicate,
            sidecar.Rating,
            sidecar.Tags,
            signals.Palette ?? "unknown");
    }

    private static ImageSignals TryReadImageSignals(string path)
    {
        if (SupportedImageFormats.IsArchive(path) || SupportedImageFormats.RequiresGhostscript(path))
            return ImageSignals.Empty;

        try
        {
            CodecRuntime.Configure();
            var readSettings = new MagickReadSettings
            {
                FrameIndex = 0,
                FrameCount = 1,
                BackgroundColor = MagickColors.White
            };

            using var ping = new MagickImage();
            ping.Ping(new FileInfo(path), readSettings);

            return new ImageSignals(
                (int)Math.Min(ping.Width, int.MaxValue),
                (int)Math.Min(ping.Height, int.MaxValue),
                TryReadPalette(path, readSettings));
        }
        catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Log.LogDebug(ex, "Could not read smart-filter image signals for {Path}", path);
            return ImageSignals.Empty;
        }
    }

    private static string? TryReadPalette(string path, MagickReadSettings readSettings)
    {
        try
        {
            using var image = new MagickImage(new FileInfo(path), readSettings);
            image.Resize(new MagickGeometry(1, 1) { IgnoreAspectRatio = true });
            var color = image.Histogram()
                .OrderByDescending(pair => pair.Value)
                .Select(pair => pair.Key)
                .FirstOrDefault();

            return color is null
                ? null
                : ClassifyPalette(ToByte(color.R), ToByte(color.G), ToByte(color.B));
        }
        catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Log.LogDebug(ex, "Could not read smart-filter palette for {Path}", path);
            return null;
        }
    }

    private static byte ToByte(ushort value)
        => (byte)Math.Clamp((int)Math.Round(value / 257d), 0, 255);

    private static string ClassifyPalette(byte red, byte green, byte blue)
    {
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var brightness = max / 255d;

        if (brightness < 0.18)
            return "dark";

        var saturation = max == 0 ? 0 : (max - min) / (double)max;
        if (saturation < 0.18)
            return brightness > 0.82 ? "light" : "gray";

        var hue = Hue(red, green, blue, max, min);
        return hue switch
        {
            < 15 or >= 345 => "red",
            < 45 => "orange",
            < 70 => "yellow",
            < 155 => "green",
            < 190 => "cyan",
            < 250 => "blue",
            < 285 => "purple",
            _ => "pink"
        };
    }

    private static double Hue(byte red, byte green, byte blue, byte max, byte min)
    {
        var delta = max - min;
        if (delta == 0)
            return 0;

        double hue;
        if (max == red)
            hue = 60d * (((green - blue) / (double)delta) % 6d);
        else if (max == green)
            hue = 60d * (((blue - red) / (double)delta) + 2d);
        else
            hue = 60d * (((red - green) / (double)delta) + 4d);

        return hue < 0 ? hue + 360d : hue;
    }

    private static string OrientationFor(int? width, int? height)
    {
        if (width is null || height is null || width <= 0 || height <= 0)
            return "unknown";
        if (Math.Abs(width.Value - height.Value) <= Math.Max(width.Value, height.Value) * 0.03)
            return "square";
        return width > height ? "landscape" : "portrait";
    }

    private static string DimensionBucketFor(int? width, int? height)
    {
        if (width is null || height is null || width <= 0 || height <= 0)
            return "unknown";

        var megapixels = width.Value * (height.Value / 1_000_000d);
        return megapixels switch
        {
            < 1 => "tiny",
            < 4 => "small",
            < 12 => "medium",
            _ => "large"
        };
    }

    private static string DateBucketFor(DateTimeOffset modifiedUtc, DateTimeOffset now)
    {
        var age = now.UtcDateTime - modifiedUtc.UtcDateTime;
        if (age < TimeSpan.Zero)
            age = TimeSpan.Zero;

        if (age < TimeSpan.FromDays(1))
            return "today";
        if (age <= TimeSpan.FromDays(7))
            return "week";
        if (age <= TimeSpan.FromDays(31))
            return "month";
        return modifiedUtc.LocalDateTime.Year == now.LocalDateTime.Year ? "year" : "older";
    }

    private static string FormatForExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return "unknown";

        var normalized = "." + extension.TrimStart('.').ToLowerInvariant();
        return SupportedImageFormats.FamilyLabelForExtension(normalized) ?? extension.ToUpperInvariant();
    }

    private static SidecarFacts ReadSidecarFacts(string path)
    {
        var rating = (int?)null;
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sidecar in SidecarPaths(path))
        {
            if (!File.Exists(sidecar))
                continue;

            try
            {
                var document = XDocument.Load(sidecar, LoadOptions.None);
                foreach (var attribute in document.Descendants().Attributes())
                {
                    if (attribute.Name.LocalName.Equals("Rating", StringComparison.OrdinalIgnoreCase))
                        rating ??= ParseRating(attribute.Value);
                }

                foreach (var element in document.Descendants())
                {
                    var localName = element.Name.LocalName;
                    var value = (element.Value ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    if (localName.Equals("Rating", StringComparison.OrdinalIgnoreCase))
                    {
                        rating ??= ParseRating(value);
                        continue;
                    }

                    if (IsKeywordElement(element))
                        AddTagValues(tags, value);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.Xml.XmlException)
            {
                Log.LogDebug(ex, "Could not read smart-filter sidecar {Path}", sidecar);
            }
        }

        return new SidecarFacts(rating, tags.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static IEnumerable<string> SidecarPaths(string path)
    {
        yield return path + ".xmp";

        var directory = System.IO.Path.GetDirectoryName(path);
        var basename = System.IO.Path.GetFileNameWithoutExtension(path);
        if (!string.IsNullOrWhiteSpace(directory) && !string.IsNullOrWhiteSpace(basename))
            yield return System.IO.Path.Combine(directory, basename + ".xmp");
    }

    private static int? ParseRating(string value)
    {
        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, -1, 5)
            : null;
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

    private static void AddTagValues(HashSet<string> tags, string value)
    {
        foreach (var part in value.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = part.Trim();
            if (!string.IsNullOrWhiteSpace(normalized))
                tags.Add(normalized);
        }
    }

    private static HashSet<string> FindDuplicatePaths(IReadOnlyList<FileSnapshot> snapshots)
    {
        var duplicatePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sameLength in snapshots.Where(snapshot => snapshot.Length > 0).GroupBy(snapshot => snapshot.Length))
        {
            var group = sameLength.ToList();
            if (group.Count < 2)
                continue;

            var byHash = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var snapshot in group)
            {
                var hash = TryHash(snapshot.Path);
                if (hash is null)
                    continue;

                if (!byHash.TryGetValue(hash, out var paths))
                {
                    paths = [];
                    byHash[hash] = paths;
                }

                paths.Add(snapshot.Path);
            }

            foreach (var duplicateGroup in byHash.Values.Where(paths => paths.Count > 1))
            {
                foreach (var path in duplicateGroup)
                    duplicatePaths.Add(path);
            }
        }

        return duplicatePaths;
    }

    private static string? TryHash(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.LogDebug(ex, "Could not hash smart-filter duplicate candidate {Path}", path);
            return null;
        }
    }

    private static string NormalizeToken(string value)
        => value.Trim().TrimStart('.').ToLowerInvariant();

    private sealed record FileSnapshot(
        string Path,
        string FileName,
        string Folder,
        string Extension,
        long Length,
        DateTimeOffset ModifiedUtc);

    private sealed record ImageSignals(int? Width, int? Height, string? Palette)
    {
        public static ImageSignals Empty { get; } = new(null, null, null);
    }

    private sealed record SidecarFacts(int? Rating, IReadOnlyList<string> Tags);

    private sealed class ParsedSmartFilterQuery
    {
        private readonly List<string> _textTerms = [];
        private readonly List<string> _formats = [];
        private readonly List<string> _folders = [];
        private readonly List<string> _orientations = [];
        private readonly List<string> _dimensions = [];
        private readonly List<string> _dates = [];
        private readonly List<string> _palettes = [];
        private readonly List<string> _tags = [];
        private readonly List<RatingFilter> _ratings = [];
        private bool? _duplicate;

        public bool IsEmpty =>
            _textTerms.Count == 0 &&
            _formats.Count == 0 &&
            _folders.Count == 0 &&
            _orientations.Count == 0 &&
            _dimensions.Count == 0 &&
            _dates.Count == 0 &&
            _palettes.Count == 0 &&
            _tags.Count == 0 &&
            _ratings.Count == 0 &&
            _duplicate is null;

        public bool HasStructuredFilters => ActiveFilters.Count > 0;

        public IReadOnlyList<string> ActiveFilters
        {
            get
            {
                var filters = new List<string>();
                AddFilter(filters, "format", _formats);
                AddFilter(filters, "folder", _folders);
                AddFilter(filters, "orientation", _orientations);
                AddFilter(filters, "dimensions", _dimensions);
                AddFilter(filters, "date", _dates);
                AddFilter(filters, "palette", _palettes);
                AddFilter(filters, "tag", _tags);
                if (_ratings.Count > 0)
                    filters.Add("rating");
                if (_duplicate is not null)
                    filters.Add(_duplicate.Value ? "duplicates" : "unique");
                return filters;
            }
        }

        public string Summary => string.Join(" · ", ActiveFilters);

        public static ParsedSmartFilterQuery Parse(string query)
        {
            var parsed = new ParsedSmartFilterQuery();
            foreach (var term in SplitTerms(query))
            {
                var separator = term.IndexOf(':');
                if (separator <= 0 || separator == term.Length - 1)
                {
                    parsed._textTerms.Add(term);
                    continue;
                }

                var key = NormalizeToken(term[..separator]);
                var value = term[(separator + 1)..].Trim();
                parsed.AddStructuredTerm(key, value);
            }

            return parsed;
        }

        public bool Matches(AssetSmartFilterItem item)
        {
            return MatchesText(item) &&
                   MatchesAny(_formats, item.Extension, item.Format) &&
                   MatchesContains(_folders, item.Folder, System.IO.Path.GetFileName(item.Folder)) &&
                   MatchesAny(_orientations, item.Orientation) &&
                   MatchesDimensions(item) &&
                   MatchesAny(_dates, item.DateBucket) &&
                   MatchesAny(_palettes, item.Palette) &&
                   MatchesTags(item) &&
                   MatchesRating(item) &&
                   (_duplicate is null || item.IsDuplicate == _duplicate.Value);
        }

        private void AddStructuredTerm(string key, string value)
        {
            var normalized = NormalizeToken(value);
            if (string.IsNullOrWhiteSpace(normalized))
                return;

            switch (key)
            {
                case "format":
                case "type":
                case "ext":
                    _formats.Add(normalized);
                    break;
                case "folder":
                case "path":
                    _folders.Add(normalized);
                    break;
                case "orientation":
                case "orient":
                    _orientations.Add(normalized);
                    break;
                case "dimensions":
                case "dimension":
                case "size":
                    _dimensions.Add(normalized);
                    break;
                case "date":
                case "modified":
                case "created":
                    _dates.Add(NormalizeDateAlias(normalized));
                    break;
                case "palette":
                case "color":
                case "colour":
                    _palettes.Add(normalized);
                    break;
                case "tag":
                case "keyword":
                    _tags.Add(normalized);
                    break;
                case "rating":
                case "stars":
                    _ratings.Add(RatingFilter.Parse(normalized));
                    break;
                case "duplicate":
                case "dupe":
                case "duplicates":
                    _duplicate = ParseBool(normalized);
                    break;
                default:
                    _textTerms.Add(value);
                    break;
            }
        }

        private bool MatchesText(AssetSmartFilterItem item)
        {
            if (_textTerms.Count == 0)
                return true;

            return _textTerms.All(term =>
                Contains(item.FileName, term) ||
                Contains(item.Path, term) ||
                Contains(item.Format, term) ||
                Contains(item.Orientation, term) ||
                Contains(item.DimensionBucket, term) ||
                Contains(item.DateBucket, term) ||
                Contains(item.Palette, term) ||
                item.Tags.Any(tag => Contains(tag, term)));
        }

        private bool MatchesDimensions(AssetSmartFilterItem item)
        {
            if (_dimensions.Count == 0)
                return true;

            return _dimensions.Any(dimension =>
            {
                if (dimension.Equals(item.DimensionBucket, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (dimension is "wide" && item is { Width: not null, Height: not null } && item.Width.Value >= item.Height.Value * 1.75)
                    return true;
                if (dimension is "tall" && item is { Width: not null, Height: not null } && item.Height.Value >= item.Width.Value * 1.75)
                    return true;
                return dimension is "square" && item.Orientation.Equals("square", StringComparison.OrdinalIgnoreCase);
            });
        }

        private bool MatchesTags(AssetSmartFilterItem item)
        {
            return _tags.Count == 0 ||
                   _tags.Any(tag => item.Tags.Any(itemTag => Contains(itemTag, tag)));
        }

        private bool MatchesRating(AssetSmartFilterItem item)
        {
            return _ratings.Count == 0 ||
                   _ratings.Any(rating => rating.Matches(item.Rating));
        }

        private static bool MatchesAny(IReadOnlyList<string> filters, params string[] values)
        {
            return filters.Count == 0 ||
                   filters.Any(filter => values.Any(value => Contains(value, filter)));
        }

        private static bool MatchesContains(IReadOnlyList<string> filters, params string?[] values)
        {
            return filters.Count == 0 ||
                   filters.Any(filter => values.Any(value => value is not null && Contains(value, filter)));
        }

        private static bool Contains(string value, string filter)
            => value.Contains(filter, StringComparison.OrdinalIgnoreCase);

        private static IEnumerable<string> SplitTerms(string query)
            => (query ?? "")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        private static string NormalizeDateAlias(string value) => value switch
        {
            "recent" or "7d" or "7days" or "last7" => "week",
            "30d" or "30days" or "last30" => "month",
            "thisyear" => "year",
            _ => value
        };

        private static bool? ParseBool(string value) => value switch
        {
            "yes" or "true" or "1" or "on" or "duplicate" or "duplicates" => true,
            "no" or "false" or "0" or "off" or "unique" => false,
            _ => null
        };

        private static void AddFilter(List<string> filters, string label, IReadOnlyList<string> values)
        {
            if (values.Count == 0)
                return;

            filters.Add($"{label}:{string.Join("/", values)}");
        }
    }

    private sealed record RatingFilter(int? Exact, int? Minimum, bool Unrated)
    {
        public bool Matches(int? rating)
        {
            if (Unrated)
                return rating is null;
            if (rating is null)
                return false;
            if (Minimum is not null)
                return rating.Value >= Minimum.Value;
            return Exact is null || rating.Value == Exact.Value;
        }

        public static RatingFilter Parse(string value)
        {
            if (value is "none" or "unrated")
                return new RatingFilter(null, null, true);

            if (value.StartsWith(">=", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(value[2..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minimum))
            {
                return new RatingFilter(null, Math.Clamp(minimum, -1, 5), false);
            }

            if (value.EndsWith('+') &&
                int.TryParse(value[..^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minimum))
            {
                return new RatingFilter(null, Math.Clamp(minimum, -1, 5), false);
            }

            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var exact)
                ? new RatingFilter(Math.Clamp(exact, -1, 5), null, false)
                : new RatingFilter(null, null, false);
        }
    }
}
