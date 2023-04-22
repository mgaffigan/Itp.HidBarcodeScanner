using System;
using System.Linq;
using System.IO.Ports;
using Esatto.Win32.Registry;
using System.Runtime.Versioning;

namespace Itp.WpfScanners.Hal;

internal class ScannerConfiguration : RegistrySettings
{
    public ScannerConfiguration(string regPath)
        : base(regPath)
    {
    }

    public ScannerConfiguration()
        : this(@"In Touch Technologies\Esatto\Wpf.Scanners\" + Guid.NewGuid().ToString())
    {
    }

    public ScannerType ScannerType
    {
        get { return GetEnum<ScannerType>(nameof(ScannerType), (int)ScannerType.Unknown); }
        set { SetEnum(nameof(ScannerType), value); }
    }

    public string? PortName
    {
        get { return GetString(nameof(PortName), null); }
        set { SetString(nameof(PortName), value); }
    }

    public int BaudRate
    {
        get { return GetInt(nameof(BaudRate), 9600); }
        set { SetInt(nameof(BaudRate), value); }
    }

    /// <summary>
    /// Indicates whether the port is currently attached to this computer
    /// </summary>
    /// <returns></returns>
    public bool IsPresent()
    {
        return SerialPort.GetPortNames()
            .Any(pn => pn.Equals(PortName, StringComparison.InvariantCultureIgnoreCase));
    }

#if NET
    [SupportedOSPlatform("windows")]
#endif
    internal void Delete()
    {
        DeleteUserKey();
    }
}
