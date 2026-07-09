using System.Diagnostics;
using System.IO;

namespace Images.Tests;

public sealed class PackageManifestScriptTests
{
    [Fact]
    public async Task TestPackageManifestHashes_AllZeroCommittedScoopHash_Fails()
    {
        using var temp = TestDirectory.Create();
        var root = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(root);
        var scoopDir = Directory.CreateDirectory(Path.Combine(root, "packaging", "scoop")).FullName;
        await File.WriteAllTextAsync(
            Path.Combine(scoopDir, "images.json"),
            """
            {
              "version": "9.9.9",
              "architecture": {
                "64bit": {
                  "url": "https://github.com/SysAdminDoc/Images/releases/download/v9.9.9/Images-v9.9.9-win-x64.zip",
                  "hash": "0000000000000000000000000000000000000000000000000000000000000000"
                }
              }
            }
            """);

        var result = await RunPowerShellAsync(
            ScriptPath("Test-PackageManifestHashes.ps1"),
            "-RepositoryRoot",
            root,
            "-Version",
            "9.9.9");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("placeholder SHA-256", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NewPackageManifests_AllZeroChecksum_FailsBeforeWriting()
    {
        using var temp = TestDirectory.Create();
        var checksumFile = temp.WriteFile(
            "checksums.txt",
            """
            0000000000000000000000000000000000000000000000000000000000000000  Images-v9.9.9-win-x64.zip
            1111111111111111111111111111111111111111111111111111111111111111  Images-v9.9.9-setup-win-x64.exe
            """);
        var outputDir = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(outputDir);

        var result = await RunPowerShellAsync(
            ScriptPath("New-PackageManifests.ps1"),
            "-Version",
            "9.9.9",
            "-ChecksumFile",
            checksumFile,
            "-OutputDir",
            outputDir);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("placeholder SHA-256", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(outputDir, "scoop", "images.json")));
    }

    private static string ScriptPath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "scripts", fileName);
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not locate scripts\\{fileName}.");
    }

    private static async Task<ProcessResult> RunPowerShellAsync(string scriptPath, params string[] arguments)
    {
        var start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(scriptPath);
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start PowerShell.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask + await errorTask;
        return new ProcessResult(process.ExitCode, output);
    }

    private sealed record ProcessResult(int ExitCode, string Output);
}
