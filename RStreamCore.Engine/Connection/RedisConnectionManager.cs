using Microsoft.Extensions.Logging;
using RStreamCore.Contracts.Connection;
using StackExchange.Redis;

namespace RStreamCore.Engine.Connection
{
    internal sealed class RedisConnectionManager : IRedisConnectionManager
    {
        private readonly RedisManagerOptions _options;
        private readonly ILogger<RedisConnectionManager> _logger;
        private readonly SemaphoreSlim _connectLock = new(1, 1);
        private readonly IConnectionMultiplexerFactory _multiplexerFactory;

        private IConnectionMultiplexer? _multiplexer;
        private RedisConnectionState _state = RedisConnectionState.Disconnected;
        private DateTime _nextRetryAt = DateTime.MinValue;
        private int _failedAttempts = 0;

        public RedisConnectionState State => _state;
        public bool IsConnected => _state == RedisConnectionState.Connected
            && (_multiplexer?.IsConnected ?? false);

        public RedisConnectionManager(
            RedisManagerOptions options,
            ILogger<RedisConnectionManager> logger,
            IConnectionMultiplexerFactory multiplexerFactory)
        {
            _logger = logger;
            _options = options;
            _multiplexerFactory = multiplexerFactory;
        }

        public async Task<IDatabase> GetDatabaseAsync(CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken);
            return _multiplexer!.GetDatabase();
        }

        public async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            if (IsConnected)
                return;

            if (_state == RedisConnectionState.Reconnecting && DateTime.UtcNow < _nextRetryAt)
            {
                throw new RedisConnectionException(
                    failureType: ConnectionFailureType.UnableToConnect,
                    message: $"Redis is unavailable. Next attempt in {(_nextRetryAt - DateTime.UtcNow).TotalSeconds:F0} seconds");
            }

            await _connectLock.WaitAsync(cancellationToken);

            try
            {
                if (IsConnected)
                    return;

                await ConnectAsync(cancellationToken);
            }
            finally
            {
                _connectLock.Release();
            }
        }

        private async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            _state = _failedAttempts == 0
                ? RedisConnectionState.Connecting
                : RedisConnectionState.Reconnecting;

            try
            {
                _logger.LogInformation("[RStreamCore] Connecting to Redis... (attempt {N})", _failedAttempts + 1);

                var config = ConfigurationOptions.Parse(_options.ConnectionString);
                config.AbortOnConnectFail = false;
                config.ConnectTimeout = _options.ConnectTimeoutMs;
                config.ReconnectRetryPolicy = new ExponentialRetry(_options.BaseDelayMs);

                var multiplexer = await _multiplexerFactory.CreateAsync(config);

                if (!multiplexer.IsConnected)
                {
                    await multiplexer.DisposeAsync();
                    throw new RedisConnectionException(
                        failureType: ConnectionFailureType.UnableToConnect,
                        message: "Redis did not respond.");
                }

                var oldMultiplexer = _multiplexer;
                _multiplexer = multiplexer;

                if (oldMultiplexer is not null)
                    await oldMultiplexer.DisposeAsync();

                _multiplexer.ConnectionFailed += OnConnectionFailed;
                _multiplexer.ConnectionRestored += OnConnectionRestored;
                _state = RedisConnectionState.Connected;
                _failedAttempts = 0;

                _logger.LogInformation("[RStreamCore] Redis connected");
            }
            catch (Exception error)
            {
                _failedAttempts++;
                _state = RedisConnectionState.Reconnecting;
                _nextRetryAt = DateTime.UtcNow + GetBackoff();
                _logger.LogWarning(
                    message: "[RStreamCore] Failed to connect to Redis: {Msg}. Retrying in {Delay} seconds",
                    error.Message, (_nextRetryAt - DateTime.UtcNow).TotalSeconds);
                throw;
            }
        }

        private void OnConnectionRestored(object? sender, ConnectionFailedEventArgs e)
        {
            _state = RedisConnectionState.Connected;
            _failedAttempts = 0;
            _logger.LogInformation("[RStreamCore] Redis reconnected: {Endpoint}", e.EndPoint);
        }

        private void OnConnectionFailed(object? sender, ConnectionFailedEventArgs e)
        {
            _state = RedisConnectionState.Reconnecting;
            _logger.LogWarning("[RStreamCore] Redis disconnected: {Endpoint} - {Type}", e.EndPoint, e.FailureType);
        }

        private TimeSpan GetBackoff()
        {
            var ms = Math.Min(
                val1: _options.BaseDelayMs * Math.Pow(_options.BackoffFactor, _failedAttempts - 1),
                val2: _options.MaxDelayMs);

            return TimeSpan.FromMilliseconds(ms);
        }

        public async ValueTask DisposeAsync()
        {
            if (_multiplexer is not null)
            {
                _multiplexer.ConnectionFailed -= OnConnectionFailed;
                _multiplexer.ConnectionRestored -= OnConnectionRestored;

                await _multiplexer.DisposeAsync();
            }

            _connectLock.Dispose();
        }
    }
}
