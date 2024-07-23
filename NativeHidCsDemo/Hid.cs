using global::System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using Windows.Win32.Devices.HumanInterfaceDevice;

namespace Windows.Win32
{

    internal static partial class PInvoke
    {
        [DllImport("HID.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern unsafe bool HidD_GetPreparsedData(SafeFileHandle HidDeviceObject, out SafeHidPreparsedDataHandle PreparsedData);
    }

    internal class SafeHidPreparsedDataHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeHidPreparsedDataHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return PInvoke.HidD_FreePreparsedData((PHIDP_PREPARSED_DATA)handle);
        }
    }

    namespace Foundation
    {
        internal partial struct NTSTATUS
        {
            public void ThrowIfFailed()
            {
                if (this.Value < 0) throw new Win32Exception(0x10000000 /* FACILITY_NT_BIT */ | Value);
            }
        }
    }

    namespace Devices.HumanInterfaceDevice
    {
        internal partial struct HIDP_VALUE_CAPS
        {
            public ushort Usage => Anonymous.NotRange.Usage;
        }
    }
}