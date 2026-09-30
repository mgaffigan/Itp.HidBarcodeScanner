using System.Windows.Media.Imaging;

namespace Itp.Handheld.WpfClient.Capture;

public interface IWpfFrame : IFrame
{
    BitmapSource PreviewImage { get; }
}
