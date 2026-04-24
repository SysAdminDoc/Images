using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Images.Services;

/// <summary>
/// Loads an image from disk. Tries WIC first (native, fast, handles most formats on modern Windows)
/// and falls back to Magick.NET for anything WIC can't decode (JXL/AVIF/PSD/TGA/RAW if no WIC codec installed).
/// </summary>
public static class ImageLoader
{
    public sealed record LoadResult(ImageSource Image, int PixelWidth, int PixelHeight, string DecoderUsed);

    public static LoadResult Load(string path)
    {
        // Load the file into memory first so we never hold a lock on the original (rename/delete must work).
        byte[] bytes;
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            if (fs.Length == 0)
                throw new InvalidOperationException($"'{Path.GetFileName(path)}' is empty.");
            bytes = new byte[fs.Length];
            fs.ReadExactly(bytes);
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
            using var image = new ImageMagick.MagickImage(bytes);
            image.Format = ImageMagick.MagickFormat.Bgra;
            image.Alpha(ImageMagick.AlphaOption.Set);

            var w = (int)image.Width;
            var h = (int)image.Height;
            var pixels = image.GetPixelsUnsafe().ToByteArray(ImageMagick.PixelMapping.BGRA)
                         ?? throw new InvalidOperationException("Magick.NET returned null pixel buffer");

            var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            wb.Lock();
            try
            {
                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, wb.BackBuffer, pixels.Length);
                wb.AddDirtyRect(new System.Windows.Int32Rect(0, 0, w, h));
            }
            finally
            {
                wb.Unlock();
            }
            wb.Freeze();
            return new LoadResult(wb, w, h, "Magick.NET");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Could not decode '{Path.GetFileName(path)}': {ex.Message}", ex);
        }
    }

    public static (int width, int height) QuickDimensions(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var frame = BitmapFrame.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            return (frame.PixelWidth, frame.PixelHeight);
        }
        catch
        {
            return (0, 0);
        }
    }
}
