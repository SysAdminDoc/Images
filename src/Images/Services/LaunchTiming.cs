using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public sealed record LaunchTimingSnapshot(
    string Milestone,
    double ProcessElapsedMs,
    double AppElapsedMs,
    string? Detail);

/// <summary>
/// Lightweight launch timing milestones for normal and peek-mode startup. Logs only local timing
/// data; no filenames are uploaded or sent anywhere.
/// </summary>
public static class LaunchTiming
{
    private static readonly DateTimeOffset ProcessStartedAt = ResolveProcessStartTime();
    private static readonly Stopwatch AppStopwatch = Stopwatch.StartNew();
    private static int _firstImageLogged;

    public static LaunchTimingSnapshot Mark(string milestone, string? detail = null)
        => CreateSnapshot(milestone, ProcessStartedAt, AppStopwatch.Elapsed, DateTimeOffset.UtcNow, detail);

    public static void Log(ILogger logger, string milestone, string? detail = null)
    {
        var snapshot = Mark(milestone, detail);
        logger.LogInformation(
            "Startup timing: {Milestone} at {ProcessElapsedMs:0.0} ms since process start, {AppElapsedMs:0.0} ms since app startup. {Detail}",
            snapshot.Milestone,
            snapshot.ProcessElapsedMs,
            snapshot.AppElapsedMs,
            snapshot.Detail ?? "");
    }

    public static void LogFirstImage(ILogger logger, string path, bool isPeekMode)
    {
        if (Interlocked.Exchange(ref _firstImageLogged, 1) != 0)
            return;

        var fileName = Path.GetFileName(path);
        Log(logger, isPeekMode ? "peek-first-image-displayed" : "first-image-displayed", fileName);
    }

    internal static LaunchTimingSnapshot CreateSnapshot(
        string milestone,
        DateTimeOffset processStartedAt,
        TimeSpan appElapsed,
        DateTimeOffset now,
        string? detail)
    {
        var processElapsed = now - processStartedAt;
        if (processElapsed < TimeSpan.Zero)
            processElapsed = TimeSpan.Zero;

        if (appElapsed < TimeSpan.Zero)
            appElapsed = TimeSpan.Zero;

        return new LaunchTimingSnapshot(
            milestone,
            processElapsed.TotalMilliseconds,
            appElapsed.TotalMilliseconds,
            string.IsNullOrWhiteSpace(detail) ? null : detail);
    }

    private static DateTimeOffset ResolveProcessStartTime()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
        }
        catch
        {
            return DateTimeOffset.UtcNow;
        }
    }
}
