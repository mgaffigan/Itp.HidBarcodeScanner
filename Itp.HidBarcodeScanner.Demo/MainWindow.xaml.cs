using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;

namespace Itp.HidBarcodeScanner.Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Scanner = new HidScannerCollection(SynchronizationContext.Current ?? throw new InvalidOperationException());
        Scanner.ScanReceived += this.Scanner_ScanReceived;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        Scanner.Dispose();
        base.OnClosing(e);
    }

    private void Scanner_ScanReceived(object sender, HidScanReceivedEventArgs e)
    {
        tb.Text = e.TextData;
    }

    public HidScannerCollection Scanner { get; }
}
