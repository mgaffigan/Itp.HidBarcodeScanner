using System.IO;
using System.Windows.Media.Imaging;

namespace Itp.Handheld.WpfClient.Capture.WinRt;

public sealed class WinRtFrame : IWpfFrame
{
    public WinRtFrame(BitmapSource previewImage)
    {
        ArgumentNullException.ThrowIfNull(previewImage);
        PreviewImage = previewImage;
    }

    public string MimeType => "image/png";

    public BitmapSource PreviewImage { get; }

    public void WriteTo(Stream stream)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(PreviewImage));
        encoder.Save(stream);
    }
}
