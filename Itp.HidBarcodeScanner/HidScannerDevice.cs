using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;

namespace Itp.HidBarcodeScanner
{
    public class HidScannerDevice
    {
        internal DeviceInformation DevInfo { get; private set; }

        public string Description => DevInfo.Name;

        private static DeviceWatcher? Watcher;
        private static bool IsHooked;
        private static readonly object SyncHook = new object();
        private static event EventHandler<HidScannerConnectedEventArgs>? _DeviceConnected;
        public static event EventHandler<HidScannerConnectedEventArgs> DeviceConnected
        {
            add
            {
                _DeviceConnected += value;
                lock (SyncHook)
                {
                    if (!IsHooked)
                    {
                        SetupDeviceConnectedHook();
                        IsHooked = true;
                    }
                }
            }
            remove
            {
                _DeviceConnected -= value;
                lock (SyncHook)
                {
                    if (_DeviceConnected == null || _DeviceConnected.GetInvocationList().Length == 0)
                    {
                        IsHooked = false;
                        Watcher?.Stop();
                        Watcher = null;
                    }
                }
            }
        }

        private static void SetupDeviceConnectedHook()
        {
            Watcher = DeviceInformation.CreateWatcher(GetCrsAqsSelector());
            Watcher.Added += (_1, e) => _DeviceConnected?.Invoke(null, new HidScannerConnectedEventArgs(
                new HidScannerDevice(e), HidScannerDeviceState.Connected));
            Watcher.Removed += (_1, e) => _DeviceConnected?.Invoke(null, new HidScannerConnectedEventArgs(
                e.Id, HidScannerDeviceState.Disconnected));
            Watcher.Start();
        }

        private HidScannerDevice(DeviceInformation dev)
        {
            this.DevInfo = dev;
        }

        internal async Task<HidScannerClaim> ConnectAsync(SynchronizationContext syncCtx)
        {
            return await HidScannerClaim.CreateAsync(DevInfo.Id, syncCtx);
        }

        public static async Task<IEnumerable<HidScannerDevice>> GetAllDevicesAsync()
        {
            string selector = GetCrsAqsSelector();

            var devices = await DeviceInformation.FindAllAsync(selector);
            return devices.Select(d => new HidScannerDevice(d)).ToArray();
        }

        private const ushort
            HID_SCANNER_USAGE_PAGE = 0x8c,
            HID_SCANNER_SCAN_COLLECTION_ID = 0x02;

        private static string GetCrsAqsSelector()
        {
            return HidDevice.GetDeviceSelector(HID_SCANNER_USAGE_PAGE, HID_SCANNER_SCAN_COLLECTION_ID);
        }
    }

    public sealed class HidScannerConnectedEventArgs : EventArgs
    {
        public HidScannerDeviceState State { get; }

        public HidScannerDevice? Device { get; }

        public string DeviceId { get; }

        internal HidScannerConnectedEventArgs(HidScannerDevice device, HidScannerDeviceState state)
        {
            this.State = state;
            this.Device = device;
            this.DeviceId = device.DevInfo.Id;
        }

        internal HidScannerConnectedEventArgs(string deviceId, HidScannerDeviceState state)
        {
            this.State = state;
            this.DeviceId = deviceId;
        }
    }

    public enum HidScannerDeviceState
    {
        Connected,
        Disconnected
    }
}
