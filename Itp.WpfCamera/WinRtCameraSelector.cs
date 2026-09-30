namespace Itp.Handheld.WpfClient.Capture.WinRt;

internal static class WinRtCameraSelector
{
    public static WinRtCameraDeviceInfo? SelectDefault(IReadOnlyList<WinRtCameraDeviceInfo> devices)
    {
        var remembered = CaptureSettings.Instance.SelectedCameraId;
        return devices.FirstOrDefault(d => d.Id == remembered) ?? devices.FirstOrDefault();
    }
}
