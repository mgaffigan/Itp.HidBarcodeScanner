using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Itp.HidBarcodeScanner.Demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Scanner = new HidScannerCollection(SynchronizationContext.Current ?? throw new InvalidOperationException());
            Scanner.ScanReceived += this.Scanner_ScanReceived;
        }

        private void Scanner_ScanReceived(object sender, HidScanReceivedEventArgs e)
        {
            this.Title = e.TextData;
        }

        public HidScannerCollection Scanner { get; }
    }
}
