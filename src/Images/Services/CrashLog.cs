using System.IO;
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
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(localAppData, "Images");
            Directory.CreateDirectory(dir);
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
                File.AppendAllText(LogPath, sb.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Intentionally swallow — we're already in a fatal-error handler; can't surface.
        }
    }
}
