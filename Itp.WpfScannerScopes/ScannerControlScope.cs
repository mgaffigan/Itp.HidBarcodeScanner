using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Threading;
using Esatto.Utilities;

namespace Itp.WpfScanners;

public class ScannerControlScope : ContentControl
{
    private readonly ContextAwareCoalescingAction caStartStop;

    public ScannerControlScope()
    {
        caStartStop = new ContextAwareCoalescingAction(evalStartStop,
            TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(250), 
            new DispatcherSynchronizationContext(this.Dispatcher));
    }

    #region ScannerController

    public ScannerController ScannerController
    {
        get { return (ScannerController)GetValue(ScannerControllerProperty); }
        set { SetValue(ScannerControllerProperty, value); }
    }

    public static readonly DependencyProperty ScannerControllerProperty =
        DependencyProperty.Register("ScannerController", typeof(ScannerController),
        typeof(ScannerControlScope), new UIPropertyMetadata(null, ScannerController_Changed));

    private static void ScannerController_Changed(DependencyObject source, DependencyPropertyChangedEventArgs args)
    {
        var @this = (ScannerControlScope)source;
        if (args.OldValue != null)
        {
            var oldv = (ScannerController)args.OldValue;

            oldv.StopListening();
            oldv.ScanReceived -= @this.scanner_ScanReceived;
        }
        if (args.NewValue != null)
        {
            var newv = (ScannerController)args.NewValue;

            newv.ScanReceived += @this.scanner_ScanReceived;
            @this.caStartStop.Set();
        }
    }

    #endregion

    #region AutoConfigure

    public bool AutoConfigure
    {
        get { return (bool)GetValue(AutoConfigureProperty); }
        set { SetValue(AutoConfigureProperty, value); }
    }

    public static readonly DependencyProperty AutoConfigureProperty =
        DependencyProperty.Register("AutoConfigure", typeof(bool),
        typeof(ScannerControlScope), new UIPropertyMetadata(false, AutoConfigure_Changed));

    private static void AutoConfigure_Changed(DependencyObject source, DependencyPropertyChangedEventArgs args)
    {
        var @this = (ScannerControlScope)source;

        if (DesignerProperties.GetIsInDesignMode(@this)) return;

        if (object.Equals(args.NewValue, true) && @this.ScannerController == null)
        {
            @this.ScannerController = new ScannerController(new DispatcherSynchronizationContext(@this.Dispatcher));
            @this.ScannerController.AutoConfigure();
        }
    }

    #endregion

    #region Scopes

    private List<ScannerScope> registeredScopes = new List<ScannerScope>();

    internal void AddScope(ScannerScope scannerScope)
    {
        if (registeredScopes.Contains(scannerScope))
            return;

        registeredScopes.Add(scannerScope);
        caStartStop.Set();
    }

    internal void RemoveScope(ScannerScope scannerScope)
    {
        if (!registeredScopes.Contains(scannerScope))
            return;

        registeredScopes.Remove(scannerScope);
        caStartStop.Set();
    }

    private void evalStartStop()
    {
        if (DesignerProperties.GetIsInDesignMode(this)) return;

        bool state = registeredScopes.Any() || _ScanOfLastResort is not null;
        var controller = ScannerController;

        if (controller != null)
        {
            controller.IsListening = state;
        }
    }

    #endregion

    #region Scan Events

    private event ScannedDataReceivedEventHandler? _ScanOfLastResort;
    public event ScannedDataReceivedEventHandler ScanReceived
    {
        add
        {
            _ScanOfLastResort += value;
            caStartStop.Set();
        }
        remove
        {
            _ScanOfLastResort -= value;
            caStartStop.Set();
        }
    }

    /// <summary>
    /// Simulate text being scanned by a scanner
    /// </summary>
    /// <param name="text">the text of the barcode scanned</param>
    public void RaiseKeyboardScan(string text)
    {
        scanner_ScanReceived(this, ScannedDataEventArgs.FromKeyboard(text));
    }

    private void scanner_ScanReceived(object? sender, ScannedDataEventArgs args)
    {
        if (!this.Dispatcher.CheckAccess())
        {
            // this cannot be synchronous, as the handler may open additional windows
            // and this might not return for minutes or more
            this.Dispatcher.BeginInvoke(new ScannedDataReceivedEventHandler(scanner_ScanReceived),
                System.Windows.Threading.DispatcherPriority.Input, sender, args);
            return;
        }

        // find the appropriate scope to send it
        var kf = Keyboard.FocusedElement as DependencyObject;

        var lastResort = _ScanOfLastResort;
        if (kf == null)
        {
            if (lastResort is not null)
            {
                lastResort(this, args);
            }
            else
            {
                Debug.WriteLine("No focused element to send data");
            }
            return;
        }

        var distance = registeredScopes
            .Select(scope => new { Distance = distToElement(kf, scope), Scope = scope })
            .OrderBy(sc => sc.Distance);

        foreach (var prop in distance)
        {
            prop.Scope.HandleScan(this, args);

            if (args.IsHandled)
                break;
        }

        if (!args.IsHandled && lastResort is not null)
        {
            lastResort(this, args);
        }

        if (!args.IsHandled)
            Debug.WriteLine("Scan went unanswered: {0}", args.TextData);
    }

    private int distToElement(DependencyObject child, ScannerScope parent)
    {
        int levels = 0;
        DependencyObject cnode = child;
        while (cnode != parent && cnode != null)
        {
            levels++;
            cnode = VisualTreeHelper.GetParent(cnode);
        }

        // parent is not in visual tree
        if (cnode == null)
            return Int32.MaxValue;

        return levels;
    }

    #endregion
}
