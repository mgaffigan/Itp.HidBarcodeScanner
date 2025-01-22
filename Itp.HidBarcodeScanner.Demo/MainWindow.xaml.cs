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
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        Scanner?.Dispose();
        base.OnClosing(e);
    }

    private void Scanner_ScanReceived(object sender, HidScanReceivedEventArgs e)
    {
        tb.Text = e.TextData;
    }

    public HidScannerCollection? Scanner { get; set; }

    private void cbEnable_Checked(object sender, RoutedEventArgs e)
    {
        Scanner = new HidScannerCollection(SynchronizationContext.Current ?? throw new InvalidOperationException());
        Scanner.ScanReceived += this.Scanner_ScanReceived;
    }

    private void cbEnable_Unchecked(object sender, RoutedEventArgs e)
    {
        Scanner.Dispose();
        Scanner = null;
    }
}
