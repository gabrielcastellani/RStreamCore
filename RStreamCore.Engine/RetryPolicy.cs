namespace RStreamCore.Engine
{
    public class RetryPolicy
    {
        public int MaxAttempts { get; init; } = 3;
        public int BaseDelayMs { get; init; } = 500;
        public int MaxDelayMs { get; init; } = 10_000;
        public double BackoffFactor { get; init; } = 2.0;

        public TimeSpan GetDelay(int attempt)
        {
            var ms = Math.Min(BaseDelayMs * Math.Pow(BackoffFactor, attempt - 1), MaxDelayMs);
            return TimeSpan.FromMilliseconds(ms);
        }
    }
}
