namespace Itp.HidCallControl;

public class TelephonyKeyPressedEventArgs : EventArgs
{
    public TelephonyKey Key { get; }

    public TelephonyKeyPressedEventArgs(TelephonyKey key)
    {
        Key = key;
    }
}
