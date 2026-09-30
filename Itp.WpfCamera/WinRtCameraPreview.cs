using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DirectN;
using DirectN.Extensions;
using DirectN.Extensions.Com;
using WinRtSurface = Windows.Graphics.DirectX.Direct3D11.IDirect3DSurface;

namespace Itp.WpfCamera;

// D3DImage only takes a D3D9Ex surface, so frames reach it through a texture shared with D3D11, never via the CPU.
//
// Only Present is entered from the frame reader thread; the UI-facing entry points assert the
// dispatcher rather than marshalling onto it. The private resource helpers below run on whichever
// thread holds _gate.
//
//   frame reader thread                       UI thread
//   -------------------                       ---------
//   Present                                   ctor, AttachBackBuffer, Dispose
//     lock (_gate) { copy to _destination11 }
//     (gate released)
//     InvokeAsync ---------------------------> PresentOnUiThread
//                    does not wait               lock (_gate) { set back buffer }
//
// That InvokeAsync is the only edge between them and it never waits, so a thread holding _gate is
// never blocked on one that wants it. No cycle, so nothing to deadlock on.
internal sealed class WinRtCameraPreview : IDisposable
{
    private readonly object _gate = new();
    private readonly D3DImage _image = new();
    private readonly Dispatcher _dispatcher;
    private readonly Action<Exception> _failed;

    private IComObject<IDirect3D9Ex>? _d3d9;
    private IComObject<IDirect3DDevice9Ex>? _device9;
    private IComObject<IDirect3DTexture9>? _texture9;
    private IComObject<IDirect3DSurface9>? _surface9;
    private HANDLE _sharedHandle;

    private IComObject<ID3D11Device>? _device11;
    private IComObject<ID3D11DeviceContext>? _context11;
    private IComObject<ID3D11Texture2D>? _destination11;
    private IComObject<ID3D11Query>? _copied11;

    // Long enough for any real copy; a hung GPU fails the camera instead of blocking frames forever.
    private static readonly TimeSpan CopyTimeout = TimeSpan.FromSeconds(1);

    private int _width;
    private int _height;
    // Both guarded by _gate.
    private bool _presentPending;
    private bool _disposed;

    // Built at the format size up front, or the bound Image lays out at 0x0 and stays black.
    // failed is called on the UI thread when presenting fails; the owner reports it and tears down.
    public WinRtCameraPreview(Dispatcher dispatcher, int width, int height, Action<Exception> failed)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(failed);
        dispatcher.VerifyAccess();

        _dispatcher = dispatcher;
        _failed = failed;
        _image.IsFrontBufferAvailableChanged += Image_IsFrontBufferAvailableChanged;

        try
        {
            lock (_gate)
            {
                EnsureD3D9(width, height);
            }

            AttachBackBuffer();
        }
        catch
        {
            // Nobody else can dispose a preview whose constructor threw.
            Dispose();
            throw;
        }
    }

    // Raised by WPF, so an exception here would reach the dispatcher.
    private void Image_IsFrontBufferAvailableChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        try
        {
            AttachBackBuffer();
        }
        catch (Exception ex)
        {
            _failed(ex);
        }
    }

    private void AttachBackBuffer()
    {
        _dispatcher.VerifyAccess();

        // The frame thread can free and rebuild the surface.
        lock (_gate)
        {
            if (_disposed || _surface9 is null || !_image.IsFrontBufferAvailable)
            {
                return;
            }

            _image.Lock();
            try
            {
                _image.SetBackBuffer(_surface9);
            }
            finally
            {
                _image.Unlock();
            }
        }
    }

    public ImageSource Image => _image;

    // Runs on the frame reader's thread.
    public void Present(WinRtSurface frameSurface)
    {
        var description = frameSurface.Description;
        using var source = frameSurface.GetTexture2D();
        lock (_gate)
        {
            // Only one copy + present cycle ever runs; drop frames rather than queue them.
            if (_disposed || _presentPending)
            {
                return;
            }

            EnsureD3D11(source, description.Width, description.Height);
            _context11!.Object.CopyResource(_destination11!.Object, source.Object);

            // Flush alone only submits the copy; WPF reads the surface from another device, so
            // wait for the copy to finish or it can show a partial frame.
            _context11.WaitForGpu(_copied11!, CopyTimeout);
            _presentPending = true;
        }

        _ = _dispatcher.InvokeAsync(PresentOnUiThread);
    }

    private void PresentOnUiThread()
    {
        _dispatcher.VerifyAccess();

        try
        {
            // The frame thread can free and rebuild the surface and change its size.
            lock (_gate)
            {
                _presentPending = false;
                if (_disposed || !_image.IsFrontBufferAvailable || _surface9 is null)
                {
                    return;
                }

                _image.Lock();
                try
                {
                    _image.SetBackBuffer(_surface9);
                    _image.AddDirtyRect(new Int32Rect(0, 0, _width, _height));
                }
                finally
                {
                    _image.Unlock();
                }
            }
        }
        catch (Exception ex)
        {
            // A lost device surfaces here.  Report it so the owner tears down rather than
            // presenting against a broken device.
            _failed(ex);
        }
    }

    // Binds to the device the frames already live on, which avoids cross-device sharing.
    private void EnsureD3D11(IComObject<ID3D11Texture2D> source, int width, int height)
    {
        // Rebuilds only if frames differ from the size the format advertised.
        EnsureD3D9(width, height);

        if (_destination11 is not null)
        {
            return;
        }

        // A previous attempt may have failed partway; release what it left before rebuilding.
        ReleaseD3D11();
        _device11 = source.GetDevice();
        _context11 = _device11.GetImmediateContext();
        _copied11 = _device11.CreateEventQuery();

        // MediaCapture shares the device with the frame server, so guard concurrent use.
        _device11.SetMultithreadProtected(true);

        _destination11 = _device11.OpenSharedResource<ID3D11Texture2D>(_sharedHandle);
    }

    private void EnsureD3D9(int width, int height)
    {
        if (_surface9 is not null && _width == width && _height == height)
        {
            return;
        }

        ReleaseResources();
        _width = width;
        _height = height;
        CreateSharedD3D9Texture(width, height);
    }

    private void CreateSharedD3D9Texture(int width, int height)
    {
        _d3d9 = Direct3D9Extensions.Direct3DCreate9Ex();

        var hwnd = Functions.GetDesktopWindow();
        var presentParameters = new D3DPRESENT_PARAMETERS
        {
            BackBufferWidth = 1,
            BackBufferHeight = 1,
            BackBufferFormat = D3DFORMAT.D3DFMT_X8R8G8B8,
            BackBufferCount = 1,
            SwapEffect = D3DSWAPEFFECT.D3DSWAPEFFECT_DISCARD,
            hDeviceWindow = hwnd,
            Windowed = true,
            PresentationInterval = unchecked((uint)Constants.D3DPRESENT_INTERVAL_IMMEDIATE),
        };

        _device9 = _d3d9.CreateDeviceEx(Constants.D3DADAPTER_DEFAULT, D3DDEVTYPE.D3DDEVTYPE_HAL, hwnd,
            unchecked((uint)(Constants.D3DCREATE_MULTITHREADED | Constants.D3DCREATE_HARDWARE_VERTEXPROCESSING)),
            ref presentParameters);

        var shared = new HANDLE();
        _texture9 = _device9.CreateTexture((uint)width, (uint)height, 1,
            (uint)Constants.D3DUSAGE_RENDERTARGET, D3DFORMAT.D3DFMT_A8R8G8B8, D3DPOOL.D3DPOOL_DEFAULT,
            ref shared);
        _sharedHandle = shared;

        _surface9 = _texture9.GetSurfaceLevel(0);
    }

    // Nulled as well as disposed: EnsureD3D9 and EnsureD3D11 rebuild whatever is null.
    private void ReleaseResources()
    {
        ReleaseD3D11();
        DisposeAndNull(ref _surface9);
        DisposeAndNull(ref _texture9);
        DisposeAndNull(ref _device9);
        DisposeAndNull(ref _d3d9);

        _sharedHandle = default;
    }

    private void ReleaseD3D11()
    {
        DisposeAndNull(ref _destination11);
        DisposeAndNull(ref _copied11);
        DisposeAndNull(ref _context11);
        DisposeAndNull(ref _device11);
    }

    private static void DisposeAndNull<T>(ref T? disposable) where T : class, IDisposable
    {
        disposable?.Dispose();
        disposable = null;
    }

    public void Dispose()
    {
        _dispatcher.VerifyAccess();

        _image.IsFrontBufferAvailableChanged -= Image_IsFrontBufferAvailableChanged;
        lock (_gate)
        {
            _disposed = true;

            // Detach WPF from the surface before releasing it.
            _image.Lock();
            try
            {
                _image.SetBackBuffer(null);
            }
            finally
            {
                _image.Unlock();
            }

            ReleaseResources();
        }
    }
}
