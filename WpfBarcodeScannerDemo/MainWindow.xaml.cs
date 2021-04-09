using Microsoft.PointOfService;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Threading;
using System.Threading;

namespace WpfBarcodeScannerDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Scanner OpenScanner;

        public MainWindow()
        {
            InitializeComponent();

            Foo();
        }

        class DispatcherOperationThunk : IAsyncResult
        {
            public DispatcherOperation Operation { get; }

            public DispatcherOperationThunk(DispatcherOperation op)
            {
                this.Operation = op;
            }

            public object AsyncState => Operation;

            public WaitHandle AsyncWaitHandle => ((IAsyncResult)Operation.Task).AsyncWaitHandle;

            public bool CompletedSynchronously => ((IAsyncResult)Operation.Task).CompletedSynchronously;

            public bool IsCompleted => ((IAsyncResult)Operation.Task).IsCompleted;
        }

        class DispatcherThunk : ISynchronizeInvoke
        {
            private Dispatcher Dispatcher;

            public DispatcherThunk(Dispatcher disp)
            {
                this.Dispatcher = disp;
            }

            public bool InvokeRequired => !Dispatcher.CheckAccess();

            public IAsyncResult BeginInvoke(Delegate method, object[] args)
            {
                return new DispatcherOperationThunk(Dispatcher.BeginInvoke(method, args));
            }

            public object EndInvoke(IAsyncResult result)
            {
                return ((DispatcherOperationThunk)result).Operation.Result;
            }

            public object Invoke(Delegate method, object[] args)
            {
                return Dispatcher.Invoke(method, args);
            }
        }

        private async void Foo()
        {
            var explorer = new PosExplorer(new DispatcherThunk(this.Dispatcher));
            var devices = explorer.GetDevices(DeviceType.Scanner);

            var scanner = (Scanner)explorer.CreateInstance(devices[1]);
            scanner.Open();
            scanner.Claim(0);
            scanner.DeviceEnabled = true;
            scanner.DataEvent += Scanner_DataEvent;
            scanner.DataEventEnabled = true;
            scanner.DecodeData = true;

            this.OpenScanner = scanner;
        }

        private void Scanner_DataEvent(object sender, DataEventArgs e)
        {
            var data = Encoding.ASCII.GetString(OpenScanner.ScanDataLabel);
            listBox.Items.Insert(0, $"{OpenScanner.ScanDataType}: {data}");
        }
    }
}
