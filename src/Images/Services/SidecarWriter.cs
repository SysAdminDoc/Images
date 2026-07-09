using System.IO;
using System.Xml.Linq;

namespace Images.Services;

/// <summary>
/// Atomic XMP sidecar writes. Every sidecar writer loads an existing sidecar
/// and rewrites it (Picasa import merges into digiKam/Lightroom-authored
/// packets), so a crash mid-write must never truncate the user's original.
/// Writes go to a sibling temp file, then swap into place.
/// </summary>
internal static class SidecarWriter
{
    private static readonly object SwapGate = new();

    public static void SaveAtomically(XDocument document, string sidecarPath, SaveOptions options = SaveOptions.None)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(sidecarPath);

        var directory = Path.GetDirectoryName(sidecarPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = sidecarPath + $".{Guid.NewGuid():N}.images-tmp";
        try
        {
            document.Save(tempPath, options);

            lock (SwapGate)
            {
                if (File.Exists(sidecarPath))
                    File.Replace(tempPath, sidecarPath, null);
                else
                    File.Move(tempPath, sidecarPath);
            }
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup only. The original sidecar remains intact.
        }
    }
}
