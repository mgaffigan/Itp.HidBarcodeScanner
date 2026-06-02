using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

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

    private void Scanner_ScanReceived(object? sender, HidScanReceivedEventArgs e)
    {
        if (cbDelay.IsChecked.GetValueOrDefault())
        {
            e.TakeDeferral(Scanner_ScanReceivedAsync(sender, e));
        }
        else
        {
            tb.Text = e.TextData;
        }
    }

    private async Task Scanner_ScanReceivedAsync(object? sender, HidScanReceivedEventArgs e)
    {
        tb.Text = e.TextData;
#if NET
        if (Random.Shared.Next(5) == 0)
        {
            throw new InvalidOperationException("Error before scan received.");
        }
#endif
        tb.Foreground = Brushes.Red;
        await Task.Delay(2000);
        tb.Foreground = Brushes.Black;
#if NET
        if (Random.Shared.Next(5) == 0)
        {
            throw new InvalidOperationException("Error after scan received.");
        }
#endif
    }

    public HidScannerCollection? Scanner { get; set; }

    private void cbEnable_Checked(object sender, RoutedEventArgs e)
    {
        Scanner = new HidScannerCollection(SynchronizationContext.Current ?? throw new InvalidOperationException());
        Scanner.ScanReceived += this.Scanner_ScanReceived;
    }

    private void cbEnable_Unchecked(object sender, RoutedEventArgs e)
    {
        Scanner?.Dispose();
        Scanner = null;
    }
}
