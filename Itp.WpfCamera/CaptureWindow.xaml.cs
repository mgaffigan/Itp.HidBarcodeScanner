using Esatto.Utilities;
using Itp.Handheld.WpfClient.Capture.WinRt;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Itp.Handheld.WpfClient.Pack;

public partial class CaptureWindow : Window
{
    private CaptureWindow(CaptureWindowViewModel viewModel)
    {
        InitializeComponent();

        this.DataContext = this.ViewModel = viewModel;
        cbCamera.ItemsSource = viewModel.Cameras;

        // Seeded before the handler is attached so the default selection does not open a camera.
        cbCamera.SelectedItem = viewModel.DefaultCamera;
        cbCamera.SelectionChanged += cbCamera_SelectionChanged;

        Loaded += CaptureWindow_Loaded;
        Activated += CaptureWindow_Activated;
        Closed += CaptureWindow_Closed;
    }

    public static async Task ShowDialogAsync(Window parent, CaptureImagesPackStep step)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(step);

        var window = new CaptureWindow(await CaptureWindowViewModel.CreateAsync(step))
        {
            Owner = parent,
        };

        window.ShowDialog();
    }

    public CaptureWindowViewModel ViewModel { get; }

    private async void CaptureWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel.DefaultCamera is not null)
        {
            await OpenCameraAsync(ViewModel.DefaultCamera);
        }
    }

    private async void cbCamera_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Reassigning ItemsSource clears the selection.
        if (cbCamera.SelectedItem is WinRtCameraDeviceInfo device)
        {
            await OpenCameraAsync(device);
        }
    }

    private async Task OpenCameraAsync(WinRtCameraDeviceInfo device)
    {
        cbCamera.IsEnabled = false;
        try
        {
            await this.RunAsync("opening camera", () => ViewModel.OpenCameraAsync(device));
        }
        finally
        {
            cbCamera.IsEnabled = true;
        }
    }

    private async void CaptureWindow_Activated(object? sender, EventArgs e)
    {
        await this.RunAsync("reconnecting camera", ViewModel.ReconnectIfNeededAsync);
    }

    private void CaptureWindow_Closed(object? sender, EventArgs e)
    {
        this.Run("closing camera", ViewModel.Dispose);
    }

    // Previewed so the key reaches here even while the camera combo box has focus.
    private async void CaptureWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F2 || !ViewModel.CanCapture)
        {
            return;
        }

        e.Handled = true;
        await CaptureImageAsync();
    }

    private async void btCapture_Click(object sender, RoutedEventArgs e)
    {
        await CaptureImageAsync();
    }

    private async Task CaptureImageAsync()
    {
        // A reported failure leaves the window up, to retry from.
        await this.RunAsync("capturing image", async () =>
        {
            await ViewModel.CaptureAsync();
            captureFlash.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0.8, 0, TimeSpan.FromMilliseconds(250)));
            Close();
        });
    }

    private void btClose_Click(object sender, RoutedEventArgs e) => Close();
}
