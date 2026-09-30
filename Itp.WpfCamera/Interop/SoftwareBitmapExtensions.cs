using System.Windows.Media;
using System.Windows.Media.Imaging;
using DirectN;
using Windows.Graphics.Imaging;

namespace Itp.WpfCamera;

internal static class SoftwareBitmapExtensions
{
    public static BitmapSource ToBitmapSource(this SoftwareBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
        {
            throw new NotSupportedException($"Unsupported pixel format {bitmap.BitmapPixelFormat}; Bgra8 is required.");
        }

        // Camera photos are backed by a Media Foundation buffer rather than WIC; LockBuffer works for either.
        using var buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Read);
        var plane = buffer.GetPlaneDescription(0);
        using var reference = buffer.CreateReference();

        using var access = ComObjectEx.QueryInterface<IMemoryBufferByteAccess>(reference);
        access.Object.GetBuffer(out var data, out var capacity).ThrowOnError();

        // Frozen so the frame can outlive the camera and cross threads.
        var image = BitmapSource.Create(plane.Width, plane.Height, 96, 96,
            PixelFormats.Bgr32, null, data + plane.StartIndex, (int)capacity - plane.StartIndex, plane.Stride);
        image.Freeze();
        return image;
    }
}
