using System;
using System.Collections.ObjectModel;
using Esatto.Win32.Registry;

namespace Itp.WpfScanners.Hal;

public class RegistryScannerConfiguration : RegistrySettings
{
    public static RegistryScannerConfiguration Config { get; } = new();

    public ObservableCollection<ScannerConfiguration> Scanners { get; private set; }
    private const string keyRoot = @"In Touch Technologies\Esatto\Wpf.Scanners";

    public RegistryScannerConfiguration()
        : base(keyRoot)
    {
        Scanners = new ObservableCollection<ScannerConfiguration>();
        var scannerKeyNames = ConfigKey.GetSubKeyNames();

        foreach (var name in scannerKeyNames)
        {
            var scanner = new ScannerConfiguration(keyRoot + @"\" + name);
            if (!string.IsNullOrWhiteSpace(scanner.PortName))
            {
                Scanners.Add(scanner);
            }
        }
    }

    public ScannerConfiguration AddConfiguration()
    {
        var scanner = new ScannerConfiguration();
        Scanners.Add(scanner);
        return scanner;
    }

    public void RemoveScanner(ScannerConfiguration config)
    {
        if (config == null)
            throw new ArgumentNullException("config");
        if (!Scanners.Contains(config))
            throw new ArgumentOutOfRangeException("config not found");

        Scanners.Remove(config);
        config.Delete();
    }

    public bool AutodetectPnP
    {
        get { return GetBool("AutodetectPnP", true); }
        set { SetBool("AutodetectPnP", value); }
    }
}
