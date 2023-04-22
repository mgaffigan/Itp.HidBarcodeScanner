using System.ComponentModel;
using System.Windows;

namespace Itp.WpfScanners;

internal static class DesignHelper
{
    private static bool designModeDetermined = false;
    private static bool isInDesignMode = false;

    public static bool InDesignMode
    {
        get
        {
            if (!designModeDetermined)
            {
                isInDesignMode = ((bool)(DesignerProperties.IsInDesignModeProperty
                    .GetMetadata(typeof(DependencyObject)).DefaultValue));
                designModeDetermined = true;
            }

            return isInDesignMode;
        }
    }
}