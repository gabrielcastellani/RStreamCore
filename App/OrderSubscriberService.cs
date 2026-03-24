using RStreamCore.Contracts;

namespace App
{
    public class OrderSubscriberService : EventBusHostedService<OrderCreatedEvent, OrderCreatedHandler>
    {
        public OrderSubscriberService(IEventBus bus) : base(bus) { }
    }
}
