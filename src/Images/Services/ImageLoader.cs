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
    public sealed record LoadResult(
        ImageSource Image,
        int PixelWidth,
        int PixelHeight,
        string DecoderUsed,
        AnimationSequence? Animation = null,
        PageSequence? Pages = null);

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
    private const int MaxRenderableDimension = 30000;

    public static LoadResult Load(string path, int pageIndex = 0)
    {
        var fi = new FileInfo(path);
        if (!fi.Exists)
            throw new FileNotFoundException($"'{Path.GetFileName(path)}' does not exist.", path);
        if (fi.Length == 0)
            throw new InvalidOperationException($"'{Path.GetFileName(path)}' is empty.");

        if (SupportedImageFormats.IsArchive(path))
            return LoadArchivePreview(path, pageIndex);

        if (SupportedImageFormats.RequiresGhostscript(path))
            return LoadDocumentPreview(path, pageIndex);

        if (PagedRasterExtensions.Contains(Path.GetExtension(path)))
        {
            var paged = TryLoadPagedRaster(path, pageIndex);
            if (paged is not null) return paged;
        }

        // V20-06: large files bypass the byte[] round-trip entirely. Animation probe is skipped
        // too — a 256 MB+ GIF is a pathological edge case and multi-frame decode needs random
        // access to frame offsets that MMF serves fine from a single view.
        if (fi.Length >= MemoryMapThreshold)
            return LoadFromMemoryMapped(path);

        // Load the file into memory first so we never hold a lock on the original (rename/delete must work).
        var bytes = ReadStableFileBytes(path);

        return LoadRasterBytes(bytes, Path.GetFileName(path), Path.GetExtension(path));
    }

    private static LoadResult LoadRasterBytes(byte[] bytes, string displayName, string extension)
    {
        // Animation probe first — only for formats that actually support it. If the file is a
        // multi-frame GIF / animated WebP / APNG, return the full sequence so the canvas can play it.
        if (AnimatedExtensions.Contains(extension))
        {
            var animated = TryLoadAnimated(bytes);
            if (animated is not null) return animated;
        }

        // Primary: WIC via BitmapImage. CacheOption.OnLoad fully reads the stream during EndInit,
        // so the MemoryStream can be disposed immediately after.
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

        // Fallback: Magick.NET decodes to BGRA bytes → WriteableBitmap.
        try
        {
            using var image = new MagickImage(bytes);
            var wb = MagickToBitmap(image);
            return new LoadResult(wb, wb.PixelWidth, wb.PixelHeight, "Magick.NET");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Could not decode '{displayName}': {ex.Message}", ex);
        }
    }

    private static LoadResult LoadArchivePreview(string path, int requestedPageIndex)
    {
        var page = ArchiveBookService.LoadPage(path, requestedPageIndex);
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

    /// <summary>
    /// Renders the first page/artboard of a PostScript-family file through Magick.NET with the
    /// configured Ghostscript runtime. This path deliberately reads from the file instead of a
    /// byte[] so large PDFs/AI files do not hit the LOH before rasterization.
    /// </summary>
    private static LoadResult LoadDocumentPreview(string path, int requestedPageIndex)
    {
        var runtime = CodecRuntime.Status;
        var ext = Path.GetExtension(path).ToUpperInvariant();
        if (!runtime.GhostscriptAvailable)
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

            using var collection = new MagickImageCollection(new FileInfo(path), settings);
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

            using var collection = new MagickImageCollection(new FileInfo(path), settings);
            if (collection.Count == 0) return null;

            using var frame = collection[0].Clone();
            var wb = MagickToBitmap(frame);
            var label = PageLabelFor(path);
            return new LoadResult(
                wb,
                wb.PixelWidth,
                wb.PixelHeight,
                $"Magick.NET ({SupportedImageFormats.FormatFamily(path)} {label.ToLowerInvariant()} {pageIndex + 1} of {pageCount})",
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
            var pages = settings is null
                ? MagickImageInfo.ReadCollection(new FileInfo(path))
                : MagickImageInfo.ReadCollection(new FileInfo(path), settings);
            return Math.Max(1, pages.Count());
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
            mmf = MemoryMappedFile.CreateFromFile(
                new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete),
                mapName: null,
                capacity: 0,
                access: MemoryMappedFileAccess.Read,
                inheritability: System.IO.HandleInheritability.None,
                leaveOpen: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new InvalidOperationException($"Could not map '{Path.GetFileName(path)}': {ex.Message}", ex);
        }

        try
        {
            // Primary: WIC via BitmapImage. EndInit fully reads the view stream under CacheOption.OnLoad.
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

            // Fallback: Magick.NET reads the mapping directly.
            try
            {
                using var view = mmf.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
                using var image = new MagickImage(view);
                var wb = MagickToBitmap(image);
                return new LoadResult(wb, wb.PixelWidth, wb.PixelHeight, "Magick.NET (memory-mapped)");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not decode '{Path.GetFileName(path)}': {ex.Message}", ex);
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
            using var collection = new MagickImageCollection(bytes);
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

            foreach (var frame in collection)
            {
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
                // convention rather than shipping a faster-than-browser playback.
                var ms = (int)frame.AnimationDelay * 10;
                if (ms <= 20) ms = 100;
                delays.Add(TimeSpan.FromMilliseconds(ms));
            }

            if (frames.Count < 2) return null;

            var seq = new AnimationSequence(frames, delays, iterations);
            return new LoadResult(
                frames[0],
                firstWidth,
                firstHeight,
                $"Magick.NET (animated, {frames.Count} frames)",
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
    private static WriteableBitmap MagickToBitmap(IMagickImage<ushort> image)
    {
        image.Format = MagickFormat.Bgra;
        image.Alpha(AlphaOption.Set);

        if (image.Width <= 0 || image.Height <= 0 ||
            image.Width > MaxRenderableDimension || image.Height > MaxRenderableDimension)
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

    public static (int width, int height) QuickDimensions(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var frame = BitmapFrame.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            return (frame.PixelWidth, frame.PixelHeight);
        }
        catch
        {
            return (0, 0);
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

public sealed record PageSequence(int PageIndex, int PageCount, string Label)
{
    public bool HasMultiplePages => PageCount > 1;
}
