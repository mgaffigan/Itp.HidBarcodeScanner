using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

namespace Itp.WpfScanners.Demo
{
    /// <summary>
    /// Interaction logic for ScanningTextBox.xaml
    /// </summary>
    public partial class ScanningTextBox : UserControl
    {
        public ScanningTextBox()
        {
            InitializeComponent();
        }

        private void ScannerScope_ScanReceived(object sender, ScannedDataEventArgs args)
        {
            args.IsHandled = true;
            this.tb.Text = $"{args.TextData} ({args.SourceSymbology} {args.Source})";
        }
    }
}
