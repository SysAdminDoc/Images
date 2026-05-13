using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public static class ShellChangeNotificationService
{
    private const uint ShcneAttributes = 0x00000800;
    private const uint ShcneUpdateDir = 0x00001000;
    private const uint ShcneUpdateItem = 0x00002000;
    private const uint ShcnfPathW = 0x0005;

    private static readonly ILogger Log = Images.Services.Log.Get(nameof(ShellChangeNotificationService));

    public static void NotifyFileUpdated(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            SHChangeNotify(ShcneUpdateItem, ShcnfPathW, fullPath, null);
            SHChangeNotify(ShcneAttributes, ShcnfPathW, fullPath, null);

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
                SHChangeNotify(ShcneUpdateDir, ShcnfPathW, directory, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or NotSupportedException or DllNotFoundException or EntryPointNotFoundException)
        {
            Log.LogDebug(ex, "Could not notify the shell about updated image {Path}", path);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, string? dwItem1, string? dwItem2);
}
