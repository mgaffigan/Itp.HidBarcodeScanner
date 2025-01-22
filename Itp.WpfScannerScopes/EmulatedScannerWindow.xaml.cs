using Itp.WpfScanners.Hal;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Itp.WpfScanners;

internal partial class EmulatedScannerWindow : Window
{
    private readonly EmulatedScannerVM ViewModel;

    public EmulatedScannerWindow(EmulatedScannerVM vm)
    {
        InitializeComponent();

        this.DataContext = this.ViewModel = vm;
        this.SourceInitialized += (s, e) => SetNoActivate(false);
        this.Deactivated += (s, e) =>
        {
            SetNoActivate(false);

            DependencyObject scope = FocusManager.GetFocusScope(cbText);
            FocusManager.SetFocusedElement(scope, cbSymbology);
        };
    }

    private void btSend_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Send();
    }

    private void cbText_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ViewModel.Send();
        }
    }

    private void cbText_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        var canActivate = (bool)e.NewValue;
        SetNoActivate(canActivate);
        if (canActivate) Activate();
    }

    #region WS_EX_NOACTIVATE

    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int GWL_EXSTYLE = -20;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    private void SetNoActivate(bool allowActivate)
    {
        var interopHelper = new WindowInteropHelper(this);
        int exStyle = GetWindowLong(interopHelper.Handle, GWL_EXSTYLE);
        if (allowActivate)
        {
            exStyle &= ~WS_EX_NOACTIVATE;
        }
        else
        {
            exStyle |= WS_EX_NOACTIVATE;
        }
        SetWindowLong(interopHelper.Handle, GWL_EXSTYLE, exStyle);
    }

    #endregion
}

public class EmulatedScanner : Scanner
{
    private readonly SynchronizationContext SyncCtx;
    private readonly EmulatedScannerVM vm;
    private readonly Thread thBackground;

    public EmulatedScanner(SynchronizationContext syncCtx)
    {
        this.SyncCtx = syncCtx ?? throw new ArgumentNullException(nameof(syncCtx));
        vm = new EmulatedScannerVM();
        vm.ScanReceived += (sym, data) => SyncCtx.Post(_ => OnScanReceived(sym, data), null);
        thBackground = new Thread(EmulatedScannerMain);
        thBackground.Name = "EmulatedScanner";
        thBackground.IsBackground = true;
        thBackground.SetApartmentState(ApartmentState.STA);
        thBackground.Start();
    }

    private void EmulatedScannerMain(object? obj)
    {
        var window = new EmulatedScannerWindow(vm);
        window.Show();

        System.Windows.Threading.Dispatcher.Run();
    }

    public override void Start()
    {
        vm.IsEnabled = true;
    }

    public override void Stop()
    {
        vm.IsEnabled = false;
    }
}

internal class EmulatedScannerVM : INotifyPropertyChanged
{
    private static readonly RegistryKey rk = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\In Touch Technologies\Esatto\Wpf.Scanners");

    public Symbology[] Symbologies =>
    [
        Symbology.Unknown,
        Symbology.Codabar,
        Symbology.ModifiedPlessey,
        Symbology.Code128,
        Symbology.GSS128,
        Symbology.GS1_GSS128,
        Symbology.EAN,
        Symbology.UPC,
        Symbology.Datamatrix,
        Symbology.QRCode,
        Symbology.AztecCode,
        Symbology.PDF417,
        Symbology.MicroPDF417,
        Symbology.Code39,
        Symbology.GS1,
        Symbology.RFID,
    ];

    private Symbology _Symbology = (Symbology)(int)rk.GetValue("EmulatedSymbology", 0);
    public Symbology Symbology
    {
        get => _Symbology;
        set
        {
            _Symbology = value;
            rk.SetValue("EmulatedSymbology", (int)value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Symbology)));
        }
    }

    private string _Text = "";
    public string Text
    {
        get => _Text;
        set
        {
            _Text = value ?? "";
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
        }
    }

    private bool _IsEnabled = false;
    public bool IsEnabled
    {
        get => _IsEnabled && !string.IsNullOrWhiteSpace(Text);
        set
        {
            _IsEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
        }
    }

    private string[] _Suggestions = rk.GetValue("EmulatedMru") as string[] ?? [];
    public string[] Suggestions
    {
        get => _Suggestions;
        set
        {
            _Suggestions = value ?? [];
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Suggestions)));
        }
    }

    public void Send()
    {
        var s = Text;
        if (string.IsNullOrWhiteSpace(s))
        {
            return;
        }

        var suggestions = new List<string>(Suggestions);
        suggestions.Remove(s);
        suggestions.Insert(0, s);
        while (suggestions.Count > 10)
        {
            suggestions.RemoveAt(suggestions.Count - 1);
        }
        rk.SetValue("EmulatedMru", suggestions.ToArray());

        var bytes = Encoding.UTF8.GetBytes(s);
        ScanReceived?.Invoke(Symbology, bytes);
    }

    public event Action<Symbology, byte[]>? ScanReceived;
    public event PropertyChangedEventHandler? PropertyChanged;
}
