using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Images.Services;

/// <summary>
/// V15-09: plain-text crash log at <c>%LOCALAPPDATA%\Images\crash.log</c>. Best-effort — any
/// failure to write is swallowed since we're already in a fatal-error handler and can't surface
/// another failure on top. Precursor to V02-07 (minidump + "Open GitHub Issue" dialog).
/// </summary>
public static class CrashLog
{
    private static readonly object _gate = new();
    private static string? _cachedPath;

    public static string LogPath
    {
        get
        {
            if (_cachedPath is not null) return _cachedPath;
            var dir = AppStorage.TryGetAppDirectory() ?? Path.GetTempPath();
            _cachedPath = Path.Combine(dir, "crash.log");
            return _cachedPath;
        }
    }

    /// <summary>
    /// Appends a crash record to the log. <paramref name="source"/> identifies which handler
    /// caught the exception (AppDomain / Dispatcher / TaskScheduler) so multiple paths don't
    /// silently clobber each other's entries.
    /// </summary>
    public static void Append(string source, Exception? ex, string? extraContext = null)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("================================================================");
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}] {source}");
            var info = AppInfo.Current;
            sb.AppendLine($"Version:  {info.ProductVersion} (file {info.FileVersion})");
            sb.AppendLine($"Runtime:  {info.RuntimeDescription}");
            sb.AppendLine($"OS:       {info.OsDescription}");
            if (!string.IsNullOrEmpty(extraContext))
                sb.AppendLine($"Context:  {extraContext}");

            if (ex is null)
            {
                sb.AppendLine("Exception: (null)");
            }
            else
            {
                // Walk the inner-exception chain — AggregateException unwrap is the common case
                // for TaskScheduler.UnobservedTaskException.
                var depth = 0;
                for (var cur = ex; cur is not null; cur = cur.InnerException, depth++)
                {
                    if (depth == 0) sb.AppendLine($"Exception: {cur.GetType().FullName}: {cur.Message}");
                    else sb.AppendLine($"  Caused by: {cur.GetType().FullName}: {cur.Message}");
                    if (cur.StackTrace is not null) sb.AppendLine(cur.StackTrace);
                }
            }
            sb.AppendLine();

            lock (_gate)
            {
                using var stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(stream, Encoding.UTF8);
                writer.Write(sb.ToString());
            }
        }
        catch
        {
            // Intentionally swallow — we're already in a fatal-error handler; can't surface.
        }
    }

    /// <summary>
    /// V02-07: write a minidump to <c>%LOCALAPPDATA%\Images\Logs\crash-<timestamp>.dmp</c>.
    /// Returns the path on success, null on any failure. Uses
    /// <c>MiniDumpWithDataSegs | MiniDumpWithUnloadedModules</c> — a "just enough to triage"
    /// dump. <c>MiniDumpWithFullMemory</c> would be overkill for a viewer.
    /// </summary>
    public static string? TryWriteMiniDump()
    {
        try
        {
            var dir = AppStorage.TryGetAppDirectory("Logs");
            if (dir is null) return null;

            var dumpPath = Path.Combine(dir, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.dmp");

            using var fs = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.None);
            var proc = System.Diagnostics.Process.GetCurrentProcess();

            // Flags constants pulled from minidumpapiset.h.
            const uint MiniDumpNormal = 0x00000000;
            const uint MiniDumpWithDataSegs = 0x00000001;
            const uint MiniDumpWithUnloadedModules = 0x00000020;
            const uint MiniDumpWithThreadInfo = 0x00001000;
            const uint flags = MiniDumpNormal | MiniDumpWithDataSegs | MiniDumpWithUnloadedModules | MiniDumpWithThreadInfo;

            var ok = MiniDumpWriteDump(
                proc.Handle,
                (uint)proc.Id,
                fs.SafeFileHandle.DangerousGetHandle(),
                flags,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            return ok ? dumpPath : null;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("dbghelp.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MiniDumpWriteDump(
        IntPtr hProcess, uint ProcessId, IntPtr hFile, uint DumpType,
        IntPtr ExceptionParam, IntPtr UserStreamParam, IntPtr CallbackParam);
}
