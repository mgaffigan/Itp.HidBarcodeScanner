using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Itp.HidBarcodeScanner
{
    public sealed class HidScanReceivedEventArgs : EventArgs
    {
        public byte[] RawData { get; }
        public HidScannerSymbology Symbology { get; }

        public string TextData => Encoding.ASCII.GetString(RawData);

        internal Task? Deferral { get; private set; }

        public HidScanReceivedEventArgs(byte[] data, HidScannerSymbology symbology)
        {
            this.RawData = data;
            this.Symbology = symbology;
        }

        public override string ToString()
            => $"{nameof(HidScanReceivedEventArgs)}: '{TextData}' ({Symbology})";

        public void TakeDeferral(Func<Task> actor)
        {
            TakeDeferral(actor());
        }

        public void TakeDeferral(Task t)
        {
            if (Deferral != null)
            {
                throw new InvalidOperationException("Deferral already taken.");
            }
            Deferral = t ?? throw new ArgumentNullException(nameof(t));
        }

        public IDisposable TakeDeferral()
        {
            var deferral = new DeferralDisposable();
            TakeDeferral(deferral.Task);
            return deferral;
        }

        private class DeferralDisposable : IDisposable
        {
            private readonly TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            public Task Task => tcs.Task;
            public void Dispose() => tcs.TrySetResult(true);
        }
    }
}
