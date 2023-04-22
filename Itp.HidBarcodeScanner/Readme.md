# ITP HID Barcode Scanner API

User-mode HID API permitting use and control of USB HID POS Barcode scanners from Windows Desktop applications

API Monitors for USB devices being connected and disconnected, and aggregates them in real time.  When no
event handler is connected to `ScanReceived`, the scanner is disabled.

Example use:

	Scanner = new HidScannerCollection(SynchronizationContext.Current ?? throw new InvalidOperationException());
	Scanner.ScanReceived += (_, e) => Console.WriteLine(e.TextData);

## Known issues

* On certain early versions (pre-Anniversary Update) of Windows 10, BSOD can result 
  from the non-Generic HID driver.  To avoid, set the driver to Generic HID instead 
  of HID POS Barcode Scanner

## Device Support

Any device which exposes the POS Barcode Scanner HID page should be compatible.  A partial list is available
on [Microsoft's website](https://learn.microsoft.com/en-us/windows/uwp/devices-sensors/pos-device-support).

Tested devices:

* Honeywell Xenon 1900
* Zebra DS2208