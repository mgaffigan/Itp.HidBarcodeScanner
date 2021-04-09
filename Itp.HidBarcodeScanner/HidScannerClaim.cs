using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.HumanInterfaceDevice;

namespace Itp.HidBarcodeScanner
{
    class HidScannerClaim : IDisposable
    {
        private readonly HidDevice Device;
        public string DeviceId { get; }
        private MemoryStream Buffer;
        private readonly SynchronizationContext SyncCtx;

        public event EventHandler<HidScanReceivedEventArgs> ScanReceived;

        private HidScannerClaim(HidDevice device, string id, SynchronizationContext syncCtx)
        {
            this.Device = device;
            this.DeviceId = id;
            this.SyncCtx = syncCtx;

            this.Buffer = new MemoryStream();
            this.Device.InputReportReceived += Device_InputReportReceived;
        }

        public void Dispose()
        {
            Device.Dispose();
        }

        private void Device_InputReportReceived(HidDevice sender, HidInputReportReceivedEventArgs args)
        {
            var data = args.Report.Data.ToArray();
            var reportID = data[0];

            switch (reportID)
            {
                case 2:
                    Device_Input_2(data);
                    break;
            }
        }

        private void Device_Input_2(byte[] data)
        {
            byte length = data[1];
            int symbology = (data[2] << 16) | (data[3] << 8) | data[4];
            byte continuation = data[63];

            Buffer.Write(data, 5, length);

            if (continuation == 0)
            {
                var scannedData = Buffer.ToArray();
                Buffer = new MemoryStream();

                SyncCtx.Post((_1) =>
                {
                    ScanReceived?.Invoke(this, new HidScanReceivedEventArgs(scannedData, (HidScannerSymbology)symbology));
                }, null);
            }
        }

        internal static Task<HidScannerClaim> CreateAsync(HidDevice device, string id, SynchronizationContext syncCtx)
        {
            var claim = new HidScannerClaim(device, id, syncCtx);
            return Task.FromResult(claim);
        }
    }
}
