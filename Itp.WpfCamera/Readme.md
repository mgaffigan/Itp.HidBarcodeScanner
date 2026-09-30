# ITP WPF Camera API

Live preview and still capture from webcams in WPF applications.  Uses WinRT `MediaCapture` for
capture and presents preview frames to a `D3DImage` without copying them through the CPU.

Windows 10+ and WPF only.  Unpackaged desktop apps are supported.

## Capture dialog

`CaptureWindow` is a maximized dialog with a live preview, a camera switcher, and a Capture button
(or F2).  It returns one still, or throws `OperationCanceledException` if the user closes it.

```csharp
try
{
    image.Source = await CaptureWindow.ShowDialogAsync(this);   // frozen, at capture resolution
}
catch (OperationCanceledException)
{
    // The user closed the dialog.
}
catch (Exception ex)
{
    // The camera could not be opened, failed while open, or could not capture.
    MessageBox.Show(this, ex.Message);
}
```

The dialog does not try to recover from failures.  Any failure (including the camera being
unplugged, taken by another application, or stopped by a session lock) closes the dialog and is
rethrown from `ShowDialogAsync`.

## Direct use

```csharp
var cameras = await WinRtCameraDeviceEnumerator.EnumerateAsync();
using var camera = await WinRtCamera.OpenAsync(cameras[0],   // on the UI thread
    (_, e) => log.LogError((Exception)e.ExceptionObject, "Camera failed"));   // optional
previewImage.Source = camera.Preview;

BitmapSource still = await camera.CaptureAsync();
```

`WinRtCamera` must be opened, used, and disposed on a WPF dispatcher thread.  Ownership is plain
`IDisposable`: a camera that cannot be opened throws from `OpenAsync`, and an opened camera is
released by `Dispose` whether or not it has failed.

The optional failure handler is notification only.  It is called once, on the dispatcher thread,
if an opened camera stops unexpectedly; afterwards `CaptureAsync` throws.  It is never called for
a camera that failed to open.

Cameras are opened for exclusive control, so only one `WinRtCamera` per device can be open at a time.

## Settings

Settings are read with `Esatto.Win32.Registry` (HKCU/HKLM, with policy overrides) under
`SOFTWARE\In Touch Technologies\Esatto\Wpf.Camera`:

| Value | Key | Type | Meaning |
|---|---|---|---|
| `SelectedCameraId` | `Wpf.Camera` | REG_SZ | Last camera opened by `CaptureWindow`; selected by default next time. |
| `FormatIndex` | `Wpf.Camera\Cameras\VID_xxxx&PID_xxxx` | REG_DWORD | Index into the color source's `SupportedFormats` for that camera model.  -1 (default) uses the driver default.  An out-of-range or unusable index fails the open.  Also settable via `CaptureCameraSettings.For(group.Id).FormatIndex`. |

## Design

* Preview frames stay in GPU memory.  Each `MediaFrameReader` frame's D3D11 surface is copied
  into a texture shared with a D3D9Ex device, whose surface is the `D3DImage` back buffer.
  Frames are dropped, not queued, while a present is pending.
* Stills use the low-lag photo pipeline, so they are not limited to the preview resolution.  The
  photo's `SoftwareBitmap` is locked in place (`LockBuffer`) and copied once into a frozen `BitmapSource`.
