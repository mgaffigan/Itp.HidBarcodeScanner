using System.Text.RegularExpressions;
using Esatto.Edm;

namespace Itp.Handheld.WpfClient.Capture;

public sealed class CaptureSettings : RegistrySettings
{
    public CaptureSettings()
        : base(@"In Touch Technologies\Esatto\HandheldWpf\Capture")
    {
    }

    public static CaptureSettings Instance { get; } = new CaptureSettings();

    // Last camera that opened successfully, as a MediaFrameSourceGroup id
    public string? SelectedCameraId
    {
        get => GetString(nameof(SelectedCameraId), null);
        set => SetString(nameof(SelectedCameraId), value);
    }
}

// Per-model overrides set at station setup, keyed by VID/PID because a device id identifies a port, not a model.
public sealed class CaptureCameraSettings : RegistrySettings
{
    private static readonly Regex VidPid = new(@"VID_([0-9A-F]{4})&PID_([0-9A-F]{4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private CaptureCameraSettings(string cameraKey)
        : base($@"In Touch Technologies\Esatto\HandheldWpf\Capture\Cameras\{cameraKey}", readOnly: true)
    {
    }

    public static CaptureCameraSettings For(string deviceId) => new(CameraKey(deviceId));

    // An index into MediaFrameSource.SupportedFormats. Null leaves the driver's own format alone.
    public int? FormatIndex => GetIntNull(nameof(FormatIndex));

    public static string CameraKey(string deviceId)
    {
        var match = VidPid.Match(deviceId);
        return match.Success
            ? $"VID_{match.Groups[1].Value.ToUpperInvariant()}&PID_{match.Groups[2].Value.ToUpperInvariant()}"
            : deviceId.Replace('\\', '_');
    }
}
