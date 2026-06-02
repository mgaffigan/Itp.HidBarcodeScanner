using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Itp.HidBarcodeScanner
{
    public enum HidScannerSymbology
    {
        // https://sps-support.honeywell.com/s/article/List-of-barcode-symbology-AIM-Identifiers
        Datamatrix = 0x5d6431,
        UCC128 = 0x5d4331,
        Code128 = 0x5d4330,
        Code93 = 0x5d4730,
        Code3of9 = 0x5d4130,
        Code2of5 = 0x5d4930,
        UpcEan13 = 0x5d4530,
        Pdf417 = 0x5d4c32,
        QRCode = 0x5d5131,
        AztecCode = 0x5d7a30,
        SerialImageData = 0x5d5830,
        SerialCommandData = 0x5d5a36
    }
}
