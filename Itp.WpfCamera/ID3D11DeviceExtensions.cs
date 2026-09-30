using DirectN;
using DirectN.Extensions.Com;
using ComObject = DirectN.Extensions.Com.ComObject;

namespace Itp.Handheld.WpfClient.Capture.WinRt;

internal static class ID3D11DeviceExtensions
{
    public static IComObject<ID3D11Device> GetDevice(this IComObject<ID3D11Texture2D> resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        resource.Object.GetDevice(out var device);
        return new ComObject<ID3D11Device>(device);
    }

    // Unwrap first, or this binds back to itself.
    public static IComObject<T> OpenSharedResource<T>(this IComObject<ID3D11Device> device, HANDLE hResource)
    {
        ArgumentNullException.ThrowIfNull(device);
        return device.Object.OpenSharedResource<T>(hResource);
    }

    public static IComObject<T> OpenSharedResource<T>(this ID3D11Device device, HANDLE hResource)
    {
        ArgumentNullException.ThrowIfNull(device);
        device.OpenSharedResource(hResource, typeof(T).GUID, out nint resource).ThrowOnError();
        return ComObject.FromPointer<T>(resource)!;
    }
}
