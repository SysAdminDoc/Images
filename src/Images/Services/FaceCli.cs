using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Images.Services;

public static class FaceCli
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static bool IsFaceCommand(string[] args) =>
        args.Length > 0 && string.Equals(args[0], "--face-detect", StringComparison.OrdinalIgnoreCase);

    public static bool TryParse(string[] args, out string? imagePath, out string? error)
    {
        imagePath = null;
        error = null;
        if (!IsFaceCommand(args))
            return false;
        if (args.Length != 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            error = "Usage: Images.exe --face-detect <imagePath>";
            return true;
        }

        imagePath = args[1];
        return true;
    }

    public static int Run(string[] args)
    {
        CliReport.TryAttachConsole();
        try
        {
            if (!TryParse(args, out var imagePath, out var error) || imagePath is null)
            {
                Console.Error.WriteLine(error ?? "Unknown face command.");
                return 64;
            }
            return Execute(imagePath, Console.Out, Console.Error);
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
        Func<string, FaceDetectionResult>? detector = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        detector ??= path => FaceDetectionService.Detect(path);
        var result = detector(imagePath);
        if (!result.Success)
        {
            error.WriteLine(result.ErrorMessage);
            return result.Status == FaceDetectionStatus.ModelUnavailable ? 2 : 1;
        }

        var payload = new
        {
            result.SourcePath,
            result.ImageWidth,
            result.ImageHeight,
            result.Runtime,
            FaceCount = result.Faces.Count,
            Faces = result.Faces.Select(face => new
            {
                face.Confidence,
                Bounds = new { face.X, face.Y, face.Width, face.Height },
                MwgArea = new
                {
                    X = face.NormalizedCenterX(result.ImageWidth),
                    Y = face.NormalizedCenterY(result.ImageHeight),
                    W = face.NormalizedWidth(result.ImageWidth),
                    H = face.NormalizedHeight(result.ImageHeight),
                    Unit = "normalized",
                },
                face.Landmarks,
            }),
        };
        output.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
        error.WriteLine($"Detected {result.Faces.Count.ToString(CultureInfo.InvariantCulture)} face(s). No files were modified.");
        return 0;
    }
}
