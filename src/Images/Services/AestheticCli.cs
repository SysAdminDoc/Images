using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Images.Services;

public static class AestheticCli
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static bool IsAestheticCommand(string[] args) => args.Length > 0 &&
        string.Equals(args[0], "--aesthetic-score", StringComparison.OrdinalIgnoreCase);

    public static int Run(string[] args)
    {
        CliReport.TryAttachConsole();
        using var cancellation = CliCancellation.OnCtrlC();
        try
        {
            if (args.Length < 2 || args.Skip(1).Any(string.IsNullOrWhiteSpace))
            {
                Console.Error.WriteLine("Usage: Images.exe --aesthetic-score <imagePath> [imagePath ...]");
                return 64;
            }
            return Execute(args.Skip(1).ToArray(), Console.Out, Console.Error, cancellationToken: cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Aesthetic scoring was canceled. No files were modified.");
            return 130;
        }
        finally
        {
            try { Console.Out.Flush(); } catch { }
            try { Console.Error.Flush(); } catch { }
        }
    }

    public static int Execute(
        IReadOnlyList<string> imagePaths,
        TextWriter output,
        TextWriter error,
        Func<IReadOnlyList<string>, IReadOnlyList<AestheticScoreResult>>? scorer = null,
        CancellationToken cancellationToken = default)
    {
        scorer ??= paths => AestheticScoringService.ScoreMany(paths, cancellationToken: cancellationToken);
        var results = scorer(imagePaths);
        var successful = results.Where(result => result.Success)
            .OrderByDescending(result => result.MeanScore)
            .ThenBy(result => result.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var failures = results.Where(result => !result.Success).ToArray();

        if (successful.Length == 0)
        {
            foreach (var message in failures.Select(result => result.ErrorMessage).Distinct(StringComparer.Ordinal))
                error.WriteLine(message);
            if (failures.Any(result => result.Status == AestheticScoreStatus.ModelUnavailable))
                return 2;
            if (failures.Any(result => result.Status == AestheticScoreStatus.ModelLoadFailed))
                return 3;
            return 1;
        }

        output.WriteLine(JsonSerializer.Serialize(new
        {
            Model = new
            {
                Id = AestheticScoringService.ModelId,
                Name = "idealo NIMA MobileNet aesthetic",
                Source = "https://github.com/idealo/image-quality-assessment",
                AestheticScoringService.SourceRevision,
                AestheticScoringService.ArtifactSha256,
                License = "Apache-2.0",
                TrainingDataset = "AVA",
            },
            Interpretation = "Relative comparisons within the same batch are more useful than universal score thresholds.",
            AutomaticWrites = false,
            Results = successful.Select((result, index) => new
            {
                Rank = index + 1,
                result.SourcePath,
                result.ImageWidth,
                result.ImageHeight,
                result.Runtime,
                result.MeanScore,
                result.StandardDeviation,
                result.Distribution,
            }),
            Failures = failures.Select(result => new { result.SourcePath, result.ErrorMessage }),
        }, JsonOptions));
        error.WriteLine($"Scored {successful.Length.ToString(CultureInfo.InvariantCulture)} image(s); {failures.Length.ToString(CultureInfo.InvariantCulture)} failed. No files or Pick/Reject labels were modified.");
        return failures.Length == 0 ? 0 : 1;
    }
}
