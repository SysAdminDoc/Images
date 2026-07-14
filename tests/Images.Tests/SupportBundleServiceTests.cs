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
    public void SanitizeText_RedactsRootedPathsWithoutRemovingDiagnosticCopy()
    {
        var text = """
            opened C:\Users\MATTHE~1\Pictures\a,b.jpg
            uri file:///C:/Users/Matthew%20Parker/Pictures/a.jpg
            slash C:/Users/Somebody/Pictures/a.jpg
            status decoder=Magick.NET exit=7
            """;

        var redacted = SupportBundleService.SanitizeText(text);

        Assert.DoesNotContain("MATTHE~1", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Matthew%20Parker", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Somebody", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("a,b.jpg", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, redacted.Split("%PATH%").Length - 1);
        Assert.Contains("status decoder=Magick.NET exit=7", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSanitizedTextEntry_RedactsAdversarialPathFormsInResultingZip()
    {
        const string payload = """
            drive D:\Photos\drive-secret.jpg
            unc \\server\share\unc-secret.jpg
            device \\?\E:\vault\device-secret.jpg
            uri file:///F:/private/uri-secret.jpg
            mixed G:\mixed/separators/mixed-secret.jpg
            quoted "H:\My Photos\quoted-secret.jpg"
            System.IO.FileNotFoundException: decoder failed at 'I:\Exceptions\exception-secret.jpg'
            useful decoder=Magick.NET exit=7 elapsed=42ms
            """;
        using var bytes = new MemoryStream();
        using (var archive = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
            SupportBundleService.WriteSanitizedTextEntry(archive, "adversarial.txt", payload);

        bytes.Position = 0;
        using var result = new ZipArchive(bytes, ZipArchiveMode.Read);
        using var reader = new StreamReader(result.GetEntry("adversarial.txt")!.Open());
        var sanitized = reader.ReadToEnd();

        foreach (var secret in new[] { "drive-secret", "unc-secret", "device-secret", "uri-secret", "mixed-secret", "quoted-secret", "exception-secret" })
            Assert.DoesNotContain(secret, sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(7, sanitized.Split("%PATH%").Length - 1);
        Assert.Contains("System.IO.FileNotFoundException: decoder failed at", sanitized, StringComparison.Ordinal);
        Assert.Contains("useful decoder=Magick.NET exit=7 elapsed=42ms", sanitized, StringComparison.Ordinal);
    }
}
