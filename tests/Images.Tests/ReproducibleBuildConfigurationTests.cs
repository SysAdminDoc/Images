using System.IO;
using System.Text.Json;
using System.Xml.Linq;

namespace Images.Tests;

public sealed class ReproducibleBuildConfigurationTests
{
    [Fact]
    public void RepositoryPinsSdkPackagesAndSbomTool()
    {
        var root = RepositoryRoot();

        using var globalJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "global.json")));
        var sdk = globalJson.RootElement.GetProperty("sdk");
        Assert.Equal("10.0.301", sdk.GetProperty("version").GetString());
        Assert.Equal("latestPatch", sdk.GetProperty("rollForward").GetString());
        Assert.False(sdk.GetProperty("allowPrerelease").GetBoolean());

        var buildProperties = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        Assert.Equal(
            "true",
            buildProperties.Descendants("RestorePackagesWithLockFile").Single().Value,
            ignoreCase: true);

        AssertLockFile(Path.Combine(root, "src", "Images", "packages.lock.json"));
        AssertLockFile(Path.Combine(root, "tests", "Images.Tests", "packages.lock.json"));

        using var toolManifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(root, ".config", "dotnet-tools.json")));
        var cycloneDx = toolManifest.RootElement.GetProperty("tools").GetProperty("cyclonedx");
        Assert.Equal("6.2.0", cycloneDx.GetProperty("version").GetString());
        Assert.False(cycloneDx.GetProperty("rollForward").GetBoolean());
        Assert.Equal("dotnet-CycloneDX", cycloneDx.GetProperty("commands")[0].GetString());
    }

    [Fact]
    public void ReleaseScriptsEnforcePinnedInputs()
    {
        var root = RepositoryRoot();
        var readiness = File.ReadAllText(Path.Combine(root, "scripts", "Test-ReleaseReadiness.ps1"));
        var sbom = File.ReadAllText(Path.Combine(root, "scripts", "New-Sbom.ps1"));

        Assert.Contains("dotnet restore $slnPath --locked-mode", readiness, StringComparison.Ordinal);
        Assert.Contains("dotnet --version", readiness, StringComparison.Ordinal);
        Assert.Contains("^10\\.0\\.3\\d{2}$", readiness, StringComparison.Ordinal);
        Assert.Contains("dotnet tool restore --tool-manifest $toolManifestPath", sbom, StringComparison.Ordinal);
        Assert.Contains("dotnet tool run dotnet-CycloneDX", sbom, StringComparison.Ordinal);
        Assert.DoesNotContain("& dotnet-CycloneDX", sbom, StringComparison.Ordinal);
    }

    private static void AssertLockFile(string path)
    {
        Assert.True(File.Exists(path), $"NuGet lock file is missing: {path}");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(1, document.RootElement.GetProperty("version").GetInt32());
        Assert.NotEmpty(document.RootElement.GetProperty("dependencies").EnumerateObject());
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Images.sln")))
            directory = directory.Parent;

        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Could not locate the Images repository root.");
    }
}
