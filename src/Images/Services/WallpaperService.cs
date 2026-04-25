using System.IO;
using System.Runtime.InteropServices;

namespace Images.Services;

/// <summary>
/// Sets the Windows desktop wallpaper via <c>SystemParametersInfo(SPI_SETDESKWALLPAPER)</c>.
/// Copies the source image to <c>%LOCALAPPDATA%\Images\wallpaper\<hash>.<ext></c> so a later
/// rename / delete of the original doesn't break the desktop. No registry writes beyond
/// what <c>SPIF_UPDATEINIFILE</c> does internally.
/// </summary>
public static class WallpaperService
{
    private const int SPI_SETDESKWALLPAPER = 0x0014;
    private const int SPIF_UPDATEINIFILE  = 0x01;
    private const int SPIF_SENDWININICHANGE = 0x02;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        int uAction, int uParam, string lpvParam, int fuWinIni);

    /// <summary>
    /// Sets <paramref name="imagePath"/> as the current wallpaper. Returns the path actually
    /// handed to the OS (the copied-to-LocalAppData version) on success. Throws
    /// <see cref="System.ComponentModel.Win32Exception"/> on SPI failure.
    /// </summary>
    public static string SetFromFile(string imagePath)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Image file not found.", imagePath);

        // Copy to a stable location so the wallpaper doesn't break if the user later renames
        // or deletes the source file from their folder.
        var wallpaperDir = AppStorage.TryGetAppDirectory("wallpaper")
            ?? throw new InvalidOperationException("Could not create a stable wallpaper folder.");

        var ext = Path.GetExtension(imagePath);
        // Single wallpaper slot — overwrite any prior one. "current" is enough of a handle for
        // Windows; we don't need to retain a history here.
        var stableTarget = Path.Combine(wallpaperDir, "current" + ext);
        File.Copy(imagePath, stableTarget, overwrite: true);

        var ok = SystemParametersInfo(
            SPI_SETDESKWALLPAPER, 0, stableTarget,
            SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);

        if (!ok)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        return stableTarget;
    }
}
