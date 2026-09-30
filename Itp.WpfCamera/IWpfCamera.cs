using System.Windows.Media;

namespace Itp.Handheld.WpfClient.Capture;

public interface IWpfCamera : ICamera
{
    ImageSource Preview { get; }
}
