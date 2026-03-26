using Microsoft.Extensions.DependencyInjection;
using RStreamCore.Contracts.DeadLetter;
using RStreamCore.Contracts.Eventbus;
using StackExchange.Redis;
using System.Text.Json;

namespace RStreamCore.Engine.EventBus
{
    public class RedisEventBus : IEventBus
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IServiceProvider _sp;
        private readonly EventBusOptions _opts;
        private readonly IDeadLetterHandler? _dlq;

        private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

        public RedisEventBus(
            IConnectionMultiplexer redis,
            IServiceProvider serviceProvider,
            EventBusOptions eventBusOptions,
            IDeadLetterHandler? dlq = null)
        {
            _redis = redis;
            _sp = serviceProvider;
            _opts = eventBusOptions;
            _dlq = dlq;
        }

        public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : IEvent
        {
            var database = _redis.GetDatabase();
            var stream = StreamKey<TEvent>();
            var payload = JsonSerializer.Serialize(@event, _json);

            await database.StreamAddAsync(stream, new NameValueEntry[]
            {
                new("type", typeof(TEvent).AssemblyQualifiedName!),
                new("payload", payload),
            });
        }

        public async Task SubscribeAsync<TEvent, THandler>(CancellationToken cancellationToken = default)
            where TEvent : IEvent
            where THandler : IEventHandler<TEvent>
        {
            var database = _redis.GetDatabase();
            var stream = StreamKey<TEvent>();
            var group = GroupName<THandler>();

            await EnsureGroupAsync(database, stream, group);

            var workers = Enumerable.Range(0, _opts.ConcurrentWorkers)
                .Select(workIndex =>
                {
                    return Task.Run(() =>
                    {
                        return ConsumeLoopAsync<TEvent, THandler>(database, stream, group, consumer: $"{_opts.ConsumerName}-{workIndex}", cancellationToken);
                    }, cancellationToken);
                });

            await Task.WhenAll(workers);
        }

        private async Task ConsumeLoopAsync<TEvent, THandler>(IDatabase database, string stream, string group, string consumer, CancellationToken cancellationToken)
            where TEvent : IEvent
            where THandler : IEventHandler<TEvent>
        {
            await ProcessPendingAsync<TEvent, THandler>(database, stream, group, consumer, cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                var entries = await database.StreamReadGroupAsync(
                    stream, group, consumer,
                    position: ">",
                    count: _opts.BatchSize);

                if (entries.Length == 0)
                {
                    await Task.Delay(_opts.PollingIntervalMs, cancellationToken);
                    continue;
                }

                await Parallel.ForEachAsync(
                    source: entries,
                    parallelOptions: new ParallelOptions { MaxDegreeOfParallelism = _opts.BatchSize, CancellationToken = cancellationToken },
                    async (entry, token) =>
                    {
                        await ProcessWithRetryAsync<TEvent, THandler>(database, stream, group, entry, token);
                    });
            }
        }

        private async Task ProcessPendingAsync<TEvent, THandler>(IDatabase database, string stream, string group, string consumer, CancellationToken cancellationToken)
            where TEvent : IEvent
            where THandler : IEventHandler<TEvent>
        {
            var pending = await database.StreamPendingMessagesAsync(stream, group, 100, consumer);

            if (pending.Length == 0)
                return;

            var ids = pending.Select(p => p.MessageId).ToArray();
            var claimed = await database.StreamClaimAsync(stream, group, consumer, minIdleTimeInMs: 0, ids);

            await Parallel.ForEachAsync(
                source: claimed,
                parallelOptions: new ParallelOptions { MaxDegreeOfParallelism = _opts.ConcurrentWorkers, CancellationToken = cancellationToken },
                async (entry, token) =>
                {
                    await ProcessWithRetryAsync<TEvent, THandler>(database, stream, group, entry, token);
                });
        }

        private async Task ProcessWithRetryAsync<TEvent, THandler>(IDatabase database, string stream, string group, StreamEntry entry, CancellationToken cancellationToken)
            where TEvent : IEvent
            where THandler : IEventHandler<TEvent>
        {
            var lastError = string.Empty;

            for (int attempt = 1; attempt <= _opts.Retry.MaxAttempts; attempt++)
            {
                try
                {
                    var payload = entry["payload"].ToString();
                    var @event = JsonSerializer.Deserialize<TEvent>(payload, _json)!;

                    await using var scope = _sp.CreateAsyncScope();
                    var handler = scope.ServiceProvider.GetRequiredService<THandler>();

                    await handler.HandleAsync(@event, cancellationToken);
                    await database.StreamAcknowledgeAsync(stream, group, entry.Id);

                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;

                    if (attempt < _opts.Retry.MaxAttempts)
                    {
                        var delay = _opts.Retry.GetDelay(attempt);
                        await Task.Delay(delay, cancellationToken);
                    }
                }
            }

            await SendToDeadLetterAsync(database, stream, entry, lastError, cancellationToken);
            await database.StreamAcknowledgeAsync(stream, group, entry.Id);
        }

        private async Task SendToDeadLetterAsync(IDatabase database, string stream, StreamEntry entry, string lastError, CancellationToken cancellationToken)
        {
            var dlqStream = stream + _opts.DeadLetterSuffix;
            var payload = entry["payload"].ToString();
            var eventType = entry["type"].ToString();

            await database.StreamAddAsync(dlqStream, new NameValueEntry[]
            {
                new("type", eventType),
                new("payload", payload),
                new("attempts", _opts.Retry.MaxAttempts.ToString()),
                new("last_error", lastError),
                new("dead_at", DateTime.UtcNow.ToString("O")),
                new("origin", stream),
            });

            if (_dlq != null)
            {
                var msg = new DeadLetterMessage(
                    StreamKey: stream,
                    EntryId: entry.Id!,
                    EventType: eventType,
                    Payload: payload,
                    Attempts: _opts.Retry.MaxAttempts,
                    LastError: lastError,
                    DeadAt: DateTime.UtcNow);

                await _dlq.HandleAsync(msg, cancellationToken);
            }
        }

        private static async Task EnsureGroupAsync(IDatabase database, string stream, string group)
        {
            try
            {
                await database.StreamCreateConsumerGroupAsync(stream, group, "$", createStream: true);
            }
            catch (RedisServerException error) when (error.Message.Contains("BUSYGROUP")) { }
        }

        private static string StreamKey<TEvent>()
        {
            return $"streamflow:{typeof(TEvent).Name.ToLowerInvariant()}";
        }

        private static string GroupName<THandler>()
        {
            return typeof(THandler).Name.ToLowerInvariant();
        }
    }
}
