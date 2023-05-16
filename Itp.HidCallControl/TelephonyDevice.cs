using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Storage;

namespace Itp.HidCallControl;

public sealed class TelephonyDevice : IDisposable
{
    private readonly DeviceInformation Info;
    private HidDevice Device;

    public event EventHandler<TelephonyKeyPressedEventArgs>? KeyPressed;

    private TelephonyDevice(DeviceInformation info, HidDevice device)
    {
        this.Info = info;
        this.Device = device;

        this.Device.InputReportReceived += Device_InputReportReceived;
    }

    public static async Task<TelephonyDevice> CreateAsync(DeviceInformation info)
    {
        var device = await HidDevice.FromIdAsync(info.Id, FileAccessMode.Read);
        return new TelephonyDevice(info, device ?? throw new FileNotFoundException());
    }

    public void Dispose()
    {
        var tdev = Device;
        Device = null!;
        tdev.Dispose();
    }

    public override string ToString() => $"{Info.Name} ({Info.Id})";

    private void Device_InputReportReceived(HidDevice sender, HidInputReportReceivedEventArgs args)
    {
        foreach (var active in args.Report.ActivatedBooleanControls)
        {
            KeyPressed?.Invoke(this, new TelephonyKeyPressedEventArgs((TelephonyKey)active.UsageId));
        }
    }
}
