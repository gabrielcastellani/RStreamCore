using StackExchange.Redis;

namespace RStreamCore.Engine.Connection
{
    public interface IConnectionMultiplexerFactory
    {
        Task<IConnectionMultiplexer> CreateAsync(ConfigurationOptions options);
    }
}
