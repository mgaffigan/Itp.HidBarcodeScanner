

using System.ComponentModel;
using Windows.Win32;
using Windows.Win32.Devices.HumanInterfaceDevice;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;

//const string path = @"\\?\hid#vid_0c2e&pid_1007&mi_00#7&18b4d1ce&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
const string path = @"\\?\hid#vid_05e0&pid_2100#6&6117359&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";

using var hFile = new FileStream(PInvoke.CreateFile(path,
    (uint)(GENERIC_ACCESS_RIGHTS.GENERIC_READ | GENERIC_ACCESS_RIGHTS.GENERIC_WRITE),
    FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
    null, FILE_CREATION_DISPOSITION.OPEN_EXISTING, 0, null), FileAccess.ReadWrite);

if (!PInvoke.HidD_GetPreparsedData(hFile.SafeFileHandle, out var preparsedData))
{
    throw new Win32Exception();
}
using var _1 = preparsedData;
var pPreparsedData = (PHIDP_PREPARSED_DATA)preparsedData.DangerousGetHandle();

PInvoke.HidP_GetCaps(pPreparsedData, out var caps).ThrowIfFailed();

var valueCaps = new HIDP_VALUE_CAPS[caps.NumberInputValueCaps];
unsafe
{
    fixed (HIDP_VALUE_CAPS* pValueCaps = valueCaps)
    {
        PInvoke.HidP_GetValueCaps(HIDP_REPORT_TYPE.HidP_Input, pValueCaps, ref caps.NumberInputValueCaps, pPreparsedData).ThrowIfFailed();
    }
}
var scannedDataValueCap = valueCaps.Single(x => x.Usage == 0xfe && x.UsagePage == 0x8c);
if (scannedDataValueCap.BitSize != 8) throw new InvalidOperationException();
var scannedDataValueLen = scannedDataValueCap.ReportCount;

var buff = new byte[64];
var ms = new MemoryStream();
while (true)
{
    var cbRead = hFile.Read(buff, 0, buff.Length);
    if (cbRead != 64) throw new InvalidOperationException();
    Console.WriteLine($"Raw: {Convert.ToHexString(buff)}");

    if (buff[0] != scannedDataValueCap.ReportID)
    {
        Console.WriteLine($"Unknown report {buff[0]}");
        continue;
    }

    // Check if there's a report of scanned data
    var readData = new byte[64];
    unsafe
    {
        fixed (byte* pBuff = buff)
        {
            fixed (byte* pReadData = readData)
            {
                PInvoke.HidP_GetUsageValueArray(HIDP_REPORT_TYPE.HidP_Input, 0x8c, 0, 0xfe,
                    pReadData, scannedDataValueLen, pPreparsedData, pBuff, (uint)cbRead
                ).ThrowIfFailed();
            }

            // get byte count
            uint byteCount = 0;
            PInvoke.HidP_GetUsageValue(HIDP_REPORT_TYPE.HidP_Input, 0x1, 0, 0x3b, &byteCount, pPreparsedData, pBuff, (uint)cbRead)
                .ThrowIfFailed();
            ms.Write(readData, 0, (int)byteCount);

            // check for continuation
            uint usageCount = caps.NumberInputButtonCaps;
            ushort[] usages = new ushort[usageCount];
            fixed (ushort* pUsages = usages)
            {
                PInvoke.HidP_GetUsages(HIDP_REPORT_TYPE.HidP_Input, 0x8c, 0, pUsages, ref usageCount, pPreparsedData, pBuff, (uint)cbRead)
                    .ThrowIfFailed();
            }
            var usagesSet = usages.AsSpan(0, (int)usageCount);
            if (usagesSet.Contains((ushort)0xff)) continue;

            var sData = System.Text.Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
            ms.SetLength(0);
            Console.WriteLine($"Data: {sData}");
        }
    }
}

//PInvoke.HidD_GetHidGuid(out Guid hidGuid);
//Console.WriteLine(hidGuid);

//using var hDevs = PInvoke.SetupDiGetClassDevs(hidGuid, null, (HWND)0, SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_PRESENT);
//if (hDevs.IsInvalid)
//{
//    throw new Win32Exception();
//}

//for (int i = 0; ; i++)
//{
//    var did = new SP_DEVICE_INTERFACE_DATA();
//    if (!PInvoke.SetupDiEnumDeviceInterfaces(hDevs, null, hidGuid, 0, ref did))
//    {
//        if (Marshal.GetLastWin32Error() == (int)WIN32_ERROR.ERROR_NO_MORE_ITEMS) break;
//        throw new Win32Exception();
//    }

//    unsafe
//    {
//        uint reqSize = 0;
//        if (!PInvoke.SetupDiGetDeviceInterfaceDetail(hDevs, did, null, 0, &reqSize, null))
//        {
//            if (Marshal.GetLastWin32Error() != (int)WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER)
//            {
//                throw new Win32Exception();
//            }
//        }

//        var data = Marshal.AllocCoTaskMem((int)reqSize);
//        try
//        {
//            var didd = new SP_DEVICE_INTERFACE_DETAIL_DATA();
//            didd.cbSize = (uint)sizeof(SP_DEVICE_INTERFACE_DETAIL_DATA);
//            if (!PInvoke.SetupDiGetDeviceInterfaceDetail(hDevs, did, data, reqSize, null, &didd))
//            {
//                throw new Win32Exception();
//            }

//            Console.WriteLine(Marshal.PtrToStringUni((IntPtr)((byte*)data + sizeof(uint))));
//        }
//        finally
//        {
//            Marshal.FreeCoTaskMem(data);
//        }
//    }
//}

