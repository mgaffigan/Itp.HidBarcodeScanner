namespace Itp.Handheld.WpfClient.Capture;

public interface ICamera : IDisposable
{
    // Raised when the camera stops unexpectedly (e.g. device disconnect).
    event EventHandler? Failed;

    Task<IFrame> CaptureAsync();
}
