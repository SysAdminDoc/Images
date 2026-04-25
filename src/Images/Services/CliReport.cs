using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Images.Services;

/// <summary>
/// V20-37: <c>--system-info</c> and <c>--codec-report</c> CLI modes. Both write a structured
/// report to stdout and exit, without ever showing a window. WPF apps default to detached
/// console handles; we attach to the parent terminal (or allocate a fresh one when launched
/// from Explorer) so the output actually lands somewhere a user can read it.
///
/// The reports lift their content from <see cref="CodecCapabilityService"/> and
/// <see cref="CodecRuntime"/> so the CLI surface and the About dialog can never disagree
/// about runtime state.
/// </summary>
public static class CliReport
{
    /// <summary>
    /// Inspects argv for a CLI report flag. Returns the matching mode or <c>null</c> if no
    /// CLI report was requested. Recognized exact tokens (case-insensitive):
    /// <c>--system-info</c>, <c>--codec-report</c>, plus <c>--help</c> / <c>-h</c> /
    /// <c>--version</c> as adjacent CLI-mode aliases.
    /// </summary>
    public static CliMode? TryResolveMode(string[] args)
    {
        if (args.Length != 1) return null;
        var token = args[0];
        if (string.Equals(token, "--system-info", StringComparison.OrdinalIgnoreCase)) return CliMode.SystemInfo;
        if (string.Equals(token, "--codec-report", StringComparison.OrdinalIgnoreCase)) return CliMode.CodecReport;
        if (string.Equals(token, "--version", StringComparison.OrdinalIgnoreCase)) return CliMode.Version;
        if (string.Equals(token, "--help", StringComparison.OrdinalIgnoreCase)) return CliMode.Help;
        if (string.Equals(token, "-h", StringComparison.OrdinalIgnoreCase)) return CliMode.Help;
        if (string.Equals(token, "/?", StringComparison.Ordinal)) return CliMode.Help;
        return null;
    }

    /// <summary>
    /// Runs the requested CLI report and returns the process exit code. The caller is
    /// expected to terminate immediately after this returns — no UI has been shown yet.
    /// </summary>
    public static int Run(CliMode mode)
    {
        TryAttachConsole();
        try
        {
            switch (mode)
            {
                case CliMode.SystemInfo:
                    Console.Out.Write(BuildSystemInfo());
                    return 0;
                case CliMode.CodecReport:
                    Console.Out.Write(CodecCapabilityService.BuildClipboardReport());
                    return 0;
                case CliMode.Version:
                    Console.Out.WriteLine($"Images {AppInfo.Current.DisplayVersion}");
                    return 0;
                case CliMode.Help:
                    Console.Out.Write(BuildHelpText());
                    return 0;
                default:
                    Console.Error.WriteLine("Unknown CLI mode.");
                    return 64;
            }
        }
        catch (Exception ex)
        {
            try { Console.Error.WriteLine($"Images CLI error: {ex.Message}"); } catch { }
            return 1;
        }
        finally
        {
            try { Console.Out.Flush(); } catch { }
            try { Console.Error.Flush(); } catch { }
        }
    }

    /// <summary>
    /// Builds the <c>--system-info</c> report. Includes app version, build, .NET runtime, OS
    /// + architecture, decoder runtime provenance (Magick.NET version + assembly path,
    /// Ghostscript path/version/source/SHA-256), and the writable storage paths Images
    /// uses at runtime so support requests can pinpoint where logs / settings / caches live.
    /// </summary>
    public static string BuildSystemInfo()
    {
        var info = AppInfo.Current;
        var provenance = CodecCapabilityService.BuildProvenance();
        var sb = new StringBuilder();

        sb.AppendLine($"Images {info.DisplayVersion} system information");
        sb.AppendLine();

        sb.AppendLine("Application");
        sb.AppendLine($"- Version:           {info.DisplayVersion}");
        sb.AppendLine($"- Product version:   {info.ProductVersion}");
        sb.AppendLine($"- File version:      {info.FileVersion}");
        sb.AppendLine($"- Binary path:       {info.BinaryPath}");
        sb.AppendLine($"- App directory:     {provenance.AppDirectory}");
        sb.AppendLine();

        sb.AppendLine("Environment");
        sb.AppendLine($"- .NET runtime:      {provenance.Runtime}");
        sb.AppendLine($"- Operating system:  {provenance.OperatingSystem}");
        sb.AppendLine($"- Process arch:      {provenance.ProcessArchitecture}");
        sb.AppendLine($"- OS arch:           {RuntimeInformation.OSArchitecture}");
        sb.AppendLine($"- Processor count:   {Environment.ProcessorCount}");
        sb.AppendLine($"- Working set:       {FormatMegabytes(Environment.WorkingSet)}");
        sb.AppendLine($"- 64-bit process:    {Environment.Is64BitProcess}");
        sb.AppendLine();

        sb.AppendLine("Decoder runtime");
        sb.AppendLine($"- Magick.NET:        {provenance.MagickVersion}");
        if (provenance.MagickAssemblyPath is not null)
            sb.AppendLine($"- Magick.NET path:   {provenance.MagickAssemblyPath}");
        sb.AppendLine($"- Ghostscript:       {(provenance.GhostscriptAvailable ? "available" : "not available")}");
        sb.AppendLine($"- Ghostscript src:   {provenance.GhostscriptDirectory ?? provenance.GhostscriptSource}");
        if (provenance.GhostscriptVersion is not null)
            sb.AppendLine($"- Ghostscript ver:   {provenance.GhostscriptVersion}");
        if (provenance.GhostscriptDllPath is not null)
            sb.AppendLine($"- Ghostscript DLL:   {provenance.GhostscriptDllPath}");
        if (provenance.GhostscriptDllSha256 is not null)
            sb.AppendLine($"- Ghostscript hash:  sha256:{provenance.GhostscriptDllSha256}");
        sb.AppendLine();

        sb.AppendLine("Format coverage");
        sb.AppendLine($"- Open extensions:   {SupportedImageFormats.Extensions.Count}");
        sb.AppendLine($"- Export extensions: {ImageExportService.ExportExtensions.Length}");
        sb.AppendLine();

        sb.AppendLine("Writable storage paths");
        AppendPath(sb, "App data root", AppStorage.TryGetAppDirectory());
        AppendPath(sb, "Logs",          AppStorage.TryGetAppDirectory("Logs"));
        AppendPath(sb, "Thumbnails",    AppStorage.TryGetAppDirectory("thumbs"));
        AppendPath(sb, "Wallpaper",     AppStorage.TryGetAppDirectory("wallpaper"));
        AppendPath(sb, "Crash log",     CrashLog.LogPath);
        sb.AppendLine();

        sb.AppendLine("More");
        sb.AppendLine("- Run `Images.exe --codec-report` for the per-format capability matrix.");
        sb.AppendLine("- Open About in the app for the same information with copy-to-clipboard.");
        return sb.ToString();
    }

    public static string BuildHelpText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Images {AppInfo.Current.DisplayVersion} — Windows image viewer");
        sb.AppendLine();
        sb.AppendLine("Usage:");
        sb.AppendLine("  Images.exe [<path>]");
        sb.AppendLine("    Open the file at <path>. With no argument, launches the empty viewer.");
        sb.AppendLine();
        sb.AppendLine("  Images.exe --peek <path>");
        sb.AppendLine("    Chromeless, topmost, maximized preview of <path>. Esc closes.");
        sb.AppendLine();
        sb.AppendLine("  Images.exe --system-info");
        sb.AppendLine("    Print runtime, OS, decoder, and storage-path information to stdout.");
        sb.AppendLine();
        sb.AppendLine("  Images.exe --codec-report");
        sb.AppendLine("    Print the per-format capability matrix and supported-extension list.");
        sb.AppendLine();
        sb.AppendLine("  Images.exe --version");
        sb.AppendLine("    Print the version and exit.");
        sb.AppendLine();
        sb.AppendLine("  Images.exe --help");
        sb.AppendLine("    Print this help and exit.");
        return sb.ToString();
    }

    private static void AppendPath(StringBuilder sb, string label, string? path)
    {
        var padded = (label + ":").PadRight(20);
        sb.AppendLine($"- {padded}{path ?? "(unavailable)"}");
    }

    private static string FormatMegabytes(long bytes)
    {
        var mb = bytes / (1024.0 * 1024.0);
        return $"{mb:0.0} MiB ({bytes:N0} bytes)";
    }

    /// <summary>
    /// Best-effort console attach. Tries to inherit the parent terminal's stdout/stderr so
    /// `Images.exe --system-info` prints into the launching shell. When launched from
    /// Explorer (no parent console) the call returns false and we silently let stdout fall
    /// through to whatever WPF gave us — there's no terminal to render into anyway, so the
    /// user is expected to redirect (`> info.txt`) or pipe.
    /// </summary>
    private static bool TryAttachConsole()
    {
        try
        {
            if (!OperatingSystem.IsWindows()) return false;
            if (!AttachConsole(ATTACH_PARENT_PROCESS)) return false;

            // Re-bind Console.Out / Console.Error to the freshly attached console so the next
            // WriteLine call reaches the parent terminal instead of the detached default
            // streams the WPF host wired up at startup.
            var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            var stderr = new StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
            Console.SetOut(stdout);
            Console.SetError(stderr);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFFu;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint dwProcessId);
}

public enum CliMode
{
    SystemInfo,
    CodecReport,
    Version,
    Help,
}
