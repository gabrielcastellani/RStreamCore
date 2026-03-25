using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RStreamCore.Engine.Connection;
using StackExchange.Redis;

namespace RStreamCore.Engine.Tests
{
    public class RedisConnectionManagerManager
    {
        [Fact]
        public async Task EnsureConnectedAsync_WhenRedisAvailable_ShouldConnect()
        {
            // Arrange
            var multiplexer = Substitute.For<IConnectionMultiplexer>();
            multiplexer.IsConnected.Returns(true);

            var factory = Substitute.For<IConnectionMultiplexerFactory>();
            factory.CreateAsync(Arg.Any<ConfigurationOptions>()).Returns(multiplexer);

            var manager = BuildManager(factory);

            // Act
            await manager.EnsureConnectedAsync();

            // Assert
            manager.IsConnected.Should().BeTrue();
            manager.State.Should().Be(RedisConnectionState.Connected);
        }

        [Fact]
        public async Task EnsureConnectedAsync_WhenRedisUnavailable_ShouldNotThrowOnStartup()
        {
            // Arrange
            var timeout = new RedisConnectionException(ConnectionFailureType.UnableToConnect, "timeout");
            var factory = Substitute.For<IConnectionMultiplexerFactory>();
            factory.CreateAsync(Arg.Any<ConfigurationOptions>())
                .Returns<IConnectionMultiplexer>(_ => throw timeout);

            var manager = BuildManager(factory);

            // Act
            var act = () => manager.EnsureConnectedAsync();

            // Assert
            await act.Should().ThrowAsync<RedisConnectionException>();
            manager.State.Should().Be(RedisConnectionState.Reconnecting);
        }

        [Fact]
        public async Task EnsureConnectedAsync_WithinCooldown_ShouldThrowWithoutCallingFactory()
        {
            // Arrange
            var timeout = new RedisConnectionException(ConnectionFailureType.UnableToConnect, "timeout");
            var factory = Substitute.For<IConnectionMultiplexerFactory>();
            factory.CreateAsync(Arg.Any<ConfigurationOptions>())
                   .Returns<IConnectionMultiplexer>(_ => throw timeout);
            var manager = BuildManager(factory, baseDelayMs: 60_000); 

            await Assert.ThrowsAsync<RedisConnectionException>(() => manager.EnsureConnectedAsync());
            
            // Act
            var act = () => manager.EnsureConnectedAsync();
            
            // Assert
            await act.Should().ThrowAsync<RedisConnectionException>().WithMessage("*Redis is unavailable*");
            await factory.Received(1).CreateAsync(Arg.Any<ConfigurationOptions>());
        }

        [Fact]
        public async Task EnsureConnectedAsync_AfterCooldown_ShouldRetry()
        {
            // Arrange
            var multiplexer = Substitute.For<IConnectionMultiplexer>();
            multiplexer.IsConnected.Returns(true);

            var factory = Substitute.For<IConnectionMultiplexerFactory>();
            factory.CreateAsync(Arg.Any<ConfigurationOptions>())
                   .Returns<IConnectionMultiplexer>(
                       _ => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"),
                       _ => multiplexer);

            var manager = BuildManager(factory, baseDelayMs: 1);

            await Assert.ThrowsAsync<RedisConnectionException>(() => manager.EnsureConnectedAsync());
            await Task.Delay(10);
            
            // Act
            await manager.EnsureConnectedAsync();
            
            // Assert
            manager.IsConnected.Should().BeTrue();
            manager.State.Should().Be(RedisConnectionState.Connected);
        }

        [Fact]
        public async Task GetDatabaseAsync_WhenConnected_ShouldReturnDatabase()
        {
            // Arrange
            var database = Substitute.For<IDatabase>();

            var multiplexer = Substitute.For<IConnectionMultiplexer>();
            multiplexer.IsConnected.Returns(true);
            multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(database);

            var factory = Substitute.For<IConnectionMultiplexerFactory>();
            factory.CreateAsync(Arg.Any<ConfigurationOptions>()).Returns(multiplexer);

            var manager = BuildManager(factory);
            await manager.EnsureConnectedAsync();

            // Act
            var db = await manager.GetDatabaseAsync();

            // Assert
            db.Should().Be(database);
        }

        [Fact]
        public async Task OnConnectionFailed_ShouldUpdateStateToReconnecting()
        {
            // Arrange
            var multiplexer = Substitute.For<IConnectionMultiplexer>();
            multiplexer.IsConnected.Returns(true);

            var factory = Substitute.For<IConnectionMultiplexerFactory>();
            factory.CreateAsync(Arg.Any<ConfigurationOptions>()).Returns(multiplexer);

            var manager = BuildManager(factory);
            await manager.EnsureConnectedAsync();

            // Act
            multiplexer.ConnectionFailed += Raise.Event<EventHandler<ConnectionFailedEventArgs>>(
                multiplexer,
                new ConnectionFailedEventArgs(null!, null, ConnectionType.Subscription, ConnectionFailureType.SocketFailure, null, null));
            
            // Assert
            manager.State.Should().Be(RedisConnectionState.Reconnecting);
        }

        private static RedisConnectionManager BuildManager(IConnectionMultiplexerFactory factory, int baseDelayMs = 1_000)
        {
            var options = new RedisManagerOptions
            {
                ConnectTimeoutMs = 1_000,
                BaseDelayMs = baseDelayMs,
                MaxDelayMs = 60_000,
                BackoffFactor = 2.0
            };

            var connectionString = "localhost:6379";
            var logger = Substitute.For<ILogger<RedisConnectionManager>>();

            return new RedisConnectionManager(connectionString, options, logger, factory);
        }
    }
}
