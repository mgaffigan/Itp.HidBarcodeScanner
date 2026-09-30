using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DirectN;
using DirectN.Extensions;
using DirectN.Extensions.Com;
using ComObject = DirectN.Extensions.Com.ComObject;
using WinRtSurface = Windows.Graphics.DirectX.Direct3D11.IDirect3DSurface;

namespace Itp.Handheld.WpfClient.Capture.WinRt;

// D3DImage only takes a D3D9Ex surface, so frames reach it through a texture shared with D3D11, never via the CPU.
//
// Only TryPresent is entered from the frame reader thread; the UI-facing entry points assert the
// dispatcher rather than marshalling onto it. The private resource helpers below run on whichever
// thread holds _gate.
//
//   frame reader thread                       UI thread
//   -------------------                       ---------
//   TryPresent                                ctor, AttachBackBuffer, Dispose
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

    private IComObject<IDirect3D9Ex>? _d3d9;
    private IComObject<IDirect3DDevice9Ex>? _device9;
    private IComObject<IDirect3DTexture9>? _texture9;
    private IComObject<IDirect3DSurface9>? _surface9;
    private IntPtr _surface9Ptr;
    private HANDLE _sharedHandle;

    private IComObject<ID3D11Device>? _device11;
    private IComObject<ID3D11DeviceContext>? _context11;
    private IComObject<ID3D11Texture2D>? _destination11;

    private int _width;
    private int _height;
    private int _presenting;
    private bool _disposed;

    // Built at the format size up front, or the bound Image lays out at 0x0 and stays black.
    public WinRtCameraPreview(Dispatcher dispatcher, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        dispatcher.VerifyAccess();

        _dispatcher = dispatcher;
        _image.IsFrontBufferAvailableChanged += (_, _) => AttachBackBuffer();

        lock (_gate)
        {
            EnsureD3D9(width, height);
        }

        AttachBackBuffer();
    }

    private void AttachBackBuffer()
    {
        _dispatcher.VerifyAccess();

        // The frame thread can free and rebuild the surface.
        lock (_gate)
        {
            if (_disposed || _surface9Ptr == IntPtr.Zero || !_image.IsFrontBufferAvailable)
            {
                return;
            }

            _image.Lock();
            try
            {
                _image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, _surface9Ptr);
            }
            finally
            {
                _image.Unlock();
            }
        }
    }

    public ImageSource Image => _image;

    // Runs on the frame reader's thread.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "No JoinableTaskContext in this app; Dispatcher is the marshalling primitive")]
    public bool TryPresent(WinRtSurface frameSurface)
    {
        if (_disposed)
        {
            return false;
        }

        var description = frameSurface.Description;

        // Claimed before the copy, so only one copy + present cycle ever runs; drop frames rather
        // than queue them. Monitor is no use: the claim is released on the thread that presents.
        if (Interlocked.CompareExchange(ref _presenting, 1, 0) != 0)
        {
            return true;
        }

        bool queued = false;
        try
        {
            using (var source = GetTexture(frameSurface))
            {
                lock (_gate)
                {
                    if (_disposed)
                    {
                        return false;
                    }

                    EnsureD3D11(source, description.Width, description.Height);
                    if (_context11 is null || _destination11 is null)
                    {
                        return false;
                    }

                    _context11.Object.CopyResource(_destination11.Object, source.Object);
                    _context11.Object.Flush();
                }
            }

            _ = _dispatcher.InvokeAsync(PresentOnUiThread);
            queued = true;
            return true;
        }
        finally
        {
            if (!queued)
            {
                Volatile.Write(ref _presenting, 0);
            }
        }
    }

    private void PresentOnUiThread()
    {
        _dispatcher.VerifyAccess();

        try
        {
            // The frame thread can free and rebuild the surface and change its size.
            lock (_gate)
            {
                if (_disposed || !_image.IsFrontBufferAvailable || _surface9Ptr == IntPtr.Zero)
                {
                    return;
                }

                _image.Lock();
                try
                {
                    _image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, _surface9Ptr);
                    _image.AddDirtyRect(new Int32Rect(0, 0, _width, _height));
                }
                finally
                {
                    _image.Unlock();
                }
            }
        }
        catch (Exception)
        {
            // A lost device surfaces here; the next frame rebuilds the resources.
        }
        finally
        {
            Volatile.Write(ref _presenting, 0);
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

        _device11 = source.GetDevice();
        _context11 = _device11.GetImmediateContext();

        // MediaCapture shares the device with the frame server, so guard concurrent use.
        if (_device11.Object is ID3D11Multithread multithread)
        {
            multithread.SetMultithreadProtected(true);
        }

        _destination11 = _device11.OpenSharedResource<ID3D11Texture2D>(_sharedHandle);
    }

    private void EnsureD3D9(int width, int height)
    {
        if (_surface9Ptr != IntPtr.Zero && _width == width && _height == height)
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

        // D3DImage only takes a raw interface pointer.
        _surface9Ptr = _surface9.ToComInstance();
    }

    // The caller owns the returned reference and releases it per frame.
    private static IComObject<ID3D11Texture2D> GetTexture(WinRtSurface surface)
    {
        var unknown = ComObject.ToComInstance(surface);
        try
        {
            using var access = ComObject.FromPointer<IDirect3DDxgiInterfaceAccess>(
                ComObject.QueryInterface<IDirect3DDxgiInterfaceAccess>(unknown, throwOnError: true))!;
            access.Object.GetInterface(typeof(ID3D11Texture2D).GUID, out var texturePtr).ThrowOnError();
            return ComObject.FromPointer<ID3D11Texture2D>(texturePtr)!;
        }
        finally
        {
            ComObject.Release(unknown);
        }
    }

    private void ReleaseResources()
    {
        _destination11?.Dispose();
        _context11?.Dispose();
        _device11?.Dispose();

        if (_surface9Ptr != IntPtr.Zero)
        {
            Marshal.Release(_surface9Ptr);
            _surface9Ptr = IntPtr.Zero;
        }

        _surface9?.Dispose();
        _texture9?.Dispose();
        _device9?.Dispose();
        _d3d9?.Dispose();

        _sharedHandle = default;
    }

    public void Dispose()
    {
        _dispatcher.VerifyAccess();

        _disposed = true;
        lock (_gate)
        {
            ReleaseResources();
        }

        ClearBackBuffer();
    }

    private void ClearBackBuffer()
    {
        try
        {
            _image.Lock();
            _image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
            _image.Unlock();
        }
        catch (Exception)
        {
            // Nothing useful to do while tearing down.
        }
    }
}
