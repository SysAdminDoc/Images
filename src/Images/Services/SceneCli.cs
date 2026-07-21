using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Images.Services;

public static class SceneCli
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static bool IsSceneCommand(string[] args) => args.Length > 0 &&
        string.Equals(args[0], "--scene-classify", StringComparison.OrdinalIgnoreCase);

    public static int Run(string[] args)
    {
        CliReport.TryAttachConsole();
        using var cancellation = CliCancellation.OnCtrlC();
        try
        {
            if (args.Length < 2 || args.Skip(1).Any(string.IsNullOrWhiteSpace))
            {
                Console.Error.WriteLine("Usage: Images.exe --scene-classify <imagePath> [imagePath ...]");
                return 64;
            }
            return Execute(args.Skip(1).ToArray(), Console.Out, Console.Error, cancellationToken: cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Scene classification was canceled. No files were modified.");
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
        Func<IReadOnlyList<string>, IReadOnlyList<SceneClassificationResult>>? classifier = null,
        CancellationToken cancellationToken = default)
    {
        classifier ??= paths => SceneClassificationService.ClassifyMany(paths, cancellationToken: cancellationToken);
        var results = classifier(imagePaths);
        var successful = results.Where(result => result.Success).ToArray();
        var failures = results.Where(result => !result.Success).ToArray();
        if (successful.Length == 0)
        {
            foreach (var message in failures.Select(result => result.ErrorMessage).Distinct(StringComparer.Ordinal))
                error.WriteLine(message);
            if (failures.Any(result => result.Status == SceneClassificationStatus.ModelUnavailable))
                return 2;
            if (failures.Any(result => result.Status == SceneClassificationStatus.ModelLoadFailed))
                return 3;
            return 1;
        }

        output.WriteLine(JsonSerializer.Serialize(new
        {
            Model = new
            {
                Id = SceneClassificationService.ModelId,
                Name = "CSAIL Places365 ResNet-18",
                Source = "https://github.com/CSAILVision/places365",
                SceneClassificationService.SourceRevision,
                SceneClassificationService.ArtifactSha256,
                License = "CC BY attribution required; upstream does not state a version",
                Attribution = "Zhou et al., Places: A 10 million Image Database for Scene Recognition, IEEE TPAMI 2017",
            },
            AutomaticWrites = false,
            Results = successful.Select(result => new
            {
                result.SourcePath,
                result.ImageWidth,
                result.ImageHeight,
                result.Runtime,
                result.Environment,
                result.EnvironmentConfidence,
                result.Predictions,
                result.SuggestedKeywords,
            }),
            Failures = failures.Select(result => new { result.SourcePath, result.ErrorMessage }),
        }, JsonOptions));
        error.WriteLine($"Classified {successful.Length.ToString(CultureInfo.InvariantCulture)} image(s); {failures.Length.ToString(CultureInfo.InvariantCulture)} failed. Scene keywords are suggestions only; no files, metadata, or smart albums were modified.");
        return failures.Length == 0 ? 0 : 1;
    }
}
