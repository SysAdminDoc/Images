using System.Collections.ObjectModel;
using System.IO;
using Images.Services;
using Windows.Media.Ocr;

namespace Images.ViewModels;

public sealed class OcrWorkflowController : ObservableObject, IDisposable
{
    public const string NoImageMessage = "No image loaded";
    public const string ExtractingMessage = "Extracting text locally...";
    public const string CanceledMessage = "Text extraction canceled";
    public const string HiddenMessage = "Text overlay hidden";
    public const string UnavailableMessage = "OCR unavailable — no Windows OCR language pack installed";
    public const string NoTextFoundMessage = "No text found";
    public const string FailureMessage = "Text extraction failed";

    private readonly Func<string?> _currentPath;
    private readonly Func<bool> _hasImage;
    private readonly Func<string, CancellationToken, Task<IReadOnlyList<OcrTextLine>?>> _extractLinesAsync;
    private readonly Action<string> _notify;
    private readonly Action<Exception> _logError;
    private bool _isOcrMode;
    private bool _isOcrBusy;
    private ObservableCollection<OcrTextLine>? _ocrOverlayLines;
    private CancellationTokenSource? _ocrCts;
    private int _ocrGeneration;

    public OcrWorkflowController(
        Func<string?> currentPath,
        Func<bool> hasImage,
        Action<string> notify,
        OcrService? ocrService = null,
        Func<string, CancellationToken, Task<IReadOnlyList<OcrTextLine>?>>? extractLinesAsync = null,
        Action<Exception>? logError = null)
    {
        _currentPath = currentPath ?? throw new ArgumentNullException(nameof(currentPath));
        _hasImage = hasImage ?? throw new ArgumentNullException(nameof(hasImage));
        _notify = notify ?? throw new ArgumentNullException(nameof(notify));
        _logError = logError ?? (_ => { });

        var service = ocrService ?? new OcrService();
        _extractLinesAsync = extractLinesAsync ?? ((path, token) => ExtractLinesFromFileAsync(path, service, token));
    }

    public bool IsOcrMode
    {
        get => _isOcrMode;
        set
        {
            if (Set(ref _isOcrMode, value))
            {
                Raise(nameof(OcrModeTooltip));
                RaiseOcrStatusState();
            }
        }
    }

    public bool IsOcrBusy
    {
        get => _isOcrBusy;
        private set
        {
            if (Set(ref _isOcrBusy, value))
            {
                Raise(nameof(OcrModeTooltip));
                RaiseOcrStatusState();
            }
        }
    }

    public bool ShowOcrStatusPanel => IsOcrBusy || IsOcrMode;

    public string OcrModeTooltip => IsOcrBusy
        ? "Cancel text extraction (E)"
        : IsOcrMode
            ? "Hide text overlay (E)"
            : "Extract text locally (E)";

    public string OcrStatusTitle => IsOcrBusy
        ? "Extracting text locally"
        : "Text overlay active";

    public string OcrStatusDetail => IsOcrBusy
        ? "Windows OCR is reading this image on your PC. Press E again to cancel."
        : $"{OcrRegionCountText}. Select a text box and press Ctrl+C to copy.";

    public string OcrRegionCountText
    {
        get
        {
            var count = OcrOverlayLines?.Count ?? 0;
            return count == 1 ? "1 text region found" : $"{count} text regions found";
        }
    }

    public ObservableCollection<OcrTextLine>? OcrOverlayLines
    {
        get => _ocrOverlayLines;
        set
        {
            if (Set(ref _ocrOverlayLines, value))
                RaiseOcrStatusState();
        }
    }

    public async Task ToggleAsync()
    {
        var path = _currentPath();
        if (path is null || !_hasImage())
        {
            _notify(NoImageMessage);
            return;
        }

        if (IsOcrMode || _ocrCts is not null)
        {
            var wasBusy = IsOcrBusy || _ocrCts is not null;
            Clear(cancelExtraction: true);
            _notify(wasBusy ? CanceledMessage : HiddenMessage);
            return;
        }

        await ExtractAsync(path).ConfigureAwait(true);
    }

    public void Clear(bool cancelExtraction)
    {
        if (cancelExtraction)
        {
            _ocrGeneration++;
            _ocrCts?.Cancel();
            _ocrCts = null;
        }

        OcrOverlayLines = null;
        IsOcrMode = false;
        IsOcrBusy = false;
    }

    private async Task ExtractAsync(string path)
    {
        var generation = ++_ocrGeneration;
        var cts = new CancellationTokenSource();
        _ocrCts = cts;
        IsOcrBusy = true;
        _notify(ExtractingMessage);

        try
        {
            var lines = await _extractLinesAsync(path, cts.Token).ConfigureAwait(true);
            if (cts.IsCancellationRequested ||
                generation != _ocrGeneration ||
                !string.Equals(path, _currentPath(), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (lines is null)
            {
                _notify(UnavailableMessage);
                IsOcrMode = false;
                return;
            }

            if (lines.Count == 0)
            {
                _notify(NoTextFoundMessage);
                IsOcrMode = false;
                return;
            }

            OcrOverlayLines = new ObservableCollection<OcrTextLine>(lines);
            IsOcrMode = true;
            _notify($"{lines.Count} text region{(lines.Count == 1 ? "" : "s")} found");
        }
        catch (OperationCanceledException)
        {
            IsOcrMode = false;
        }
        catch (Exception ex)
        {
            _logError(ex);
            _notify(FailureMessage);
            IsOcrMode = false;
        }
        finally
        {
            if (ReferenceEquals(_ocrCts, cts))
                _ocrCts = null;

            IsOcrBusy = false;
            cts.Dispose();
        }
    }

    private static async Task<IReadOnlyList<OcrTextLine>?> ExtractLinesFromFileAsync(
        string path,
        OcrService ocrService,
        CancellationToken token)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var result = await ocrService.ExtractTextAsync(fileStream, token).ConfigureAwait(false);
        return result is null ? null : ConvertResultToLines(result);
    }

    private static IReadOnlyList<OcrTextLine> ConvertResultToLines(OcrResult result)
    {
        var lines = new List<OcrTextLine>();
        foreach (var line in result.Lines)
        {
            if (line.Words.Count == 0 || string.IsNullOrWhiteSpace(line.Text))
                continue;

            var left = line.Words.Min(word => word.BoundingRect.Left);
            var top = line.Words.Min(word => word.BoundingRect.Top);
            var right = line.Words.Max(word => word.BoundingRect.Right);
            var bottom = line.Words.Max(word => word.BoundingRect.Bottom);
            lines.Add(new OcrTextLine
            {
                Text = line.Text,
                BoundingBox = new Windows.Foundation.Rect(
                    left,
                    top,
                    Math.Max(1, right - left),
                    Math.Max(1, bottom - top))
            });
        }

        return lines;
    }

    private void RaiseOcrStatusState()
    {
        Raise(nameof(ShowOcrStatusPanel));
        Raise(nameof(OcrStatusTitle));
        Raise(nameof(OcrStatusDetail));
        Raise(nameof(OcrRegionCountText));
    }

    public void Dispose()
    {
        Clear(cancelExtraction: true);
    }
}

/// <summary>
/// Represents one line of OCR text with an image-pixel bounding box for overlay rendering.
/// </summary>
public class OcrTextLine : ObservableObject
{
    public string Text { get; set; } = string.Empty;
    public Windows.Foundation.Rect BoundingBox { get; set; }
}
