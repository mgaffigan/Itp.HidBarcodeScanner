using System.Windows.Media;
using Esatto;
using Itp.Handheld.WpfClient.Capture;
using Itp.Handheld.WpfClient.Capture.WinRt;

namespace Itp.Handheld.WpfClient.Pack;

public sealed class CaptureWindowViewModel : NotificationObject, IDisposable
{
    private readonly CaptureImagesPackStep _step;
    private IWpfCamera? _camera;
    private WinRtCameraDeviceInfo? _device;

    // Every member here runs on the UI thread, so plain fields are enough to guard both flags.
    private bool _isOpening;

    private CaptureWindowViewModel(CaptureImagesPackStep step,
        IReadOnlyList<WinRtCameraDeviceInfo> cameras)
    {
        _step = step;
        Cameras = cameras;
        DefaultCamera = WinRtCameraSelector.SelectDefault(cameras);

        // Otherwise an empty dropdown and a dead Capture button are the only clue.
        if (cameras.Count == 0)
        {
            CameraStatus = "No camera found.";
        }
    }

    public static async Task<CaptureWindowViewModel> CreateAsync(CaptureImagesPackStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return new CaptureWindowViewModel(step, await WinRtCameraDeviceEnumerator.EnumerateAsync());
    }

    public IReadOnlyList<WinRtCameraDeviceInfo> Cameras { get; }

    // The camera to show on open; null when the machine has none.
    public WinRtCameraDeviceInfo? DefaultCamera { get; }

    public ImageSource? Preview => _camera?.Preview;

    public bool CanCapture => _camera is not null && !IsCapturing;

    private bool IsCapturing
    {
        get;
        set
        {
            field = value;
            RaisePropertyChanged(nameof(CanCapture));
        }
    }

    private string? _cameraStatus;
    public string? CameraStatus
    {
        get => _cameraStatus;
        private set
        {
            _cameraStatus = value;
            RaisePropertyChanged(nameof(CameraStatus));
        }
    }

    public async Task OpenCameraAsync(WinRtCameraDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // Activated fires while an open is still awaiting, and _camera is null throughout, so
        // ReconnectIfNeededAsync would otherwise start a second open over the top of this one.
        if (_isOpening)
        {
            return;
        }

        _isOpening = true;
        CloseCamera();
        CameraStatus = $"Opening \"{device.FriendlyName}\"...";
        try
        {
            var factory = new WinRtCameraFactory(device.FriendlyName, device.Id);
            var camera = await factory.CreateAsync();
            camera.Failed += Camera_Failed;
            _camera = camera;

            _device = device;
            CaptureSettings.Instance.SelectedCameraId = device.Id;

            RaisePropertyChanged(nameof(Preview));
            RaisePropertyChanged(nameof(CanCapture));
        }
        catch (Exception ex)
        {
            _step.Fail(ex);
            throw;
        }
        finally
        {
            _isOpening = false;
            CameraStatus = null;
        }
    }

    public async Task CaptureAsync()
    {
        if (!CanCapture)
        {
            throw new InvalidOperationException("Cannot capture while another capture is in progress");
        }

        IsCapturing = true;
        try
        {
            _step.AddFrame(await _camera!.CaptureAsync());
            CameraStatus = $"Captured {_step.Frames.Count} image(s)";
        }
        catch (Exception ex)
        {
            // Don't leave a stale success count next to a failure.
            CameraStatus = null;
            _step.Fail(ex);
            throw;
        }
        finally
        {
            IsCapturing = false;
        }
    }

    private void Camera_Failed(object? sender, EventArgs e)
    {
        CloseCamera();

        // Locking the session releases the camera; explain rather than leave a black preview.
        CameraStatus = "The camera stopped. Reconnecting when this window is next active.";
        _step.Fail(new InvalidOperationException("The camera stopped unexpectedly."));
    }

    // Recovers after a session lock or another app taking the camera.
    public async Task ReconnectIfNeededAsync()
    {
        if (_camera is not null || _device is null)
        {
            return;
        }

        await OpenCameraAsync(_device);
    }

    private void CloseCamera()
    {
        if (_camera is null)
        {
            return;
        }

        _camera.Failed -= Camera_Failed;
        _camera.Dispose();
        _camera = null;

        RaisePropertyChanged(nameof(Preview));
        RaisePropertyChanged(nameof(CanCapture));
    }

    public void Dispose() => CloseCamera();
}
