using System.Text.RegularExpressions;
using Esatto.Win32.Registry;

namespace Itp.WpfCamera;

/// <summary>
/// Settings for <see cref="CaptureWindow"/>, under <c>SOFTWARE\In Touch Technologies\Esatto\Wpf.Camera</c>.
/// </summary>
public sealed class CaptureSettings : RegistrySettings
{
    internal const string KeyRoot = @"In Touch Technologies\Esatto\Wpf.Camera";

    public CaptureSettings()
        : base(KeyRoot)
    {
    }

    /// <summary>
    /// The shared instance used by <see cref="CaptureWindow"/>.
    /// </summary>
    public static CaptureSettings Instance { get; } = new CaptureSettings();

    /// <summary>
    /// The last camera <see cref="CaptureWindow"/> opened successfully, as a
    /// <see cref="Windows.Media.Capture.Frames.MediaFrameSourceGroup.Id"/>; selected by default next time.
    /// </summary>
    public string? SelectedCameraId
    {
        get => GetString(nameof(SelectedCameraId), null);
        set => SetString(nameof(SelectedCameraId), value);
    }
}

/// <summary>
/// Per-model camera settings, under <c>...\Wpf.Camera\Cameras\VID_xxxx&amp;PID_xxxx</c>.  Keyed by
/// VID/PID because a device id identifies a port, not a model.
/// </summary>
public sealed class CaptureCameraSettings : RegistrySettings
{
    private static readonly Regex VidPid = new(@"VID_([0-9A-F]{4})&PID_([0-9A-F]{4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private CaptureCameraSettings(string cameraKey)
        : base($@"{CaptureSettings.KeyRoot}\Cameras\{cameraKey}")
    {
        CameraKey = cameraKey;
    }

    /// <summary>
    /// Opens the settings for the model of the camera <paramref name="deviceId"/>.
    /// </summary>
    /// <param name="deviceId">A <see cref="Windows.Media.Capture.Frames.MediaFrameSourceGroup.Id"/>.</param>
    public static CaptureCameraSettings For(string deviceId) => new(GetCameraKey(deviceId));

    /// <summary>
    /// The registry key name for this camera model, e.g. <c>VID_046D&amp;PID_085C</c>.
    /// </summary>
    public string CameraKey { get; }

    /// <summary>
    /// An index into the color source's <see cref="Windows.Media.Capture.Frames.MediaFrameSource.SupportedFormats"/>.
    /// -1 (the default) leaves the driver's own format alone.  An out-of-range or unusable index
    /// fails <see cref="WinRtCamera.OpenAsync"/>.
    /// </summary>
    public int FormatIndex
    {
        get => GetInt(nameof(FormatIndex), -1);
        set => SetInt(nameof(FormatIndex), value);
    }

    private static string GetCameraKey(string deviceId)
    {
        var match = VidPid.Match(deviceId);
        return match.Success
            ? $"VID_{match.Groups[1].Value.ToUpperInvariant()}&PID_{match.Groups[2].Value.ToUpperInvariant()}"
            : deviceId.Replace('\\', '_');
    }
}
