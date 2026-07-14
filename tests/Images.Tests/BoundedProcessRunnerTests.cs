using System.Diagnostics;
using System.IO;
using System.Text;
using Images.Services;

namespace Images.Tests;

[Collection("TimingSensitive")]
public sealed class BoundedProcessRunnerTests
{
    private const int TestLimitBytes = 32 * 1024;

    [Fact]
    public void Run_StopsStdoutFloodAtConfiguredLimit()
    {
        var result = BoundedProcessRunner.Run(
            CreatePowerShell("$chunk = 'o' * 4096; while ($true) { [Console]::Out.Write($chunk) }"),
            timeoutMilliseconds: 10000,
            TestLimitBytes,
            TestLimitBytes);

        Assert.Equal(BoundedProcessStatus.OutputLimitExceeded, result.Status);
        Assert.Equal(TestLimitBytes, Encoding.UTF8.GetByteCount(result.StandardOutput));
        Assert.True(Encoding.UTF8.GetByteCount(result.StandardError) <= TestLimitBytes);
    }

    [Fact]
    public void Run_StopsStderrFloodAtConfiguredLimit()
    {
        var result = BoundedProcessRunner.Run(
            CreatePowerShell("$chunk = 'e' * 4096; while ($true) { [Console]::Error.Write($chunk) }"),
            timeoutMilliseconds: 10000,
            TestLimitBytes,
            TestLimitBytes);

        Assert.Equal(BoundedProcessStatus.OutputLimitExceeded, result.Status);
        Assert.Equal(TestLimitBytes, Encoding.UTF8.GetByteCount(result.StandardError));
        Assert.True(Encoding.UTF8.GetByteCount(result.StandardOutput) <= TestLimitBytes);
    }

    [Fact]
    public void Run_DrainsBothFloodingStreamsWithoutDeadlock()
    {
        var result = BoundedProcessRunner.Run(
            CreatePowerShell("$chunk = 'x' * 2048; while ($true) { [Console]::Out.Write($chunk); [Console]::Error.Write($chunk) }"),
            timeoutMilliseconds: 10000,
            TestLimitBytes,
            TestLimitBytes);

        Assert.Equal(BoundedProcessStatus.OutputLimitExceeded, result.Status);
        Assert.InRange(Encoding.UTF8.GetByteCount(result.StandardOutput), 1, TestLimitBytes);
        Assert.InRange(Encoding.UTF8.GetByteCount(result.StandardError), 1, TestLimitBytes);
    }

    [Fact]
    public void Run_DistinguishesTimeoutFromOutputLimit()
    {
        var result = BoundedProcessRunner.Run(
            CreatePowerShell("Start-Sleep -Seconds 30"),
            timeoutMilliseconds: 250,
            TestLimitBytes,
            TestLimitBytes);

        Assert.Equal(BoundedProcessStatus.TimedOut, result.Status);
        Assert.False(result.OutputLimitExceeded);
    }

    [Fact]
    public void Run_PreservesNonzeroExitAsCompletedProcess()
    {
        var result = BoundedProcessRunner.Run(
            CreatePowerShell("[Console]::Error.Write('failure'); exit 7"),
            timeoutMilliseconds: 10000,
            TestLimitBytes,
            TestLimitBytes);

        Assert.Equal(BoundedProcessStatus.Completed, result.Status);
        Assert.Equal(7, result.ExitCode);
        Assert.Equal("failure", result.StandardError);
    }

    private static ProcessStartInfo CreatePowerShell(string command)
    {
        var executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        Assert.True(File.Exists(executable), "Windows PowerShell is required for process boundary tests.");

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(command);
        return startInfo;
    }
}
