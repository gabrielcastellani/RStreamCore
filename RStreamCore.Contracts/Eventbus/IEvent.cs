namespace RStreamCore.Contracts.Eventbus
{
    public interface IEvent
    {
        string EventId { get; }
        DateTime OccurredAt { get; }
    }
}
