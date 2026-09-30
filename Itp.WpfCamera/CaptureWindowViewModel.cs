using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Capture.Frames;

namespace Itp.WpfCamera;

// Owns exactly one open camera at a time.  Accept, capture, and dispose are serialized, so a
// camera is never disposed under an in-flight capture.
internal sealed class CaptureWindowViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private WinRtCamera _camera;
    private bool _disposed;

    // Takes ownership of camera.
    public CaptureWindowViewModel(WinRtCamera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        _camera = camera;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MediaFrameSourceGroup Group => _camera.Group;

    public ImageSource Preview => _camera.Preview;

    // Takes ownership of camera, even on failure, and disposes the one it replaces.
    public async Task AcceptAsync(WinRtCamera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        await _mutex.WaitAsync();
        try
        {
            if (_disposed)
            {
                camera.Dispose();
                throw new ObjectDisposedException(nameof(CaptureWindowViewModel));
            }

            // Own the new camera before disposing the old, so a failure there cannot orphan it.
            var old = _camera;
            _camera = camera;
            old.Dispose();
        }
        finally
        {
            _mutex.Release();
        }

        CaptureSettings.Instance.SelectedCameraId = camera.Group.Id;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Group)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Preview)));
    }

    public async Task<BitmapSource> CaptureAsync()
    {
        await _mutex.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return await _camera.CaptureAsync();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _mutex.WaitAsync();
        try
        {
            if (!_disposed)
            {
                _disposed = true;
                _camera.Dispose();
            }
        }
        finally
        {
            _mutex.Release();
        }
    }
}
