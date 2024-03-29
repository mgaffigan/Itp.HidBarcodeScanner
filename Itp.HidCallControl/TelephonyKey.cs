﻿namespace Itp.HidCallControl;

// https://www.usb.org/sites/default/files/documents/hut1_12v2.pdf#page=69
public enum TelephonyKey
{
    Unknown = 0,
    HookSwitch = 0x20,
    Flash = 0x21,
    Feature = 0x22,
    Hold = 0x23,
    Redial = 0x24,
    Transfer = 0x25,
    Drop = 0x26,
    Park = 0x27,
    PhoneMute = 0x2f,
}
