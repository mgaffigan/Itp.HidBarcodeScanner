using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Windows.Media.Capture.Frames;

namespace Itp.WpfCamera;

/// <summary>
/// A maximized dialog with a camera picker and live preview that captures a single still.  Show it
/// with <see cref="ShowDialogAsync"/>.
/// </summary>
public partial class CaptureWindow : Window
{
    private readonly CaptureWindowViewModel _viewModel;
    private readonly TaskCompletionSource<BitmapSource> _result;

    private CaptureWindow(IReadOnlyList<MediaFrameSourceGroup> cameras, CaptureWindowViewModel viewModel,
        TaskCompletionSource<BitmapSource> result)
    {
        InitializeComponent();

        this.DataContext = _viewModel = viewModel;
        _result = result;

        btCamera.ContextMenu.ItemsSource = cameras;
        btCamera.Visibility = cameras.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        Loaded += CaptureWindow_Loaded;
        Closing += (_, _) => _result.TrySetCanceled();
    }

    /// <summary>
    /// Shows the capture dialog over <paramref name="owner"/> and waits for a still.
    /// </summary>
    /// <returns>A frozen still at the camera's capture resolution.</returns>
    /// <exception cref="OperationCanceledException">The user closed the dialog without capturing.</exception>
    /// <exception cref="InvalidOperationException">
    /// No camera was found, or the camera could not be opened, failed while open, or could not capture.
    /// </exception>
    public static async Task<BitmapSource> ShowDialogAsync(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var cameras = await WinRtCameraDeviceEnumerator.EnumerateAsync();
        var remembered = CaptureSettings.Instance.SelectedCameraId;
        var initial = cameras.FirstOrDefault(c => c.Id == remembered) ?? cameras.FirstOrDefault()
            ?? throw new InvalidOperationException("No camera was found.");

        // Completed once, by whichever comes first: a capture, a failure, or the user closing the window.
        var result = new TaskCompletionSource<BitmapSource>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var viewModel = new CaptureWindowViewModel(await OpenCameraAsync(initial, result));
        var window = new CaptureWindow(cameras, viewModel, result)
        {
            Owner = owner,
        };

        window.ShowDialog();
        return await result.Task;
    }

    private static Task<WinRtCamera> OpenCameraAsync(MediaFrameSourceGroup group, TaskCompletionSource<BitmapSource> result)
        => WinRtCamera.OpenAsync(group, (_, e) => result.TrySetException((Exception)e.ExceptionObject));

    // Handlers are async void, so nothing may escape them or it would reach the dispatcher.
    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _result.TrySetException(ex);
        }
    }

    private async void CaptureWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Completing the result, for any reason, ends the dialog.  Close is a no-op if the user
        // already closed it.
        await Task.WhenAny(_result.Task);
        Close();
    }

    // Opens the camera menu on a left click too.
    private void btCamera_Click(object sender, RoutedEventArgs e)
    {
        btCamera.ContextMenu.PlacementTarget = btCamera;
        btCamera.ContextMenu.IsOpen = true;
    }

    private async void Capture_Executed(object sender, ExecutedRoutedEventArgs e)
        => await RunAsync(async () => _result.TrySetResult(await _viewModel.CaptureAsync()));

    // The open camera is held exclusively, so it cannot be opened again.
    private void SwitchCamera_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        => e.CanExecute = e.Parameter is MediaFrameSourceGroup group && group.Id != _viewModel.Group.Id;

    private async void SwitchCamera_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var group = (MediaFrameSourceGroup)e.Parameter;
        await RunAsync(async () => await _viewModel.AcceptAsync(await OpenCameraAsync(group, _result)));
    }

    private void Close_Executed(object sender, ExecutedRoutedEventArgs e) => Close();
}
