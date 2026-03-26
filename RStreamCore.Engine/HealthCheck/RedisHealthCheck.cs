using Microsoft.Extensions.Diagnostics.HealthChecks;
using RStreamCore.Engine.Connection;

namespace RStreamCore.Engine.HealthCheck
{
    internal class RedisHealthCheck : IHealthCheck
    {
        private readonly IRedisConnectionManager _connectionManager;

        public RedisHealthCheck(IRedisConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var data = new Dictionary<string, object>
            {
                ["state"] = _connectionManager.State.ToString(),
                ["connected"] = _connectionManager.IsConnected
            };

            if (_connectionManager.IsConnected)
                return HealthCheckResult.Healthy("Redis connected", data);

            try
            {
                await _connectionManager.EnsureConnectedAsync(cancellationToken);
                return HealthCheckResult.Healthy("Redis reconnecting", data);
            }
            catch (Exception error)
            {
                return HealthCheckResult.Unhealthy("Redis is unavailable", error, data);
            }
        }
    }
}
