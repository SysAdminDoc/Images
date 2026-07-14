using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Images.Services;

namespace Images.Tests;

[Collection("TimingSensitive")]
public sealed class AdversarialCodecCorpusTests
{
    [Theory]
    [Trait("Category", "AdversarialCorpus")]
    [InlineData("avif.avif")]
    [InlineData("heic.heic")]
    [InlineData("Reagan.jxl")]
    [InlineData("IMG_1361.dng")]
    [InlineData("20110626_213900.psd")]
    public async Task LicensedFormatFixture_DecodesInsideResourceGuard(string fileName)
    {
        var metadata = ReadMetadata();
        var fixture = Assert.Single(metadata.Fixtures, item => item.File == fileName);
        var path = Path.Combine(CorpusDirectory(), fileName);

        Assert.Equal(fixture.Sha256, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        Assert.Equal("GPL-2.0-or-later", metadata.License);
        Assert.StartsWith("https://raw.githubusercontent.com/Exiv2/exiv2/", fixture.Source, StringComparison.Ordinal);

        var result = await CodecProbeRunner.RunAsync("image", path);

        AssertCompleted(result, "Decoded");
        Assert.True(result.Payload!.Width > 0);
        Assert.True(result.Payload.Height > 0);
        Assert.InRange(result.PeakWorkingSetBytes, 1, CodecProbeRunner.MemoryLimitBytes);
    }

    [Theory]
    [Trait("Category", "AdversarialCorpus")]
    [InlineData("truncated.png")]
    [InlineData("truncated.jpg")]
    [InlineData("truncated.webp")]
    [InlineData("truncated.tiff")]
    [InlineData("truncated.gif")]
    [InlineData("malformed.svg")]
    [InlineData("dimension-bomb.png")]
    public async Task MalformedRaster_IsClassifiedWithoutCrashOrHang(string fileName)
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, fileName);
        File.WriteAllBytes(path, MalformedBytes(fileName));

        var result = await CodecProbeRunner.RunAsync("image", path);

        AssertCompleted(result, "Rejected");
        Assert.InRange(result.PeakWorkingSetBytes, 1, CodecProbeRunner.MemoryLimitBytes);
    }

    [Fact]
    [Trait("Category", "AdversarialCorpus")]
    public async Task ArchiveEntryFlood_IsRejectedInsideResourceGuard()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "entry-flood.cbz");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            for (var i = 0; i <= ArchiveBudgetPolicy.MaxEntryCount; i++)
                archive.CreateEntry($"notes/{i:D5}.txt");
        }

        var result = await CodecProbeRunner.RunAsync("archive", path);

        AssertCompleted(result, "Rejected");
    }

    [Fact]
    [Trait("Category", "AdversarialCorpus")]
    public async Task ArchiveCompressionBomb_IsRejectedInsideResourceGuard()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "ratio-bomb.cbz");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("page.png", CompressionLevel.Optimal);
            using var stream = entry.Open();
            stream.Write(new byte[8 * 1024 * 1024]);
        }

        var result = await CodecProbeRunner.RunAsync("archive", path);

        AssertCompleted(result, "Rejected");
    }

    [Fact]
    [Trait("Category", "AdversarialCorpus")]
    public void CorpusMetadata_RecordsLicenseAndHashesForEveryFixture()
    {
        var metadata = ReadMetadata();
        Assert.Equal(1, metadata.SchemaVersion);
        Assert.Equal(40, metadata.SourceCommit.Length);
        Assert.Equal(5, metadata.Fixtures.Count);

        var licensePath = Path.Combine(CorpusDirectory(), metadata.LicenseFile);
        Assert.Equal(metadata.LicenseSha256, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(licensePath))));
        foreach (var fixture in metadata.Fixtures)
        {
            Assert.Equal(64, fixture.Sha256.Length);
            Assert.False(string.IsNullOrWhiteSpace(fixture.Format));
            Assert.Contains(metadata.SourceCommit, fixture.Source, StringComparison.Ordinal);
        }
    }

    private static void AssertCompleted(CodecProbeExecution result, string expectedClassification)
    {
        Assert.True(
            result.Status == CodecProbeExecutionStatus.Completed,
            $"Probe status={result.Status}, exit={result.ExitCode}, peak={result.PeakWorkingSetBytes}, stderr={result.StandardError}");
        Assert.NotNull(result.Payload);
        Assert.Equal(expectedClassification, result.Payload.Classification);
    }

    private static byte[] MalformedBytes(string fileName)
        => fileName switch
        {
            "truncated.png" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48],
            "truncated.jpg" => [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 0x4A, 0x46, 0x49, 0x46],
            "truncated.webp" => "RIFF\x20\0\0\0WEBPVP8 "u8.ToArray(),
            "truncated.tiff" => [0x49, 0x49, 0x2A, 0, 8, 0, 0, 0, 1, 0],
            "truncated.gif" => "GIF89a\x10\0\x10\0"u8.ToArray(),
            "malformed.svg" => Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><g><path d='M 0 0'"),
            "dimension-bomb.png" => BuildPngDimensionBomb(),
            _ => throw new ArgumentOutOfRangeException(nameof(fileName)),
        };

    private static byte[] BuildPngDimensionBomb()
    {
        var bytes = new byte[33];
        byte[] prefix = [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0, 0, 0, 13,
            0x49, 0x48, 0x44, 0x52,
            0x7F, 0xFF, 0xFF, 0xFF,
            0x7F, 0xFF, 0xFF, 0xFF,
            8, 6, 0, 0, 0
        ];
        prefix.CopyTo(bytes, 0);
        return bytes;
    }

    private static CorpusMetadata ReadMetadata()
        => JsonSerializer.Deserialize<CorpusMetadata>(
               File.ReadAllText(Path.Combine(CorpusDirectory(), "metadata.json")),
               new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
           ?? throw new InvalidDataException("Codec corpus metadata is empty.");

    private static string CorpusDirectory()
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", "codec-corpus");

    private sealed record CorpusMetadata(
        int SchemaVersion,
        string SourceCommit,
        string License,
        string LicenseFile,
        string LicenseSha256,
        IReadOnlyList<CorpusFixture> Fixtures);

    private sealed record CorpusFixture(
        string File,
        string Format,
        string Sha256,
        string Source);
}
