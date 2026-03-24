using RStreamCore.Contracts;

namespace App
{
    public abstract class EventBusHostedService<TEvent, THandler> : BackgroundService
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>
    {
        private readonly IEventBus _bus;
        protected EventBusHostedService(IEventBus bus) => _bus = bus;
        protected override Task ExecuteAsync(CancellationToken ct)
            => _bus.SubscribeAsync<TEvent, THandler>(ct);
    }
}
