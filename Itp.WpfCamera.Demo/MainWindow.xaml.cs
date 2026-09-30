using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Windows.Media.Capture.Frames;

namespace Itp.WpfCamera.Demo;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<BitmapSource> _images = new();
    private WinRtCamera? _camera;

    public MainWindow()
    {
        InitializeComponent();

        lbImages.ItemsSource = _images;
        Loaded += MainWindow_Loaded;
        Closed += (_, _) => ClosePreview();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            cbCamera.ItemsSource = await WinRtCameraDeviceEnumerator.EnumerateAsync();
            cbCamera.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            ShowError("enumerating cameras", ex);
        }
    }

    // Capture dialog: the library throws, the host reports.
    private async void btCapture_Click(object sender, RoutedEventArgs e)
    {
        // Cameras are opened for exclusive control, so release the live preview first.
        ClosePreview();

        try
        {
            var image = await CaptureWindow.ShowDialogAsync(this);
            _images.Add(image);
            lbImages.SelectedItem = image;
        }
        catch (OperationCanceledException)
        {
            // The user closed the dialog.
        }
        catch (Exception ex)
        {
            ShowError("capturing an image", ex);
        }
    }

    private void btSave_Click(object sender, RoutedEventArgs e)
    {
        if (lbImages.SelectedItem is not BitmapSource image)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "PNG image (*.png)|*.png",
            FileName = "capture.png",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);
        }
        catch (Exception ex)
        {
            ShowError("saving the image", ex);
        }
    }

    // Live preview: direct use of WinRtCamera.
    private async void btOpen_Click(object sender, RoutedEventArgs e)
    {
        if (cbCamera.SelectedItem is not MediaFrameSourceGroup group)
        {
            return;
        }

        ClosePreview();
        btOpen.IsEnabled = false;
        try
        {
            _camera = await WinRtCamera.OpenAsync(group, Camera_Failed);
            imgPreview.Source = _camera.Preview;
        }
        catch (Exception ex)
        {
            ShowError("opening the camera", ex);
        }
        finally
        {
            btOpen.IsEnabled = true;
        }
    }

    private void btClosePreview_Click(object sender, RoutedEventArgs e) => ClosePreview();

    // Notification only; the camera is unusable but still owned (and disposed) by ClosePreview.
    private void Camera_Failed(object sender, UnhandledExceptionEventArgs e)
    {
        ClosePreview();
        ShowError("previewing", (Exception)e.ExceptionObject);
    }

    private void ClosePreview()
    {
        if (_camera is null)
        {
            return;
        }

        imgPreview.Source = null;
        _camera.Dispose();
        _camera = null;
    }

    private void ShowError(string action, Exception ex)
        => MessageBox.Show(this, $"Error {action}: {ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Error);
}
