using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;

namespace Itp.WpfCamera;

/// <summary>
/// An open camera, with a GPU-only live <see cref="Preview"/> and still capture through the photo
/// pipeline.  Must be opened, used, and disposed on a WPF dispatcher thread.
/// </summary>
public sealed class WinRtCamera : IDisposable
{
    private readonly MediaCapture _capture;
    private readonly MediaFrameReader _reader;
    private readonly WinRtCameraPreview _preview;
    private readonly Dispatcher _dispatcher;

    // Faulted by the first failure, from any thread; cancelled by Dispose.
    private readonly TaskCompletionSource _failure = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _disposed;

    private WinRtCamera(MediaFrameSourceGroup group, MediaCapture capture, MediaFrameReader reader,
        Dispatcher dispatcher, int width, int height)
    {
        Group = group;
        _capture = capture;
        _reader = reader;
        _dispatcher = dispatcher;
        _preview = new WinRtCameraPreview(dispatcher, width, height, RaiseFailed);

        _capture.Failed += Capture_Failed;
        _reader.FrameArrived += Reader_FrameArrived;
    }

    /// <summary>
    /// The camera this was opened from.
    /// </summary>
    public MediaFrameSourceGroup Group { get; }

    /// <summary>
    /// The live preview, suitable for <see cref="System.Windows.Controls.Image.Source"/>.
    /// </summary>
    public ImageSource Preview => _preview.Image;

    /// <summary>
    /// Opens <paramref name="group"/> for exclusive control and starts the preview.  Must be called
    /// on a WPF dispatcher thread.
    /// </summary>
    /// <param name="group">A camera from <see cref="WinRtCameraDeviceEnumerator.EnumerateAsync"/>.</param>
    /// <param name="failed">
    /// Optional notification, called once on the dispatcher thread if the opened camera later stops
    /// unexpectedly (e.g. device disconnect or session lock).  The camera is unusable afterwards and
    /// <see cref="CaptureAsync"/> throws, but it must still be disposed by its owner as usual.  Never
    /// called for a camera that failed to open; throws instead.
    /// </param>
    /// <exception cref="InvalidOperationException">The camera could not be opened or started.</exception>
    public static async Task<WinRtCamera> OpenAsync(MediaFrameSourceGroup group, UnhandledExceptionEventHandler? failed = null)
    {
        ArgumentNullException.ThrowIfNull(group);
        var dispatcher = Dispatcher.FromThread(Thread.CurrentThread)
            ?? throw new InvalidOperationException("A camera must be opened on a WPF dispatcher thread.");

        // Awaited without ConfigureAwait throughout, so every continuation lands back on the dispatcher.
        MediaCapture? capture = null;
        MediaFrameReader? reader = null;
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

            using var settings = CaptureCameraSettings.For(group.Id);
            var formatIndex = settings.FormatIndex;
            if (formatIndex >= 0)
            {
                if (formatIndex >= source.SupportedFormats.Count)
                {
                    throw new InvalidOperationException(
                        $"FormatIndex {formatIndex} configured for {settings.CameraKey} is out of range ({source.SupportedFormats.Count} formats).");
                }

                await source.SetFormatAsync(source.SupportedFormats[formatIndex]);
            }

            // Bgra8 is what makes the pipeline hand back a B8G8R8A8 surface D3DImage can take.
            reader = await capture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8);

            var video = source.CurrentFormat.VideoFormat;
            var camera = new WinRtCamera(group, capture, reader, dispatcher, (int)video.Width, (int)video.Height);

            // Owned by the camera now; keep the cleanup below from disposing them.
            capture = null;
            reader = null;

            try
            {
                var status = await camera._reader.StartAsync();
                if (status != MediaFrameReaderStartStatus.Success)
                {
                    throw new InvalidOperationException(formatIndex >= 0
                        ? $"The camera could not start ({status}) with FormatIndex {formatIndex} configured for {settings.CameraKey}."
                        : $"The camera could not start ({status}).");
                }

                // A failure so far fails the open.  Any later one is seen by ReportFailure,
                // since a continuation on an already-faulted task still runs.
                camera.ThrowIfFailed();
                camera.ReportFailure(failed);
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
            reader?.Dispose();
            capture?.Dispose();
        }
    }

    /// <summary>
    /// Captures a still through the photo pipeline, so it is not limited to the preview resolution.
    /// </summary>
    /// <returns>A frozen image at the camera's capture resolution.</returns>
    /// <exception cref="InvalidOperationException">The camera has failed, or the capture failed.</exception>
    public async Task<BitmapSource> CaptureAsync()
    {
        _dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfFailed();

        var lowLag = await _capture.PrepareLowLagPhotoCaptureAsync(
            ImageEncodingProperties.CreateUncompressed(MediaPixelFormat.Bgra8));
        try
        {
            var photo = await lowLag.CaptureAsync();
            using var thumbnail = photo.Thumbnail;
            using var frame = photo.Frame;
            using var bitmap = frame.SoftwareBitmap
                ?? throw new InvalidOperationException("The camera returned a photo with no image data.");

            return bitmap.ToBitmapSource();
        }
        finally
        {
            await lowLag.FinishAsync();
        }
    }

    private void ThrowIfFailed()
    {
        if (_failure.Task.Exception is { } failure)
        {
            throw new InvalidOperationException("The camera has failed.", failure.InnerException);
        }
    }

    // Resumes on the dispatcher, since it is started from OpenAsync on the dispatcher thread.  async
    // void, like any event handler: if the owner's handler throws, that reaches the dispatcher
    // rather than faulting a task nobody observes.
    private async void ReportFailure(UnhandledExceptionEventHandler? failed)
    {
        Exception failure;
        try
        {
            // Never completes successfully: faulted on failure, cancelled by Dispose.
            await _failure.Task;
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        // The owner may have disposed the camera while the failure was on its way here.
        if (!_disposed)
        {
            failed?.Invoke(this, new UnhandledExceptionEventArgs(failure, isTerminating: false));
        }
    }

    // Runs on a frame-reader thread, so nothing may escape it.
    private void Reader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        try
        {
            using var reference = sender.TryAcquireLatestFrame();
            using var surface = reference?.VideoMediaFrame?.Direct3DSurface;
            if (surface is null)
            {
                // Frames legitimately arrive empty while the reader is starting or stopping.
                return;
            }

            _preview.Present(surface);
        }
        catch (Exception ex)
        {
            // Likely device loss; report it rather than throw on a WinRT callback thread.
            RaiseFailed(ex);
        }
    }

    private void Capture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        => RaiseFailed(new InvalidOperationException(
            $"The camera stopped unexpectedly: {errorEventArgs.Message} (0x{errorEventArgs.Code:X8})"));

    // Failure can arrive on any thread and more than once; only the first is kept.
    private void RaiseFailed(Exception exception) => _failure.TrySetException(exception);

    /// <summary>
    /// Stops the preview and releases the camera.  Must be called on the dispatcher thread the
    /// camera was opened on.
    /// </summary>
    public void Dispose()
    {
        _dispatcher.VerifyAccess();

        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _failure.TrySetCanceled();

        // Detach first so no frame can arrive against half-disposed state.
        _reader.FrameArrived -= Reader_FrameArrived;
        _capture.Failed -= Capture_Failed;

        // Chained so a failure releasing one still releases the rest.
        try
        {
            _reader.Dispose();
        }
        finally
        {
            try
            {
                _preview.Dispose();
            }
            finally
            {
                _capture.Dispose();
            }
        }
    }
}
