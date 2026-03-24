namespace RStreamCore.Contracts
{
    public interface IEvent
    {
        string EventId { get; }
        DateTime OccurredAt { get; }
    }
}
