#define DEBUG

using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
#if NETFRAMEWORK
using System.Reflection;
#endif
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Itp.HidBarcodeScanner;

class HidScannerClaim : IDisposable
{
    public string DeviceId { get; }
    private readonly SynchronizationContext SyncCtx;
    private readonly Task ReadPromise;
    private readonly CancellationTokenSource cts;

    public event EventHandler<HidScanReceivedEventArgs>? ScanReceived;
    public event UnhandledExceptionEventHandler? Exception;

    private HidScannerClaim(string id, SynchronizationContext syncCtx)
    {
        this.DeviceId = id;
        this.SyncCtx = syncCtx;
        this.cts = new CancellationTokenSource();

        Debug.WriteLine("Claiming HID device: " + id);
        this.ReadPromise = ReadAsync();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, FileShare dwShareMode,
        IntPtr lpSecurityAttributes, FileMode dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    public void Dispose()
    {
        Debug.WriteLine("Releasing HID device: " + DeviceId);
        cts.Cancel();
        this.ReadPromise.GetAwaiter().GetResult();
    }

    private async Task ReadAsync()
    {
        try
        {
            using var fh = CreateFile(DeviceId, 0xC0000000 /* GENERIC_READ | GENERIC_WRITE */, FileShare.ReadWrite,
                IntPtr.Zero, FileMode.Open, 0x40000000 /* FILE_FLAG_OVERLAPPED */, IntPtr.Zero);
            using var fs = new FileStream(fh, FileAccess.ReadWrite, bufferSize: 4096, isAsync: true);
            MungeFilestreamIntoAPipe(fs);
            using var hid = new HidDescriptor(fh);
            var scanReportID = hid.GetReportIdForValueCap(0x8c, 0xfe);
            var ScanData = new MemoryStream();
            var duration = Stopwatch.StartNew();

            while (true)
            {
                var buffer = new byte[hid.InputReportByteLength];
                int bytesRead;
                try
                {
                    bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                    if (bytesRead == 0) break;
                }
                catch (ObjectDisposedException) { break; }
                catch (OperationCanceledException) { break; }

                // Reports will queue if no one is reading from the queue. Since they could be from
                // minutes ago, we don't want them.
                if (duration.Elapsed < TimeSpan.FromMilliseconds(250)) continue;

                // We only care about the scan report
                if (buffer[0] != scanReportID) continue;

                try
                {
                    HandleReport(buffer.AsSpan(0, bytesRead), hid, ScanData);
                }
                catch (Exception ex)
                {
                    Exception?.Invoke(this, new UnhandledExceptionEventArgs(ex, false));
                }
            }
        }
        catch (Exception ex)
        {
            Exception?.Invoke(this, new UnhandledExceptionEventArgs(ex, true));
            throw;
        }
        finally
        {
            Debug.WriteLine("Released HID device: " + DeviceId);
        }
    }

    private void MungeFilestreamIntoAPipe(FileStream fs)
    {
#if NETFRAMEWORK
        // Workaround some BS I don't understand leading to synchronous writes
        // https://referencesource.microsoft.com/#mscorlib/system/io/filestream.cs,1905
        // Fixed (seemingly) in .net core 8.  Prior versions not tested
        typeof(FileStream).GetField("_isPipe", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(fs, true);
        typeof(FileStream).GetField("_canSeek", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(fs, false);
#endif
    }

    private void HandleReport(ReadOnlySpan<byte> report, HidDescriptor hid, MemoryStream accumulatedScanData)
    {
        uint symbology;
        try
        {
            // This will throw if the report is not of the expected type
            symbology
                = (hid.GetInputUsageValue(0x8c, 0xfd, report) << 0)
                | (hid.GetInputUsageValue(0x8c, 0xfc, report) << 8)
                | (hid.GetInputUsageValue(0x8c, 0xfb, report) << 16);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == unchecked((int)0xC011000A /* HIDP_STATUS_INCOMPATIBLE_REPORT_ID */))
        {
            // might be a button or something? bail
            return;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == unchecked((int)0xC0110004 /* HIDP_STATUS_USAGE_NOT_FOUND */))
        {
            // might be a button or something? bail
            return;
        }

        var byteLen = hid.GetInputUsageValue(0x01, 0x3b, report);
        var scannedData = hid.GetInputUsageValueArray(0x8c, 0xfe, report);
        var continuation = hid.HasUsage(0x8c, 0xff, report);
        accumulatedScanData.Write(scannedData, 0, (int)byteLen);

        if (continuation) return;

        scannedData = accumulatedScanData.ToArray();
        accumulatedScanData.SetLength(0);
        SyncCtx.Post((_1) =>
        {
            ScanReceived?.Invoke(this, new HidScanReceivedEventArgs(scannedData, (HidScannerSymbology)symbology));
        }, null);
    }

    internal static Task<HidScannerClaim> CreateAsync(string id, SynchronizationContext syncCtx)
    {
        var claim = new HidScannerClaim(id, syncCtx);
        return Task.FromResult(claim);
    }
}

class HidDescriptor : IDisposable
{
    private readonly SafeFileHandle HFile;
    private readonly SafeHidPreparsedDataHandle preparsedData;
    private readonly HIDP_CAPS caps;
    public int InputReportByteLength => caps.InputReportByteLength;
    public int NumberInputButtonCaps => caps.NumberInputButtonCaps;

    public HidDescriptor(SafeFileHandle handle)
    {
        this.HFile = handle;

        if (!HidD_GetPreparsedData(HFile, out preparsedData))
        {
            throw new Win32Exception();
        }

        try
        {
            VerifyNtResult(HidP_GetCaps(preparsedData, out caps));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        preparsedData.Dispose();
    }

    [DllImport("HID.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool HidD_GetPreparsedData(SafeFileHandle HidDeviceObject, out SafeHidPreparsedDataHandle PreparsedData);

    [DllImport("HID.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern unsafe int HidP_GetCaps(SafeHidPreparsedDataHandle PreparsedData, out HIDP_CAPS Capabilities);

    internal partial struct HIDP_CAPS
    {
        internal ushort Usage;
        internal ushort UsagePage;
        internal ushort InputReportByteLength;
        internal ushort OutputReportByteLength;
        internal ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        internal ushort[] Reserved;
        internal ushort NumberLinkCollectionNodes;
        internal ushort NumberInputButtonCaps;
        internal ushort NumberInputValueCaps;
        internal ushort NumberInputDataIndices;
        internal ushort NumberOutputButtonCaps;
        internal ushort NumberOutputValueCaps;
        internal ushort NumberOutputDataIndices;
        internal ushort NumberFeatureButtonCaps;
        internal ushort NumberFeatureValueCaps;
        internal ushort NumberFeatureDataIndices;
    }

    [DllImport("HID.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern unsafe int HidP_GetValueCaps(
        int ReportType,
        IntPtr ValueCaps,
        ref ushort ValueCapsLength,
        SafeHidPreparsedDataHandle PreparsedData
    );

#pragma warning disable CS0649 // Field is never assigned to
    private partial struct HIDP_VALUE_CAPS
    {
        internal ushort UsagePage;
        internal byte ReportID;
        internal bool IsAlias;
        internal ushort BitField;
        internal ushort LinkCollection;
        internal ushort LinkUsage;
        internal ushort LinkUsagePage;
        internal bool IsRange;
        internal bool IsStringRange;
        internal bool IsDesignatorRange;
        internal bool IsAbsolute;
        internal bool HasNull;
        internal byte Reserved;
        internal ushort BitSize;
        internal ushort ReportCount;
        internal ushort Reserved2_0;
        internal ushort Reserved2_1;
        internal ushort Reserved2_2;
        internal ushort Reserved2_3;
        internal ushort Reserved2_4;
        internal uint UnitsExp;
        internal uint Units;
        internal int LogicalMin;
        internal int LogicalMax;
        internal int PhysicalMin;
        internal int PhysicalMax;
        internal ushort UsageMin;
        internal ushort UsageMax;
        internal ushort StringMin;
        internal ushort StringMax;
        internal ushort DesignatorMin;
        internal ushort DesignatorMax;
        internal ushort DataIndexMin;
        internal ushort DataIndexMax;
    }
#pragma warning restore CS0649 // Field is never assigned to

    private void VerifyNtResult(int i)
    {
        if (i < 0) throw new Win32Exception(i);
    }

    private class SafeHidPreparsedDataHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeHidPreparsedDataHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return HidD_FreePreparsedData((IntPtr)handle);
        }

        [DllImport("HID.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool HidD_FreePreparsedData(IntPtr PreparsedData);
    }

    public unsafe uint GetInputUsageValue(int page, int usage, ReadOnlySpan<byte> report)
    {
        uint result;
        fixed (byte* pReport = report)
        {
            VerifyNtResult(HidP_GetUsageValue(0 /* HidP_Input */, (ushort)page, 0, (ushort)usage,
                out result, preparsedData, pReport, (uint)report.Length));
        }
        return result;
    }

    [DllImport("HID.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern unsafe int HidP_GetUsageValue(int ReportType, ushort UsagePage, ushort LinkCollection, ushort Usage,
        out uint UsageValue, SafeHidPreparsedDataHandle PreparsedData, byte* Report, uint ReportLength);

    public unsafe byte[] GetInputUsageValueArray(ushort page, ushort usage, ReadOnlySpan<byte> report)
    {
        var cap = GetValueCap(page, usage);
        if (report[0] != cap.ReportID) throw new InvalidOperationException("Unexpected report ID");

        var result = new byte[cap.ReportCount];
        fixed (byte* pReport = report)
        fixed (byte* pResult = result)
        {
            VerifyNtResult(HidP_GetUsageValueArray(0 /* HidP_Input */, page, 0, usage,
                pResult, cap.ReportCount, preparsedData, pReport, (uint)report.Length));
        }
        return result;
    }

    public ushort GetReportIdForValueCap(ushort page, ushort usage)
    {
        var cap = GetValueCap(page, usage);
        return cap.ReportID;
    }

    private unsafe HIDP_VALUE_CAPS GetValueCap(ushort page, ushort usage)
    {
        ushort capsLength = (ushort)caps.NumberInputValueCaps;
        var valueCaps = new HIDP_VALUE_CAPS[capsLength];
        fixed (HIDP_VALUE_CAPS* pValueCaps = valueCaps)
        {
            VerifyNtResult(HidP_GetValueCaps(0 /* HidP_Input */, (nint)pValueCaps, ref capsLength, preparsedData));
        }
        var cap = valueCaps.Single(x => x.UsageMin == usage && x.UsagePage == page);
        if (cap.BitSize != 8) throw new InvalidOperationException("Expected a byte-sized value");
        return cap;
    }

    [DllImport("HID.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private unsafe static extern int HidP_GetUsageValueArray(int ReportType, ushort UsagePage, ushort LinkCollection, ushort Usage,
        byte* UsageValue, ushort UsageValueByteLength, SafeHidPreparsedDataHandle PreparsedData, byte* Report, uint ReportLength);

    public unsafe bool HasUsage(ushort page, ushort usage, ReadOnlySpan<byte> report)
    {
        uint usageCount = caps.NumberInputButtonCaps;
        ushort[] usages = new ushort[usageCount];
        fixed (byte* pReport = report)
        fixed (ushort* pUsages = usages)
        {
            VerifyNtResult(HidP_GetUsages(0 /* HidP_Input */, page, 0, pUsages,
                ref usageCount, preparsedData, pReport, (uint)report.Length));
        }
        var usagesSet = usages.AsSpan(0, (int)usageCount);
        return usagesSet.IndexOf(usage) >= 0;
    }

    [DllImport("HID.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern unsafe int HidP_GetUsages(int ReportType, ushort UsagePage, ushort LinkCollection,
        ushort* UsageList, ref uint UsageLength, SafeHidPreparsedDataHandle PreparsedData, byte* Report, uint ReportLength);
}