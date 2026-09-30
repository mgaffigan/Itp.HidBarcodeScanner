using System.IO;

namespace Itp.Handheld.WpfClient.Capture;

public interface IFrame
{
    string MimeType { get; }

    void WriteTo(Stream stream);
}
