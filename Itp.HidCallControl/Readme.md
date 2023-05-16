# ITP HID Call Control API

User-mode HID API permitting USB HID keypads, headsets, and handsets from Windows Desktop applications.
Does not depend upon "companion software" to read keypresses.

API Monitors for USB devices being connected and disconnected, and aggregates them in real time.  When no
event handler is connected to `TelephonyControlCollection.KeyPressed`, the device is disconnected.

Example use:

    var dev = new TelephonyControlCollection(new DispatcherSynchronizationContext());
    dev.KeyPressed += (_, e) => Console.WriteLine(e.Key);
    dev.UnhandledException += (_, ex) => Debug.WriteLine(ex);

## Device Support

Any device which exposes the [Telephony HID page](https://www.usb.org/sites/default/files/documents/hut1_12v2.pdf#page=69) should be compatible.  All input 
reports for single-bit inputs (push buttons) are passed on to app.

Tested devices:

* Gigaset ION (DECT Wireless Handset)
* Poly/Plantronics C3200 (USB Headset)