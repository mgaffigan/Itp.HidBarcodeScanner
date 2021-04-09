using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Itp.HidBarcodeScanner
{
    public sealed class HidScanReceivedEventArgs : EventArgs
    {
        public byte[] RawData { get; }
        public HidScannerSymbology Symbology { get; }

        public string TextData => Encoding.ASCII.GetString(RawData);

        public HidScanReceivedEventArgs(byte[] data, HidScannerSymbology symbology)
        {
            this.RawData = data;
            this.Symbology = symbology;
        }
    }
}
