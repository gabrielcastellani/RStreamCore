using StackExchange.Redis;

namespace RStreamCore.Engine.Connection
{
    internal sealed class ConnectionMultiplexerFactory : IConnectionMultiplexerFactory
    {
        public async Task<IConnectionMultiplexer> CreateAsync(ConfigurationOptions options)
        {
            return await ConnectionMultiplexer.ConnectAsync(options);
        }
    }
}
