using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class DuplicateCleanupServiceTests
{
    [Fact]
    public void Scan_GroupsExactHashDuplicates()
    {
        using var temp = TestDirectory.Create();
        var first = Path.Combine(temp.Path, "first.png");
        var second = Path.Combine(temp.Path, "second.png");
        WriteImage(first, MagickColors.Red);
        File.Copy(first, second);
        WriteImage(Path.Combine(temp.Path, "different.png"), MagickColors.Blue);

        var result = new DuplicateCleanupService().Scan([temp.Path], similarityThreshold: 4);

        var exact = Assert.Single(result.Findings, finding => finding.Kind == DuplicateCleanupFindingKind.ExactDuplicate);
        Assert.Equal(2, exact.CandidateCount);
        Assert.Contains(exact.Candidates, candidate => candidate.FileName == "first.png");
        Assert.Contains(exact.Candidates, candidate => candidate.FileName == "second.png");
        Assert.Equal(3, result.FileCount);
        Assert.Equal(1, result.ExactGroupCount);
    }

    [Fact]
    public void Scan_FindsPerceptuallySimilarImagesWhenHashesDiffer()
    {
        using var temp = TestDirectory.Create();
        WritePatternedImage(Path.Combine(temp.Path, "pattern-a.png"), variant: false);
        WritePatternedImage(Path.Combine(temp.Path, "pattern-b.png"), variant: true);

        var result = new DuplicateCleanupService().Scan([temp.Path], similarityThreshold: 6);

        var similar = Assert.Single(result.Findings, finding => finding.Kind == DuplicateCleanupFindingKind.SimilarImage);
        Assert.Equal(2, similar.CandidateCount);
        Assert.True(similar.Distance <= 6);
        Assert.Contains("pattern", similar.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scan_SortsReferenceFolderCandidateAsPreferredKeep()
    {
        using var temp = TestDirectory.Create();
        var scan = Directory.CreateDirectory(Path.Combine(temp.Path, "scan"));
        var reference = Directory.CreateDirectory(Path.Combine(temp.Path, "reference"));
        var scanCopy = Path.Combine(scan.FullName, "copy.png");
        var referenceCopy = Path.Combine(reference.FullName, "original.png");
        WriteImage(referenceCopy, MagickColors.Green);
        File.Copy(referenceCopy, scanCopy);

        var result = new DuplicateCleanupService().Scan([temp.Path], [reference.FullName], similarityThreshold: 4);

        var exact = Assert.Single(result.Findings, finding => finding.Kind == DuplicateCleanupFindingKind.ExactDuplicate);
        Assert.True(exact.Candidates[0].IsReference);
        Assert.Equal(referenceCopy, exact.Candidates[0].Path);
        Assert.Equal(scanCopy, exact.ExtraCandidates[0].Path);
    }

    [Fact]
    public void Quarantine_MovesFilesIntoUniqueBatchAndWritesManifest()
    {
        using var temp = TestDirectory.Create();
        var quarantineRoot = Path.Combine(temp.Path, "quarantine-root");
        var first = Path.Combine(temp.Path, "first.png");
        var second = Path.Combine(temp.Path, "second.png");
        WriteImage(first, MagickColors.Yellow);
        WriteImage(second, MagickColors.Blue);
        var service = new DuplicateCleanupService(() => quarantineRoot);

        var result = service.Quarantine([first, second]);

        Assert.True(result.IsAvailable);
        Assert.NotNull(result.BatchDirectory);
        Assert.Equal(2, result.MovedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.False(File.Exists(first));
        Assert.False(File.Exists(second));
        Assert.All(result.Moved, moved => Assert.True(File.Exists(moved.DestinationPath)));
        Assert.True(File.Exists(Path.Combine(result.BatchDirectory!, "manifest.tsv")));
    }

    [Fact]
    public void HammingDistance_CountsChangedBits()
    {
        Assert.Equal(4, DuplicateCleanupService.HammingDistance(0b1111UL, 0b0000UL));
    }

    private static void WriteImage(string path, IMagickColor<ushort> color)
    {
        using var image = new MagickImage(color, 16, 16)
        {
            Format = MagickFormat.Png
        };
        image.Write(path);
    }

    private static void WritePatternedImage(string path, bool variant)
    {
        const int width = 16;
        const int height = 16;
        const int stride = width * 4;
        var pixels = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * stride) + (x * 4);
                var value = (byte)((x * 9) + (y * 5));
                if (variant && x is >= 6 and <= 9 && y is >= 6 and <= 9)
                    value = (byte)Math.Min(255, value + 18);

                pixels[offset] = (byte)Math.Max(0, value - 8);
                pixels[offset + 1] = value;
                pixels[offset + 2] = (byte)Math.Min(255, value + 10);
                pixels[offset + 3] = 0xFF;
            }
        }

        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        bitmap.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
