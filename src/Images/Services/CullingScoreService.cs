using System.IO;
using System.Globalization;
using ImageMagick;

namespace Images.Services;

public sealed record CullingScoreItem(
    string Path,
    string FileName,
    string Folder,
    int Rank,
    double Score,
    IReadOnlyList<string> Reasons,
    int? Rating,
    ReviewLabelKind Label,
    int SharpnessScore,
    int ExposureScore,
    int SimilarityPenalty,
    int SimilarMatchCount)
{
    public string RankText => $"#{Rank}";
    public string ScoreText => Score.ToString("0", CultureInfo.InvariantCulture);
    public string PrimaryReason => Reasons.Count > 0 ? Reasons[0] : "Local score ready";
    public string ReasonsText => string.Join(" · ", Reasons);
    public string ReviewText => Rating is null
        ? LabelText
        : $"{Rating.Value} star{(Rating.Value == 1 ? "" : "s")} · {LabelText}";
    public string LabelText => Label switch
    {
        ReviewLabelKind.Pick => "Pick",
        ReviewLabelKind.Reject => "Reject",
        _ => "No label"
    };
    public bool IsPick => Label == ReviewLabelKind.Pick;
    public bool IsReject => Label == ReviewLabelKind.Reject;
}

public sealed record CullingScoreResult(IReadOnlyList<CullingScoreItem> Items, int FailedCount)
{
    public bool HasItems => Items.Count > 0;
    public string Summary => FailedCount == 0
        ? $"Ranked {Items.Count} local image{Plural(Items.Count)}."
        : $"Ranked {Items.Count} local image{Plural(Items.Count)}; {FailedCount} failed.";

    private static string Plural(int count) => count == 1 ? "" : "s";
}

public sealed class CullingScoreService
{
    private const int HashSize = 8;
    private const int AnalysisMaxEdge = 96;
    private const int SimilarityThreshold = 6;
    private readonly ReviewLabelService _reviewLabels;

    public CullingScoreService(ReviewLabelService? reviewLabels = null)
    {
        _reviewLabels = reviewLabels ?? new ReviewLabelService();
    }

    public CullingScoreResult RankFiles(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var failures = 0;
        var normalized = NormalizePaths(paths, ref failures);
        var drafts = new List<CullingScoreDraft>(normalized.Count);

        foreach (var path in normalized)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var state = _reviewLabels.ReadState(path);
                var signals = AnalyzeImage(path, cancellationToken);
                var reasons = BuildInitialReasons(signals, state);
                var score = BuildBaseScore(signals, state);
                var info = new FileInfo(path);

                drafts.Add(new CullingScoreDraft(
                    Path.GetFullPath(path),
                    info.Name,
                    info.DirectoryName ?? "",
                    score,
                    reasons,
                    state,
                    signals));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is MagickException
                                      or IOException
                                      or UnauthorizedAccessException
                                      or ArgumentException
                                      or NotSupportedException
                                      or InvalidOperationException)
            {
                failures++;
            }
        }

        ApplySimilarityPenalties(drafts);

        var ranked = drafts
            .Select(draft =>
            {
                if (draft.SimilarityPenalty > 0)
                {
                    var matchText = draft.SimilarMatchCount == 1
                        ? "1 stronger similar frame"
                        : $"{draft.SimilarMatchCount} stronger similar frames";
                    draft.Reasons.Add($"Similarity penalty: {matchText}");
                }

                draft.Score = Math.Clamp(draft.Score - draft.SimilarityPenalty, 0, 100);
                return draft;
            })
            .OrderByDescending(draft => draft.Score)
            .ThenBy(draft => draft.FileName, StringComparer.OrdinalIgnoreCase)
            .Select((draft, index) => new CullingScoreItem(
                draft.Path,
                draft.FileName,
                draft.Folder,
                index + 1,
                draft.Score,
                draft.Reasons.ToArray(),
                draft.ReviewState.Rating,
                draft.ReviewState.Label,
                draft.Signals.SharpnessScore,
                draft.Signals.ExposureScore,
                draft.SimilarityPenalty,
                draft.SimilarMatchCount))
            .ToArray();

        return new CullingScoreResult(ranked, failures);
    }

    private static List<string> NormalizePaths(IEnumerable<string> paths, ref int failures)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                failures++;
                continue;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
            {
                failures++;
                continue;
            }

            if (!File.Exists(fullPath)
                || !SupportedImageFormats.IsSupported(fullPath)
                || SupportedImageFormats.IsArchive(fullPath))
            {
                failures++;
                continue;
            }

            if (seen.Add(fullPath))
                normalized.Add(fullPath);
        }

        return normalized;
    }

    private static ImageSignals AnalyzeImage(string path, CancellationToken cancellationToken)
    {
        CodecRuntime.Configure();
        cancellationToken.ThrowIfCancellationRequested();

        using var image = new MagickImage(path);
        image.AutoOrient();
        image.BackgroundColor = MagickColors.White;
        image.Alpha(AlphaOption.Remove);

        cancellationToken.ThrowIfCancellationRequested();
        using var hashImage = (MagickImage)image.Clone();
        var hash = ComputeAverageHash(hashImage);

        using var sample = (MagickImage)image.Clone();
        sample.Resize(new MagickGeometry(AnalysisMaxEdge, AnalysisMaxEdge)
        {
            Greater = true
        });
        sample.Format = MagickFormat.Bgra;

        cancellationToken.ThrowIfCancellationRequested();
        var width = checked((int)sample.Width);
        var height = checked((int)sample.Height);
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("Decoded image dimensions are not valid for culling analysis.");

        var pixels = sample.GetPixelsUnsafe().ToByteArray(PixelMapping.BGRA)
                     ?? throw new InvalidOperationException("Magick.NET returned no culling-analysis pixels.");
        var count = checked(width * height);
        if (pixels.Length < count * 4)
            throw new InvalidOperationException("Magick.NET returned an incomplete culling-analysis pixel buffer.");

        var luminance = new double[count];
        var lumaSum = 0.0;
        var shadowClipped = 0;
        var highlightClipped = 0;

        for (var i = 0; i < count; i++)
        {
            var offset = i * 4;
            var blue = pixels[offset];
            var green = pixels[offset + 1];
            var red = pixels[offset + 2];
            var value = (0.299 * red) + (0.587 * green) + (0.114 * blue);
            luminance[i] = value;
            lumaSum += value;

            if (value <= 8)
                shadowClipped++;
            if (value >= 247)
                highlightClipped++;
        }

        var meanLuma = lumaSum / count;
        var shadowPercent = shadowClipped * 100.0 / count;
        var highlightPercent = highlightClipped * 100.0 / count;
        return new ImageSignals(
            ScoreSharpness(luminance, width, height),
            ScoreExposure(meanLuma, shadowPercent, highlightPercent),
            shadowPercent,
            highlightPercent,
            hash);
    }

    private static ulong? ComputeAverageHash(MagickImage image)
    {
        image.Resize(new MagickGeometry(HashSize, HashSize)
        {
            IgnoreAspectRatio = true
        });
        image.Format = MagickFormat.Bgra;

        var pixels = image.GetPixelsUnsafe().ToByteArray(PixelMapping.BGRA);
        if (pixels is null || pixels.Length < HashSize * HashSize * 4)
            return null;

        Span<double> luminance = stackalloc double[HashSize * HashSize];
        var sum = 0.0;
        for (var i = 0; i < luminance.Length; i++)
        {
            var offset = i * 4;
            var blue = pixels[offset];
            var green = pixels[offset + 1];
            var red = pixels[offset + 2];
            var value = (0.299 * red) + (0.587 * green) + (0.114 * blue);
            luminance[i] = value;
            sum += value;
        }

        var average = sum / luminance.Length;
        var hash = 0UL;
        for (var i = 0; i < luminance.Length; i++)
        {
            if (luminance[i] >= average)
                hash |= 1UL << i;
        }

        return hash;
    }

    private static int ScoreSharpness(IReadOnlyList<double> luminance, int width, int height)
    {
        if (width < 3 || height < 3)
            return 0;

        var total = 0.0;
        var samples = 0;
        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                var center = luminance[(y * width) + x];
                var laplacian = Math.Abs(
                    (4 * center)
                    - luminance[(y * width) + x - 1]
                    - luminance[(y * width) + x + 1]
                    - luminance[((y - 1) * width) + x]
                    - luminance[((y + 1) * width) + x]);
                total += laplacian;
                samples++;
            }
        }

        var mean = samples == 0 ? 0 : total / samples;
        return ClampSignal(mean * 0.85);
    }

    private static int ScoreExposure(double meanLuma, double shadowClipPercent, double highlightClipPercent)
    {
        var clipPenalty = Math.Min(100, (shadowClipPercent + highlightClipPercent) * 1.2);
        var meanPenalty = Math.Abs(meanLuma - 128) / 128 * 35;
        return ClampSignal(100 - clipPenalty - meanPenalty);
    }

    private static List<string> BuildInitialReasons(ImageSignals signals, ReviewLabelState state)
    {
        var reasons = new List<string>
        {
            $"Sharpness {signals.SharpnessScore}/100"
        };

        if (signals.HighlightClipPercent >= 2 && signals.ShadowClipPercent >= 2)
            reasons.Add($"Shadow/highlight clipping {FormatPercent(signals.ShadowClipPercent)}/{FormatPercent(signals.HighlightClipPercent)}");
        else if (signals.HighlightClipPercent >= 2)
            reasons.Add($"Highlight clipping {FormatPercent(signals.HighlightClipPercent)}");
        else if (signals.ShadowClipPercent >= 2)
            reasons.Add($"Shadow clipping {FormatPercent(signals.ShadowClipPercent)}");
        else
            reasons.Add($"Exposure {signals.ExposureScore}/100");

        if (state.Rating is { } rating and > 0)
            reasons.Add($"{rating}-star rating boost");
        else if (state.Rating == 0)
            reasons.Add("Rating cleared");

        if (state.Label == ReviewLabelKind.Pick)
            reasons.Add("Existing pick boost");
        else if (state.Label == ReviewLabelKind.Reject)
            reasons.Add("Existing reject penalty");

        return reasons;
    }

    private static double BuildBaseScore(ImageSignals signals, ReviewLabelState state)
    {
        var score = 10 + (signals.SharpnessScore * 0.45) + (signals.ExposureScore * 0.35);
        if (state.Rating is { } rating)
            score += Math.Clamp(rating, 0, 5) * 5;

        score += state.Label switch
        {
            ReviewLabelKind.Pick => 15,
            ReviewLabelKind.Reject => -35,
            _ => 0
        };

        return Math.Clamp(score, 0, 100);
    }

    private static void ApplySimilarityPenalties(IReadOnlyList<CullingScoreDraft> drafts)
    {
        for (var i = 0; i < drafts.Count; i++)
        {
            var left = drafts[i];
            if (left.Signals.AverageHash is not { } leftHash)
                continue;

            for (var j = i + 1; j < drafts.Count; j++)
            {
                var right = drafts[j];
                if (right.Signals.AverageHash is not { } rightHash)
                    continue;

                var distance = DuplicateCleanupService.HammingDistance(leftHash, rightHash);
                if (distance > SimilarityThreshold)
                    continue;

                var lower = ChooseLowerRankedSimilar(left, right);
                lower.SimilarMatchCount++;
                lower.SimilarityPenalty += distance <= 2 ? 18 : 12;
            }
        }
    }

    private static CullingScoreDraft ChooseLowerRankedSimilar(CullingScoreDraft left, CullingScoreDraft right)
    {
        var scoreCompare = left.Score.CompareTo(right.Score);
        if (scoreCompare < 0)
            return left;
        if (scoreCompare > 0)
            return right;

        return string.Compare(left.FileName, right.FileName, StringComparison.OrdinalIgnoreCase) <= 0
            ? right
            : left;
    }

    private static int ClampSignal(double value)
        => (int)Math.Round(Math.Clamp(value, 0, 100), MidpointRounding.AwayFromZero);

    private static string FormatPercent(double percent)
        => percent.ToString("0", CultureInfo.InvariantCulture) + "%";

    private sealed class CullingScoreDraft
    {
        public CullingScoreDraft(
            string path,
            string fileName,
            string folder,
            double score,
            List<string> reasons,
            ReviewLabelState reviewState,
            ImageSignals signals)
        {
            Path = path;
            FileName = fileName;
            Folder = folder;
            Score = score;
            Reasons = reasons;
            ReviewState = reviewState;
            Signals = signals;
        }

        public string Path { get; }
        public string FileName { get; }
        public string Folder { get; }
        public double Score { get; set; }
        public List<string> Reasons { get; }
        public ReviewLabelState ReviewState { get; }
        public ImageSignals Signals { get; }
        public int SimilarityPenalty { get; set; }
        public int SimilarMatchCount { get; set; }
    }

    private sealed record ImageSignals(
        int SharpnessScore,
        int ExposureScore,
        double ShadowClipPercent,
        double HighlightClipPercent,
        ulong? AverageHash);
}
