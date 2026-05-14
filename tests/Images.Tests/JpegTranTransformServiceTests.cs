using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class JpegTranTransformServiceTests
{
    [Fact]
    public void BuildCropArguments_DropsStaleEmbeddedThumbnailsAndUsesOutfile()
    {
        var args = JpegTranTransformService.BuildCropArguments(
            new PixelSelection(16, 32, 48, 64),
            @"C:\temp\out.jpg",
            @"C:\temp\source.jpg");

        Assert.Equal(
            [
                "-copy",
                "icc",
                "-crop",
                "48x64+16+32",
                "-outfile",
                @"C:\temp\out.jpg",
                @"C:\temp\source.jpg"
            ],
            args);
    }

    [Fact]
    public void BuildRotateArguments_DropsStaleEmbeddedThumbnailsAndUsesOutfile()
    {
        var args = JpegTranTransformService.BuildRotateArguments(
            LosslessJpegRotation.Rotate90,
            @"C:\temp\rotated.jpg",
            @"C:\temp\source.jpg");

        Assert.Equal(
            [
                "-copy",
                "icc",
                "-rotate",
                "90",
                "-outfile",
                @"C:\temp\rotated.jpg",
                @"C:\temp\source.jpg"
            ],
            args);
    }

    [Fact]
    public void TryApplyExactCrop_WhenRuntimeAvailable_ReplacesSourceAndCleansTempFiles()
    {
        using var temp = TestDirectory.Create();
        var source = WriteJpeg(temp.Path, "source.jpg", 32, 32, MagickColors.Red);
        var originalBytes = File.ReadAllBytes(source);
        var runtime = RuntimeFor(temp);
        IReadOnlyList<string>? capturedArgs = null;

        var result = JpegTranTransformService.TryApplyExactCrop(
            source,
            new PixelSelection(0, 0, 16, 16),
            imageWidth: 32,
            imageHeight: 32,
            runtime,
            (_, args, _) =>
            {
                capturedArgs = args.ToList();
                WriteJpeg(PathFromOutfile(args), 16, 16, MagickColors.Blue);
                return new JpegTranProcessResult(0, "", "");
            });

        Assert.True(result.Attempted);
        Assert.True(result.Applied);
        Assert.NotEqual(originalBytes, File.ReadAllBytes(source));
        Assert.Equal((16, 16), ReadImageSize(source));
        var args = Assert.IsAssignableFrom<IReadOnlyList<string>>(capturedArgs);
        Assert.Contains("-crop", args);
        Assert.Contains("16x16+0+0", args);
        Assert.Empty(Directory.EnumerateFiles(temp.Path, ".images-jpegtran-*", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void TryApplyExactRotation_WhenRuntimeAvailable_ReplacesSourceAndCleansTempFiles()
    {
        using var temp = TestDirectory.Create();
        var source = WriteJpeg(temp.Path, "source.jpg", 32, 48, MagickColors.Red);
        var originalBytes = File.ReadAllBytes(source);
        var runtime = RuntimeFor(temp);
        IReadOnlyList<string>? capturedArgs = null;

        var result = JpegTranTransformService.TryApplyExactRotation(
            source,
            LosslessJpegRotation.Rotate90,
            imageWidth: 32,
            imageHeight: 48,
            runtime,
            (_, args, _) =>
            {
                capturedArgs = args.ToList();
                WriteJpeg(PathFromOutfile(args), 48, 32, MagickColors.Blue);
                return new JpegTranProcessResult(0, "", "");
            });

        Assert.True(result.Attempted);
        Assert.True(result.Applied);
        Assert.NotEqual(originalBytes, File.ReadAllBytes(source));
        Assert.Equal((48, 32), ReadImageSize(source));
        var args = Assert.IsAssignableFrom<IReadOnlyList<string>>(capturedArgs);
        Assert.Contains("-rotate", args);
        Assert.Contains("90", args);
        Assert.Empty(Directory.EnumerateFiles(temp.Path, ".images-jpegtran-*", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void TryApplyExactRotation_WhenRotationNeedsTrim_DoesNotRunJpegTran()
    {
        using var temp = TestDirectory.Create();
        var source = WriteJpeg(temp.Path, "source.jpg", 33, 48, MagickColors.Red);
        var runtime = RuntimeFor(temp);
        var invoked = false;

        var result = JpegTranTransformService.TryApplyExactRotation(
            source,
            LosslessJpegRotation.Rotate90,
            imageWidth: 33,
            imageHeight: 48,
            runtime,
            (_, _, _) =>
            {
                invoked = true;
                return new JpegTranProcessResult(0, "", "");
            });

        Assert.False(result.Attempted);
        Assert.False(result.Applied);
        Assert.False(invoked);
        Assert.Equal((33, 48), ReadImageSize(source));
    }

    [Fact]
    public void TryApplyExactRotation_WhenTrimConfirmed_RunsJpegTranWithTrim()
    {
        using var temp = TestDirectory.Create();
        var source = WriteJpeg(temp.Path, "source.jpg", 33, 48, MagickColors.Red);
        var runtime = RuntimeFor(temp);
        IReadOnlyList<string>? capturedArgs = null;

        var result = JpegTranTransformService.TryApplyExactRotation(
            source,
            LosslessJpegRotation.Rotate90,
            imageWidth: 33,
            imageHeight: 48,
            runtime,
            (_, args, _) =>
            {
                capturedArgs = args.ToList();
                WriteJpeg(PathFromOutfile(args), 48, 32, MagickColors.Blue);
                return new JpegTranProcessResult(0, "", "");
            },
            allowTrim: true);

        Assert.True(result.Attempted);
        Assert.True(result.Applied);
        Assert.Contains("confirmed MCU trim", result.Message);
        var args = Assert.IsAssignableFrom<IReadOnlyList<string>>(capturedArgs);
        Assert.Contains("-trim", args);
        Assert.Equal((48, 32), ReadImageSize(source));
    }

    [Fact]
    public void TryApplyExactCrop_WhenSelectionNeedsTrim_DoesNotRunJpegTran()
    {
        using var temp = TestDirectory.Create();
        var source = WriteJpeg(temp.Path, "source.jpg", 32, 32, MagickColors.Red);
        var runtime = RuntimeFor(temp);
        var invoked = false;

        var result = JpegTranTransformService.TryApplyExactCrop(
            source,
            new PixelSelection(1, 0, 16, 16),
            imageWidth: 32,
            imageHeight: 32,
            runtime,
            (_, _, _) =>
            {
                invoked = true;
                return new JpegTranProcessResult(0, "", "");
            });

        Assert.False(result.Attempted);
        Assert.False(result.Applied);
        Assert.False(invoked);
        Assert.Equal((32, 32), ReadImageSize(source));
    }

    [Fact]
    public void TryApplyExactCrop_WhenTrimConfirmed_RunsAlignedJpegTranCrop()
    {
        using var temp = TestDirectory.Create();
        var source = WriteJpeg(temp.Path, "source.jpg", 64, 64, MagickColors.Red);
        var runtime = RuntimeFor(temp);
        IReadOnlyList<string>? capturedArgs = null;

        var result = JpegTranTransformService.TryApplyExactCrop(
            source,
            new PixelSelection(1, 1, 63, 63),
            imageWidth: 64,
            imageHeight: 64,
            runtime,
            (_, args, _) =>
            {
                capturedArgs = args.ToList();
                WriteJpeg(PathFromOutfile(args), 48, 48, MagickColors.Blue);
                return new JpegTranProcessResult(0, "", "");
            },
            allowTrim: true);

        Assert.True(result.Attempted);
        Assert.True(result.Applied);
        Assert.Contains("confirmed MCU trim", result.Message);
        var args = Assert.IsAssignableFrom<IReadOnlyList<string>>(capturedArgs);
        Assert.Contains("48x48+16+16", args);
        Assert.Equal((48, 48), ReadImageSize(source));
    }

    [Fact]
    public void TryApplyExactCrop_WhenProcessFails_LeavesOriginalSource()
    {
        using var temp = TestDirectory.Create();
        var source = WriteJpeg(temp.Path, "source.jpg", 32, 32, MagickColors.Red);
        var originalBytes = File.ReadAllBytes(source);
        var runtime = RuntimeFor(temp);

        var result = JpegTranTransformService.TryApplyExactCrop(
            source,
            new PixelSelection(0, 0, 16, 16),
            imageWidth: 32,
            imageHeight: 32,
            runtime,
            (_, _, _) => new JpegTranProcessResult(2, "", "bad crop"));

        Assert.True(result.Attempted);
        Assert.False(result.Applied);
        Assert.Contains("bad crop", result.Message);
        Assert.Equal(originalBytes, File.ReadAllBytes(source));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, ".images-jpegtran-*", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void TryApplyExactCrop_WhenOutputValidationFails_LeavesOriginalSource()
    {
        using var temp = TestDirectory.Create();
        var source = WriteJpeg(temp.Path, "source.jpg", 32, 32, MagickColors.Red);
        var originalBytes = File.ReadAllBytes(source);
        var runtime = RuntimeFor(temp);

        var result = JpegTranTransformService.TryApplyExactCrop(
            source,
            new PixelSelection(0, 0, 16, 16),
            imageWidth: 32,
            imageHeight: 32,
            runtime,
            (_, args, _) =>
            {
                WriteJpeg(PathFromOutfile(args), 8, 8, MagickColors.Blue);
                return new JpegTranProcessResult(0, "", "");
            });

        Assert.True(result.Attempted);
        Assert.False(result.Applied);
        Assert.Contains("expected 16x16", result.Message);
        Assert.Equal(originalBytes, File.ReadAllBytes(source));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, ".images-jpegtran-*", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void Overwrite_WhenExactJpegCropAndRuntimeAvailable_UsesJpegTranPath()
    {
        using var temp = TestDirectory.Create();
        var source = WriteJpeg(temp.Path, "source.jpg", 32, 32, MagickColors.Red);
        var runtime = RuntimeFor(temp);
        var operation = new EditOperation(
            "crop",
            "crop",
            DateTimeOffset.UtcNow,
            Enabled: true,
            CropSelectionService.ToEditParameters(new PixelSelection(16, 16, 16, 16)),
            "Crop");
        var invoked = false;

        var savedPath = ImageExportService.Overwrite(
            source,
            [operation],
            runtime,
            (_, args, _) =>
            {
                invoked = true;
                Assert.Contains("16x16+16+16", args);
                WriteJpeg(PathFromOutfile(args), 16, 16, MagickColors.Green);
                return new JpegTranProcessResult(0, "", "");
            });

        Assert.True(invoked);
        Assert.Equal(Path.GetFullPath(source), savedPath);
        Assert.Equal((16, 16), ReadImageSize(source));
    }

    [Fact]
    public void Overwrite_WhenExactJpegRotationAndRuntimeAvailable_UsesJpegTranPath()
    {
        using var temp = TestDirectory.Create();
        var source = WriteJpeg(temp.Path, "source.jpg", 32, 48, MagickColors.Red);
        var runtime = RuntimeFor(temp);
        var operation = new EditOperation(
            "rotate",
            "rotate",
            DateTimeOffset.UtcNow,
            Enabled: true,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["degrees"] = "90"
            },
            "Rotate");
        var invoked = false;

        var savedPath = ImageExportService.Overwrite(
            source,
            [operation],
            runtime,
            (_, args, _) =>
            {
                invoked = true;
                Assert.Contains("-rotate", args);
                Assert.Contains("90", args);
                WriteJpeg(PathFromOutfile(args), 48, 32, MagickColors.Green);
                return new JpegTranProcessResult(0, "", "");
            });

        Assert.True(invoked);
        Assert.Equal(Path.GetFullPath(source), savedPath);
        Assert.Equal((48, 32), ReadImageSize(source));
    }

    [Fact]
    public void Overwrite_WhenConfirmedTrimJpegCrop_UsesAlignedJpegTranPath()
    {
        using var temp = TestDirectory.Create();
        var source = WriteJpeg(temp.Path, "source.jpg", 64, 64, MagickColors.Red);
        var runtime = RuntimeFor(temp);
        var operation = new EditOperation(
            "crop",
            "crop",
            DateTimeOffset.UtcNow,
            Enabled: true,
            CropSelectionService.ToEditParameters(new PixelSelection(1, 1, 63, 63)),
            "Crop");
        var invoked = false;

        var savedPath = ImageExportService.Overwrite(
            source,
            [operation],
            runtime,
            (_, args, _) =>
            {
                invoked = true;
                Assert.Contains("48x48+16+16", args);
                WriteJpeg(PathFromOutfile(args), 48, 48, MagickColors.Green);
                return new JpegTranProcessResult(0, "", "");
            },
            allowLosslessJpegTrim: true);

        Assert.True(invoked);
        Assert.Equal(Path.GetFullPath(source), savedPath);
        Assert.Equal((48, 48), ReadImageSize(source));
    }

    [Fact]
    public void Overwrite_WhenConfirmedTrimJpegRotation_UsesTrimmedJpegTranPath()
    {
        using var temp = TestDirectory.Create();
        var source = WriteJpeg(temp.Path, "source.jpg", 33, 48, MagickColors.Red);
        var runtime = RuntimeFor(temp);
        var operation = new EditOperation(
            "rotate",
            "rotate",
            DateTimeOffset.UtcNow,
            Enabled: true,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["degrees"] = "90"
            },
            "Rotate");
        var invoked = false;

        var savedPath = ImageExportService.Overwrite(
            source,
            [operation],
            runtime,
            (_, args, _) =>
            {
                invoked = true;
                Assert.Contains("-trim", args);
                WriteJpeg(PathFromOutfile(args), 48, 32, MagickColors.Green);
                return new JpegTranProcessResult(0, "", "");
            },
            allowLosslessJpegTrim: true);

        Assert.True(invoked);
        Assert.Equal(Path.GetFullPath(source), savedPath);
        Assert.Equal((48, 32), ReadImageSize(source));
    }

    [Fact]
    public void TryPlanLosslessJpegCropTrimConfirmation_WhenRuntimeAvailable_ReturnsTrimPlan()
    {
        using var temp = TestDirectory.Create();
        var source = WriteJpeg(temp.Path, "source.jpg", 64, 64, MagickColors.Red);

        var plan = ImageExportService.TryPlanLosslessJpegCropTrimConfirmation(
            source,
            new PixelSelection(1, 1, 63, 63),
            imageWidth: 64,
            imageHeight: 64,
            existingOperations: [],
            RuntimeFor(temp));

        Assert.NotNull(plan);
        Assert.True(plan.RequiresTrimConfirmation);
        Assert.Equal(new PixelSelection(16, 16, 48, 48), plan.AlignedSelection);
    }

    private static JpegTranRuntimeStatus RuntimeFor(TestDirectory temp)
    {
        var exe = temp.WriteFile("jpegtran.exe", "fake");
        return new JpegTranRuntimeStatus(
            Available: true,
            ExecutablePath: exe,
            Source: "test",
            Version: "test",
            Sha256: null,
            StatusText: "jpegtran test runtime");
    }

    private static string PathFromOutfile(IReadOnlyList<string> args)
    {
        var index = Enumerable.Range(0, args.Count)
            .Where(i => string.Equals(args[i], "-outfile", StringComparison.Ordinal))
            .DefaultIfEmpty(-1)
            .First();
        Assert.True(index >= 0 && index + 1 < args.Count);
        return args[index + 1];
    }

    private static string WriteJpeg(string folder, string name, uint width, uint height, IMagickColor<ushort> color)
        => WriteJpeg(Path.Combine(folder, name), width, height, color);

    private static string WriteJpeg(string path, uint width, uint height, IMagickColor<ushort> color)
    {
        using var image = new MagickImage(color, width, height)
        {
            Format = MagickFormat.Jpeg
        };
        image.Write(path);
        return path;
    }

    private static (int Width, int Height) ReadImageSize(string path)
    {
        using var image = new MagickImage(path);
        return ((int)image.Width, (int)image.Height);
    }
}
