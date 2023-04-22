using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;
using Itp.WpfScanners.Hal;

namespace Itp.WpfScanners;

public class ScannerScope : ContentControl
{
    private ScannerControlScope? ConnectedScope;

    public ScannerScope()
    {
        this.Loaded += ScannerScope_Loaded;
        this.Unloaded += ScannerScope_Unloaded;
    }

    private void ScannerScope_Loaded(object? sender, RoutedEventArgs e)
    {
        if (!AlwaysActive)
        {
            return;
        }

        Connect();
    }

    private void ScannerScope_Unloaded(object? sender, RoutedEventArgs e)
    {
        Disconnect();
    }

    public bool AlwaysActive
    {
        get { return (bool)GetValue(AlwaysActiveProperty); }
        set { SetValue(AlwaysActiveProperty, value); }
    }

    public static readonly DependencyProperty AlwaysActiveProperty =
        DependencyProperty.Register("AlwaysActive", typeof(bool), typeof(ScannerScope), new PropertyMetadata(false));

    protected override void OnIsKeyboardFocusWithinChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsKeyboardFocusWithinChanged(e);
        var newValue = (bool)e.NewValue;

        if (newValue)
        {
            Connect();
        }
        else if (!AlwaysActive)
        {
            Disconnect();
        }
    }

    private void Connect()
    {
        if (ConnectedScope != null || DesignHelper.InDesignMode)
        {
            return;
        }

        ConnectedScope = findParentScope();
        ConnectedScope?.AddScope(this);
    }

    private void Disconnect()
    {
        if (ConnectedScope == null)
        {
            return;
        }

        ConnectedScope.RemoveScope(this);
        ConnectedScope = null;
    }

    private ScannerControlScope? findParentScope()
    {
        DependencyObject cnode = this;
        while (!(cnode is ScannerControlScope) && cnode != null)
        {
            cnode = VisualTreeHelper.GetParent(cnode);
        }

        var parent = cnode as ScannerControlScope;
        if (parent == null)
        {
            Debug.WriteLine("Scanner scope is unable to find parent");
        }

        return parent;
    }

    public event ScannedDataReceivedEventHandler? ScanReceived;

    internal void HandleScan(object? sender, ScannedDataEventArgs args)
    {
        ScanReceived?.Invoke(sender, args);
    }
}
