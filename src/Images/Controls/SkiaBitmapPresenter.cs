using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace Images.Controls;

/// <summary>
/// Software Skia presenter for one static WPF bitmap. Decode and metadata ownership remain with
/// the existing image pipeline; this class only owns the premultiplied pixel copy used to paint.
/// </summary>
internal sealed class SkiaBitmapPresenter : SKElement
{
    private SKBitmap? _bitmap;
    private bool _nearestNeighbor;

    internal bool HasBitmap => _bitmap is not null;

    internal bool SetSource(BitmapSource? source)
    {
        var replacement = source is null ? null : CopyToSkia(source);
        var previous = _bitmap;
        _bitmap = replacement;
        previous?.Dispose();
        InvalidateVisual();
        return replacement is not null;
    }

    internal void SetNearestNeighbor(bool nearestNeighbor)
    {
        if (_nearestNeighbor == nearestNeighbor)
            return;

        _nearestNeighbor = nearestNeighbor;
        InvalidateVisual();
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (_bitmap is not { Width: > 0, Height: > 0 } bitmap ||
            e.Info.Width <= 0 || e.Info.Height <= 0)
        {
            return;
        }

        var scale = Math.Min((float)e.Info.Width / bitmap.Width, (float)e.Info.Height / bitmap.Height);
        var width = bitmap.Width * scale;
        var height = bitmap.Height * scale;
        var destination = SKRect.Create(
            (e.Info.Width - width) / 2f,
            (e.Info.Height - height) / 2f,
            width,
            height);
        var sampling = _nearestNeighbor
            ? new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None)
            : new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

        canvas.DrawBitmap(bitmap, destination, sampling);
    }

    private static SKBitmap CopyToSkia(BitmapSource source)
    {
        BitmapSource premultiplied = source.Format == PixelFormats.Pbgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        var stride = checked(premultiplied.PixelWidth * 4);
        var pixels = new byte[checked(stride * premultiplied.PixelHeight)];
        premultiplied.CopyPixels(pixels, stride, 0);

        var bitmap = new SKBitmap(new SKImageInfo(
            premultiplied.PixelWidth,
            premultiplied.PixelHeight,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));
        if (bitmap.GetPixels() == IntPtr.Zero)
        {
            bitmap.Dispose();
            throw new InvalidOperationException("Skia could not allocate the static image surface.");
        }

        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        return bitmap;
    }
}
