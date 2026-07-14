using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace Images.Tests;

internal enum CodecProbeExecutionStatus
{
    Completed,
    TimedOut,
    MemoryLimitExceeded,
    Crashed,
    InvalidResponse,
}

internal sealed record CodecProbePayload(
    string Classification,
    string? Detail,
    int? PageCount,
    int? Width,
    int? Height);

internal sealed record CodecProbeExecution(
    CodecProbeExecutionStatus Status,
    CodecProbePayload? Payload,
    int? ExitCode,
    long PeakWorkingSetBytes,
    string StandardError);

internal static class CodecProbeRunner
{
    public const long MemoryLimitBytes = 768L * 1024 * 1024;
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    public static async Task<CodecProbeExecution> RunAsync(string mode, string inputPath)
    {
        var probeDirectory = ProbeOutputDirectory();
        var probePath = Path.Combine(probeDirectory, "Images.CodecProbe.dll");
        if (!File.Exists(probePath))
            throw new FileNotFoundException("Codec probe executable was not built.", probePath);

        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            WorkingDirectory = probeDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(probePath);
        startInfo.ArgumentList.Add(mode);
        startInfo.ArgumentList.Add(Path.GetFullPath(inputPath));

        using var job = ProcessMemoryJob.Create(MemoryLimitBytes);
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            return new CodecProbeExecution(CodecProbeExecutionStatus.Crashed, null, null, 0, "Probe did not start.");

        job.Assign(process);
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var workingSetTask = MonitorWorkingSetAsync(process);
        var timedOut = false;

        using (var timeout = new CancellationTokenSource(Timeout))
        {
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                timedOut = true;
                TryKill(process);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        var stdout = await stdoutTask.WaitAsync(TimeSpan.FromSeconds(5));
        var stderr = await stderrTask.WaitAsync(TimeSpan.FromSeconds(5));
        var peakWorkingSet = Math.Max(
            await workingSetTask.WaitAsync(TimeSpan.FromSeconds(5)),
            ReadPeakWorkingSet(process));
        if (timedOut)
        {
            return new CodecProbeExecution(
                CodecProbeExecutionStatus.TimedOut,
                null,
                TryGetExitCode(process),
                peakWorkingSet,
                stderr);
        }

        var exitCode = TryGetExitCode(process);
        if (exitCode != 0)
        {
            var status = peakWorkingSet >= MemoryLimitBytes * 3 / 4
                ? CodecProbeExecutionStatus.MemoryLimitExceeded
                : CodecProbeExecutionStatus.Crashed;
            return new CodecProbeExecution(status, null, exitCode, peakWorkingSet, stderr);
        }

        try
        {
            var payload = JsonSerializer.Deserialize<CodecProbePayload>(
                stdout,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return payload is null
                ? new CodecProbeExecution(CodecProbeExecutionStatus.InvalidResponse, null, exitCode, peakWorkingSet, stderr)
                : new CodecProbeExecution(CodecProbeExecutionStatus.Completed, payload, exitCode, peakWorkingSet, stderr);
        }
        catch (JsonException)
        {
            return new CodecProbeExecution(
                CodecProbeExecutionStatus.InvalidResponse,
                null,
                exitCode,
                peakWorkingSet,
                stderr);
        }
    }

    private static string ProbeOutputDirectory()
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Release";
        return Path.Combine(
            RepositoryRoot(),
            "tests",
            "Images.CodecProbe",
            "bin",
            configuration,
            "net10.0-windows10.0.22621.0");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Images.sln")))
            directory = directory.Parent;

        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Could not locate the Images repository root.");
    }

    private static long ReadPeakWorkingSet(Process process)
    {
        try
        {
            process.Refresh();
            return process.PeakWorkingSet64;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    private static async Task<long> MonitorWorkingSetAsync(Process process)
    {
        var peak = 0L;
        while (true)
        {
            try
            {
                process.Refresh();
                peak = Math.Max(peak, process.WorkingSet64);
                if (process.HasExited)
                    return peak;
            }
            catch (InvalidOperationException)
            {
                return peak;
            }

            await Task.Delay(10);
        }
    }

    private static int? TryGetExitCode(Process process)
    {
        try
        {
            return process.HasExited ? process.ExitCode : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static void TryKill(Process process)
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

    private sealed class ProcessMemoryJob : IDisposable
    {
        private const uint JobObjectLimitProcessMemory = 0x00000100;
        private const uint JobObjectLimitKillOnJobClose = 0x00002000;
        private const int JobObjectExtendedLimitInformationClass = 9;

        private readonly SafeFileHandle _handle;

        private ProcessMemoryJob(SafeFileHandle handle) => _handle = handle;

        public static ProcessMemoryJob Create(long memoryLimitBytes)
        {
            var rawHandle = CreateJobObject(IntPtr.Zero, null);
            if (rawHandle == IntPtr.Zero)
                throw new InvalidOperationException($"CreateJobObject failed with Win32 error {Marshal.GetLastWin32Error()}.");

            var handle = new SafeFileHandle(rawHandle, ownsHandle: true);
            var limits = new JobObjectExtendedLimitInformation
            {
                BasicLimitInformation = new JobObjectBasicLimitInformation
                {
                    LimitFlags = JobObjectLimitProcessMemory | JobObjectLimitKillOnJobClose,
                },
                ProcessMemoryLimit = (nuint)memoryLimitBytes,
            };

            if (!SetInformationJobObject(
                    handle,
                    JobObjectExtendedLimitInformationClass,
                    ref limits,
                    (uint)Marshal.SizeOf<JobObjectExtendedLimitInformation>()))
            {
                var error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new InvalidOperationException($"SetInformationJobObject failed with Win32 error {error}.");
            }

            return new ProcessMemoryJob(handle);
        }

        public void Assign(Process process)
        {
            if (!AssignProcessToJobObject(_handle, process.Handle))
                throw new InvalidOperationException($"AssignProcessToJobObject failed with Win32 error {Marshal.GetLastWin32Error()}.");
        }

        public void Dispose() => _handle.Dispose();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr jobAttributes, string? name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(
            SafeFileHandle job,
            int informationClass,
            ref JobObjectExtendedLimitInformation information,
            uint informationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicLimitInformation
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public nuint MinimumWorkingSetSize;
            public nuint MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public nuint Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectExtendedLimitInformation
        {
            public JobObjectBasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public nuint ProcessMemoryLimit;
            public nuint JobMemoryLimit;
            public nuint PeakProcessMemoryUsed;
            public nuint PeakJobMemoryUsed;
        }
    }
}
