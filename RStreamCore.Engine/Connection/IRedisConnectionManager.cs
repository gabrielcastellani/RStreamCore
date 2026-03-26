using RStreamCore.Contracts.Connection;
using StackExchange.Redis;

namespace RStreamCore.Engine.Connection
{
    public interface IRedisConnectionManager : IAsyncDisposable
    {
        RedisConnectionState State { get; }
        bool IsConnected { get; }

        Task<IDatabase> GetDatabaseAsync(CancellationToken cancellationToken = default);
        Task EnsureConnectedAsync(CancellationToken cancellationToken = default);
    }
}
