using System.Diagnostics;
using System.IO;
using System.Text;

namespace Images.Services;

internal enum BoundedProcessStatus
{
    Completed,
    TimedOut,
    OutputLimitExceeded,
    StartFailed,
}

internal sealed record BoundedProcessResult(
    BoundedProcessStatus Status,
    int? ExitCode,
    string StandardOutput,
    string StandardError)
{
    public bool TimedOut => Status == BoundedProcessStatus.TimedOut;
    public bool OutputLimitExceeded => Status == BoundedProcessStatus.OutputLimitExceeded;
}

/// <summary>
/// Runs a redirected child process while retaining no more than the configured bytes from
/// either output stream. Both streams continue draining concurrently until the child exits;
/// reaching a limit terminates the complete child process tree.
/// </summary>
internal static class BoundedProcessRunner
{
    public const int VersionProbeOutputLimitBytes = 256 * 1024;
    public const int OperationOutputLimitBytes = 4 * 1024 * 1024;

    private static readonly TimeSpan TerminationDrainTimeout = TimeSpan.FromSeconds(5);

    public static BoundedProcessResult Run(
        ProcessStartInfo startInfo,
        int timeoutMilliseconds,
        int standardOutputLimitBytes,
        int standardErrorLimitBytes)
        => RunAsync(
                startInfo,
                timeoutMilliseconds,
                standardOutputLimitBytes,
                standardErrorLimitBytes)
            .GetAwaiter()
            .GetResult();

    private static async Task<BoundedProcessResult> RunAsync(
        ProcessStartInfo startInfo,
        int timeoutMilliseconds,
        int standardOutputLimitBytes,
        int standardErrorLimitBytes)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        ArgumentOutOfRangeException.ThrowIfLessThan(standardOutputLimitBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(standardErrorLimitBytes, 1);
        if (timeoutMilliseconds < Timeout.Infinite)
            throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
        if (!startInfo.RedirectStandardOutput || !startInfo.RedirectStandardError)
            throw new ArgumentException("Standard output and error must both be redirected.", nameof(startInfo));
        if (startInfo.UseShellExecute)
            throw new ArgumentException("Shell execution is not supported.", nameof(startInfo));

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            return new BoundedProcessResult(BoundedProcessStatus.StartFailed, null, "", "");

        var limitReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stdoutTask = DrainAsync(
            process.StandardOutput.BaseStream,
            process.StandardOutput.CurrentEncoding,
            standardOutputLimitBytes,
            limitReached);
        var stderrTask = DrainAsync(
            process.StandardError.BaseStream,
            process.StandardError.CurrentEncoding,
            standardErrorLimitBytes,
            limitReached);
        var exitTask = process.WaitForExitAsync();
        var timeoutTask = timeoutMilliseconds == Timeout.Infinite
            ? Task.Delay(Timeout.InfiniteTimeSpan)
            : Task.Delay(timeoutMilliseconds);

        var first = await Task.WhenAny(exitTask, timeoutTask, limitReached.Task).ConfigureAwait(false);
        var status = first == limitReached.Task
            ? BoundedProcessStatus.OutputLimitExceeded
            : first == timeoutTask
                ? BoundedProcessStatus.TimedOut
                : BoundedProcessStatus.Completed;

        if (status != BoundedProcessStatus.Completed)
            TryKillProcessTree(process);

        await WaitForTerminationAndDrainAsync(exitTask, stdoutTask, stderrTask).ConfigureAwait(false);

        var stdout = GetCompletedDrain(stdoutTask);
        var stderr = GetCompletedDrain(stderrTask);
        if (stdout.LimitExceeded || stderr.LimitExceeded)
            status = BoundedProcessStatus.OutputLimitExceeded;

        int? exitCode = null;
        try
        {
            if (process.HasExited)
                exitCode = process.ExitCode;
        }
        catch (InvalidOperationException)
        {
        }

        return new BoundedProcessResult(status, exitCode, stdout.Text, stderr.Text);
    }

    private static async Task<DrainResult> DrainAsync(
        Stream stream,
        Encoding encoding,
        int limitBytes,
        TaskCompletionSource limitReached)
    {
        using var retained = new MemoryStream(capacity: Math.Min(limitBytes, 64 * 1024));
        var buffer = new byte[16 * 1024];
        var exceeded = false;

        while (true)
        {
            var read = await stream.ReadAsync(buffer).ConfigureAwait(false);
            if (read == 0)
                break;

            var remaining = limitBytes - checked((int)retained.Length);
            if (remaining > 0)
                retained.Write(buffer, 0, Math.Min(remaining, read));

            if (retained.Length >= limitBytes)
            {
                exceeded = true;
                limitReached.TrySetResult();
            }
        }

        return new DrainResult(encoding.GetString(retained.GetBuffer(), 0, checked((int)retained.Length)), exceeded);
    }

    private static async Task WaitForTerminationAndDrainAsync(
        Task exitTask,
        Task<DrainResult> stdoutTask,
        Task<DrainResult> stderrTask)
    {
        try
        {
            var all = Task.WhenAll(exitTask, stdoutTask, stderrTask);
            await Task.WhenAny(all, Task.Delay(TerminationDrainTimeout)).ConfigureAwait(false);
        }
        catch
        {
            // The caller receives whatever bounded output completed before termination.
        }
    }

    private static DrainResult GetCompletedDrain(Task<DrainResult> task)
    {
        try
        {
            return task.IsCompletedSuccessfully ? task.Result : new DrainResult("", false);
        }
        catch
        {
            return new DrainResult("", false);
        }
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }

    private sealed record DrainResult(string Text, bool LimitExceeded);
}
