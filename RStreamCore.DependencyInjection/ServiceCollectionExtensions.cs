using Microsoft.Extensions.DependencyInjection;
using RStreamCore.Contracts.Connection;
using RStreamCore.Contracts.DeadLetter;
using RStreamCore.Contracts.Eventbus;
using RStreamCore.Engine.Connection;
using RStreamCore.Engine.EventBus;
using RStreamCore.Engine.HealthCheck;

namespace RStreamCore.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRStreamCore(
            this IServiceCollection services,
            Action<EventBusOptions>? configureEventbus = null,
            Action<RedisManagerOptions>? configureManager = null)
        {
            var eventBusOptions = new EventBusOptions();
            var managerOptions = new RedisManagerOptions();

            configureEventbus?.Invoke(eventBusOptions);
            configureManager?.Invoke(managerOptions);

            services.AddSingleton(eventBusOptions);
            services.AddSingleton(managerOptions);
            services.AddSingleton<IConnectionMultiplexerFactory, ConnectionMultiplexerFactory>();
            services.AddSingleton<IRedisConnectionManager, RedisConnectionManager>();
            services.AddSingleton<IEventBus, RedisEventBus>();

            return services;
        }

        public static IServiceCollection AddDeadLetterHandler<THandler>(this IServiceCollection services)
            where THandler : class, IDeadLetterHandler
        {
            services.AddSingleton<IDeadLetterHandler, THandler>();
            return services;
        }

        public static IServiceCollection AddRStreamCoreHealthCheck(this IServiceCollection services)
        {
            services.AddHealthChecks()
                .AddCheck<RedisHealthCheck>(
                    name: "redis",
                    tags: new[] { "infra", "redis" });

            return services;
        }
    }
}
