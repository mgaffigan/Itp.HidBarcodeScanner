using DirectN;
using DirectN.Extensions.Com;
using ComObject = DirectN.Extensions.Com.ComObject;
using WinRtSurface = Windows.Graphics.DirectX.Direct3D11.IDirect3DSurface;

namespace Itp.WpfCamera;

internal static class Direct3DSurfaceExtensions
{
    // The D3D11 texture behind a WinRT surface.  The caller owns the returned texture.
    public static IComObject<ID3D11Texture2D> GetTexture2D(this WinRtSurface surface)
    {
        using var access = ComObjectEx.QueryInterface<IDirect3DDxgiInterfaceAccess>(surface);

        // GetInterface returns an AddRef'd texture, which the returned wrapper adopts.
        access.Object.GetInterface(typeof(ID3D11Texture2D).GUID, out var texture).ThrowOnError();
        return ComObject.FromPointer<ID3D11Texture2D>(texture)!;
    }
}
