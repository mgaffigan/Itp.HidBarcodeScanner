using System.Windows.Threading;

namespace Itp.Handheld.WpfClient.Capture.WinRt;

internal sealed class WinRtCameraFactory : ICameraFactory
{
    private readonly string _id;
    private readonly Dispatcher _dispatcher;

    // Constructed on the UI thread; the preview and the Failed event marshal back to it.
    public WinRtCameraFactory(string name, string id)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(id);

        Name = name;
        _id = id;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    public string Name { get; }

    public Task<IWpfCamera> CreateAsync() => WinRtCamera.OpenAsync(_id, _dispatcher);
}
