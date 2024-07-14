namespace Itp.WpfScanners;

public enum Symbology
{
    Unknown = 0,
    Codabar = 1,
    ModifiedPlessey = 2,
    Code128 = 3,
    GSS128 = 4,
    GS1_GSS128 = 5,
    EAN = 6,//(All Types to one value)
    UPC = 7,//(All Types to one Value)
    Datamatrix = 8,
    QRCode = 9,
    AztecCode = 10,
    PDF417 = 11,
    MicroPDF417 = 12,
    Code39 = 13,
    GS1 = 14, //all types
    RFID = 15,
}
