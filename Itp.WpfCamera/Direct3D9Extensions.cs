using System.Runtime.CompilerServices;
using DirectN;
using DirectN.Extensions;
using DirectN.Extensions.Com;
using ComObject = DirectN.Extensions.Com.ComObject;

namespace Itp.Handheld.WpfClient.Capture.WinRt;

// DirectN.Extensions wraps DXGI, D2D and D3D12 this way, but not D3D9Ex.
internal static class Direct3D9Extensions
{
    public static IComObject<IDirect3D9Ex> Direct3DCreate9Ex()
    {
        Functions.Direct3DCreate9Ex(Constants.D3D_SDK_VERSION, out var d3d9).ThrowOnError();
        return new ComObject<IDirect3D9Ex>(d3d9);
    }

    public static IComObject<IDirect3DDevice9Ex> CreateDeviceEx(this IComObject<IDirect3D9Ex> d3d9,
        uint adapter, D3DDEVTYPE deviceType, HWND focusWindow, uint behaviorFlags,
        ref D3DPRESENT_PARAMETERS presentParameters)
    {
        ArgumentNullException.ThrowIfNull(d3d9);
        d3d9.Object.CreateDeviceEx(adapter, deviceType, focusWindow, behaviorFlags,
            ref presentParameters, ref Unsafe.NullRef<D3DDISPLAYMODEEX>(), out var device)
            .ThrowOnError();
        return new ComObject<IDirect3DDevice9Ex>(device);
    }

    // sharedHandle doubles as an out-parameter: passing a non-null one is what asks D3D9 to make
    // the texture shareable.
    public static IComObject<IDirect3DTexture9> CreateTexture(this IComObject<IDirect3DDevice9Ex> device,
        uint width, uint height, uint levels, uint usage, D3DFORMAT format, D3DPOOL pool,
        ref HANDLE sharedHandle)
    {
        ArgumentNullException.ThrowIfNull(device);
        device.Object.CreateTexture(width, height, levels, usage, format, pool, out var texture,
            ref sharedHandle).ThrowOnError();
        return new ComObject<IDirect3DTexture9>(texture);
    }

    public static IComObject<IDirect3DSurface9> GetSurfaceLevel(
        this IComObject<IDirect3DTexture9> texture, uint level)
    {
        ArgumentNullException.ThrowIfNull(texture);
        texture.Object.GetSurfaceLevel(level, out var surface).ThrowOnError();
        return new ComObject<IDirect3DSurface9>(surface);
    }
}
