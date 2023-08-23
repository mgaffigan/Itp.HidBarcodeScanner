using System;

namespace Itp.WpfScanners.Hal;

public abstract class Scanner
{
    public event ScannedDataReceivedEventHandler? ScanReceived;

    protected void OnScanReceived(Symbology symbology, byte[] data)
    {
        ScanReceived?.Invoke(this, new ScannedDataEventArgs(this, symbology, data));
    }

    internal static Scanner FromConfig(ScannerConfiguration config)
    {
        if (config.ScannerType == ScannerType.SybleXb6299)
            return new SybleXb6299(config);
        else
            throw new NotSupportedException($"Unknown scanner type: '{config.ScannerType}'");
    }

    public abstract void Start();

    public abstract void Stop();
}

public sealed class NullScanner : Scanner
{
    public static NullScanner Instance { get; } = new NullScanner();

    public override void Start()
    {
        // nop
    }

    public override void Stop()
    {
        // nop
    }
}