using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Itp.HidBarcodeScanner
{
    public class HidScannerCollection : IDisposable
    {
        private readonly SynchronizationContext SyncCtx;
        private readonly object SyncRoot;
        private bool IsEnabled;

        private List<HidScannerClaim> Scanners;

        private event EventHandler<HidScanReceivedEventArgs>? _ScanReceived;
        public event EventHandler<HidScanReceivedEventArgs> ScanReceived
        {
            add
            {
                _ScanReceived += value;
                bool shouldConnect = false;
                lock (SyncRoot)
                {
                    if (!IsEnabled)
                    {
                        shouldConnect = true;
                        IsEnabled = true;
                    }
                }
                if (shouldConnect)
                {
                    Connect();
                }
            }
            remove
            {
                _ScanReceived -= value;
                bool shouldConnect = false;
                lock (SyncRoot)
                {
                    if (_ScanReceived == null || _ScanReceived.GetInvocationList().Length == 0)
                    {
                        IsEnabled = false;
                        shouldConnect = true;
                    }
                }
                if (shouldConnect)
                {
                    Disconnect();
                }
            }
        }

        public event EventHandler<UnhandledExceptionEventArgs>? BackgroundException;

        public HidScannerCollection(SynchronizationContext syncCtx)
        {
            this.SyncCtx = syncCtx ?? new SynchronizationContext();

            this.Scanners = new List<HidScannerClaim>();
            this.SyncRoot = new object();
        }

        public void Dispose()
        {
            Disconnect();
        }

        private void Connect()
        {
            HidScannerDevice.DeviceConnected += HidScannerDevice_DeviceConnected;
        }

        private void SetupDevice(HidScannerClaim connectedDev)
        {
            connectedDev.ScanReceived += ConnectedDev_ScanReceived;

            lock (SyncRoot)
            {
                Scanners.Add(connectedDev);
            }
        }

        private void Disconnect()
        {
            HidScannerDevice.DeviceConnected -= HidScannerDevice_DeviceConnected;

            List<HidScannerClaim> claims;
            lock (SyncRoot)
            {
                claims = Scanners.ToList();
                Scanners.Clear();
            }

            foreach (var dev in claims)
            {
                dev.Dispose();
            }
        }

        private void ConnectedDev_ScanReceived(object? sender, HidScanReceivedEventArgs e)
        {
            try
            {
                _ScanReceived?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        private void HandleException(Exception ex)
        {
            try
            {
                BackgroundException?.Invoke(this, new UnhandledExceptionEventArgs(ex, false));
            }
            catch
            {
                // double-fault, no-op
            }
        }

        private async void HidScannerDevice_DeviceConnected(object? sender, HidScannerConnectedEventArgs e)
        {
            if (e.State == HidScannerDeviceState.Connected)
            {
                try
                {
                    var dev = await e.Device!.ConnectAsync(SyncCtx);
                    SetupDevice(dev);
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                }
            }
        }
    }
}
