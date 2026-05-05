using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public readonly record struct BackgroundTaskSnapshot(
    long Started,
    long Running,
    long Completed,
    long Faulted,
    long Canceled);

public static class BackgroundTaskTracker
{
    private static readonly ILogger _log = Log.Get(nameof(BackgroundTaskTracker));
    private static readonly CounterSet _totals = new();
    private static readonly ConcurrentDictionary<string, CounterSet> _byName = new(StringComparer.OrdinalIgnoreCase);

    public static BackgroundTaskSnapshot Snapshot => _totals.Snapshot;

    public static IReadOnlyDictionary<string, BackgroundTaskSnapshot> SnapshotByName
        => _byName.ToDictionary(kv => kv.Key, kv => kv.Value.Snapshot, StringComparer.OrdinalIgnoreCase);

    public static Task Queue(string name, Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Queue(name, () =>
        {
            action();
            return Task.CompletedTask;
        }, cancellationToken);
    }

    public static Task Queue(string name, Func<Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        var taskName = NormalizeName(name);
        var counters = MarkStarted(taskName);

        return Task.Run(async () =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await action().ConfigureAwait(false);
                MarkCompleted(taskName, counters, sw.Elapsed);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                MarkCanceled(taskName, counters, sw.Elapsed);
            }
            catch (Exception ex)
            {
                MarkFaulted(taskName, counters, sw.Elapsed, ex);
            }
            finally
            {
                counters.DecrementRunning();
                _totals.DecrementRunning();
            }
        });
    }

    public static Task<T> Run<T>(string name, Func<T> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        var taskName = NormalizeName(name);
        var counters = MarkStarted(taskName);

        return Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = action();
                MarkCompleted(taskName, counters, sw.Elapsed);
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                MarkCanceled(taskName, counters, sw.Elapsed);
                throw;
            }
            catch (Exception ex)
            {
                MarkFaulted(taskName, counters, sw.Elapsed, ex);
                throw;
            }
            finally
            {
                counters.DecrementRunning();
                _totals.DecrementRunning();
            }
        });
    }

    public static async Task<T> RunAsync<T>(
        string name,
        Func<Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        var taskName = NormalizeName(name);
        var counters = MarkStarted(taskName);
        var sw = Stopwatch.StartNew();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await action().ConfigureAwait(false);
            MarkCompleted(taskName, counters, sw.Elapsed);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            MarkCanceled(taskName, counters, sw.Elapsed);
            throw;
        }
        catch (Exception ex)
        {
            MarkFaulted(taskName, counters, sw.Elapsed, ex);
            throw;
        }
        finally
        {
            counters.DecrementRunning();
            _totals.DecrementRunning();
        }
    }

    internal static void ResetForTests()
    {
        _totals.Reset();
        _byName.Clear();
    }

    private static string NormalizeName(string name)
        => string.IsNullOrWhiteSpace(name) ? "background-task" : name.Trim();

    private static CounterSet MarkStarted(string name)
    {
        var counters = _byName.GetOrAdd(name, _ => new CounterSet());
        counters.MarkStarted();
        _totals.MarkStarted();
        _log.LogDebug("background task started: {Name}", name);
        return counters;
    }

    private static void MarkCompleted(string name, CounterSet counters, TimeSpan elapsed)
    {
        counters.MarkCompleted();
        _totals.MarkCompleted();
        _log.LogDebug("background task completed: {Name} in {ElapsedMs} ms", name, elapsed.TotalMilliseconds);
    }

    private static void MarkCanceled(string name, CounterSet counters, TimeSpan elapsed)
    {
        counters.MarkCanceled();
        _totals.MarkCanceled();
        _log.LogDebug("background task canceled: {Name} after {ElapsedMs} ms", name, elapsed.TotalMilliseconds);
    }

    private static void MarkFaulted(string name, CounterSet counters, TimeSpan elapsed, Exception exception)
    {
        counters.MarkFaulted();
        _totals.MarkFaulted();
        _log.LogWarning(exception, "background task failed: {Name} after {ElapsedMs} ms", name, elapsed.TotalMilliseconds);
    }

    private sealed class CounterSet
    {
        private long _started;
        private long _running;
        private long _completed;
        private long _faulted;
        private long _canceled;

        public BackgroundTaskSnapshot Snapshot => new(
            Interlocked.Read(ref _started),
            Interlocked.Read(ref _running),
            Interlocked.Read(ref _completed),
            Interlocked.Read(ref _faulted),
            Interlocked.Read(ref _canceled));

        public void MarkStarted()
        {
            Interlocked.Increment(ref _started);
            Interlocked.Increment(ref _running);
        }

        public void MarkCompleted() => Interlocked.Increment(ref _completed);

        public void MarkCanceled() => Interlocked.Increment(ref _canceled);

        public void MarkFaulted() => Interlocked.Increment(ref _faulted);

        public void DecrementRunning() => Interlocked.Decrement(ref _running);

        public void Reset()
        {
            Interlocked.Exchange(ref _started, 0);
            Interlocked.Exchange(ref _running, 0);
            Interlocked.Exchange(ref _completed, 0);
            Interlocked.Exchange(ref _faulted, 0);
            Interlocked.Exchange(ref _canceled, 0);
        }
    }
}
