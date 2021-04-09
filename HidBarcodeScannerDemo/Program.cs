using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;

namespace HidBarcodeScannerDemo
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Func().Wait();
        }

        static async Task Func()
        { 
            var sta = HidDevice.GetDeviceSelector(0x8c, 0x02);
            var devices = await DeviceInformation.FindAllAsync(sta);
            var hiddev = await HidDevice.FromIdAsync(devices.FirstOrDefault().Id, Windows.Storage.FileAccessMode.Read);
            var scanner = new HidBarcodeScanner(hiddev);
            Console.ReadLine();
        }
    }



    class HidBarcodeScanner : IDisposable
    {
        private readonly HidDevice Device;
        private readonly MemoryStream Buffer;

        public HidBarcodeScanner(HidDevice device)
        {
            Contract.Requires(device != null);

            this.Device = device;
            this.Buffer = new MemoryStream(64);

            device.InputReportReceived += Device_InputReportReceived;
        }

        private void Device_InputReportReceived(HidDevice sender, HidInputReportReceivedEventArgs args)
        {
            var data = args.Report.Data.ToArray();
            var report = data[0];

            if (report == 2 /* Scanned data */)
            {
                byte length = data[1];
                int symbology = (data[2] << 16) | (data[3] << 8) | data[4];
                byte continuation = data[63];

                //var barcodeData = new byte[length];
                //Array.Copy(data, 5, barcodeData, 0, length);

                //var sData = Encoding.ASCII.GetString(barcodeData);
                //Console.WriteLine($"Symbology: \t{symbology:x6}\r\nData:\t\t{sData}\r\n");

                Buffer.Write(data, 5, length);

                if (continuation == 0)
                {
                    var sData = Encoding.ASCII.GetString(Buffer.ToArray());
                    Console.WriteLine($"Symbology: \t{symbology:x6}\r\nData:\t\t{sData}\r\n");
                    Buffer.SetLength(0);
                }
            }

            var sb = new StringBuilder();
            for (int i = 0; i < 54; i++)
            {
                sb.Append(i.ToString("00"));
                sb.Append(" ");
            }
            sb.AppendLine();

            foreach (var d in data)
            {
                sb.Append(d.ToString("x2"));
                sb.Append(" ");
            }
            sb.AppendLine();

            foreach (var d in data)
            {
                sb.Append(" ");
                char l = (char)d;
                if (char.IsLetterOrDigit(l))
                {
                    sb.Append(l);
                }
                else
                {
                    sb.Append(".");
                }
                sb.Append(" ");
            }
            sb.AppendLine();
            Console.WriteLine(sb.ToString());
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
