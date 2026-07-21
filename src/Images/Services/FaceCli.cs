using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;

namespace Images.Services;

public static class FaceCli
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static bool IsFaceCommand(string[] args) =>
        args.Length > 0 &&
        (string.Equals(args[0], "--face-detect", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(args[0], "--face-cluster", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(args[0], "--face-xmp", StringComparison.OrdinalIgnoreCase));

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
        using var cancellation = CliCancellation.OnCtrlC();
        try
        {
            if (args.Length > 0 && string.Equals(args[0], "--face-cluster", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3)
                {
                    Console.Error.WriteLine("Usage: Images.exe --face-cluster <imagePath> <imagePath> [...]");
                    return 64;
                }
                try
                {
                    return ExecuteCluster(args.Skip(1).ToArray(), Console.Out, Console.Error, cancellationToken: cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.Error.WriteLine("Face clustering was canceled. No files were modified.");
                    return 130;
                }
            }

            if (args.Length > 0 && string.Equals(args[0], "--face-xmp", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length != 2 || string.IsNullOrWhiteSpace(args[1]))
                {
                    Console.Error.WriteLine("Usage: Images.exe --face-xmp <imagePath>");
                    return 64;
                }
                return ExecuteXmp(args[1], Console.Out, Console.Error);
            }

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

    public static int ExecuteXmp(
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

        output.WriteLine(FaceMwgRegionService.BuildDraft(result).ToString(SaveOptions.DisableFormatting));
        error.WriteLine($"Emitted an MWG-rs XMP draft with {result.Faces.Count.ToString(CultureInfo.InvariantCulture)} unassigned face region(s). No files were modified.");
        return 0;
    }

    public static int ExecuteCluster(
        IReadOnlyList<string> imagePaths,
        TextWriter output,
        TextWriter error,
        Func<string, FaceRecognitionResult>? analyzer = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imagePaths);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (imagePaths.Count < 2)
        {
            error.WriteLine("At least two image paths are required for face clustering.");
            return 64;
        }

        // Production path reuses one detection + one recognition session across the batch and
        // honors cancellation; tests inject a per-path analyzer to exercise the aggregation logic.
        var analyses = analyzer is null
            ? FaceRecognitionService.AnalyzeMany(imagePaths, cancellationToken: cancellationToken)
            : imagePaths.Select(path =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return analyzer(path);
            }).ToArray();
        var successful = analyses.Where(result => result.Success).ToArray();
        if (successful.Length == 0)
        {
            foreach (var failure in analyses)
                error.WriteLine(failure.ErrorMessage);
            return 1;
        }

        var clustered = FaceClusterService.Cluster(successful.SelectMany(result => result.Faces));
        var payload = new
        {
            SimilarityThreshold = FaceClusterService.DefaultSimilarityThreshold,
            Clusters = clustered.Clusters.Select(cluster => new
            {
                cluster.ClusterId,
                Members = cluster.Members.Select(member => new
                {
                    member.SourcePath,
                    member.FaceIndex,
                    member.Detection.Confidence,
                    Bounds = new
                    {
                        member.Detection.X,
                        member.Detection.Y,
                        member.Detection.Width,
                        member.Detection.Height,
                    },
                }),
            }),
            RejectedFaces = clustered.RejectedFaces.Select(face => new
            {
                face.SourcePath,
                face.FaceIndex,
                Quality = face.Quality.ToString(),
                face.RejectionReason,
            }),
            Errors = analyses.Where(result => !result.Success).Select(result => new
            {
                result.SourcePath,
                result.ErrorMessage,
            }),
        };
        output.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
        error.WriteLine($"Grouped {clustered.Clusters.Sum(cluster => cluster.Members.Count).ToString(CultureInfo.InvariantCulture)} accepted face(s) into {clustered.Clusters.Count.ToString(CultureInfo.InvariantCulture)} local cluster(s); rejected {clustered.RejectedFaces.Count.ToString(CultureInfo.InvariantCulture)}. No files were modified.");
        return 0;
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
