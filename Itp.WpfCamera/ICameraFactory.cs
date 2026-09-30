namespace Itp.Handheld.WpfClient.Capture;

public interface ICameraFactory
{
    string Name { get; }

    Task<IWpfCamera> CreateAsync();
}
