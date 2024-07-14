using Esatto.Win32.Windows;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Itp.WpfScanners.Demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            //RunAsync();
        }

        private async void RunAsync()
        {
            while (true)
            {
                //wb.Focus();
                await Task.Delay(1000);

                var focused = Keyboard.FocusedElement?.ToString();
                if (focused is null)
                {
                    var i = GetHwndHostWindow(new Win32Window(GetFocus()));
                    if (i is not null)
                    {
                        var o = GetAutomationPeer(i);
                        focused = o?.ToString();
                    }
                }

                this.Title = focused ?? "N/A";
            }
        }

        object GetAutomationPeer(Win32Window window)
        {
            var iid = new Guid("00020400-0000-0000-C000-000000000046");
            object? obj = null;
            AccessibleObjectFromWindow(window.Handle, 0, ref iid, ref obj);
            return obj;
        }

        private Win32Window? GetHwndHostWindow(Win32Window hwnd)
        {
            var thisWindow = new Win32Window(new WindowInteropHelper(this).Handle);
            if (hwnd == thisWindow)
            {
                return null;
            }

            while (true)
            {
                var parent = hwnd.GetParent();
                if (parent.Handle == 0) break;
                if (parent == thisWindow) return hwnd;
                hwnd = parent;
            }

            return null;
        }

        //private HwndHost? GetHwndHostForHwnd(Win32Window hwnd)
        //{
        //    if (hwnd.CachedClass.StartsWith("HwndWrapper"))
        //    {
        //        return HwndSource.FromHwnd(hwnd.Handle);
        //    }
        //}

        private void wb_ScanReceived(object sender, ScannedDataEventArgs args)
        {
            wb.Source = new Uri($"https://www.google.com/search?q={Uri.EscapeDataString(args.TextData)}");
        }

        private void wb_GotFocus(object sender, RoutedEventArgs e)
        {

        }


        [DllImport("oleacc.dll", SetLastError = true, PreserveSig = false)]
        internal static extern void AccessibleObjectFromWindow(IntPtr hwnd, uint id, ref Guid iid,
            [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object ppvObject);

        [DllImport("User32.dll")]
        private static extern IntPtr GetFocus();
    }
}