using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Images.Services;

public static class OrientationCli
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static bool IsOrientationCommand(string[] args) => args.Length > 0 &&
        string.Equals(args[0], "--orientation-suggest", StringComparison.OrdinalIgnoreCase);

    public static int Run(string[] args)
    {
        CliReport.TryAttachConsole();
        try
        {
            if (args.Length != 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                Console.Error.WriteLine("Usage: Images.exe --orientation-suggest <imagePath>");
                return 64;
            }
            return Execute(args[1], Console.Out, Console.Error);
        }
        finally
        {
            try { Console.Out.Flush(); } catch { }
            try { Console.Error.Flush(); } catch { }
        }
    }

    public static int Execute(
        string imagePath,
        TextWriter output,
        TextWriter error,
        Func<string, OrientationSuggestionResult>? suggester = null)
    {
        suggester ??= path => OrientationSuggestionService.Suggest(path);
        var result = suggester(imagePath);
        if (!result.Success)
        {
            error.WriteLine(result.ErrorMessage);
            return result.Status == OrientationSuggestionStatus.ModelUnavailable ? 2 : 1;
        }

        output.WriteLine(JsonSerializer.Serialize(new
        {
            result.SourcePath,
            result.ImageWidth,
            result.ImageHeight,
            result.Runtime,
            result.Assessment,
            result.IsConfident,
            result.SuggestedCorrectionDegreesClockwise,
            result.Confidence,
            result.Margin,
            result.Probabilities,
            ModelDomain = "Receipts, invoices, documents, and screenshots",
        }, JsonOptions));
        error.WriteLine($"Orientation assessment: {result.Assessment}; confidence {result.Confidence.ToString("0.0000", CultureInfo.InvariantCulture)}. Suggestion only; no files were modified.");
        return 0;
    }
}
