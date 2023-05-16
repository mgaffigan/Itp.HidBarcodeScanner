using Windows.Devices.Enumeration;

namespace Itp.HidCallControl;

public class TelephonyDeviceWatcherMulticast : IDisposable
{
    private readonly UnhandledExceptionEventHandler Faulted;
    private readonly List<TelephonyDevice> Devices;
    private readonly DeviceWatcher Watcher;

    public event EventHandler<TelephonyKeyPressedEventArgs>? KeyPressed;

    public TelephonyDeviceWatcherMulticast(UnhandledExceptionEventHandler faulted)
    {
        Faulted = faulted;
        Devices = new List<TelephonyDevice>();

        // Get all HID with Usage Page 11
        // Can't use HidDevice.GetDeviceSelector since it requires a single UsageID
        // Spec has many UsageID, so can't use OR with multiple calls.
        //var aql = HidDevice.GetDeviceSelector(0x000b /* Telephony */, 0x0005 /* Handset */);
        var aql = "System.Devices.InterfaceClassGuid:=\"{4D1E55B2-F16F-11CF-88CB-001111000030}\" " +
            "AND System.Devices.InterfaceEnabled:=System.StructuredQueryType.Boolean#True " +
            "AND System.DeviceInterface.Hid.UsagePage:=11";

        Watcher = DeviceInformation.CreateWatcher(aql);
        Watcher.Added += Watcher_Added;
        Watcher.Removed += Watcher_Removed;
        Watcher.Start();
    }

    private void Watcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        // nop, but required to get added notices
    }

    public void Dispose()
    {
        Watcher.Stop();

        var tdev = Devices.ToArray();
        Devices.Clear();

        var exceptions = new List<Exception>();
        foreach (var dev in tdev)
        {
            try
            {
                dev.Dispose();
            }
            catch (Exception ex) { exceptions.Add(ex); }
        }
        if (exceptions.Any()) throw new AggregateException(exceptions);
    }

    private async void Watcher_Added(DeviceWatcher sender, DeviceInformation args)
    {
        try
        {
            var dev = await TelephonyDevice.CreateAsync(args);
            Devices.Add(dev);
            dev.KeyPressed += Device_KeyPressed;
        }
        catch (Exception ex)
        {
            Faulted?.Invoke(args, new UnhandledExceptionEventArgs(ex, false));
        }
    }

    private void Device_KeyPressed(object? sender, TelephonyKeyPressedEventArgs e)
    {
        try
        {
            KeyPressed?.Invoke(sender, e);
        }
        catch (Exception ex)
        {
            Faulted?.Invoke(this, new UnhandledExceptionEventArgs(ex, false));
        }
    }
}
