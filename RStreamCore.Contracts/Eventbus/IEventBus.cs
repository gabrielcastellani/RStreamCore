namespace RStreamCore.Contracts.Eventbus
{
    public interface IEventBus
    {
        Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : IEvent;

        Task SubscribeAsync<TEvent, THandler>(CancellationToken cancellationToken = default)
            where TEvent : IEvent
            where THandler : IEventHandler<TEvent>;
    }
}
