using System;
using System.Text;

namespace Itp.WpfScanners.Hal;

public delegate void ScannedDataReceivedEventHandler(object? sender, ScannedDataEventArgs args);

public class ScannedDataEventArgs : EventArgs
{
    /// <summary>
    /// Scanner used to scan the barcode
    /// </summary>
    public Scanner Source { get; internal set; }

    /// <summary>
    /// Text representation of the scanned data
    /// </summary>
    public string TextData { get; internal set; }

    /// <summary>
    /// Raw data received as decoded from the scanner
    /// </summary>
    public byte[] RawData { get; internal set; }

    /// <summary>
    /// Symbology of the scanned barcode, if known
    /// </summary>
    public Symbology SourceSymbology { get; internal set; }

    public bool IsHandled { get; set; }

    public ScannedDataEventArgs(Scanner scanner, Symbology symbol, byte[] data)
    {
        Source = scanner;
        SourceSymbology = symbol;
        RawData = data;

        TextData = Encoding.ASCII.GetString(data);
    }
}
