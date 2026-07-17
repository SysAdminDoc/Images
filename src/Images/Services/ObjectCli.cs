using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;

namespace Images.Services;

public static class ObjectCli
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static bool IsObjectCommand(string[] args) => args.Length > 0 &&
        (string.Equals(args[0], "--object-detect", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(args[0], "--object-xmp", StringComparison.OrdinalIgnoreCase));

    public static int Run(string[] args)
    {
        CliReport.TryAttachConsole();
        try
        {
            if (args.Length != 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                Console.Error.WriteLine("Usage: Images.exe --object-detect <imagePath> | --object-xmp <imagePath>");
                return 64;
            }
            return string.Equals(args[0], "--object-xmp", StringComparison.OrdinalIgnoreCase)
                ? ExecuteXmp(args[1], Console.Out, Console.Error)
                : Execute(args[1], Console.Out, Console.Error);
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
        Func<string, ObjectDetectionResult>? detector = null)
    {
        detector ??= path => ObjectDetectionService.Detect(path);
        var result = detector(imagePath);
        if (!result.Success)
        {
            error.WriteLine(result.ErrorMessage);
            return 2;
        }
        output.WriteLine(JsonSerializer.Serialize(new
        {
            result.SourcePath,
            result.ImageWidth,
            result.ImageHeight,
            result.Runtime,
            result.SuggestedKeywords,
            result.Detections,
        }, JsonOptions));
        error.WriteLine($"Detected {result.Detections.Count.ToString(CultureInfo.InvariantCulture)} object(s) and suggested {result.SuggestedKeywords.Count.ToString(CultureInfo.InvariantCulture)} keyword(s). No files were modified.");
        return 0;
    }

    public static int ExecuteXmp(
        string imagePath,
        TextWriter output,
        TextWriter error,
        Func<string, ObjectDetectionResult>? detector = null)
    {
        detector ??= path => ObjectDetectionService.Detect(path);
        var result = detector(imagePath);
        if (!result.Success)
        {
            error.WriteLine(result.ErrorMessage);
            return 2;
        }
        output.WriteLine(ObjectKeywordDraftService.BuildDraft(result).ToString(SaveOptions.DisableFormatting));
        error.WriteLine($"Emitted an XMP draft with {result.SuggestedKeywords.Count.ToString(CultureInfo.InvariantCulture)} reviewed-prefix object keyword(s). No files were modified.");
        return 0;
    }
}
