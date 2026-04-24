using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Images.Services;

/// <summary>
/// App-wide version / build / environment metadata, read once and cached. Used by the About
/// dialog (V15-06) and the crash log (V15-09) so both surfaces agree on what "this build" is.
/// </summary>
public static class AppInfo
{
    private static readonly Lazy<AppInfoSnapshot> _snapshot = new(Build);

    public static AppInfoSnapshot Current => _snapshot.Value;

    private static AppInfoSnapshot Build()
    {
        var asm = Assembly.GetExecutingAssembly();
        var asmPath = asm.Location;

        // ProductVersion includes the `+<commit sha>` suffix when SourceLink is active (i.e.
        // the build pipeline injected it). FileVersion is the plain 4-digit assembly version.
        string productVersion;
        string fileVersion;
        try
        {
            var info = FileVersionInfo.GetVersionInfo(asmPath);
            productVersion = info.ProductVersion ?? asm.GetName().Version?.ToString() ?? "0.0.0";
            fileVersion = info.FileVersion ?? asm.GetName().Version?.ToString() ?? "0.0.0";
        }
        catch
        {
            productVersion = asm.GetName().Version?.ToString() ?? "0.0.0";
            fileVersion = productVersion;
        }

        // "0.1.5" — drop the trailing ".0" revision if the assembly's revision field is zero
        // (always is for us, since FileVersion is 0.1.5.0 with the 4th slot unused).
        var displayVersion = fileVersion;
        if (displayVersion.EndsWith(".0", StringComparison.Ordinal))
            displayVersion = displayVersion[..^2];

        var runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        var os = System.Runtime.InteropServices.RuntimeInformation.OSDescription;

        return new AppInfoSnapshot(
            DisplayVersion: displayVersion,
            ProductVersion: productVersion,
            FileVersion: fileVersion,
            RuntimeDescription: runtime,
            OsDescription: os,
            BinaryPath: asmPath);
    }
}

public sealed record AppInfoSnapshot(
    string DisplayVersion,
    string ProductVersion,
    string FileVersion,
    string RuntimeDescription,
    string OsDescription,
    string BinaryPath);
