using System.Diagnostics.Tracing;

namespace Images.Services;

/// <summary>
/// ETW / EventPipe event source for the decode pipeline. Exposes both structured events
/// (for <c>dotnet-trace</c>) and incrementing counters (for <c>dotnet-counters</c>).
///
/// Usage with dotnet-counters:
///   dotnet-counters monitor --process-id &lt;PID&gt; --counters Images-Decode
/// </summary>
[EventSource(Name = "Images-Decode")]
public sealed class ImageEventSource : EventSource
{
    public static readonly ImageEventSource Instance = new();

    // --- counters (lazy-init on first enable) ---
    private IncrementingEventCounter? _imagesDecoded;
    private EventCounter? _decodeDuration;
    private IncrementingEventCounter? _wicDecodes;
    private IncrementingEventCounter? _magickFallbackDecodes;
    private IncrementingEventCounter? _thumbnailWrites;
    private IncrementingEventCounter? _decodeFailures;

    private ImageEventSource() : base("Images-Decode") { }

    // ---- Keywords ----
    public static class Keywords
    {
        public const EventKeywords Decode = (EventKeywords)0x0001;
        public const EventKeywords Thumbnail = (EventKeywords)0x0002;
    }

    // ---- Events ----

    [Event(1, Message = "Decode started: {0} via {1}", Level = EventLevel.Informational, Keywords = (EventKeywords)0x0001)]
    public void DecodeStarted(string path, string decoder)
    {
        if (IsEnabled()) WriteEvent(1, path, decoder);
    }

    [Event(2, Message = "Decode completed: {0} via {1} in {2} ms", Level = EventLevel.Informational, Keywords = (EventKeywords)0x0001)]
    public void DecodeCompleted(string path, string decoder, long durationMs)
    {
        if (IsEnabled()) WriteEvent(2, path, decoder, durationMs);
    }

    [Event(3, Message = "Decode failed: {0} — {1}", Level = EventLevel.Warning, Keywords = (EventKeywords)0x0001)]
    public void DecodeFailed(string path, string error)
    {
        if (IsEnabled()) WriteEvent(3, path, error);
    }

    // ---- Counter helpers (allocation-free when disabled) ----

    public void RecordDecodeAttempt() => _imagesDecoded?.Increment();

    public void RecordDecodeDuration(double milliseconds) => _decodeDuration?.WriteMetric(milliseconds);

    public void RecordWicDecode() => _wicDecodes?.Increment();

    public void RecordMagickFallbackDecode() => _magickFallbackDecodes?.Increment();

    public void RecordThumbnailWrite() => _thumbnailWrites?.Increment();

    public void RecordDecodeFailure() => _decodeFailures?.Increment();

    // ---- Lifecycle ----

    protected override void OnEventCommand(EventCommandEventArgs command)
    {
        if (command.Command == EventCommand.Enable)
        {
            _imagesDecoded ??= new IncrementingEventCounter("images-decoded", this)
            {
                DisplayName = "Images decoded",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };

            _decodeDuration ??= new EventCounter("decode-duration-ms", this)
            {
                DisplayName = "Decode duration (ms)"
            };

            _wicDecodes ??= new IncrementingEventCounter("wic-decodes", this)
            {
                DisplayName = "WIC decodes",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };

            _magickFallbackDecodes ??= new IncrementingEventCounter("magick-fallback-decodes", this)
            {
                DisplayName = "Magick.NET fallback decodes",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };

            _thumbnailWrites ??= new IncrementingEventCounter("thumbnail-writes", this)
            {
                DisplayName = "Thumbnail cache writes",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };

            _decodeFailures ??= new IncrementingEventCounter("decode-failures", this)
            {
                DisplayName = "Decode failures",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };
        }
    }

    protected override void Dispose(bool disposing)
    {
        _imagesDecoded?.Dispose();
        _decodeDuration?.Dispose();
        _wicDecodes?.Dispose();
        _magickFallbackDecodes?.Dispose();
        _thumbnailWrites?.Dispose();
        _decodeFailures?.Dispose();
        base.Dispose(disposing);
    }
}
