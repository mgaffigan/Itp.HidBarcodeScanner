using System;
using System.Linq;
using System.Collections.ObjectModel;
using Itp.WpfScanners.Hal;
using Esatto.Win32.CommonControls;
using System.Threading;
using Esatto.Utilities;

namespace Itp.WpfScanners;

/// <summary>
/// Class which manages the scanners and actuates events up the tree
/// </summary>
public class ScannerController : IDisposable
{
    private ObservableCollection<Scanner> _scanners = new ObservableCollection<Scanner>();
    public ObservableCollection<Scanner> Scanners { get { return _scanners; } }
    public event ScannedDataReceivedEventHandler? ScanReceived;
    private bool isStarted = false;
    private readonly SynchronizationContext SyncCtx;
    private readonly ThreadAssert thread = new ThreadAssert();

    public ScannerController()
        : this(SynchronizationContext.Current ?? new SynchronizationContext())
    {
    }

    public ScannerController(SynchronizationContext syncCtx)
    {
        if (syncCtx == null)
            throw new ArgumentNullException("syncCtx");

        this.SyncCtx = syncCtx;
    }

    public void Dispose()
    {
        thread.Assert();
        foreach (var scanner in Scanners)
        {
            scanner.Dispose();
        }
    }

    internal void AddScanner(ScannerConfiguration config)
    {
        thread.Assert();
        if (config == null)
            throw new ArgumentNullException("config");

        if (!config.IsPresent())
            return;

        AddScanner(Scanner.FromConfig(config));
    }

    public void AddPnPScanners()
    {
        thread.Assert();
        var list = DetectedPort.GetAllPorts();
        foreach (var l in list)
        {
            // Syble XB-6299 (ITP Branded)
            if (l.HardwareID.StartsWith(@"USB\VID_6666&PID_7777", StringComparison.InvariantCultureIgnoreCase))
            {
                AddScanner(new SybleXb6299(l.PortName));
            }
        }
        AddScanner(new HidScanner());
    }

    private void AddScanner(Scanner scanner)
    {
        thread.Assert();
        this.Scanners.Add(scanner);

        scanner.ScanReceived += scanner_ScanReceived;

        if (isStarted)
        {
            scanner.Start();
        }
    }

    public void StartListening()
    {
        thread.Assert();
        if (isStarted)
            return;

        foreach (var scanner in Scanners)
            scanner.Start();

        // we set IsStarted after all scanners are started to prevent a TOCTOU
        // with multiple callers in IsListening
        isStarted = true;
    }

    public void StopListening()
    {
        thread.Assert();
        if (!isStarted)
            return;

        foreach (var scanner in Scanners)
            scanner.Stop();

        // we set IsStarted after all scanners are started to prevent a TOCTOU
        // with multiple callers in IsListening
        isStarted = false;
    }

    public bool IsListening
    {
        get { return isStarted; }
        set
        {
            thread.Assert();
            try
            {
                if (value)
                {
                    StartListening();
                }
                else
                {
                    StopListening();
                }
            }
            catch (UnauthorizedAccessException)
            {
                //perhaps the previous owner did not release in time...
                System.Threading.Thread.Sleep(200);

                if (value)
                {
                    StartListening();
                }
                else
                {
                    StopListening();
                }
            }
        }
    }

    private void scanner_ScanReceived(object? sender, ScannedDataEventArgs args)
    {
        thread.Assert();
        ScanReceived?.Invoke(this, args);
    }

    public void AutoConfigure()
    {
        thread.Assert();
        foreach (var scanner in RegistryScannerConfiguration.Config
            .Scanners.Where(s => s.IsPresent()))
        {
            this.AddScanner(scanner);
        }
        if (RegistryScannerConfiguration.Config.AutodetectPnP)
        {
            this.AddPnPScanners();
        }
        if (RegistryScannerConfiguration.Config.ShowEmulatedScanner)
        {
            this.AddEmulatedScanner();
        }
    }

    private void AddEmulatedScanner()
    {
        thread.Assert();
        AddScanner(EmulatedScanner.Instance);
    }
}
