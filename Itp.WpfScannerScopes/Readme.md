# ITP WPF Barcode Scanner API

Keyboard-like incorporation of barcode scanners into WPF applications.  Support for Serial and HID
barcode scanners.

`ScannerScope`s are a container element which handles scans which occur when focus is within the scope.

Scanner scopes connect to a parent `ScannerControlScope`, which "owns" the scanner and interacts with hardware.

When focus is outside of any `ScannerScope`s, the scanner is disabled or ceded to other applications

## Example use

    <Window x:Class="Itp.WpfScanners.TestClient.ScannerTestWindow"
            xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" 
            xmlns:cscan="urn:itp:scanners"
            Title="ScannerTestWindow" Height="300" Width="300">
        <cscan:ScannerControlScope AutoConfigure="True">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="1*" />
                    <ColumnDefinition Width="1*" />
                </Grid.ColumnDefinitions>

                <TextBox Text="This side of the form is not scanner enabled" />
                <GroupBox Header="This side is enabled" Grid.Column="1">
                    <cscan:ScannerScope x:Name="scope1" ScanReceived="scope1_ScanReceived">
                        <StackPanel Orientation="Vertical">
                            <TextBox x:Name="tbS1" Text="This is attached to the outside" />
                                <GroupBox Header="still enabled">
                                <cscan:ScannerScope x:Name="scope2" ScanReceived="scope2_ScanReceived">
                                    <StackPanel Orientation="Vertical">
                                        <Button Content="this is on the inside" x:Name="btS2" />
                                    </StackPanel>
                                </cscan:ScannerScope>
                            </GroupBox>
                        </StackPanel>
                    </cscan:ScannerScope>
                </GroupBox>
            </Grid>
        </cscan:ScannerControlScope>
    </Window>

Codebehind:

    using System.Windows;

    namespace Itp.WpfScanners.TestClient
    {
        public partial class ScannerTestWindow : Window
        {
            public ScannerTestWindow()
            {
                InitializeComponent();
            }

            private void scope1_ScanReceived(object? sender, ScannedDataEventArgs args)
            {
                tbS1.Text = args.TextData;
                args.IsHandled = true;
            }

            private void scope2_ScanReceived(object? sender, ScannedDataEventArgs args)
            {
                btS2.Content = args.TextData;
                args.IsHandled = true;
            }
        }
    }


## Known issues

* [BSOD on early Windows 10 with HID Scanners](../Itp.HidBarcodeScanner/)