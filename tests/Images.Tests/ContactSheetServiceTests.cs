using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class ContactSheetServiceTests
{
    [Fact]
    public void Plan_CreatesGridLayout()
    {
        var paths = Enumerable.Range(1, 7).Select(i => $@"C:\photos\img{i}.jpg").ToList();
        var service = new ContactSheetService();

        var plan = service.Plan(paths, new ContactSheetOptions(Columns: 3));

        Assert.Equal(7, plan.Cells.Count);
        Assert.Equal(3, plan.Columns);
        Assert.Equal(3, plan.Rows);
        Assert.True(plan.SheetWidthPx > 0);
        Assert.True(plan.SheetHeightPx > 0);
    }

    [Fact]
    public void Plan_EmptyList_ReturnsEmpty()
    {
        var service = new ContactSheetService();
        var plan = service.Plan([]);

        Assert.True(plan.IsEmpty);
        Assert.Empty(plan.Cells);
    }

    [Fact]
    public void Plan_AssignsCorrectColumnAndRow()
    {
        var paths = new[] { "a.jpg", "b.jpg", "c.jpg", "d.jpg", "e.jpg" };
        var service = new ContactSheetService();

        var plan = service.Plan(paths, new ContactSheetOptions(Columns: 2));

        Assert.Equal((0, 0), (plan.Cells[0].Column, plan.Cells[0].Row));
        Assert.Equal((1, 0), (plan.Cells[1].Column, plan.Cells[1].Row));
        Assert.Equal((0, 1), (plan.Cells[2].Column, plan.Cells[2].Row));
        Assert.Equal((1, 1), (plan.Cells[3].Column, plan.Cells[3].Row));
        Assert.Equal((0, 2), (plan.Cells[4].Column, plan.Cells[4].Row));
    }

    [Fact]
    public void Render_CreatesOutputFile()
    {
        using var temp = TestDirectory.Create();
        var img1 = WriteTestImage(temp.Path, "photo1.png");
        var img2 = WriteTestImage(temp.Path, "photo2.png");
        var outputPath = Path.Combine(temp.Path, "sheet.png");
        var service = new ContactSheetService();

        var plan = service.Plan([img1, img2], new ContactSheetOptions(Columns: 2, ThumbnailWidth: 64, ThumbnailHeight: 48));
        var result = service.Render(plan, outputPath);

        Assert.True(result.Success);
        Assert.True(File.Exists(outputPath));
        Assert.Equal(2, result.CellCount);

        using var output = new MagickImage(outputPath);
        Assert.True(output.Width > 0);
        Assert.True(output.Height > 0);
    }

    [Fact]
    public void Render_HandlesUnreadableImages()
    {
        using var temp = TestDirectory.Create();
        var good = WriteTestImage(temp.Path, "good.png");
        var bad = Path.Combine(temp.Path, "bad.png");
        File.WriteAllText(bad, "not an image");
        var outputPath = Path.Combine(temp.Path, "sheet.png");
        var service = new ContactSheetService();

        var plan = service.Plan([good, bad], new ContactSheetOptions(Columns: 2, ThumbnailWidth: 64, ThumbnailHeight: 48));
        var result = service.Render(plan, outputPath);

        Assert.True(result.Success);
        Assert.Equal(2, result.CellCount);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public void Plan_SummaryDescribesGrid()
    {
        var paths = Enumerable.Range(1, 6).Select(i => $"img{i}.jpg").ToList();
        var service = new ContactSheetService();
        var plan = service.Plan(paths, new ContactSheetOptions(Columns: 3));

        Assert.Contains("6 images", plan.Summary);
        Assert.Contains("3x2", plan.Summary);
    }

    private static string WriteTestImage(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(MagickColors.Blue, 32, 24) { Format = MagickFormat.Png };
        image.Write(path);
        return path;
    }
}
