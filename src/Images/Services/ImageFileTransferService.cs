using System.IO;
using System.Security;

namespace Images.Services;

public enum ImageFileTransferMode
{
    Copy,
    Move
}

public enum ImageFileTransferStatus
{
    Succeeded,
    SourceMissing,
    DestinationMissing,
    UnsupportedSource,
    AlreadyInDestination,
    Failed
}

public sealed record ImageFileTransferResult(
    ImageFileTransferStatus Status,
    ImageFileTransferMode Mode,
    string SourcePath,
    string DestinationPath,
    IReadOnlyList<string> SidecarDestinationPaths,
    string Message)
{
    public bool Success => Status == ImageFileTransferStatus.Succeeded;

    public static ImageFileTransferResult SourceMissing(ImageFileTransferMode mode, string sourcePath)
        => new(ImageFileTransferStatus.SourceMissing, mode, sourcePath, "", [], "Source file no longer exists.");

    public static ImageFileTransferResult DestinationMissing(ImageFileTransferMode mode, string sourcePath, string destinationFolder)
        => new(ImageFileTransferStatus.DestinationMissing, mode, sourcePath, destinationFolder, [], "Destination folder no longer exists.");

    public static ImageFileTransferResult UnsupportedSource(ImageFileTransferMode mode, string sourcePath)
        => new(ImageFileTransferStatus.UnsupportedSource, mode, sourcePath, "", [], "File type is not supported by Images.");

    public static ImageFileTransferResult AlreadyInDestination(ImageFileTransferMode mode, string sourcePath, string destinationFolder)
        => new(ImageFileTransferStatus.AlreadyInDestination, mode, sourcePath, destinationFolder, [], "Image is already in that folder.");

    public static ImageFileTransferResult Failed(ImageFileTransferMode mode, string sourcePath, string destinationPath, string message)
        => new(ImageFileTransferStatus.Failed, mode, sourcePath, destinationPath, [], message);

    public static ImageFileTransferResult SucceededResult(
        ImageFileTransferMode mode,
        string sourcePath,
        string destinationPath,
        IReadOnlyList<string> sidecarDestinationPaths)
        => new(ImageFileTransferStatus.Succeeded, mode, sourcePath, destinationPath, sidecarDestinationPaths, "");
}

public sealed class ImageFileTransferService
{
    public ImageFileTransferResult Transfer(string sourcePath, string destinationFolder, ImageFileTransferMode mode)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return ImageFileTransferResult.SourceMissing(mode, sourcePath);

        string normalizedSource;
        string normalizedDestinationFolder;
        try
        {
            normalizedSource = Path.GetFullPath(sourcePath);
            normalizedDestinationFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destinationFolder));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or SecurityException)
        {
            return ImageFileTransferResult.Failed(mode, sourcePath, destinationFolder, ex.Message);
        }

        if (!SupportedImageFormats.IsSupported(normalizedSource))
            return ImageFileTransferResult.UnsupportedSource(mode, normalizedSource);

        if (!Directory.Exists(normalizedDestinationFolder))
            return ImageFileTransferResult.DestinationMissing(mode, normalizedSource, normalizedDestinationFolder);

        var sourceFolder = Path.GetDirectoryName(normalizedSource);
        if (mode == ImageFileTransferMode.Move &&
            !string.IsNullOrWhiteSpace(sourceFolder) &&
            SamePath(sourceFolder, normalizedDestinationFolder))
        {
            return ImageFileTransferResult.AlreadyInDestination(mode, normalizedSource, normalizedDestinationFolder);
        }

        var sidecars = FindExistingSidecars(normalizedSource);
        var destinationPath = ResolveUniqueDestination(normalizedSource, normalizedDestinationFolder, sidecars);
        var sidecarTransfers = sidecars
            .Select(sidecar => new FileTransferPlan(sidecar.SourcePath, TargetSidecarPath(sidecar, destinationPath)))
            .ToList();

        try
        {
            if (mode == ImageFileTransferMode.Copy)
                CopyFiles(normalizedSource, destinationPath, sidecarTransfers);
            else
                MoveFiles(normalizedSource, destinationPath, sidecarTransfers);

            return ImageFileTransferResult.SucceededResult(
                mode,
                normalizedSource,
                destinationPath,
                sidecarTransfers.Select(t => t.DestinationPath).ToList());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException or InvalidOperationException)
        {
            return ImageFileTransferResult.Failed(mode, normalizedSource, destinationPath, ex.Message);
        }
    }

    private static void CopyFiles(string sourcePath, string destinationPath, IReadOnlyList<FileTransferPlan> sidecarTransfers)
    {
        var copied = new List<string>();
        try
        {
            CopyOne(sourcePath, destinationPath);
            copied.Add(destinationPath);

            foreach (var sidecar in sidecarTransfers)
            {
                CopyOne(sidecar.SourcePath, sidecar.DestinationPath);
                copied.Add(sidecar.DestinationPath);
            }
        }
        catch
        {
            foreach (var path in copied)
                TryDelete(path);
            throw;
        }
    }

    private static void MoveFiles(string sourcePath, string destinationPath, IReadOnlyList<FileTransferPlan> sidecarTransfers)
    {
        var moved = new List<FileTransferPlan>();
        try
        {
            foreach (var sidecar in sidecarTransfers)
            {
                MoveOne(sidecar.SourcePath, sidecar.DestinationPath);
                moved.Add(sidecar);
            }

            var image = new FileTransferPlan(sourcePath, destinationPath);
            MoveOne(image.SourcePath, image.DestinationPath);
            moved.Add(image);
        }
        catch
        {
            for (var i = moved.Count - 1; i >= 0; i--)
                TryMoveBack(moved[i]);
            throw;
        }
    }

    private static void CopyOne(string sourcePath, string destinationPath)
    {
        EnsureDestinationParent(destinationPath);
        File.Copy(sourcePath, destinationPath, overwrite: false);
    }

    private static void MoveOne(string sourcePath, string destinationPath)
    {
        EnsureDestinationParent(destinationPath);
        File.Move(sourcePath, destinationPath, overwrite: false);
    }

    private static void EnsureDestinationParent(string destinationPath)
    {
        var folder = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrWhiteSpace(folder))
            throw new IOException("Transfer destination has no folder.");
        Directory.CreateDirectory(folder);
    }

    private static string ResolveUniqueDestination(
        string sourcePath,
        string destinationFolder,
        IReadOnlyList<ExistingSidecar> sidecars)
    {
        var stem = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = Path.GetExtension(sourcePath);

        for (var counter = 0; counter < 1000; counter++)
        {
            var candidateStem = counter == 0 ? stem : $"{stem} ({counter + 1})";
            var candidate = Path.Combine(destinationFolder, candidateStem + extension);
            if (!DestinationExists(candidate) &&
                sidecars.All(sidecar => !DestinationExists(TargetSidecarPath(sidecar, candidate))))
            {
                return candidate;
            }
        }

        throw new IOException("Could not find an available destination filename.");
    }

    private static bool DestinationExists(string path)
        => File.Exists(path) || Directory.Exists(path);

    private static IReadOnlyList<ExistingSidecar> FindExistingSidecars(string sourcePath)
    {
        var result = new List<ExistingSidecar>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddIfExists(new ExistingSidecar(sourcePath + ".xmp", SidecarKind.FullName));

        var directory = Path.GetDirectoryName(sourcePath);
        var stem = Path.GetFileNameWithoutExtension(sourcePath);
        if (!string.IsNullOrWhiteSpace(directory) && !string.IsNullOrWhiteSpace(stem))
            AddIfExists(new ExistingSidecar(Path.Combine(directory, stem + ".xmp"), SidecarKind.Stem));

        return result;

        void AddIfExists(ExistingSidecar sidecar)
        {
            if (!seen.Add(sidecar.SourcePath)) return;
            if (File.Exists(sidecar.SourcePath))
                result.Add(sidecar);
        }
    }

    private static string TargetSidecarPath(ExistingSidecar sidecar, string destinationPath)
    {
        if (sidecar.Kind == SidecarKind.FullName)
            return destinationPath + ".xmp";

        var directory = Path.GetDirectoryName(destinationPath);
        var stem = Path.GetFileNameWithoutExtension(destinationPath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(stem))
            throw new IOException("Sidecar destination has no folder.");

        return Path.Combine(directory, stem + ".xmp");
    }

    private static bool SamePath(string left, string right)
    {
        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort rollback cleanup.
        }
    }

    private static void TryMoveBack(FileTransferPlan moved)
    {
        try
        {
            if (File.Exists(moved.DestinationPath) && !File.Exists(moved.SourcePath))
                File.Move(moved.DestinationPath, moved.SourcePath);
        }
        catch
        {
            // Best-effort rollback; the UI reports the original transfer failure.
        }
    }

    private sealed record ExistingSidecar(string SourcePath, SidecarKind Kind);

    private sealed record FileTransferPlan(string SourcePath, string DestinationPath);

    private enum SidecarKind
    {
        FullName,
        Stem
    }
}
