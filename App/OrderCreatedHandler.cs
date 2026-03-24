using RStreamCore.Contracts;

namespace App
{
    public class OrderCreatedHandler : IEventHandler<OrderCreatedEvent>
    {
        private readonly ILogger<OrderCreatedHandler> _log;
        public OrderCreatedHandler(ILogger<OrderCreatedHandler> log) => _log = log;
        public Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
        {
            _log.LogInformation("Order {Id} — R$ {Total}", @event.OrderId, @event.Total);
            return Task.CompletedTask;
        }
    }
}
