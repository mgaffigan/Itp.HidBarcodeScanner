// Define DEBUG and TRACE to ensure that Debug.WriteLine works in release mode
#define DEBUG
#define TRACE

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;
using System.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Itp.WpfScanners;

public class ScannerScope : ContentControl
{
    private WeakReference<ScannerControlScope>? ConnectedScope;
    private List<WeakReference<List<WeakReference<ScannerScope>>>>? ConnectedDelegationScopes;

    public ScannerScope()
    {
        this.Loaded += ScannerScope_Loaded;
        this.Unloaded += ScannerScope_Unloaded;
    }

    private void ScannerScope_Loaded(object? sender, RoutedEventArgs e)
    {
        if (DesignerProperties.GetIsInDesignMode(this)) return;

        // Add to delegation scopes
        for (var cNode = (DependencyObject)this; cNode is not null; cNode = VisualTreeHelper.GetParent(cNode))
        {
            var delegationList = GetDelegationScopes(cNode);
            if (delegationList is not null)
            {
                ConnectedDelegationScopes ??= new();
                ConnectedDelegationScopes.Add(new(delegationList));
                delegationList.Add(new(this));
            }
        }

        if (!AlwaysActive)
        {
            return;
        }

        Connect();
    }

    private void ScannerScope_Unloaded(object? sender, RoutedEventArgs e)
    {
        if (DesignerProperties.GetIsInDesignMode(this)) return;

        var delegationScopes = ConnectedDelegationScopes;
        ConnectedDelegationScopes = null;
        if (delegationScopes != null)
        {
            foreach (var wr in delegationScopes)
            {
                if (!wr.TryGetTarget(out var scopes)) continue;
                // remove any dead or this
                scopes.RemoveAll(wr => !wr.TryGetTarget(out var target) || target == this);
            }
        }

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

        if (DesignerProperties.GetIsInDesignMode(this)) return;

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

        // Walk to the top of the visual tree to find the parent ScannerControlScope
        for (var cNode = (DependencyObject)this; cNode is not null; cNode = VisualTreeHelper.GetParent(cNode))
        {
            if (cNode is ScannerControlScope parent)
            {
                parent.AddScope(this);
                ConnectedScope = new(parent);
                return;
            }
        }

        // If we reach this point, we didn't find a parent ScannerControlScope
        if (Debugger.IsAttached)
        {
            Debug.WriteLine($"Scanner scope {this} is unable to find parent");
        }
    }

    private void Disconnect()
    {
        if (ConnectedScope is not null && ConnectedScope.TryGetTarget(out var parent))
        {
            parent.RemoveScope(this);
        }
        ConnectedScope = null;
    }

    public event ScannedDataReceivedEventHandler? ScanReceived;

    internal void HandleScan(ScannedDataEventArgs args)
    {
        ScanReceived?.Invoke(this, args);
    }

    public int Priority
    {
        get { return (int)GetValue(PriorityProperty); }
        set { SetValue(PriorityProperty, value); }
    }

    public static readonly DependencyProperty PriorityProperty =
        DependencyProperty.Register("Priority", typeof(int), typeof(ScannerScope), new PropertyMetadata(1000));

    #region Delegation Scope

    public static bool GetIsDelegationScope(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsDelegationScopeProperty);
    }

    public static void SetIsDelegationScope(DependencyObject obj, bool value)
    {
        obj.SetValue(IsDelegationScopeProperty, value);
    }

    public static readonly DependencyProperty IsDelegationScopeProperty =
        DependencyProperty.RegisterAttached("IsDelegationScope", typeof(bool), typeof(ScannerScope),
            new PropertyMetadata(false, IsDelegationScope_Changed));

    private static void IsDelegationScope_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var isDelegationScope = (bool)e.NewValue;

        if (isDelegationScope)
        {
            if (GetDelegationScopes(d) is null)
            {
                SetDelegationScopes(d, new List<WeakReference<ScannerScope>>());
            }
        }
        else
        {
            SetDelegationScopes(d, null);
        }
    }

    private static List<WeakReference<ScannerScope>>? GetDelegationScopes(DependencyObject obj)
    {
        return (List<WeakReference<ScannerScope>>?)obj.GetValue(DelegationScopesProperty);
    }

    private static void SetDelegationScopes(DependencyObject obj, List<WeakReference<ScannerScope>>? value)
    {
        obj.SetValue(DelegationScopesProperty, value);
    }

    private static readonly DependencyProperty DelegationScopesProperty =
        DependencyProperty.RegisterAttached("DelegationScopes", typeof(List<WeakReference<ScannerScope>>), typeof(ScannerScope), new PropertyMetadata(null));

    public static bool TryDelegateScanTo(DependencyObject delgationScope, ScannedDataEventArgs scan)
    {
        if (delgationScope is null) throw new ArgumentNullException(nameof(delgationScope));
        if (scan is null) throw new ArgumentNullException(nameof(scan));
        if (scan.IsHandled) throw new InvalidOperationException("Scan already handled");

        var scopes = GetDelegationScopes(delgationScope) ?? throw new ArgumentOutOfRangeException($"{delgationScope} is not a delegation scope");
        var extantScopes = scopes.Select(wr => wr.TryGetTarget(out var target) ? target! : null!).Where(s => s is not null).ToList();
        foreach (var scope in extantScopes.OrderBy(s => s.Priority).ToList())
        {
            scope.HandleScan(scan);
            if (scan.IsHandled) break;
        }

        return scan.IsHandled;
    }

    #endregion
}
