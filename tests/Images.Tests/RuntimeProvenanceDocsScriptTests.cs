using System.Diagnostics;
using System.IO;

namespace Images.Tests;

public sealed class RuntimeProvenanceDocsScriptTests
{
    [Fact]
    public async Task TestRuntimeProvenanceDocs_CurrentRepositoryPasses()
    {
        var result = await RunPowerShellAsync(
            ScriptPath("Test-RuntimeProvenanceDocs.ps1"),
            "-RepositoryRoot",
            RepositoryRoot());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("synchronized", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestRuntimeProvenanceDocs_StaleSharpCompressDocsFail()
    {
        using var temp = TestDirectory.Create();
        var root = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "src", "Images", "Codecs", "JpegTran"));
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        Directory.CreateDirectory(Path.Combine(root, "scripts"));

        await File.WriteAllTextAsync(
            Path.Combine(root, "src", "Images", "Images.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Magick.NET-Q16-HDRI-AnyCPU" Version="14.14.0" />
                <PackageReference Include="Magick.NET.Core" Version="14.14.0" />
                <PackageReference Include="SharpCompress" Version="0.49.1" />
                <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.9" />
                <PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" Version="3.0.3" />
              </ItemGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(
            Path.Combine(root, "src", "Images", "Codecs", "JpegTran", "PROVENANCE.md"),
            "- Version: 3.1.4.1");
        await File.WriteAllTextAsync(
            Path.Combine(root, "scripts", "New-Sbom.ps1"),
            "Ghostscript 10.07.0 (Artifex)");
        await File.WriteAllTextAsync(
            Path.Combine(root, "docs", "integration-policy.md"),
            "Magick.NET 14.14.0`nSharpCompress 0.48.1`nMicrosoft.Data.Sqlite 10.0.9`nSQLitePCLRaw.bundle_e_sqlite3 3.0.3`nGhostscript 10.07.0`nlibjpeg-turbo 3.1.4.1");
        await File.WriteAllTextAsync(
            Path.Combine(root, "docs", "archive-runtime-review.md"),
            "`SharpCompress` 0.48.1 from NuGet; upgraded to 0.48.1");
        await File.WriteAllTextAsync(
            Path.Combine(root, "docs", "codec-bundling.md"),
            "Ghostscript 10.07.0`nlibjpeg-turbo 3.1.4.1");
        await File.WriteAllTextAsync(
            Path.Combine(root, "docs", "codec-support-policy.md"),
            "Ghostscript 10.07.0");
        await File.WriteAllTextAsync(
            Path.Combine(root, "docs", "lossless-jpeg-transform-policy.md"),
            "libjpeg-turbo 3.1.4.1");

        var result = await RunPowerShellAsync(
            ScriptPath("Test-RuntimeProvenanceDocs.ps1"),
            "-RepositoryRoot",
            root);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("SharpCompress", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Images.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static string ScriptPath(string fileName)
    {
        var root = RepositoryRoot();
        var candidate = Path.Combine(root, "scripts", fileName);
        if (File.Exists(candidate))
            return candidate;

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
