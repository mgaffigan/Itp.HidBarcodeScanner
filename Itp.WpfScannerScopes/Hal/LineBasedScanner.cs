using System;
using System.IO.Ports;
using System.Threading;

namespace Itp.WpfScanners.Hal;

public abstract class LineBasedScanner : Scanner
{
    protected readonly SynchronizationContext SyncCtx;
    private SerialPort spComm;
    private int cBufPos;
    private byte[]? buffer;
    private readonly object syncComm = new object();

    public LineBasedScanner(string portName, SynchronizationContext syncCtx)
        : this(new SerialPort(portName, 9600, Parity.None, 8, StopBits.One), syncCtx)
    {
    }

    public LineBasedScanner(SerialPort serialPort, SynchronizationContext syncCtx)
    {
        SyncCtx = syncCtx;
        spComm = serialPort;
        spComm.DataReceived += new SerialDataReceivedEventHandler(spComm_DataReceived);
    }

    public override void Dispose() => spComm.Dispose();

    public override void Start()
    {
        lock (syncComm)
        {
            if (!spComm.IsOpen)
            {
                spComm.Open();
            }
        }
    }

    public override void Stop()
    {
        lock (syncComm)
        {
            if (!spComm.IsOpen)
                throw new InvalidOperationException("Not open");

            spComm.Close();
            buffer = null;
        }
    }

    private void spComm_DataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        const int BUFFER_SIZE = 1024 * 10 /* 10 kb */;

        // if the buffer needs to be enbiggend, do so based off of BytesToRead
        if (buffer == null || buffer.Length < cBufPos + spComm.BytesToRead)
        {
            buffer = new byte[cBufPos + spComm.BytesToRead + BUFFER_SIZE];
        }
        if (buffer.Length > 1 * 1024 * 1024 /* 1 MB */)
        {
            throw new OverflowException("Buffer is too large");
        }

        cBufPos += spComm.Read(buffer, cBufPos, spComm.BytesToRead);
        int keepAtAfter;
        DataReceivedInternal(buffer, out keepAtAfter, cBufPos);

        if (keepAtAfter > 0)
        {
            var tbuff = buffer;
            buffer = new byte[cBufPos - keepAtAfter + BUFFER_SIZE];
            Buffer.BlockCopy(tbuff, keepAtAfter, buffer, 0, cBufPos - keepAtAfter);
        }
        cBufPos = 0;
    }

    protected abstract void DataReceivedInternal(byte[] buffer, out int keepAtAfter, int length);
}
