using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Graphics.Imaging;

namespace Itp.Handheld.WpfClient.Capture.WinRt;

internal static class SoftwareBitmapExtensions
{
    public static BitmapSource ToBitmapSource(this SoftwareBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        SoftwareBitmap? converted = null;
        try
        {
            var source = bitmap;
            if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
            {
                converted = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);
                source = converted;
            }

            var stride = source.PixelWidth * 4;
            var buffer = new Windows.Storage.Streams.Buffer((uint)(stride * source.PixelHeight));
            source.CopyToBuffer(buffer);

            var pixels = new byte[buffer.Length];
            using var reader = Windows.Storage.Streams.DataReader.FromBuffer(buffer);
            reader.ReadBytes(pixels);

            // Frozen so the frame can outlive the camera and cross threads.
            var image = BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96,
                PixelFormats.Bgr32, null, pixels, stride);
            image.Freeze();
            return image;
        }
        finally
        {
            converted?.Dispose();
        }
    }
}
