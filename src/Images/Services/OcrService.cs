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
    public async Task<OcrResult?> ExtractTextAsync(Stream imageStream, CancellationToken ct = default)
    {
        try
        {
            if (imageStream is null || !imageStream.CanRead)
                throw new InvalidOperationException("OCR input stream is not readable.");

            // Convert Stream to IRandomAccessStream (WinRT requirement).
            // Keep the write adapter alive until after WinRT has decoded the stream;
            // disposing it closes the underlying InMemoryRandomAccessStream.
            using var memStream = new InMemoryRandomAccessStream();
            using var writer = memStream.AsStreamForWrite();
            await imageStream.CopyToAsync(writer, ct).ConfigureAwait(false);
            await writer.FlushAsync(ct).ConfigureAwait(false);
            memStream.Seek(0);

            // Decode image to SoftwareBitmap
            var decoder = await BitmapDecoder.CreateAsync(memStream).AsTask(ct).ConfigureAwait(false);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync().AsTask(ct).ConfigureAwait(false);

            try
            {
                // Convert to compatible pixel format if needed (OCR requires Bgra8 or Gray8)
                if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 &&
                    softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Gray8)
                {
                    var converted = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8);
                    softwareBitmap.Dispose();
                    softwareBitmap = converted;
                }

                // Get or create OCR engine
                _cachedEngine ??= OcrEngine.TryCreateFromUserProfileLanguages();
                if (_cachedEngine == null)
                {
                    _log.LogError("OCR engine unavailable — no Windows OCR language pack installed");
                    return null;
                }

                // Recognize text
                var result = await _cachedEngine.RecognizeAsync(softwareBitmap).AsTask(ct).ConfigureAwait(false);
                return result;
            }
            finally
            {
                softwareBitmap.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "OCR extraction failed: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Get list of languages available for OCR on this system.
    /// Languages come from installed Windows OCR optional capabilities.
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
