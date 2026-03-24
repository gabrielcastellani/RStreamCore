using Microsoft.Extensions.DependencyInjection;
using RStreamCore.Contracts;
using RStreamCore.Engine;
using StackExchange.Redis;

namespace RStreamCore.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRStreamCore(this IServiceCollection services, string redisConnection, Action<EventBusOptions>? configure = null)
        {
            var eventBusOptions = new EventBusOptions();
            configure?.Invoke(eventBusOptions);

            services.AddSingleton(eventBusOptions);
            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnection));
            services.AddSingleton<IEventBus, RedisEventBus>();

            return services;
        }

        public static IServiceCollection AddDeadLetterHandler<THandler>(this IServiceCollection services)
            where THandler : class, IDeadLetterHandler
        {
            services.AddSingleton<IDeadLetterHandler, THandler>();
            return services;
        }
    }
}
