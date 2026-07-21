using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Images.Services;

public static class SafetyCli
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static bool IsSafetyCommand(string[] args) => args.Length > 0 &&
        string.Equals(args[0], "--safety-classify", StringComparison.OrdinalIgnoreCase);

    public static int Run(string[] args)
    {
        CliReport.TryAttachConsole();
        using var cancellation = CliCancellation.OnCtrlC();
        try
        {
            if (args.Length < 2 || args.Skip(1).Any(string.IsNullOrWhiteSpace))
            {
                Console.Error.WriteLine("Usage: Images.exe --safety-classify <imagePath> [imagePath ...]");
                return 64;
            }
            return Execute(args.Skip(1).ToArray(), Console.Out, Console.Error, cancellationToken: cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Safety classification was canceled. No files were modified.");
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
        Func<IReadOnlyList<string>, IReadOnlyList<SafetyClassificationResult>>? classifier = null,
        CancellationToken cancellationToken = default)
    {
        classifier ??= paths => SafetyClassificationService.ClassifyMany(paths, cancellationToken: cancellationToken);
        var results = classifier(imagePaths);
        var successful = results.Where(result => result.Success).ToArray();
        var failures = results.Where(result => !result.Success).ToArray();
        if (successful.Length == 0)
        {
            foreach (var message in failures.Select(result => result.ErrorMessage).Distinct(StringComparer.Ordinal))
                error.WriteLine(message);
            if (failures.Any(result => result.Status == SafetyClassificationStatus.ModelUnavailable))
                return 2;
            if (failures.Any(result => result.Status == SafetyClassificationStatus.ModelLoadFailed))
                return 3;
            return 1;
        }

        output.WriteLine(JsonSerializer.Serialize(new
        {
            Model = new
            {
                Id = SafetyClassificationService.ModelId,
                Name = "Marqo NSFW Image Detection 384",
                Source = "https://huggingface.co/Marqo/nsfw-image-detection-384",
                SafetyClassificationService.SourceRevision,
                SafetyClassificationService.ArtifactSha256,
                License = "Apache-2.0",
                Labels = new[] { "NSFW", "SFW" },
            },
            ScoreExport = "explicit-stdout",
            AutomaticWrites = false,
            DecisionPolicy = "review-only; no threshold or moderation decision is imposed",
            Results = successful.Select(result => new
            {
                result.SourcePath,
                result.ImageWidth,
                result.ImageHeight,
                result.Runtime,
                result.MostLikelyLabel,
                result.Confidence,
                result.Predictions,
            }),
            Failures = failures.Select(result => new { result.SourcePath, result.ErrorMessage }),
        }, JsonOptions));
        error.WriteLine($"Classified {successful.Length.ToString(CultureInfo.InvariantCulture)} image(s); {failures.Length.ToString(CultureInfo.InvariantCulture)} failed. Scores were exported only to stdout; no files, metadata, labels, or logs were modified with classification results.");
        return failures.Length == 0 ? 0 : 1;
    }
}
