using System.Collections.Concurrent;
using System.Diagnostics;

namespace Images.Services;

public enum BackgroundJobState
{
    Running,
    Completed,
    Faulted,
    Canceled
}

public sealed record BackgroundJobEntry(
    string Id,
    string Name,
    BackgroundJobState State,
    DateTimeOffset StartedUtc,
    DateTimeOffset? CompletedUtc,
    string? ErrorMessage,
    int AffectedCount)
{
    public TimeSpan Duration => (CompletedUtc ?? DateTimeOffset.UtcNow) - StartedUtc;

    public string DurationText
    {
        get
        {
            var d = Duration;
            return d.TotalSeconds < 1 ? "<1s"
                 : d.TotalSeconds < 60 ? $"{d.TotalSeconds:F0}s"
                 : d.TotalMinutes < 60 ? $"{d.TotalMinutes:F0}m {d.Seconds}s"
                 : $"{d.TotalHours:F0}h {d.Minutes}m";
        }
    }

    public string StateText => State switch
    {
        BackgroundJobState.Running => "Running",
        BackgroundJobState.Completed => AffectedCount > 0 ? $"Done ({AffectedCount})" : "Done",
        BackgroundJobState.Faulted => "Failed",
        BackgroundJobState.Canceled => "Canceled",
        _ => "Unknown"
    };
}

public static class BackgroundJobsService
{
    private static readonly ConcurrentQueue<BackgroundJobEntry> _history = new();
    private static readonly ConcurrentDictionary<string, BackgroundJobEntry> _running = new(StringComparer.Ordinal);
    private const int MaxHistory = 50;
    private static int _nextId;

    public static event EventHandler? Changed;

    public static string StartJob(string name)
    {
        var id = $"job-{Interlocked.Increment(ref _nextId)}";
        var entry = new BackgroundJobEntry(id, name, BackgroundJobState.Running, DateTimeOffset.UtcNow, null, null, 0);
        _running[id] = entry;
        Changed?.Invoke(null, EventArgs.Empty);
        return id;
    }

    public static void CompleteJob(string id, int affectedCount = 0)
    {
        if (!_running.TryRemove(id, out var entry)) return;
        var completed = entry with { State = BackgroundJobState.Completed, CompletedUtc = DateTimeOffset.UtcNow, AffectedCount = affectedCount };
        Enqueue(completed);
    }

    public static void FailJob(string id, string error)
    {
        if (!_running.TryRemove(id, out var entry)) return;
        var faulted = entry with { State = BackgroundJobState.Faulted, CompletedUtc = DateTimeOffset.UtcNow, ErrorMessage = error };
        Enqueue(faulted);
    }

    public static void CancelJob(string id)
    {
        if (!_running.TryRemove(id, out var entry)) return;
        var canceled = entry with { State = BackgroundJobState.Canceled, CompletedUtc = DateTimeOffset.UtcNow };
        Enqueue(canceled);
    }

    public static IReadOnlyList<BackgroundJobEntry> GetRunning()
        => _running.Values.OrderByDescending(j => j.StartedUtc).ToList();

    public static IReadOnlyList<BackgroundJobEntry> GetRecent(int max = 20)
        => _history.OrderByDescending(j => j.CompletedUtc ?? j.StartedUtc).Take(max).ToList();

    public static IReadOnlyList<BackgroundJobEntry> GetAll(int max = 30)
    {
        var all = new List<BackgroundJobEntry>(_running.Values);
        all.AddRange(_history);
        return all.OrderByDescending(j => j.CompletedUtc ?? j.StartedUtc).Take(max).ToList();
    }

    public static string BuildSummaryText()
    {
        var running = _running.Count;
        var snap = BackgroundTaskTracker.Snapshot;
        return running > 0
            ? $"{running} running, {snap.Completed} completed, {snap.Faulted} faulted"
            : $"{snap.Completed} completed, {snap.Faulted} faulted this session";
    }

    internal static void ResetForTests()
    {
        while (_history.TryDequeue(out _)) { }
        _running.Clear();
        Interlocked.Exchange(ref _nextId, 0);
    }

    private static void Enqueue(BackgroundJobEntry entry)
    {
        _history.Enqueue(entry);
        while (_history.Count > MaxHistory) _history.TryDequeue(out _);
        Changed?.Invoke(null, EventArgs.Empty);
    }
}
