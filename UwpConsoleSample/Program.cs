using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Devices.PointOfService;

{
    using var pos = await BarcodeScanner.GetDefaultAsync();
    using var claim = await pos.ClaimScannerAsync();
    await claim.EnableAsync();
    claim.IsDecodeDataEnabled = true;
    claim.DataReceived += (s, e) => Console.WriteLine($"{e.Report.ScanDataType:x8}: {Encoding.UTF8.GetString(e.Report.ScanData.ToArray())}");

    Console.ReadLine();
}
Console.WriteLine("Disconnected");
Console.ReadLine();