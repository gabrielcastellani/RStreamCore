namespace RStreamCore.Engine.Connection
{
    public class RedisManagerOptions
    {
        public int ConnectTimeoutMs { get; init; } = 5_000;
        public int BaseDelayMs { get; init; } = 1_000;
        public int MaxDelayMs { get; init; } = 60_000;
        public double BackoffFactor { get; init; } = 2.0;
    }
}
