namespace RStreamCore.Engine.Connection
{
    public enum RedisConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting
    }
}
