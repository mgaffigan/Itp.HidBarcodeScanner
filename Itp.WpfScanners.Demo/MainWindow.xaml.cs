using System.Windows;

namespace Itp.WpfScanners.Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    //ScannerController explicitController;

    private void cbController_Checked(object sender, RoutedEventArgs e)
    {
        //if (explicitController is null)
        //{
        //    explicitController = new ScannerController();
        //    explicitController.AutoConfigure();
        //    explicitController.ScanReceived += (_, e) => cbController.Content = e.ToString();
        //}

        //explicitController.IsListening = cbController.IsChecked.GetValueOrDefault();

        // avoid duplicates
        this.scs.ScanReceived -= Scs_ScanReceived;
        if (cbController.IsChecked.GetValueOrDefault())
        {
            this.scs.ScanReceived += Scs_ScanReceived;
        }
    }

    private void Scs_ScanReceived(object sender, ScannedDataEventArgs args)
    {
        cbController.Content = args.ToString();
    }

    private void cbTabControl_Checked(object sender, RoutedEventArgs e)
    {
        this.scs.ScanReceived -= Scs_ScanReceivedTabControl;
        if (cbTabControl.IsChecked.GetValueOrDefault())
        {
            this.scs.ScanReceived += Scs_ScanReceivedTabControl;
        }
    }

    private void Scs_ScanReceivedTabControl(object sender, ScannedDataEventArgs args)
    {
        _ = ScannerScope.TryDelegateScanTo(tabControl, args);
    }
}