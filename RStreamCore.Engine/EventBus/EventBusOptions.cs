namespace RStreamCore.Engine.EventBus
{
    public class EventBusOptions
    {
        public int BatchSize { get; set; } = 10;
        public int PollingIntervalMs { get; set; } = 500;
        public int ConcurrentWorkers { get; set; } = 4;
        public RetryPolicy Retry { get; set; } = new();
        public string DeadLetterSuffix { get; set; } = ":dlq";
        public string ConsumerName { get; set; } = Environment.MachineName;
    }
}
