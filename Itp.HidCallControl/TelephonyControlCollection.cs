namespace Itp.HidCallControl;

public class TelephonyControlCollection
{
    private TelephonyDeviceWatcherMulticast? Connection;
    private readonly SynchronizationContext SyncCtx;

    public TelephonyControlCollection(SynchronizationContext syncCtx)
    {
        this.SyncCtx = syncCtx;
    }

    private event EventHandler<TelephonyKeyPressedEventArgs>? _KeyPressed;
    public event EventHandler<TelephonyKeyPressedEventArgs> KeyPressed
    {
        add
        {
            _KeyPressed += value;
            OnListenerChanged();
        }
        remove
        {
            _KeyPressed -= value;
            OnListenerChanged();
        }
    }

    public event UnhandledExceptionEventHandler? UnhandledException;

    private void OnListenerChanged()
    {
        bool shouldBeConnected = _KeyPressed != null;
        bool isConnected = Connection != null;
        if (shouldBeConnected != isConnected)
        {
            if (!isConnected)
            {
                Connect();
            }
            else
            {
                Disconnect();
            }
        }
    }

    private void Connect()
    {
        var connection = new TelephonyDeviceWatcherMulticast(
            (_, e) => UnhandledException?.Invoke(this, e));
        Connection = connection;
        connection.KeyPressed += (s, e) =>
        {
            SyncCtx.Post(_ =>
            {
                try
                {
                    _KeyPressed?.Invoke(s, e);
                }
                catch (Exception ex)
                {
                    UnhandledException?.Invoke(this, new UnhandledExceptionEventArgs(ex, false));
                }
            }, null);
        };
    }

    private void Disconnect()
    {
        var tcon = Connection;
        Connection = null;
        tcon?.Dispose();
    }
}