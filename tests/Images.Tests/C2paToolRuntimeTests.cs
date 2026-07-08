using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class C2paToolRuntimeTests
{
    [Fact]
    public void Inspect_WhenRuntimeMissing_ReportsNotFound()
    {
        using var temp = TestDirectory.Create();

        var status = C2paToolRuntime.Inspect(temp.Path, environmentExecutablePath: null);

        Assert.False(status.Available);
        Assert.Null(status.ExecutablePath);
        Assert.Equal("Not found", status.Source);
        Assert.Contains("c2patool not found", status.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void Inspect_ResolvesAppLocalRuntimeAndReportsHash()
    {
        using var temp = TestDirectory.Create();
        var runtimeDir = Directory.CreateDirectory(Path.Combine(temp.Path, "Codecs", "C2paTool")).FullName;
        var exe = Path.Combine(runtimeDir, "c2patool.exe");
        File.WriteAllText(exe, "fake c2patool");

        var status = C2paToolRuntime.Inspect(
            temp.Path,
            environmentExecutablePath: null,
            versionReader: _ => "c2patool 0.38.0");

        Assert.True(status.Available);
        Assert.Equal(exe, status.ExecutablePath);
        Assert.Equal("app-local Codecs\\C2paTool", status.Source);
        Assert.Equal("c2patool 0.38.0", status.Version);
        Assert.Equal(C2paToolRuntime.GetSha256(exe), status.Sha256);
    }

    [Fact]
    public void Inspect_EnvironmentOverrideWinsOverAppLocal()
    {
        using var temp = TestDirectory.Create();
        var runtimeDir = Directory.CreateDirectory(Path.Combine(temp.Path, "Codecs", "C2paTool")).FullName;
        File.WriteAllText(Path.Combine(runtimeDir, "c2patool.exe"), "app local");
        var overrideExe = temp.WriteFile("override-c2patool.exe", "override");

        var status = C2paToolRuntime.Inspect(
            temp.Path,
            overrideExe,
            versionReader: _ => "override version");

        Assert.True(status.Available);
        Assert.Equal(overrideExe, status.ExecutablePath);
        Assert.Equal(C2paToolRuntime.EnvironmentVariable, status.Source);
        Assert.Equal("override version", status.Version);
    }

    [Fact]
    public void Inspect_EnvironmentOverrideToMissingFile_ReportsExplicitError()
    {
        using var temp = TestDirectory.Create();
        var missingPath = Path.Combine(temp.Path, "does-not-exist.exe");

        var status = C2paToolRuntime.Inspect(temp.Path, missingPath);

        Assert.False(status.Available);
        Assert.Contains(C2paToolRuntime.EnvironmentVariable, status.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveExecutable_WithoutAppLocalOrOverride_DoesNotSearchPath()
    {
        using var temp = TestDirectory.Create();
        var pathDir = Directory.CreateDirectory(Path.Combine(temp.Path, "PathBin")).FullName;
        File.WriteAllText(Path.Combine(pathDir, "c2patool.exe"), "fake path runtime");
        var previousPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            Environment.SetEnvironmentVariable("PATH", pathDir);

            var location = C2paToolRuntime.ResolveExecutable(temp.Path, environmentExecutablePath: null);

            Assert.Null(location);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
        }
    }
}
