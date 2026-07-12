using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Images.Services;

/// <summary>
/// P-03: records every outbound network call the app makes. Stores entries in-memory
/// (observable for UI binding) and optionally appends each to a JSONL file at
/// <c>%LOCALAPPDATA%\Images\network-egress.jsonl</c>. The charter line is
/// "no competitor ships this" — full network transparency for a desktop image viewer.
/// </summary>
public static class NetworkEgressService
{
    private static readonly ObservableCollection<NetworkEgressEntry> _entries = [];
    private static readonly object _lock = new();
    private static readonly ILogger _log = Log.Get("Images.NetworkEgress");

    /// <summary>All recorded entries, newest first. Observable for WPF binding.</summary>
    public static ReadOnlyObservableCollection<NetworkEgressEntry> Entries { get; } = new(_entries);

    /// <summary>Record an outbound network call.</summary>
    public static void Record(string url, string purpose, long bytes, long durationMs)
        => Record(url, purpose, bytes, durationMs, "outbound");

    private static void InsertEntry(NetworkEgressEntry entry)
    {
        // Insert at 0 so newest is first.
        _entries.Insert(0, entry);

        // The startup load and the JSONL file are capped, but a long-lived
        // listen-mode session records an entry per received path — trim the
        // in-memory list too or it grows for the life of the process.
        while (_entries.Count > MaxLoadedEntries)
            _entries.RemoveAt(_entries.Count - 1);
    }

    public static void RecordInbound(string url, string purpose, long bytes)
    {
        Record(url, purpose, bytes, 0, "inbound");
    }

    public static void Record(string url, string purpose, long bytes, long durationMs,
        string direction = "outbound")
    {
        var entry = new NetworkEgressEntry(
            Url: url ?? "(unknown)",
            Purpose: purpose ?? "(unknown)",
            Bytes: Math.Max(0, bytes),
            DurationMs: Math.Max(0, durationMs),
            Timestamp: DateTimeOffset.UtcNow,
            Direction: direction);

        _log.LogInformation(
            "network-{Direction}: {Purpose} {Url} — {Bytes} bytes in {DurationMs} ms",
            entry.Direction, entry.Purpose, entry.Url, entry.Bytes, entry.DurationMs);

        lock (_lock)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is not null && ShouldUseDispatcher(dispatcher))
            {
                dispatcher.BeginInvoke(() => InsertEntry(entry));
            }
            else
            {
                InsertEntry(entry);
            }
        }

        PersistEntry(entry);
    }

    /// <summary>Clear all in-memory entries. Does not delete the persisted JSONL file.</summary>
    public static void Clear()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && ShouldUseDispatcher(dispatcher))
        {
            dispatcher.BeginInvoke(() => _entries.Clear());
        }
        else
        {
            _entries.Clear();
        }
    }

    /// <summary>Clear in-memory entries and delete the persisted JSONL file.</summary>
    public static void ClearAll()
    {
        Clear();
        try
        {
            var path = GetJsonlPath();
            lock (_persistLock)
            {
                if (path is not null && File.Exists(path))
                    File.Delete(path);
                _persistedLineCountPath = null;
                _persistedLineCount = null;
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "network-egress: could not delete persisted log");
        }
    }

    /// <summary>Total number of bytes transferred across all recorded entries.</summary>
    public static long TotalBytes
    {
        get
        {
            long sum = 0;
            // Snapshot under the mutation lock so a concurrent Record() (reachable in tests or
            // during startup/shutdown when dispatcher marshalling is bypassed) cannot resize the
            // backing store mid-enumeration.
            NetworkEgressEntry[] snapshot;
            lock (_lock) snapshot = _entries.ToArray();
            foreach (var e in snapshot) sum += e.Bytes;
            return sum;
        }
    }

    /// <summary>
    /// Build a plain-text summary suitable for clipboard copy.
    /// </summary>
    public static string BuildClipboardText()
    {
        NetworkEgressEntry[] snapshot;
        lock (_lock) snapshot = _entries.ToArray();
        if (snapshot.Length == 0) return "No network activity recorded.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Images — Network activity log ({snapshot.Length} entries)");
        sb.AppendLine(new string('-', 60));
        foreach (var e in snapshot)
        {
            sb.AppendLine($"[{e.Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}]");
            sb.AppendLine($"  URL:      {e.Url}");
            sb.AppendLine($"  Purpose:  {e.Purpose}");
            sb.AppendLine($"  Bytes:    {e.Bytes}");
            sb.AppendLine($"  Duration: {e.DurationMs} ms");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>
    /// Load previously persisted entries from the JSONL file into the in-memory collection.
    /// Called once at startup so the About panel shows history from prior sessions.
    /// </summary>
    public static void LoadPersistedEntries()
    {
        var path = GetJsonlPath();
        if (path is null || !File.Exists(path)) return;

        try
        {
            var loaded = ReadPersistedEntriesForDisplay(File.ReadLines(path), MaxLoadedEntries).ToList();
            if (loaded.Count == 0) return;

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is not null && ShouldUseDispatcher(dispatcher))
            {
                dispatcher.BeginInvoke(() =>
                {
                    foreach (var entry in loaded)
                        InsertEntry(entry);
                });
            }
            else
            {
                foreach (var entry in loaded)
                    InsertEntry(entry);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "network-egress: could not load persisted entries");
        }
    }

    internal const int MaxLoadedEntries = 500;
    internal const int MaxPersistedEntries = 2000;
    internal static bool DisableDispatcherMarshallingForTests { get; set; }

    internal static IReadOnlyList<NetworkEgressEntry> ReadPersistedEntriesForDisplay(
        IEnumerable<string> lines,
        int maxLines)
    {
        var entries = new List<NetworkEgressEntry>();
        foreach (var line in TakeLatestLines(lines, maxLines))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var entry = JsonSerializer.Deserialize(line, EgressJsonContext.Default.NetworkEgressEntry);
                if (entry is not null)
                    entries.Add(entry);
            }
            catch
            {
                // Skip malformed lines.
            }
        }

        entries.Reverse();
        return entries;
    }

    internal static IReadOnlyList<string> TakeLatestLines(IEnumerable<string> lines, int maxLines)
    {
        if (maxLines <= 0) return [];

        var buffer = new Queue<string>(maxLines);
        foreach (var line in lines)
        {
            if (buffer.Count == maxLines)
                buffer.Dequeue();
            buffer.Enqueue(line);
        }

        return buffer.ToArray();
    }

    private static readonly object _persistLock = new();
    private static string? _persistedLineCountPath;
    private static int? _persistedLineCount;

    private static void PersistEntry(NetworkEgressEntry entry)
    {
        try
        {
            var path = GetJsonlPath();
            if (path is null) return;

            var json = JsonSerializer.Serialize(entry, EgressJsonContext.Default.NetworkEgressEntry);

            // Concurrent appends throw sharing violations and silently drop
            // the entry from the JSONL; serialize writers.
            lock (_persistLock)
            {
                var lineCount = GetPersistedLineCount(path);
                File.AppendAllText(path, json + Environment.NewLine);
                lineCount++;
                _persistedLineCount = lineCount;
                RotateIfNeeded(path, lineCount);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "network-egress: could not persist entry");
        }
    }

    private static int GetPersistedLineCount(string path)
    {
        if (_persistedLineCountPath is not null &&
            _persistedLineCountPath.Equals(path, StringComparison.OrdinalIgnoreCase) &&
            _persistedLineCount is not null)
        {
            return _persistedLineCount.Value;
        }

        _persistedLineCountPath = path;
        _persistedLineCount = CountPersistedLines(path);
        return _persistedLineCount.Value;
    }

    private static int CountPersistedLines(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadLines(path).Count() : 0;
        }
        catch
        {
            return 0;
        }
    }

    internal static void RotateIfNeeded(string path, int lineCount)
    {
        try
        {
            if (lineCount <= MaxPersistedEntries) return;

            var lines = File.ReadAllLines(path);
            if (lines.Length <= MaxPersistedEntries)
            {
                _persistedLineCountPath = path;
                _persistedLineCount = lines.Length;
                return;
            }

            var trimmed = lines[^MaxPersistedEntries..];
            File.WriteAllLines(path, trimmed);
            _persistedLineCountPath = path;
            _persistedLineCount = trimmed.Length;
        }
        catch
        {
            // Best effort — don't block the caller
        }
    }

    private static string? GetJsonlPath()
    {
        var dir = AppStorage.TryGetAppDirectory();
        return dir is null ? null : Path.Combine(dir, "network-egress.jsonl");
    }

    private static bool ShouldUseDispatcher(System.Windows.Threading.Dispatcher dispatcher)
        => !DisableDispatcherMarshallingForTests &&
           !dispatcher.CheckAccess() &&
           dispatcher.Thread.IsAlive &&
           !dispatcher.HasShutdownStarted &&
           !dispatcher.HasShutdownFinished;
}

public sealed record NetworkEgressEntry(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("purpose")] string Purpose,
    [property: JsonPropertyName("bytes")] long Bytes,
    [property: JsonPropertyName("durationMs")] long DurationMs,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("direction")] string Direction = "outbound")
{
    /// <summary>Human-readable byte count for display.</summary>
    [JsonIgnore]
    public string BytesDisplay => FormatBytes(Bytes);

    /// <summary>Duration with unit for display.</summary>
    [JsonIgnore]
    public string DurationDisplay => $"{DurationMs} ms";

    /// <summary>Short timestamp for display.</summary>
    [JsonIgnore]
    public string TimestampDisplay => Timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB"];
        var value = Math.Max(0, bytes);
        var displayValue = (double)value;
        var unitIndex = 0;

        while (displayValue >= 1024 && unitIndex < units.Length - 1)
        {
            displayValue /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{value} {units[unitIndex]}"
            : $"{displayValue:0.#} {units[unitIndex]}";
    }
}

[JsonSerializable(typeof(NetworkEgressEntry))]
internal partial class EgressJsonContext : JsonSerializerContext { }
