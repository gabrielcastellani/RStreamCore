using RStreamCore.Contracts;

namespace App
{
    public record OrderCreatedEvent(string OrderId, decimal Total) : IEvent
    {
        public string EventId { get; } = Guid.NewGuid().ToString();
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
    }
}
