namespace Images.Services;

/// <summary>
/// V120-02: shared Ctrl+C wiring for long-running ML batch CLIs
/// (<c>--scene-classify</c>, <c>--aesthetic-score</c>, <c>--safety-classify</c>,
/// <c>--face-cluster</c>). A single Ctrl+C requests cooperative cancellation of the batch
/// (the token is checked between images) instead of hard-killing the process, so partial
/// results are reported and no files are touched. A second Ctrl+C falls back to the default
/// terminate behavior.
/// </summary>
public sealed class CliCancellation : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ConsoleCancelEventHandler _handler;
    private int _requests;

    private CliCancellation()
    {
        _handler = OnCancelKeyPress;
        try
        {
            Console.CancelKeyPress += _handler;
        }
        catch
        {
            // Console.CancelKeyPress is unavailable in some hosts (no console); cancellation
            // then simply never fires, which is the safe default for a batch that runs to completion.
        }
    }

    public CancellationToken Token => _cts.Token;

    public static CliCancellation OnCtrlC() => new();

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        // Handle the first Ctrl+C ourselves; let a second one terminate the process normally.
        if (Interlocked.Increment(ref _requests) == 1)
        {
            e.Cancel = true;
            try { _cts.Cancel(); } catch (ObjectDisposedException) { }
        }
    }

    public void Dispose()
    {
        try { Console.CancelKeyPress -= _handler; } catch { }
        _cts.Dispose();
    }
}
