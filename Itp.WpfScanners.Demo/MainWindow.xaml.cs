using System.Windows;

namespace Itp.WpfScanners.Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    ScannerController explicitController;

    private void cbController_Checked(object sender, RoutedEventArgs e)
    {
        if (explicitController is null)
        {
            explicitController = new ScannerController();
            explicitController.AutoConfigure();
            explicitController.ScanReceived += (_, e) => cbController.Content = e.ToString();
        }

        explicitController.IsListening = cbController.IsChecked.GetValueOrDefault();
    }
}