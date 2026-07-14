using System.Text;

namespace Images.Services;

public enum ArchiveBudgetKind
{
    EntryCount,
    EntryNameBytes,
    AggregatePageBytes,
    CompressionRatio,
}

public sealed class ArchiveBudgetExceededException : InvalidOperationException
{
    public ArchiveBudgetExceededException(ArchiveBudgetKind budget, string message)
        : base(message)
    {
        Budget = budget;
    }

    public ArchiveBudgetKind Budget { get; }
}

/// <summary>
/// Stateful limits for archive metadata enumeration. The messages intentionally omit entry and
/// archive names so hostile metadata cannot be reflected into UI or diagnostics.
/// </summary>
public sealed class ArchiveBudgetPolicy
{
    public const int MaxEntryCount = 10_000;
    public const long MaxEntryNameBytes = 4L * 1024 * 1024;
    public const long MaxAggregatePageBytes = 4L * 1024 * 1024 * 1024;
    public const long MaxCompressionRatio = 1_000;

    private int _entryCount;
    private long _entryNameBytes;
    private long _aggregatePageBytes;

    internal void AccountEntry(string entryName)
    {
        _entryCount++;
        if (_entryCount > MaxEntryCount)
        {
            throw new ArchiveBudgetExceededException(
                ArchiveBudgetKind.EntryCount,
                "Archive entry count exceeds the 10,000-entry safety budget.");
        }

        _entryNameBytes += Encoding.UTF8.GetByteCount(entryName);
        if (_entryNameBytes > MaxEntryNameBytes)
        {
            throw new ArchiveBudgetExceededException(
                ArchiveBudgetKind.EntryNameBytes,
                "Archive entry-name metadata exceeds the 4 MiB safety budget.");
        }
    }

    internal void AccountPage(long declaredSize, long compressedSize)
    {
        if (declaredSize > MaxAggregatePageBytes - _aggregatePageBytes)
        {
            throw new ArchiveBudgetExceededException(
                ArchiveBudgetKind.AggregatePageBytes,
                "Archive image pages exceed the 4 GiB declared-size safety budget.");
        }

        _aggregatePageBytes += declaredSize;

        // Some archive formats do not expose a compressed size. Enforce the ratio whenever both
        // sides are known, without multiplying attacker-controlled values and risking overflow.
        var wholeRatio = compressedSize > 0 ? declaredSize / compressedSize : 0;
        var ratioRemainder = compressedSize > 0 ? declaredSize % compressedSize : 0;
        if (declaredSize > 0 && compressedSize > 0 &&
            (wholeRatio > MaxCompressionRatio ||
             (wholeRatio == MaxCompressionRatio && ratioRemainder > 0)))
        {
            throw new ArchiveBudgetExceededException(
                ArchiveBudgetKind.CompressionRatio,
                "An archive image page exceeds the 1000:1 compression-ratio safety budget.");
        }
    }
}
