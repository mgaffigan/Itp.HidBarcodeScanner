using System;
using System.Text;
using Itp.WpfScanners.Hal;

namespace Itp.WpfScanners;

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
        : this(scanner, symbol, data, Encoding.ASCII.GetString(data))
    {
        // nop
    }
    public ScannedDataEventArgs(Scanner scanner, Symbology symbol, string textData)
        : this(scanner, symbol, Encoding.ASCII.GetBytes(textData), textData)
    {
        // nop
    }

    public ScannedDataEventArgs(Scanner scanner, Symbology symbol, byte[] data, string textData)
    {
        Source = scanner ?? throw new ArgumentNullException(nameof(scanner));
        SourceSymbology = symbol;
        RawData = data;
        TextData = textData;
    }

    public static ScannedDataEventArgs FromKeyboard(string s) 
        => new ScannedDataEventArgs(NullScanner.Instance, Symbology.Unknown, s);

    public override string ToString() => $"{TextData} ({SourceSymbology} from {Source})";
}
