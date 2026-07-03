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
    public static void SaveAtomically(XDocument document, string sidecarPath, SaveOptions options = SaveOptions.None)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(sidecarPath);

        var directory = Path.GetDirectoryName(sidecarPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = sidecarPath + ".images-tmp";
        try
        {
            document.Save(tempPath, options);

            if (File.Exists(sidecarPath))
                File.Replace(tempPath, sidecarPath, null);
            else
                File.Move(tempPath, sidecarPath);
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            throw;
        }
    }
}
