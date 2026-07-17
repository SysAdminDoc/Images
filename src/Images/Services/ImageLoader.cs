using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;

namespace Images.Services;

/// <summary>
/// Loads an image from disk. Tries WIC first for native Windows codecs, falls back to Magick.NET
/// for broad raster/RAW/vector coverage, and routes PostScript-family documents through the
/// configured Ghostscript runtime when available.
/// </summary>
public static class ImageLoader
{
    /// <summary>
    /// Single-image load result. <see cref="Animation"/> is non-null only when the file decoded
    /// as a multi-frame sequence (animated GIF today; animated WebP/APNG whenever the Magick
    /// build supports them). The <see cref="Image"/> always carries the first frame so static
    /// metadata/size readouts keep working.
    /// </summary>
    public sealed record FormatMismatchInfo(string DetectedFormat, string FileExtension, string SuggestedExtension);

    public sealed record LoadResult(
        ImageSource Image,
        int PixelWidth,
        int PixelHeight,
        string DecoderUsed,
        AnimationSequence? Animation = null,
        PageSequence? Pages = null,
        TilePyramidInfo? TilePyramid = null,
        FormatMismatchInfo? FormatMismatch = null,
        MotionPhotoInfo? MotionPhoto = null,
        bool IsPreview = false,
        bool WicJpegSecurityFallback = false);

    // Extensions worth probing for animated content. Pure-photo formats (JPEG, RAW, etc.) skip the
    // MagickImageCollection path so we don't pay for a second decoder on every single-frame image.
    private static readonly HashSet<string> AnimatedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".gif", ".webp", ".apng", ".png"
    };

    private static readonly HashSet<string> PagedRasterExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tif", ".tiff", ".psd", ".psb", ".ico", ".cur", ".dcx", ".dcm", ".dicom",
        ".fits", ".fit", ".fts", ".jpm"
    };

    // Files at or above this size skip the managed byte[] path and decode directly from a
    // memory-mapped view. Keeps a 500 MB RAW or multi-GB PSD off the LOH entirely — the OS pages
    // the mapping in on demand and can evict clean pages without touching swap.
    private const long MemoryMapThreshold = 256L * 1024 * 1024;
    private const int DocumentPreviewDpi = 144;
    private const int StableReadRetryCount = 3;
    private const int StableReadRetryDelayMs = 80;
    private static readonly BitmapSource TilePlaceholder = CreateTilePlaceholder();

    private static volatile bool _colorManagedDisplay;
    private static volatile ToneMapOperator _toneMapOperator = ToneMapOperator.Reinhard;

    /// <summary>
    /// RD-02: when true, normal raster loads are decoded through Magick.NET and any embedded ICC
    /// profile is transformed to the active legacy monitor profile, or to sRGB when Windows
    /// Advanced Color is active/unavailable. Opt-in (default off) because it routes the fast WIC
    /// path through Magick; huge (memory-mapped) and tiled images are exempt.
    /// Volatile so background decode threads observe UI-thread toggles without a torn/stale read.
    /// </summary>
    public static bool ColorManagedDisplay
    {
        get => _colorManagedDisplay;
        set => _colorManagedDisplay = value;
    }

    public static ToneMapOperator HdrToneMapOperator
    {
        get => _toneMapOperator;
        set => _toneMapOperator = value;
    }

    public static LoadResult Load(
        string path,
        int pageIndex = 0,
        bool archiveSpreadMode = false,
        bool archiveRightToLeft = false,
        string? archivePassword = null)
    {
        var fi = new FileInfo(path);
        if (!fi.Exists)
            throw new FileNotFoundException($"'{Path.GetFileName(path)}' does not exist.", path);
        if (fi.Length == 0)
            throw new InvalidOperationException($"'{Path.GetFileName(path)}' is empty.");

        var mismatch = DetectFormatMismatch(path);

        if (SupportedImageFormats.IsArchive(path))
            return WithMismatch(LoadArchivePreview(path, pageIndex, archiveSpreadMode, archiveRightToLeft, archivePassword), mismatch);

        if (SupportedImageFormats.RequiresGhostscript(path))
            return WithMismatch(LoadDocumentPreview(path, pageIndex), mismatch);

        var preflight = TileService.Preflight(path);
        switch (preflight.Status)
        {
            case ImagePreflightStatus.Small:
                break;
            case ImagePreflightStatus.Large:
                return WithColorManagementNote(WithMismatch(LoadTilePyramid(path, pageIndex), mismatch));
            case ImagePreflightStatus.Rejected:
                throw new InvalidOperationException(
                    $"Images refused to decode '{Path.GetFileName(path)}' because its dimensions could not be read safely. The file may be corrupt or unsupported.");
            case ImagePreflightStatus.Unknown:
                throw new IOException(
                    $"Images could not safely inspect '{Path.GetFileName(path)}' before decoding. The file was not opened.");
            default:
                throw new IOException(
                    $"Images could not classify '{Path.GetFileName(path)}' safely before decoding. The file was not opened.");
        }

        if (PagedRasterExtensions.Contains(Path.GetExtension(path)))
        {
            var paged = TryLoadPagedRaster(path, pageIndex);
            if (paged is not null) return WithMismatch(paged, mismatch);
        }

        // V20-06: large files bypass the byte[] round-trip entirely. Animation probe is skipped
        // too — a 256 MB+ GIF is a pathological edge case and multi-frame decode needs random
        // access to frame offsets that MMF serves fine from a single view.
        if (fi.Length >= MemoryMapThreshold)
            return WithColorManagementNote(WithMotionPhoto(WithMismatch(LoadFromMemoryMapped(path), mismatch), path));

        // Load the file into memory first so we never hold a lock on the original (rename/delete must work).
        var bytes = ReadStableFileBytes(path);

        return WithMotionPhoto(WithMismatch(LoadRasterBytes(bytes, Path.GetFileName(path), Path.GetExtension(path)), mismatch), path);
    }

    private static FormatMismatchInfo? DetectFormatMismatch(string path)
    {
        var result = FormatSignatureDetector.DetectMismatch(path);
        if (result is null) return null;
        var (sig, ext) = result.Value;
        return new FormatMismatchInfo(sig.FormatName, ext, sig.SuggestedExtension);
    }

    private static LoadResult WithMismatch(LoadResult result, FormatMismatchInfo? mismatch)
        => mismatch is null ? result : result with { FormatMismatch = mismatch };

    // The tile and memory-mapped paths cannot run the embedded-ICC->sRGB transform; when color
    // management is on, note in the decoder string that it was not applied to this large image.
    private static LoadResult WithColorManagementNote(LoadResult result)
        => ColorManagedDisplay
            ? result with { DecoderUsed = $"{result.DecoderUsed} · color management not applied" }
            : result;

    private static LoadResult WithMotionPhoto(LoadResult result, string path)
    {
        var info = MotionPhotoService.Detect(path);
        return info is null ? result : result with { MotionPhoto = info };
    }

    internal static LoadResult LoadRasterBytes(
        byte[] bytes,
        string displayName,
        string extension,
        WicJpegSecurityStatus? wicSecurityStatus = null)
    {
        // Animation probe first — only for formats that actually support it. If the file is a
        // multi-frame GIF / animated WebP / APNG, return the full sequence so the canvas can play it.
        if (AnimatedExtensions.Contains(extension))
        {
            var animated = TryLoadAnimated(bytes);
            if (animated is not null) return animated;
        }

        if (ToneMapService.ShouldProbe(bytes, extension))
        {
            var toneMapped = TryLoadToneMapped(bytes, displayName, extension);
            if (toneMapped is not null) return toneMapped;
        }

        // RD-02: color-managed decode (opt-in). Falls through to the normal WIC/Magick path if it
        // fails so a decode error never costs the image.
        if (ColorManagedDisplay)
        {
            var managed = TryLoadColorManaged(bytes, displayName, extension);
            if (managed is not null) return managed;
        }

        var evtSrc = ImageEventSource.Instance;
        evtSrc.RecordDecodeAttempt();
        var sw = Stopwatch.StartNew();
        var bypassWicJpeg = WicJpegSecurityPolicy.ShouldBypassWic(extension, wicSecurityStatus);
        if (!bypassWicJpeg)
            evtSrc.DecodeStarted(displayName, "WIC");

        // Primary: WIC via BitmapImage. CacheOption.OnLoad fully reads the stream during EndInit,
        // so the MemoryStream can be disposed immediately after.
        if (!bypassWicJpeg)
        {
            try
            {
                using var ms = new MemoryStream(bytes, writable: false);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();

                sw.Stop();
                evtSrc.RecordWicDecode();
                evtSrc.RecordDecodeDuration(sw.Elapsed.TotalMilliseconds);
                evtSrc.DecodeCompleted(displayName, "WIC", (long)sw.Elapsed.TotalMilliseconds);
                return new LoadResult(bmp, bmp.PixelWidth, bmp.PixelHeight, "WIC");
            }
            // Narrow: only fall through for decode/format failures. Let OOM, stack overflow,
            // and thread aborts bubble up — those aren't "try the other decoder" situations.
            catch (Exception ex) when (
                ex is NotSupportedException or
                      System.Runtime.InteropServices.COMException or
                      FileFormatException or
                      InvalidOperationException or
                      ArgumentException)
            {
                // Fall through to Magick.NET
            }
        }

        // Fallback: Magick.NET decodes to BGRA bytes → WriteableBitmap.
        sw.Restart();
        evtSrc.DecodeStarted(
            displayName,
            bypassWicJpeg ? "Magick.NET (WIC JPEG security fallback)" : "Magick.NET");
        try
        {
            using var image = MagickSafeReader.Read(bytes, extension);
            var toneMapped = ToneMapService.ApplyIfNeeded(image, extension, HdrToneMapOperator);
            var wb = MagickToBitmap(image);

            sw.Stop();
            evtSrc.RecordMagickFallbackDecode();
            evtSrc.RecordDecodeDuration(sw.Elapsed.TotalMilliseconds);
            var decoder = WithToneMapStatus("Magick.NET", toneMapped);
            if (bypassWicJpeg)
                decoder += " · WIC JPEG disabled; Windows update recommended";
            evtSrc.DecodeCompleted(displayName, decoder, (long)sw.Elapsed.TotalMilliseconds);
            return new LoadResult(
                wb,
                wb.PixelWidth,
                wb.PixelHeight,
                decoder,
                WicJpegSecurityFallback: bypassWicJpeg);
        }
        catch (Exception ex)
        {
            sw.Stop();
            evtSrc.RecordDecodeFailure();
            evtSrc.DecodeFailed(displayName, ex.Message);
            throw new InvalidOperationException($"Could not decode '{displayName}': {ex.Message}", ex);
        }
    }

    private static LoadResult? TryLoadToneMapped(byte[] bytes, string displayName, string extension)
    {
        var evtSrc = ImageEventSource.Instance;
        evtSrc.RecordDecodeAttempt();
        var sw = Stopwatch.StartNew();
        evtSrc.DecodeStarted(displayName, "Magick.NET (HDRI)");
        try
        {
            using var image = MagickSafeReader.Read(bytes, extension);
            if (!ToneMapService.ApplyIfNeeded(image, extension, HdrToneMapOperator))
                return null;

            var colorTarget = ColorManagedDisplay ? TransformForDisplayIfProfiled(image) : null;
            var wb = MagickToBitmap(image);
            sw.Stop();
            evtSrc.RecordMagickFallbackDecode();
            evtSrc.RecordDecodeDuration(sw.Elapsed.TotalMilliseconds);
            var decoder = WithToneMapStatus(WithColorManagementStatus("Magick.NET", colorTarget), true);
            evtSrc.DecodeCompleted(displayName, decoder, (long)sw.Elapsed.TotalMilliseconds);
            return new LoadResult(wb, wb.PixelWidth, wb.PixelHeight, decoder);
        }
        catch (Exception ex) when (ex is MagickException or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    private static string WithToneMapStatus(string decoder, bool toneMapped)
        => toneMapped
            ? $"{decoder} · {HdrToneMapOperator} tonemapped to SDR"
            : decoder;

    private static LoadResult LoadArchivePreview(
        string path,
        int requestedPageIndex,
        bool spreadMode,
        bool rightToLeft,
        string? password = null)
    {
        if (spreadMode)
            return LoadArchiveSpreadPreview(path, requestedPageIndex, rightToLeft, password);

        var page = ArchiveBookService.LoadPage(path, requestedPageIndex, password);
        var loaded = LoadRasterBytes(page.Bytes, page.EntryName, Path.GetExtension(page.EntryName));
        var pageDescription = page.IsCover
            ? $"archive cover, page {page.PageIndex + 1} of {page.PageCount}"
            : $"archive page {page.PageIndex + 1} of {page.PageCount}";
        return loaded with
        {
            DecoderUsed = $"{loaded.DecoderUsed} ({pageDescription})",
            Pages = new PageSequence(page.PageIndex, page.PageCount, "Page")
        };
    }

    private static LoadResult LoadArchiveSpreadPreview(
        string path,
        int requestedPageIndex,
        bool rightToLeft,
        string? password = null)
    {
        var spread = ArchiveBookService.LoadSpread(path, requestedPageIndex, password);
        if (spread.Pages.Count == 0)
            throw new InvalidOperationException("The archive spread did not produce a preview page.");

        var loadedPages = spread.Pages
            .Select(page => new
            {
                Page = page,
                Loaded = LoadRasterBytes(page.Bytes, page.EntryName, Path.GetExtension(page.EntryName))
            })
            .ToList();

        if (loadedPages.Count == 1)
        {
            var page = loadedPages[0].Page;
            var loaded = loadedPages[0].Loaded;
            var pageDescription = page.IsCover
                ? $"archive cover, page {page.PageIndex + 1} of {page.PageCount}"
                : $"archive page {page.PageIndex + 1} of {page.PageCount}";

            return loaded with
            {
                DecoderUsed = $"{loaded.DecoderUsed} ({pageDescription})",
                Pages = new PageSequence(page.PageIndex, page.PageCount, "Page")
            };
        }

        var bitmaps = loadedPages
            .Select(item => item.Loaded.Image as BitmapSource
                ?? throw new InvalidOperationException($"Archive page '{item.Page.EntryName}' did not decode to a bitmap."))
            .ToList();
        var spreadImage = ComposeArchiveSpread(bitmaps, rightToLeft);
        var start = spread.PageIndex + 1;
        var end = Math.Min(spread.PageIndex + spread.PageSpan, spread.PageCount);
        var decoder = loadedPages[0].Loaded.DecoderUsed;

        return new LoadResult(
            spreadImage,
            spreadImage.PixelWidth,
            spreadImage.PixelHeight,
            $"{decoder} (archive spread, pages {start}-{end} of {spread.PageCount})",
            Pages: new PageSequence(spread.PageIndex, spread.PageCount, "Pages", spread.PageSpan));
    }

    private static BitmapSource ComposeArchiveSpread(IReadOnlyList<BitmapSource> pages, bool rightToLeft)
    {
        var ordered = rightToLeft ? pages.Reverse().ToList() : pages.ToList();
        var height = ordered.Max(page => page.PixelHeight);
        var widths = ordered
            .Select(page => page.PixelHeight == height
                ? page.PixelWidth
                : (int)Math.Round(page.PixelWidth * (height / (double)Math.Max(1, page.PixelHeight))))
            .ToList();
        // Sum as long: a wide multi-page spread (high-DPI scans normalized to a tall page's height)
        // can exceed int.MaxValue, and an unchecked int Sum() wraps to a negative width that
        // RenderTargetBitmap rejects with an opaque error. Validate against the same dimension policy
        // every other decode path uses, and fail with the shared "too large" message.
        var widthLong = widths.Aggregate(0L, (acc, w) => acc + w);
        if (!MagickSecurityPolicy.IsRenderableDimensions(widthLong, height))
            throw new InvalidOperationException("Archive spread is too large to render.");
        var width = (int)widthLong;

        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            var x = 0.0;
            for (var i = 0; i < ordered.Count; i++)
            {
                var page = ordered[i];
                var renderedWidth = widths[i];
                drawing.DrawImage(page, new Rect(x, 0, renderedWidth, height));
                x += renderedWidth;
            }
        }

        var spread = new RenderTargetBitmap(width, height, ordered[0].DpiX, ordered[0].DpiY, PixelFormats.Pbgra32);
        spread.Render(visual);
        spread.Freeze();
        return spread;
    }

    /// <summary>
    /// Renders the first page/artboard of a PostScript-family file through Magick.NET with the
    /// configured Ghostscript runtime. This path deliberately reads from the file instead of a
    /// byte[] so large PDFs/AI files do not hit the LOH before rasterization.
    /// </summary>
    private static LoadResult LoadDocumentPreview(string path, int requestedPageIndex)
    {
        var runtime = CodecRuntime.Status;
        var ext = Path.GetExtension(path).ToUpperInvariant();
        if (!runtime.GhostscriptAvailable || !MagickSecurityPolicy.DocumentDelegatesEnabled)
        {
            throw new InvalidOperationException(
                $"{ext} preview requires Ghostscript. This build can use a bundled copy in the app's " +
                "Codecs\\Ghostscript folder, IMAGES_GHOSTSCRIPT_DIR, or an installed Ghostscript runtime.");
        }

        try
        {
            var pageCount = CountImageFrames(path, CreateDocumentReadSettings(countOnly: true));
            var pageIndex = ClampPageIndex(requestedPageIndex, pageCount);
            var settings = new MagickReadSettings
            {
                Density = new Density(DocumentPreviewDpi),
                FrameIndex = (uint)pageIndex,
                FrameCount = 1,
                BackgroundColor = MagickColors.White
            };

            using var collection = MagickSafeReader.ReadCollection(path, settings);
            if (collection.Count == 0)
                throw new InvalidOperationException("The document did not produce a preview page.");

            using var first = collection[0].Clone();
            first.BackgroundColor = MagickColors.White;
            first.Alpha(AlphaOption.Remove);

            var wb = MagickToBitmap(first);
            var pageSuffix = pageCount > 1 ? $", page {pageIndex + 1} of {pageCount}" : "";
            return new LoadResult(
                wb,
                wb.PixelWidth,
                wb.PixelHeight,
                $"Magick.NET + Ghostscript ({SupportedImageFormats.FormatFamily(path)} preview, {DocumentPreviewDpi} DPI{pageSuffix})",
                Pages: pageCount > 1 ? new PageSequence(pageIndex, pageCount, "Page") : null);
        }
        catch (MagickException ex)
        {
            throw new InvalidOperationException(
                $"Could not render '{Path.GetFileName(path)}' with Ghostscript: {ex.Message}", ex);
        }
    }

    private static byte[] ReadStableFileBytes(string path)
    {
        var fileName = Path.GetFileName(path);
        Exception? lastError = null;

        for (var attempt = 1; attempt <= StableReadRetryCount; attempt++)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var length = fs.Length;

                if (length <= 0)
                    throw new InvalidOperationException($"'{fileName}' is empty.");

                if (length >= MemoryMapThreshold || length > int.MaxValue)
                    throw new IOException("File size changed while it was being read.");

                var bytes = new byte[(int)length];
                fs.ReadExactly(bytes);

                if (fs.Length == length)
                    return bytes;

                lastError = new IOException("File size changed while it was being read.");
            }
            catch (IOException ex)
            {
                lastError = ex;
            }

            if (attempt < StableReadRetryCount)
                Thread.Sleep(StableReadRetryDelayMs);
        }

        throw new InvalidOperationException($"'{fileName}' is still being written or changed. Try again in a moment.", lastError);
    }

    private static LoadResult? TryLoadPagedRaster(string path, int requestedPageIndex)
    {
        try
        {
            var pageCount = CountImageFrames(path);
            if (pageCount <= 1) return null;

            var pageIndex = ClampPageIndex(requestedPageIndex, pageCount);
            var settings = new MagickReadSettings
            {
                FrameIndex = (uint)pageIndex,
                FrameCount = 1,
                BackgroundColor = MagickColors.White
            };

            using var collection = MagickSafeReader.ReadCollection(path, settings);
            if (collection.Count == 0) return null;

            using var frame = collection[0].Clone();
            var toneMapped = ToneMapService.ApplyIfNeeded(frame, Path.GetExtension(path), HdrToneMapOperator);
            var colorTarget = ColorManagedDisplay ? TransformForDisplayIfProfiled(frame) : null;
            var wb = MagickToBitmap(frame);
            var label = PageLabelFor(path);
            return new LoadResult(
                wb,
                wb.PixelWidth,
                wb.PixelHeight,
                WithToneMapStatus(
                    WithColorManagementStatus(
                        $"Magick.NET ({SupportedImageFormats.FormatFamily(path)} {label.ToLowerInvariant()} {pageIndex + 1} of {pageCount})",
                        colorTarget),
                    toneMapped),
                Pages: new PageSequence(pageIndex, pageCount, label));
        }
        catch (MagickException)
        {
            return null;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static LoadResult LoadTilePyramid(string path, int requestedPageIndex)
    {
        var pageCount = PagedRasterExtensions.Contains(Path.GetExtension(path))
            ? CountImageFrames(path)
            : 1;
        var pageIndex = ClampPageIndex(requestedPageIndex, pageCount);
        var pyramid = TileService.BuildPyramid(path, pageIndex: pageIndex);
        var pageSuffix = pageCount > 1 ? $", page {pageIndex + 1} of {pageCount}" : "";

        return new LoadResult(
            TilePlaceholder,
            pyramid.SourceWidth,
            pyramid.SourceHeight,
            WithToneMapStatus($"Magick.NET tile pyramid (DZI/WebP, {pyramid.TotalTiles} tiles{pageSuffix})", pyramid.ToneMappedToSdr),
            Pages: pageCount > 1 ? new PageSequence(pageIndex, pageCount, PageLabelFor(path)) : null,
            TilePyramid: pyramid);
    }

    private static MagickReadSettings CreateDocumentReadSettings(bool countOnly)
        => new()
        {
            Density = new Density(countOnly ? 72 : DocumentPreviewDpi),
            BackgroundColor = MagickColors.White
        };

    private static int CountImageFrames(string path, MagickReadSettings? settings = null)
    {
        try
        {
            return MagickSafeReader.CountFrames(path, settings);
        }
        catch
        {
            return 1;
        }
    }

    private static int ClampPageIndex(int requestedPageIndex, int pageCount)
        => Math.Clamp(requestedPageIndex, 0, Math.Max(1, pageCount) - 1);

    private static string PageLabelFor(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".ico", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".cur", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".dcx", StringComparison.OrdinalIgnoreCase)
            ? "Frame"
            : "Page";
    }

    /// <summary>
    /// Decode path for files above <see cref="MemoryMapThreshold"/>. Opens a read-only mapping
    /// and gives each decoder its own view stream — WIC first, Magick.NET on failure. We do not
    /// attempt animated decode here because the formats that actually use MMF in practice
    /// (huge PSDs, 400 MP RAWs, multi-page TIFFs) are always single-sequence.
    /// </summary>
    private static LoadResult LoadFromMemoryMapped(string path)
    {
        MemoryMappedFile mmf;
        try
        {
            // FileShare.Read mirrors the byte[] path's share mode so an Explorer-initiated rename
            // of the current file doesn't block because we happen to still have it mapped.
            var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            try
            {
                mmf = MemoryMappedFile.CreateFromFile(
                    fs,
                    mapName: null,
                    capacity: 0,
                    access: MemoryMappedFileAccess.Read,
                    inheritability: System.IO.HandleInheritability.None,
                    leaveOpen: false);
            }
            catch
            {
                fs.Dispose();
                throw;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new InvalidOperationException($"Could not map '{Path.GetFileName(path)}': {ex.Message}", ex);
        }

        var evtSrc = ImageEventSource.Instance;
        var displayName = Path.GetFileName(path);
        evtSrc.RecordDecodeAttempt();
        var sw = Stopwatch.StartNew();
        var bypassWicJpeg = WicJpegSecurityPolicy.ShouldBypassWic(path);
        if (!bypassWicJpeg)
            evtSrc.DecodeStarted(displayName, "WIC (memory-mapped)");

        try
        {
            // Primary: WIC via BitmapImage. HDR candidates skip WIC because its SDR surface can
            // quantize extended samples before the HDRI tonemap sees them.
            if (!bypassWicJpeg && !ToneMapService.IsCandidateExtension(Path.GetExtension(path)))
            {
                try
                {
                    using var view = mmf.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    bmp.StreamSource = view;
                    bmp.EndInit();
                    bmp.Freeze();

                    sw.Stop();
                    evtSrc.RecordWicDecode();
                    evtSrc.RecordDecodeDuration(sw.Elapsed.TotalMilliseconds);
                    evtSrc.DecodeCompleted(displayName, "WIC (memory-mapped)", (long)sw.Elapsed.TotalMilliseconds);
                    return new LoadResult(bmp, bmp.PixelWidth, bmp.PixelHeight, "WIC (memory-mapped)");
                }
                catch (Exception ex) when (
                    ex is NotSupportedException or
                          System.Runtime.InteropServices.COMException or
                          FileFormatException or
                          InvalidOperationException or
                          ArgumentException)
                {
                    // Fall through to Magick.NET.
                }
            }

            // Fallback: Magick.NET reads the mapping directly.
            sw.Restart();
            evtSrc.DecodeStarted(
                displayName,
                bypassWicJpeg
                    ? "Magick.NET (memory-mapped WIC JPEG security fallback)"
                    : "Magick.NET (memory-mapped)");
            try
            {
                using var view = mmf.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
                using var image = MagickSafeReader.Read(view, Path.GetExtension(path));
                var toneMapped = ToneMapService.ApplyIfNeeded(image, Path.GetExtension(path), HdrToneMapOperator);
                var wb = MagickToBitmap(image);

                sw.Stop();
                evtSrc.RecordMagickFallbackDecode();
                evtSrc.RecordDecodeDuration(sw.Elapsed.TotalMilliseconds);
                var decoder = WithToneMapStatus("Magick.NET (memory-mapped)", toneMapped);
                if (bypassWicJpeg)
                    decoder += " · WIC JPEG disabled; Windows update recommended";
                evtSrc.DecodeCompleted(displayName, decoder, (long)sw.Elapsed.TotalMilliseconds);
                return new LoadResult(
                    wb,
                    wb.PixelWidth,
                    wb.PixelHeight,
                    decoder,
                    WicJpegSecurityFallback: bypassWicJpeg);
            }
            catch (Exception ex)
            {
                sw.Stop();
                evtSrc.RecordDecodeFailure();
                evtSrc.DecodeFailed(displayName, ex.Message);
                throw new InvalidOperationException($"Could not decode '{displayName}': {ex.Message}", ex);
            }
        }
        finally
        {
            mmf.Dispose();
        }
    }

    /// <summary>
    /// Attempts to decode a multi-frame animation from <paramref name="bytes"/> using
    /// <see cref="MagickImageCollection"/>. Returns null on any failure or when the collection
    /// has fewer than two frames (so plain single-frame GIFs/PNGs take the fast WIC path).
    /// </summary>
    private static LoadResult? TryLoadAnimated(byte[] bytes)
    {
        try
        {
            using var collection = MagickSafeReader.ReadCollection(bytes);
            if (collection.Count < 2) return null;

            // Coalesce rewrites every frame to full canvas size with disposal methods resolved.
            // Without this, partial "DoNotDispose" frames paint only their changed rectangle and
            // the animation glitches into a pile of fragments.
            collection.Coalesce();

            var frames = new List<BitmapSource>(collection.Count);
            var delays = new List<TimeSpan>(collection.Count);
            int firstWidth = 0;
            int firstHeight = 0;
            int iterations = 0;
            bool first = true;

            string? colorTarget = null;
            foreach (var frame in collection)
            {
                if (ColorManagedDisplay)
                {
                    var frameTarget = TransformForDisplayIfProfiled(frame);
                    colorTarget ??= frameTarget;
                }
                var wb = MagickToBitmap(frame);
                if (first)
                {
                    firstWidth = wb.PixelWidth;
                    firstHeight = wb.PixelHeight;
                    iterations = (int)frame.AnimationIterations;
                    first = false;
                }
                frames.Add(wb);

                // GIF frame delays are stored in 1/100 s units. Browsers clamp anything under 20 ms
                // up to 100 ms to stop "0-delay" GIFs from pinning a CPU core — we match that
                // convention rather than shipping a faster-than-browser playback. AnimationDelay is a
                // uint (32-bit for WebP/APNG); compute in long so a large/malformed field cannot
                // overflow int into a negative delay, and cap absurd values at 60 s.
                var ms = (long)frame.AnimationDelay * 10;
                if (ms < 20) ms = 100;
                else if (ms > 60_000) ms = 60_000;
                delays.Add(TimeSpan.FromMilliseconds(ms));
            }

            if (frames.Count < 2) return null;

            var seq = new AnimationSequence(frames, delays, iterations);
            return new LoadResult(
                frames[0],
                firstWidth,
                firstHeight,
                WithColorManagementStatus($"Magick.NET (animated, {frames.Count} frames)", colorTarget),
                seq);
        }
        // Missing codec / unknown format / corrupt header — fall back to the regular single-image path.
        catch (MagickException)
        {
            return null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Copies a Magick.NET <see cref="IMagickImage{QuantumType}"/> into a frozen
    /// <see cref="WriteableBitmap"/> in BGRA32. Shared by the static fallback path and the
    /// animated-frame decoder.
    /// </summary>
    private static LoadResult? TryLoadColorManaged(byte[] bytes, string displayName, string extension)
    {
        var evtSrc = ImageEventSource.Instance;
        evtSrc.RecordDecodeAttempt();
        var sw = Stopwatch.StartNew();
        evtSrc.DecodeStarted(displayName, "Magick.NET (color-managed)");
        try
        {
            using var image = MagickSafeReader.Read(bytes, extension);
            var toneMapped = ToneMapService.ApplyIfNeeded(image, extension, HdrToneMapOperator);
            var colorTarget = TransformForDisplayIfProfiled(image);
            var wb = MagickToBitmap(image);

            sw.Stop();
            evtSrc.RecordMagickFallbackDecode();
            evtSrc.RecordDecodeDuration(sw.Elapsed.TotalMilliseconds);
            var decoder = WithToneMapStatus(WithColorManagementStatus("Magick.NET", colorTarget), toneMapped);
            evtSrc.DecodeCompleted(displayName, decoder, (long)sw.Elapsed.TotalMilliseconds);
            return new LoadResult(wb, wb.PixelWidth, wb.PixelHeight, decoder);
        }
        catch (Exception ex) when (
            ex is MagickException or
                  NotSupportedException or
                  InvalidOperationException or
                  ArgumentException)
        {
            // Non-fatal: fall back to the normal decode path.
            return null;
        }
    }

    /// <summary>
    /// Transforms the image's embedded ICC profile to sRGB in place. Returns true if the image
    /// carried an embedded profile (was honored); false for untagged images (already assumed sRGB).
    /// </summary>
    internal static bool TransformToSrgbIfProfiled(IMagickImage<float> image)
    {
        if (image.GetColorProfile() is null)
            return false;

        // The first argument is only used when no profile is embedded (not our case here); the
        // embedded profile is the real source, sRGB the target. Identity for sRGB-tagged images.
        image.TransformColorSpace(ColorProfiles.SRGB, ColorProfiles.SRGB);
        return true;
    }

    /// <summary>
    /// Transforms an embedded source profile to the effective display destination. Legacy SDR
    /// monitors use their validated ICC profile; Advanced Color and uncertain display states use
    /// sRGB so Windows can perform (or safely assume) the desktop conversion. Returns the target
    /// label for decoder diagnostics, or null when the image was untagged.
    /// </summary>
    internal static string? TransformForDisplayIfProfiled(
        IMagickImage<float> image,
        DisplayColorState? display = null)
    {
        if (image.GetColorProfile() is null)
            return null;

        display ??= DisplayColorService.Current;
        if (display.UseLegacyMonitorProfile && display.ProfileData is { Length: > 0 } profileData)
        {
            var destination = new ColorProfile(profileData);
            image.TransformColorSpace(ColorProfiles.SRGB, destination);
            return display.DestinationLabel;
        }

        image.TransformColorSpace(ColorProfiles.SRGB, ColorProfiles.SRGB);
        return "sRGB";
    }

    private static string WithColorManagementStatus(string decoder, string? target)
        => target is null ? decoder : $"{decoder} · color-managed → {target}";

    private static WriteableBitmap MagickToBitmap(IMagickImage<float> image)
    {
        image.Format = MagickFormat.Bgra;
        image.Alpha(AlphaOption.Set);

        if (!MagickSecurityPolicy.IsRenderableDimensions((long)image.Width, (long)image.Height))
            throw new InvalidOperationException("Decoded image dimensions are not supported.");

        var w = (int)image.Width;
        var h = (int)image.Height;
        int stride;
        try
        {
            stride = checked(w * 4);
        }
        catch (OverflowException ex)
        {
            throw new InvalidOperationException("Decoded image is too large to render.", ex);
        }

        if (stride <= 0)
            throw new InvalidOperationException("Decoded image is too large to render.");

        var expectedLength = (long)stride * h;
        if (expectedLength > int.MaxValue)
            throw new InvalidOperationException("Decoded image is too large to render.");

        var pixels = image.GetPixelsUnsafe().ToByteArray(PixelMapping.BGRA)
                     ?? throw new InvalidOperationException("Magick.NET returned null pixel buffer");
        if (pixels.LongLength != expectedLength)
            throw new InvalidOperationException("Magick.NET returned an unexpected pixel buffer size.");

        var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        wb.Lock();
        try
        {
            Marshal.Copy(pixels, 0, wb.BackBuffer, pixels.Length);
            wb.AddDirtyRect(new Int32Rect(0, 0, w, h));
        }
        finally
        {
            wb.Unlock();
        }
        wb.Freeze();
        return wb;
    }

    private static BitmapSource CreateTilePlaceholder()
    {
        var wb = new WriteableBitmap(1, 1, 96, 96, PixelFormats.Bgra32, null);
        wb.Lock();
        try
        {
            wb.AddDirtyRect(new Int32Rect(0, 0, 1, 1));
        }
        finally
        {
            wb.Unlock();
        }

        wb.Freeze();
        return wb;
    }

    public static (int width, int height) QuickDimensions(string path)
    {
        try
        {
            if (WicJpegSecurityPolicy.ShouldBypassWic(path))
            {
                using var image = MagickSafeReader.Ping(path);
                return (checked((int)image.Width), checked((int)image.Height));
            }

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var frame = BitmapFrame.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnLoad);
            return (frame.PixelWidth, frame.PixelHeight);
        }
        catch
        {
            return (0, 0);
        }
    }

    public static ImageSource LoadPreviewImage(string path, int maxPixelDimension = 1024)
    {
        if (maxPixelDimension <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPixelDimension), "Preview dimension cap must be positive.");

        if (!ToneMapService.IsCandidateExtension(Path.GetExtension(path)) &&
            TryLoadWicPreview(path, maxPixelDimension) is { } preview)
            return preview;

        CodecRuntime.Configure();
        using var image = MagickSafeReader.Read(path);
        image.AutoOrient();
        image.Resize(new MagickGeometry((uint)maxPixelDimension, (uint)maxPixelDimension) { Greater = true });
        ToneMapService.ApplyIfNeeded(image, Path.GetExtension(path), HdrToneMapOperator);
        return MagickToBitmap(image);
    }

    private static BitmapSource? TryLoadWicPreview(string path, int maxPixelDimension)
    {
        if (WicJpegSecurityPolicy.ShouldBypassWic(path))
            return null;

        try
        {
            int width;
            int height;
            using (var metadata = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var frame = BitmapFrame.Create(metadata, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                width = frame.PixelWidth;
                height = frame.PixelHeight;
            }

            if (width <= 0 || height <= 0)
                return null;

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bmp.StreamSource = stream;
            if (width >= height)
                bmp.DecodePixelWidth = Math.Min(width, maxPixelDimension);
            else
                bmp.DecodePixelHeight = Math.Min(height, maxPixelDimension);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch (Exception ex) when (
            ex is IOException or
                  UnauthorizedAccessException or
                  NotSupportedException or
                  System.Runtime.InteropServices.COMException or
                  FileFormatException or
                  InvalidOperationException or
                  ArgumentException)
        {
            return null;
        }
    }

    public static bool IsRawExtension(string path)
        => SupportedImageFormats.RawExtensions.Contains(
            Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static LoadResult? TryLoadRawPreview(string path)
    {
        if (!IsRawExtension(path))
            return null;

        try
        {
            using var image = MagickSafeReader.Ping(path);

            var exifProfile = image.GetExifProfile();
            if (exifProfile is null || exifProfile.ThumbnailLength <= 0)
                return null;

            using var thumb = exifProfile.CreateThumbnail();
            if (thumb is null)
                return null;

            var thumbBytes = thumb.ToByteArray(MagickFormat.Jpeg);
            if (thumbBytes is null || thumbBytes.Length < 100)
                return null;

            using var ms = new MemoryStream(thumbBytes, writable: false);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();

            return new LoadResult(
                bmp,
                bmp.PixelWidth,
                bmp.PixelHeight,
                "RAW embedded preview",
                IsPreview: true);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// A decoded multi-frame animation — the caller plays it by cycling <see cref="Frames"/> with
/// the matching <see cref="Delays"/>. <see cref="LoopCount"/> follows the GIF convention:
/// zero means infinite, any other value is the exact iteration count.
/// </summary>
public sealed class AnimationSequence
{
    public IReadOnlyList<BitmapSource> Frames { get; }
    public IReadOnlyList<TimeSpan> Delays { get; }
    public int LoopCount { get; }

    public AnimationSequence(IReadOnlyList<BitmapSource> frames, IReadOnlyList<TimeSpan> delays, int loopCount)
    {
        if (frames.Count == 0) throw new ArgumentException("Animation must have at least one frame.", nameof(frames));
        if (frames.Count != delays.Count) throw new ArgumentException("Frames / delays length mismatch.", nameof(delays));
        Frames = frames;
        Delays = delays;
        LoopCount = loopCount;
    }

    public TimeSpan TotalDuration
    {
        get
        {
            var total = TimeSpan.Zero;
            foreach (var d in Delays) total += d;
            return total;
        }
    }
}

public sealed record PageSequence(int PageIndex, int PageCount, string Label, int PageSpan = 1)
{
    public bool HasMultiplePages => PageCount > 1;
}
