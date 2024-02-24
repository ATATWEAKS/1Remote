namespace _1RM.View.Host.ProtocolHosts;

public enum ProtocolHostStatus
{
    NotInit,
    Initializing,
    Initialized,
    Connecting,
    Connected,
    Disconnected,
    WaitingForReconnect
}