using DirectN.Extensions.Com;
using ComObject = DirectN.Extensions.Com.ComObject;

namespace Itp.WpfCamera;

internal static class ComObjectEx
{
    // QIs o (e.g. a WinRT RCW) for T.  The caller owns the result and releases it by disposing;
    // o is unaffected.  Throws InvalidCastException if o does not implement T.
    public static IComObject<T> QueryInterface<T>(object o)
    {
        ArgumentNullException.ThrowIfNull(o);

        // ToComInstanceOfType returns an AddRef'd pointer that we own; FromPointer adopts it.
        return ComObject.FromPointer<T>(ComObject.ToComInstanceOfType<T>(o))!;
    }
}
