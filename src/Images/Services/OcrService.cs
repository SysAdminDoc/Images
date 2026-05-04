using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using Microsoft.Extensions.Logging;

namespace Images.Services;

/// <summary>
/// OCR text extraction service using Windows.Media.Ocr (local, privacy-first).
/// </summary>
public class OcrService
{
    private static readonly ILogger _log = Log.For<OcrService>();
    private OcrEngine? _cachedEngine;

    /// <summary>
    /// Extract text from an image stream using Windows OCR.
    /// </summary>
    public async Task<OcrResult?> ExtractTextAsync(Stream imageStream)
    {
        try
        {
            // Convert Stream to IRandomAccessStream (WinRT requirement)
            using var memStream = new InMemoryRandomAccessStream();
            await imageStream.CopyToAsync(memStream.AsStreamForWrite());
            memStream.Seek(0);

            // Decode image to SoftwareBitmap
            var decoder = await BitmapDecoder.CreateAsync(memStream);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            // Convert to compatible pixel format if needed (OCR requires Bgra8 or Gray8)
            if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 &&
                softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Gray8)
            {
                softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8);
            }

            // Get or create OCR engine
            _cachedEngine ??= OcrEngine.TryCreateFromUserProfileLanguages();
            if (_cachedEngine == null)
            {
                _log.LogError("OCR engine unavailable — no language packs installed");
                return null;
            }

            // Recognize text
            var result = await _cachedEngine.RecognizeAsync(softwareBitmap);
            return result;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "OCR extraction failed: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Get list of languages available for OCR on this system.
    /// English is guaranteed; others require Windows language packs.
    /// </summary>
    public IReadOnlyList<Windows.Globalization.Language> GetAvailableLanguages()
    {
        return OcrEngine.AvailableRecognizerLanguages.ToList();
    }

    /// <summary>
    /// Check if OCR is available (at least one language installed).
    /// </summary>
    public bool IsAvailable()
    {
        return OcrEngine.AvailableRecognizerLanguages.Count > 0;
    }
}
