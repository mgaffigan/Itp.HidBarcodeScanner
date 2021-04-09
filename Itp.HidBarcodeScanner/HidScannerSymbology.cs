using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Itp.HidBarcodeScanner
{
    public enum HidScannerSymbology
    {
        Datamatrix = 0x5d6431,
        UCC128 = 0x5d4331,
        Code128 = 0x5d4330,
        Code93 = 0x5d4730,
        Code3of9 = 0x5d4130,
        Code2of5 = 0x5d4930,
        UpcEan13 = 0x5d4530,
        Pdf417 = 0x5d4c32,
        QRCode = 0x5d5131,
        AztecCode = 0x5d7a30
    }
}
