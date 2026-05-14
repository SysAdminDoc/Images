using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class JpegTranRuntimeTests
{
    [Fact]
    public void Inspect_WhenRuntimeMissing_DoesNotSearchPath()
    {
        using var temp = TestDirectory.Create();

        var status = JpegTranRuntime.Inspect(temp.Path, environmentExecutablePath: null);

        Assert.False(status.Available);
        Assert.Null(status.ExecutablePath);
        Assert.Equal("Not found", status.Source);
        Assert.Contains("Codecs\\JpegTran", status.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void Inspect_ResolvesAppLocalRuntimeAndReportsHash()
    {
        using var temp = TestDirectory.Create();
        var runtimeDir = Directory.CreateDirectory(Path.Combine(temp.Path, "Codecs", "JpegTran")).FullName;
        var exe = Path.Combine(runtimeDir, "jpegtran.exe");
        File.WriteAllText(exe, "fake jpegtran");

        var status = JpegTranRuntime.Inspect(
            temp.Path,
            environmentExecutablePath: null,
            versionReader: _ => "libjpeg-turbo 3.1.4.1");

        Assert.True(status.Available);
        Assert.Equal(exe, status.ExecutablePath);
        Assert.Equal("app-local Codecs\\JpegTran", status.Source);
        Assert.Equal("libjpeg-turbo 3.1.4.1", status.Version);
        Assert.Equal(JpegTranRuntime.GetSha256(exe), status.Sha256);
    }

    [Fact]
    public void Inspect_EnvironmentOverrideWinsOverAppLocalRuntime()
    {
        using var temp = TestDirectory.Create();
        var runtimeDir = Directory.CreateDirectory(Path.Combine(temp.Path, "Codecs", "JpegTran")).FullName;
        File.WriteAllText(Path.Combine(runtimeDir, "jpegtran.exe"), "app local");
        var overrideExe = temp.WriteFile("override-jpegtran.exe", "override");

        var status = JpegTranRuntime.Inspect(
            temp.Path,
            overrideExe,
            versionReader: _ => "override version");

        Assert.True(status.Available);
        Assert.Equal(overrideExe, status.ExecutablePath);
        Assert.Equal(JpegTranRuntime.EnvironmentVariable, status.Source);
        Assert.Equal("override version", status.Version);
    }

    [Fact]
    public void Inspect_InvalidEnvironmentOverrideDoesNotFallBackToAppLocalRuntime()
    {
        using var temp = TestDirectory.Create();
        var runtimeDir = Directory.CreateDirectory(Path.Combine(temp.Path, "Codecs", "JpegTran")).FullName;
        File.WriteAllText(Path.Combine(runtimeDir, "jpegtran.exe"), "app local");

        var status = JpegTranRuntime.Inspect(
            temp.Path,
            Path.Combine(temp.Path, "missing", "jpegtran.exe"));

        Assert.False(status.Available);
        Assert.Null(status.ExecutablePath);
        Assert.Equal("Not found", status.Source);
        Assert.Contains(JpegTranRuntime.EnvironmentVariable, status.StatusText, StringComparison.Ordinal);
    }
}
