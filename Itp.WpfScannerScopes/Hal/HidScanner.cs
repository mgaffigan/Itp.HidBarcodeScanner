using Itp.HidBarcodeScanner;
using System.Threading;

namespace Itp.WpfScanners.Hal;

internal sealed class HidScanner : Scanner
{
    private HidScannerCollection Collection;

    public HidScanner()
    {
        Collection = new HidScannerCollection(SynchronizationContext.Current);
    }

    public override void Start()
    {
        Collection.ScanReceived += Collection_ScanReceived;
    }

    public override void Stop()
    {
        Collection.ScanReceived -= Collection_ScanReceived;
    }

    private void Collection_ScanReceived(object? sender, HidScanReceivedEventArgs e)
    {
        OnScanReceived(TranslateSymbology(e.Symbology), e.RawData);
    }

    private Symbology TranslateSymbology(HidScannerSymbology symbology)
    {
        switch (symbology)
        {
            case HidScannerSymbology.Datamatrix: return Symbology.Datamatrix;
            case HidScannerSymbology.Pdf417: return Symbology.PDF417;
            case HidScannerSymbology.Code128: return Symbology.Code128;
            case HidScannerSymbology.Code3of9: return Symbology.Code39;
            case HidScannerSymbology.QRCode: return Symbology.QRCode;
            case HidScannerSymbology.UpcEan13: return Symbology.UPC;
            default: return Symbology.Unknown;
        }
    }
}
