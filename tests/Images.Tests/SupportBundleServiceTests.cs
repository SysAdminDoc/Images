using System.IO;
using System.IO.Compression;
using Images.Services;

namespace Images.Tests;

public sealed class SupportBundleServiceTests
{
    [Fact]
    public void Build_CreatesZipWithBundleInfoEntry()
    {
        var zipPath = SupportBundleService.Build();
        try
        {
            Assert.True(File.Exists(zipPath));
            Assert.EndsWith(".zip", zipPath, StringComparison.OrdinalIgnoreCase);

            using var archive = ZipFile.OpenRead(zipPath);
            Assert.Contains(archive.Entries, e => e.FullName == "bundle-info.txt");
        }
        finally
        {
            try { File.Delete(zipPath); } catch { }
        }
    }

    [Fact]
    public void Build_BundleInfoContainsVersionAndRuntime()
    {
        var zipPath = SupportBundleService.Build();
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entry = archive.GetEntry("bundle-info.txt");
            Assert.NotNull(entry);

            using var reader = new StreamReader(entry!.Open());
            var text = reader.ReadToEnd();
            Assert.Contains("Version:", text);
            Assert.Contains("Runtime:", text);
            Assert.Contains("OS:", text);
        }
        finally
        {
            try { File.Delete(zipPath); } catch { }
        }
    }

    [Fact]
    public void Build_ZipContainsDiagnosticsStatus()
    {
        var zipPath = SupportBundleService.Build();
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            Assert.Contains(archive.Entries, e => e.FullName == "diagnostics-status.txt");
        }
        finally
        {
            try { File.Delete(zipPath); } catch { }
        }
    }

    [Fact]
    public void RedactProfilePaths_RedactsShortAndUriUserProfileForms()
    {
        var text = """
            opened C:\Users\MATTHE~1\Pictures\a,b.jpg
            uri file:///C:/Users/Matthew%20Parker/Pictures/a.jpg
            slash C:/Users/Somebody/Pictures/a.jpg
            """;

        var redacted = SupportBundleService.RedactProfilePaths(text);

        Assert.DoesNotContain(@"C:\Users\MATTHE~1", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:/Users/Matthew%20Parker", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:/Users/Somebody", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(@"%USERPROFILE%\Pictures\a,b.jpg", redacted);
        Assert.Contains("file:///%USERPROFILE%/Pictures/a.jpg", redacted);
        Assert.Contains("%USERPROFILE%/Pictures/a.jpg", redacted);
    }
}
