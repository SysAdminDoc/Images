using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Images.Services;

public sealed record PerfMeasurement(
    string Name,
    double ElapsedMs,
    double? ThresholdMs,
    bool Passed,
    string? Detail);

public static class PerformanceBudgetService
{
    public static string BuildReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Images Performance Report — {AppInfo.Current.DisplayVersion}");
        sb.AppendLine($"Runtime: {AppInfo.Current.RuntimeDescription}");
        sb.AppendLine($"OS: {AppInfo.Current.OsDescription}");
        sb.AppendLine($"Generated: {DateTime.UtcNow:O}");
        sb.AppendLine();

        var measurements = RunMeasurements();
        var maxName = measurements.Max(m => m.Name.Length);

        sb.AppendLine($"{"Measurement".PadRight(maxName)}  {"Time":>10}  {"Budget":>10}  Status");
        sb.AppendLine(new string('-', maxName + 38));

        var passed = 0;
        var warned = 0;
        foreach (var m in measurements)
        {
            var time = $"{m.ElapsedMs:F1} ms";
            var budget = m.ThresholdMs is not null ? $"{m.ThresholdMs:F0} ms" : "—";
            var status = m.ThresholdMs is null ? "info" : m.Passed ? "ok" : "WARN";
            sb.AppendLine($"{m.Name.PadRight(maxName)}  {time,10}  {budget,10}  {status}");
            if (m.ThresholdMs is not null)
            {
                if (m.Passed) passed++;
                else warned++;
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Summary: {passed} passed, {warned} warnings out of {passed + warned} budgeted measurements.");

        if (measurements.Any(m => !string.IsNullOrWhiteSpace(m.Detail)))
        {
            sb.AppendLine();
            sb.AppendLine("Details:");
            foreach (var m in measurements.Where(m => !string.IsNullOrWhiteSpace(m.Detail)))
                sb.AppendLine($"  {m.Name}: {m.Detail}");
        }

        return sb.ToString();
    }

    private static IReadOnlyList<PerfMeasurement> RunMeasurements()
    {
        var results = new List<PerfMeasurement>();

        results.Add(MeasureLaunchTiming());
        results.Add(MeasureDirectoryScan());
        results.Add(MeasureThumbnailCacheHealth());
        results.Add(MeasureSettingsDbAccess());
        results.Add(MeasureMemorySnapshot());

        return results;
    }

    private static PerfMeasurement MeasureLaunchTiming()
    {
        var snapshot = LaunchTiming.Mark("perf-report");
        return new PerfMeasurement(
            "Process-to-CLI",
            snapshot.ProcessElapsedMs,
            ThresholdMs: 1000,
            Passed: snapshot.ProcessElapsedMs < 1000,
            Detail: $"App elapsed: {snapshot.AppElapsedMs:F1} ms; target: sub-second cold start");
    }

    private static PerfMeasurement MeasureDirectoryScan()
    {
        var testDir = AppStorage.TryGetAppDirectory("thumbs");
        if (testDir is null || !Directory.Exists(testDir))
        {
            return new PerfMeasurement(
                "Directory scan (thumbs)",
                0,
                ThresholdMs: null,
                Passed: true,
                Detail: "Thumbs directory not available");
        }

        var sw = Stopwatch.StartNew();
        var count = 0;
        try
        {
            count = Directory.EnumerateFiles(testDir, "*", SearchOption.AllDirectories).Count();
        }
        catch { }
        sw.Stop();

        return new PerfMeasurement(
            "Directory scan (thumbs)",
            sw.Elapsed.TotalMilliseconds,
            ThresholdMs: 5000,
            Passed: sw.Elapsed.TotalMilliseconds < 5000,
            Detail: $"{count} files enumerated");
    }

    private static PerfMeasurement MeasureThumbnailCacheHealth()
    {
        var sw = Stopwatch.StartNew();
        var health = ThumbnailCache.Instance.GetHealth();
        sw.Stop();

        return new PerfMeasurement(
            "Thumbnail cache health",
            sw.Elapsed.TotalMilliseconds,
            ThresholdMs: 500,
            Passed: sw.Elapsed.TotalMilliseconds < 500,
            Detail: $"{health.FileCount} files, {health.Bytes / (1024.0 * 1024.0):F1} MB");
    }

    private static PerfMeasurement MeasureSettingsDbAccess()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            SettingsService.Instance.GetString("perf-probe", null);
        }
        catch { }
        sw.Stop();

        return new PerfMeasurement(
            "Settings DB read",
            sw.Elapsed.TotalMilliseconds,
            ThresholdMs: 100,
            Passed: sw.Elapsed.TotalMilliseconds < 100,
            Detail: null);
    }

    private static PerfMeasurement MeasureMemorySnapshot()
    {
        var process = Process.GetCurrentProcess();
        var workingSetMb = process.WorkingSet64 / (1024.0 * 1024.0);
        var privateMemMb = process.PrivateMemorySize64 / (1024.0 * 1024.0);

        return new PerfMeasurement(
            "Memory (working set)",
            workingSetMb,
            ThresholdMs: null,
            Passed: true,
            Detail: $"Working set: {workingSetMb:F1} MB, Private: {privateMemMb:F1} MB");
    }
}
