using System.Windows.Media;
using System.Windows.Threading;
using Esatto;
using Microsoft.Extensions.Logging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;

namespace Itp.Handheld.WpfClient.Capture.WinRt;

internal sealed class WinRtCamera : IWpfCamera
{
    private readonly MediaCapture _capture;
    private readonly MediaFrameReader _reader;
    private readonly WinRtCameraPreview _preview;
    private readonly Dispatcher _dispatcher;

    private int _failedRaised;
    private bool _disposed;

    private WinRtCamera(MediaCapture capture, MediaFrameReader reader, WinRtCameraPreview preview,
        Dispatcher dispatcher)
    {
        _capture = capture;
        _reader = reader;
        _preview = preview;
        _dispatcher = dispatcher;

        _capture.Failed += Capture_Failed;
        _reader.FrameArrived += Reader_FrameArrived;
    }

    public event EventHandler? Failed;

    public ImageSource Preview => _preview.Image;

    internal static Task<IWpfCamera> OpenAsync(string id, Dispatcher dispatcher)
        => OpenAsync(id, dispatcher, useConfiguredFormat: true);

    // Awaited without ConfigureAwait throughout, so every continuation lands back on the dispatcher.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD010:Invoke single-threaded types on Main thread", Justification = "No JoinableTaskContext; continuations resume on the dispatcher")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD109:Switch instead of assert in async methods", Justification = "No JoinableTaskContext; the assert is the contract")]
    private static async Task<IWpfCamera> OpenAsync(string id, Dispatcher dispatcher,
        bool useConfiguredFormat)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(dispatcher);
        dispatcher.VerifyAccess();

        var group = await MediaFrameSourceGroup.FromIdAsync(id)
            ?? throw new InvalidOperationException("The camera is no longer available.");

        MediaCapture? capture = null;
        MediaFrameReader? reader = null;
        WinRtCameraPreview? preview = null;
        try
        {
            capture = new MediaCapture();
            await capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                SourceGroup = group,
                SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                // Auto leaves frames in GPU memory, which is what the preview path needs.
                MemoryPreference = MediaCaptureMemoryPreference.Auto,
                StreamingCaptureMode = StreamingCaptureMode.Video,
                PhotoCaptureSource = PhotoCaptureSource.Auto,
            });

            var sourceInfo = group.SourceInfos.First(WinRtCameraDeviceEnumerator.IsColorVideoSource);
            var source = capture.FrameSources[sourceInfo.Id];

            var configured = useConfiguredFormat ? ConfiguredFormat(id, source) : null;
            if (configured is not null)
            {
                await source.SetFormatAsync(configured);
            }

            // Bgra8 is what makes the pipeline hand back a B8G8R8A8 surface D3DImage can take.
            reader = await capture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8);

            var video = source.CurrentFormat.VideoFormat;
            preview = new WinRtCameraPreview(dispatcher, (int)video.Width, (int)video.Height);

            var camera = new WinRtCamera(capture, reader, preview, dispatcher);

            // Owned by the camera now; keep the cleanup below from disposing them.
            capture = null;
            reader = null;
            preview = null;

            try
            {
                await camera.StartAsync();
            }
            catch when (configured is not null)
            {
                // An unusable configured format only fails at start; fall back rather than not open.
                camera.Dispose();
                StaticLogger.Logger.LogWarning(
                    "Capture: configured FormatIndex for {Camera} could not be started; using the camera default.",
                    CaptureCameraSettings.CameraKey(id));
                return await OpenAsync(id, dispatcher, useConfiguredFormat: false);
            }
            catch
            {
                camera.Dispose();
                throw;
            }

            return camera;
        }
        finally
        {
            preview?.Dispose();
            reader?.Dispose();
            capture?.Dispose();
        }
    }

    private static MediaFrameFormat? ConfiguredFormat(string id, MediaFrameSource source)
    {
        var index = CaptureCameraSettings.For(id).FormatIndex;
        if (index is not int i)
        {
            return null;
        }

        if (i < 0 || i >= source.SupportedFormats.Count)
        {
            StaticLogger.Logger.LogWarning(
                "Capture: FormatIndex {Index} for {Camera} is out of range ({Count} formats); using the camera default.",
                i, CaptureCameraSettings.CameraKey(id), source.SupportedFormats.Count);
            return null;
        }

        return source.SupportedFormats[i];
    }

    private async Task StartAsync()
    {
        var status = await _reader.StartAsync();
        if (status != MediaFrameReaderStartStatus.Success)
        {
            throw new InvalidOperationException($"The camera could not start ({status}).");
        }
    }

    public async Task<IFrame> CaptureAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // The photo pipeline, so stills are not limited to the preview resolution.
        var lowLag = await _capture.PrepareLowLagPhotoCaptureAsync(
            ImageEncodingProperties.CreateUncompressed(MediaPixelFormat.Bgra8));
        try
        {
            var photo = await lowLag.CaptureAsync();
            using var frame = photo.Frame;
            using var bitmap = frame.SoftwareBitmap
                ?? throw new InvalidOperationException("The camera returned a photo with no image data.");

            return new WinRtFrame(bitmap.ToBitmapSource());
        }
        finally
        {
            await lowLag.FinishAsync();
        }
    }

    // Runs on a frame-reader thread.
    private void Reader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        using var reference = sender.TryAcquireLatestFrame();
        var surface = reference?.VideoMediaFrame?.Direct3DSurface;
        if (surface is null)
        {
            // Frames legitimately arrive empty while the reader is starting or stopping.
            return;
        }

        using (surface)
        {
            try
            {
                _preview.TryPresent(surface);
            }
            catch (Exception)
            {
                // Likely device loss; report it rather than throw on a WinRT callback thread.
                RaiseFailed();
            }
        }
    }

    private void Capture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        => RaiseFailed();

    // Failure can arrive on any thread and more than once; callers see it once, on the UI thread.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "No JoinableTaskContext in this app; Dispatcher is the marshalling primitive")]
    private void RaiseFailed()
    {
        if (Interlocked.Exchange(ref _failedRaised, 1) == 1)
        {
            return;
        }

        _ = _dispatcher.InvokeAsync(() => Failed?.Invoke(this, EventArgs.Empty));
    }

    public void Dispose()
    {
        _dispatcher.VerifyAccess();

        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Detach first so no frame can arrive against half-disposed state.
        _reader.FrameArrived -= Reader_FrameArrived;
        _capture.Failed -= Capture_Failed;

        _reader.Dispose();
        _preview.Dispose();
        _capture.Dispose();
    }
}
