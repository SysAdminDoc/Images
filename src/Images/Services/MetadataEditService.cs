using System.IO;
using ImageMagick;

namespace Images.Services;

/// <summary>
/// In-place metadata editing operations that preserve pixel data.
/// Writes are atomic: a temp file is produced alongside the source and
/// then swapped on success, so a crash mid-write leaves the original
/// file untouched.
/// </summary>
public static class MetadataEditService
{
    /// <summary>
    /// Removes all GPS EXIF values from <paramref name="path"/> and
    /// writes the result back in-place.  Returns the number of GPS
    /// fields that were removed; returns 0 if no GPS data was present.
    /// </summary>
    public static int StripGpsMetadata(string path)
    {
        using var image = new MagickImage(path);

        var exif = image.GetExifProfile();
        if (exif is null) return 0;

        var gpsTags = exif.Values
            .Where(v => v.Tag.ToString().StartsWith("Gps", StringComparison.OrdinalIgnoreCase))
            .Select(v => v.Tag)
            .ToList();

        if (gpsTags.Count == 0) return 0;

        foreach (var tag in gpsTags)
            exif.RemoveValue(tag);

        image.SetProfile(exif);

        // Write to a sibling temp file then atomically replace — crash-safe.
        var dir = Path.GetDirectoryName(path) ?? Path.GetTempPath();
        var tmp = Path.Combine(dir, $".images-{Guid.NewGuid():N}{Path.GetExtension(path)}.tmp");
        try
        {
            image.Write(tmp);
            File.Replace(tmp, path, null);
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best-effort cleanup */ }
            throw;
        }

        return gpsTags.Count;
    }
}
