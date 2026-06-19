using System.IO;
using ImageMagick;
using ImageMagick.Drawing;
using Microsoft.Extensions.Logging;

namespace Images.Services;

public sealed record ContactSheetOptions(
    int Columns = 4,
    int ThumbnailWidth = 300,
    int ThumbnailHeight = 225,
    int MarginPx = 20,
    int CaptionHeightPx = 24,
    bool ShowFilename = true,
    bool ShowRating = false,
    bool ShowDimensions = false,
    string? WatermarkText = null,
    MagickColor? BackgroundColor = null)
{
    public MagickColor EffectiveBackground => BackgroundColor ?? new MagickColor("#1E1E2E");
}

public sealed record ContactSheetPlan(
    IReadOnlyList<ContactSheetCell> Cells,
    int Columns,
    int Rows,
    int SheetWidthPx,
    int SheetHeightPx,
    string Summary)
{
    public bool IsEmpty => Cells.Count == 0;
}

public sealed record ContactSheetCell(
    string SourcePath,
    string FileName,
    string CaptionText,
    int Column,
    int Row);

public sealed record ContactSheetResult(
    bool Success,
    string? OutputPath,
    int CellCount,
    string Message);

public sealed class ContactSheetService
{
    private static readonly ILogger _log = Log.Get(nameof(ContactSheetService));

    public ContactSheetPlan Plan(
        IReadOnlyList<string> imagePaths,
        ContactSheetOptions? options = null)
    {
        options ??= new ContactSheetOptions();
        if (imagePaths.Count == 0)
            return new ContactSheetPlan([], 0, 0, 0, 0, "No images selected.");

        var cols = Math.Max(1, options.Columns);
        var rows = (int)Math.Ceiling((double)imagePaths.Count / cols);
        var cellW = options.ThumbnailWidth + options.MarginPx;
        var cellH = options.ThumbnailHeight + options.CaptionHeightPx + options.MarginPx;
        var sheetW = cols * cellW + options.MarginPx;
        var sheetH = rows * cellH + options.MarginPx;

        var cells = new List<ContactSheetCell>();
        for (var i = 0; i < imagePaths.Count; i++)
        {
            var path = imagePaths[i];
            var fileName = Path.GetFileName(path);
            var caption = BuildCaption(path, fileName, options);
            cells.Add(new ContactSheetCell(path, fileName, caption, i % cols, i / cols));
        }

        return new ContactSheetPlan(cells, cols, rows, sheetW, sheetH,
            $"{cells.Count} images in {cols}x{rows} grid ({sheetW}x{sheetH} px).");
    }

    public ContactSheetResult Render(
        ContactSheetPlan plan,
        string outputPath,
        ContactSheetOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ContactSheetOptions();

        if (plan.IsEmpty)
            return new ContactSheetResult(false, null, 0, "No cells to render.");

        try
        {
            using var sheet = new MagickImage(
                options.EffectiveBackground,
                (uint)plan.SheetWidthPx,
                (uint)plan.SheetHeightPx);

            var drawables = new Drawables();
            drawables.FillColor(new MagickColor("#CDD6F4"));
            drawables.Font("Segoe UI");
            drawables.FontPointSize(11);

            foreach (var cell in plan.Cells)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var x = options.MarginPx + cell.Column * (options.ThumbnailWidth + options.MarginPx);
                var y = options.MarginPx + cell.Row * (options.ThumbnailHeight + options.CaptionHeightPx + options.MarginPx);

                try
                {
                    using var thumb = new MagickImage(cell.SourcePath);
                    thumb.Resize(new MagickGeometry((uint)options.ThumbnailWidth, (uint)options.ThumbnailHeight)
                    {
                        FillArea = false,
                        IgnoreAspectRatio = false
                    });

                    var offsetX = x + (options.ThumbnailWidth - (int)thumb.Width) / 2;
                    var offsetY = y + (options.ThumbnailHeight - (int)thumb.Height) / 2;
                    sheet.Composite(thumb, offsetX, offsetY, CompositeOperator.Over);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.LogDebug(ex, "Could not load thumbnail for contact sheet cell: {Path}", cell.SourcePath);
                    drawables.FillColor(new MagickColor("#45475A"));
                    drawables.Rectangle(x, y, x + options.ThumbnailWidth, y + options.ThumbnailHeight);
                    drawables.FillColor(new MagickColor("#CDD6F4"));
                }

                if (!string.IsNullOrWhiteSpace(cell.CaptionText))
                {
                    var captionY = y + options.ThumbnailHeight + options.CaptionHeightPx - 4;
                    drawables.Text(x, captionY, Truncate(cell.CaptionText, 40));
                }
            }

            if (!string.IsNullOrWhiteSpace(options.WatermarkText))
            {
                drawables.FillColor(new MagickColor("#80CDD6F4"));
                drawables.FontPointSize(14);
                drawables.Gravity(Gravity.Southeast);
                drawables.Text(options.MarginPx, options.MarginPx, options.WatermarkText);
            }

            drawables.Draw(sheet);

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            sheet.Write(outputPath);

            return new ContactSheetResult(true, outputPath, plan.Cells.Count,
                $"Contact sheet saved: {plan.Cells.Count} images, {plan.SheetWidthPx}x{plan.SheetHeightPx} px.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Contact sheet render failed");
            return new ContactSheetResult(false, null, 0, $"Render failed: {ex.Message}");
        }
    }

    private static string BuildCaption(string path, string fileName, ContactSheetOptions options)
    {
        var parts = new List<string>();
        if (options.ShowFilename) parts.Add(fileName);

        if (options.ShowDimensions)
        {
            try
            {
                using var probe = new MagickImage();
                probe.Ping(path);
                parts.Add($"{probe.Width}x{probe.Height}");
            }
            catch { }
        }

        return string.Join(" | ", parts);
    }

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
}
