namespace RStreamCore.Contracts.Connection
{
    public enum RedisConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting
    }
}
