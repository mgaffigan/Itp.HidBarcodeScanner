using System;

namespace Itp.WpfScanners.Hal;

class SybleXb6299 : LineBasedScanner
{
    public SybleXb6299(string portName)
        : base(portName)
    {
    }

    public SybleXb6299(ScannerConfiguration config)
        : this(config.PortName ?? throw new InvalidOperationException("Port name must be specified"))
    {
    }

    protected override void DataReceivedInternal(byte[] buffer, out int processedThrough, int length)
    {
        const byte SOM = 0x01, EOM = 0x03;

        int somPos = 0;
        for (; somPos < length && buffer[somPos] != SOM; somPos++) /* no-op */;
        if (somPos == length)
        {
            // No SOM found, ignore all
            processedThrough = length;
            return;
        }

        int eomPos = somPos + 2 /* STX Symbology */;
        for (; eomPos < length && buffer[eomPos] != EOM; eomPos++) /* no-op */;
        if (eomPos == length)
        {
            // No SOM found, ignore all
            processedThrough = somPos;
            return;
        }

        // processed through EOM
        // Note: multiple barcodes in same read is not supported
        processedThrough = length;

        var barcode = new byte[eomPos - somPos - 2];
        Buffer.BlockCopy(buffer, somPos + 2, barcode, 0, barcode.Length);
        OnScanReceived(parseSymbology((char)buffer[somPos + 1]), barcode);
    }

    private Symbology parseSymbology(char c)
    {
        switch (c)
        {
            case 'd': return Symbology.EAN;
            case 'c': return Symbology.UPC;
            case 'j': return Symbology.GS1;
            case 'b': return Symbology.Code39;
            case 'i': return Symbology.Unknown /* Code 39 */;
            case 'a': return Symbology.Codabar;
            case 'e': return Symbology.Unknown /* Interleaved 2 of 5 */;
            case 'D': return Symbology.Unknown /* Industrial 2 of 5 */;
            case 'v': return Symbology.Unknown /* Matrix 2 of 5 */;
            case 'H': return Symbology.Unknown /* Code 11 */;
            case 'm': return Symbology.ModifiedPlessey;
            case 'R': return Symbology.Unknown /* RSS */;
            case 'Q': return Symbology.QRCode;
            case 'u': return Symbology.Datamatrix;
            case 'r': return Symbology.PDF417;
            default: return Symbology.Unknown;
        }
    }
}
