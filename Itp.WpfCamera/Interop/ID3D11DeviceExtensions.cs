using System.Diagnostics;
using DirectN;
using DirectN.Extensions.Com;
using ComObject = DirectN.Extensions.Com.ComObject;

namespace Itp.WpfCamera;

internal static class ID3D11DeviceExtensions
{
    public static IComObject<ID3D11Device> GetDevice(this IComObject<ID3D11Texture2D> resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        resource.Object.GetDevice(out var device);
        return new ComObject<ID3D11Device>(device);
    }

    // The caller owns the returned resource.
    public static IComObject<T> OpenSharedResource<T>(this IComObject<ID3D11Device> device, HANDLE hResource)
    {
        ArgumentNullException.ThrowIfNull(device);

        // OpenSharedResource returns an AddRef'd pointer, which the returned wrapper adopts.
        device.Object.OpenSharedResource(hResource, typeof(T).GUID, out nint resource).ThrowOnError();
        return ComObject.FromPointer<T>(resource)!;
    }

    public static void SetMultithreadProtected(this IComObject<ID3D11Device> device, bool multithreadProtected)
    {
        using var multithread = ComObjectEx.QueryInterface<ID3D11Multithread>(device);
        multithread.Object.SetMultithreadProtected(multithreadProtected);
    }

    // The caller owns the returned query.
    public static unsafe IComObject<ID3D11Query> CreateEventQuery(this IComObject<ID3D11Device> device)
    {
        ArgumentNullException.ThrowIfNull(device);

        var desc = new D3D11_QUERY_DESC { Query = D3D11_QUERY.D3D11_QUERY_EVENT };
        nint query;

        // CreateQuery returns an AddRef'd pointer, which the returned wrapper adopts.
        device.Object.CreateQuery(in desc, (nint)(&query)).ThrowOnError();
        return ComObject.FromPointer<ID3D11Query>(query)!;
    }

    // Blocks until the GPU has executed everything issued on context so far.  eventQuery is from
    // CreateEventQuery on the context's device.
    public static void WaitForGpu(this IComObject<ID3D11DeviceContext> context, IComObject<ID3D11Query> eventQuery,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(eventQuery);

        context.Object.End(eventQuery.Object);

        // GetData with no flags flushes the context, so the query is certain to complete.
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            var hr = context.Object.GetData(eventQuery.Object, 0, 0, 0);
            hr.ThrowOnError();
            if (hr != Constants.S_FALSE)
            {
                return;
            }

            if (elapsed.Elapsed > timeout)
            {
                throw new TimeoutException($"The GPU did not finish within {timeout.TotalMilliseconds} ms.");
            }

            Thread.Yield();
        }
    }
}
