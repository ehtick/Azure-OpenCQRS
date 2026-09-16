using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Memoria.Messaging.RabbitMq.Configuration;
using Memoria.Results;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace Memoria.Messaging.RabbitMq;

/// <summary>
/// Provides messaging functionality using RabbitMQ for sending queue and topic messages.
/// </summary>
/// <remarks>
/// The connection is opened on the first send rather than in the constructor: RabbitMQ.Client 7
/// opens connections asynchronously only, and a constructor cannot await. A connection handed in
/// through the second constructor is used as it is. Failing to connect surfaces as the send's
/// <see cref="Failure"/>, the way any other error on the send does.
/// </remarks>
public class RabbitMqMessagingProvider : IMessagingProvider, IAsyncDisposable, IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _connecting = new(1, 1);
    private readonly ConcurrentDictionary<string, IChannel> _queueChannels = new();
    private readonly ConcurrentDictionary<string, IChannel> _exchangeChannels = new();
    private IConnection? _connection;
    private bool _delayedExchangeEnsured;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqMessagingProvider"/> class.
    /// </summary>
    /// <param name="options">The RabbitMQ options.</param>
    public RabbitMqMessagingProvider(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqMessagingProvider"/> class with a custom connection.
    /// </summary>
    /// <param name="options">The RabbitMQ options.</param>
    /// <param name="connection">The RabbitMQ connection.</param>
    public RabbitMqMessagingProvider(IOptions<RabbitMqOptions> options, IConnection connection)
    {
        _options = options.Value;
        _connection = connection;
    }

    /// <summary>
    /// Sends a message to the specified queue.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message, which must implement IQueueMessage.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the send operation.</returns>
    public async Task<Result> SendQueueMessage<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : IQueueMessage
    {
        try
        {
            if (string.IsNullOrEmpty(message.QueueName))
            {
                return new Failure(Title: "Queue name", Description: "Queue name cannot be null or empty");
            }

            var connection = await GetConnectionAsync(cancellationToken);
            var channel = await GetOrCreateChannelAsync(_queueChannels, connection, message.QueueName, cancellationToken);

            await channel.QueueDeclareAsync(
                queue: message.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            var body = CreateMessageBody(message);
            var properties = CreateBasicProperties(message);

            if (message.ScheduledEnqueueTimeUtc.HasValue)
            {
                var delay = message.ScheduledEnqueueTimeUtc.Value - DateTimeOffset.UtcNow;
                if (delay.TotalMilliseconds > 0)
                {
                    properties.Headers ??= new Dictionary<string, object?>();
                    properties.Headers["x-delay"] = (int)delay.TotalMilliseconds;

                    await channel.BasicPublishAsync(
                        exchange: _options.DelayedExchangeName,
                        routingKey: message.QueueName,
                        mandatory: false,
                        basicProperties: properties,
                        body: body,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    await channel.BasicPublishAsync(
                        exchange: string.Empty,
                        routingKey: message.QueueName,
                        mandatory: false,
                        basicProperties: properties,
                        body: body,
                        cancellationToken: cancellationToken);
                }
            }
            else
            {
                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: message.QueueName,
                    mandatory: false,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: cancellationToken);
            }

            return Result.Ok();
        }
        catch (Exception ex)
        {
            var tagList = new TagList { { "Operation description", "Sending RabbitMQ queue message" } };
            Activity.Current?.AddException(ex, tagList, DateTimeOffset.UtcNow);
            return new Failure
            (
                Title: "Error",
                Description: "There was an error when processing the request"
            );
        }
    }

    /// <summary>
    /// Sends a message to the specified topic.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message, which must implement ITopicMessage.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the send operation.</returns>
    public async Task<Result> SendTopicMessage<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : ITopicMessage
    {
        try
        {
            if (string.IsNullOrEmpty(message.TopicName))
            {
                return new Failure(Title: "Topic name", Description: "Topic name cannot be null or empty");
            }

            var connection = await GetConnectionAsync(cancellationToken);
            var channel = await GetOrCreateChannelAsync(_exchangeChannels, connection, message.TopicName, cancellationToken);

            await channel.ExchangeDeclareAsync(
                exchange: message.TopicName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            var body = CreateMessageBody(message);
            var properties = CreateBasicProperties(message);

            var routingKey = GetRoutingKey(message);

            if (message.ScheduledEnqueueTimeUtc.HasValue)
            {
                var delay = message.ScheduledEnqueueTimeUtc.Value - DateTimeOffset.UtcNow;
                if (delay.TotalMilliseconds > 0)
                {
                    properties.Headers ??= new Dictionary<string, object?>();
                    properties.Headers["x-delay"] = (int)delay.TotalMilliseconds;
                    properties.Headers["x-original-exchange"] = message.TopicName;
                    properties.Headers["x-original-routing-key"] = routingKey;

                    await channel.BasicPublishAsync(
                        exchange: _options.DelayedExchangeName,
                        routingKey: $"topic.{message.TopicName}.{routingKey}",
                        mandatory: false,
                        basicProperties: properties,
                        body: body,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    await channel.BasicPublishAsync(
                        exchange: message.TopicName,
                        routingKey: routingKey,
                        mandatory: false,
                        basicProperties: properties,
                        body: body,
                        cancellationToken: cancellationToken);
                }
            }
            else
            {
                await channel.BasicPublishAsync(
                    exchange: message.TopicName,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: cancellationToken);
            }

            return Result.Ok();
        }
        catch (Exception ex)
        {
            var tagList = new TagList { { "Operation description", "Sending RabbitMQ topic message" } };
            Activity.Current?.AddException(ex, tagList, DateTimeOffset.UtcNow);
            return new Failure
            (
                Title: "Error",
                Description: "There was an error when processing the request"
            );
        }
    }

    /// <summary>
    /// The connection every send goes through, opened on the first call and reused after. The
    /// delayed exchange, when asked for, is declared once on the same first call, so a
    /// connection handed in through the constructor is treated the same as one opened here.
    /// </summary>
    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null && (_delayedExchangeEnsured || !_options.CreateDelayedExchange))
        {
            return _connection;
        }

        await _connecting.WaitAsync(cancellationToken);
        try
        {
            _connection ??= await CreateConnectionAsync(cancellationToken);

            if (_options.CreateDelayedExchange && !_delayedExchangeEnsured)
            {
                await EnsureDelayedExchangeExistsAsync(_connection, cancellationToken);
                _delayedExchangeEnsured = true;
            }

            return _connection;
        }
        finally
        {
            _connecting.Release();
        }
    }

    private Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_options.ConnectionString),
            VirtualHost = _options.VirtualHost,
            RequestedConnectionTimeout = TimeSpan.FromMilliseconds(_options.RequestedConnectionTimeout),
            RequestedHeartbeat = TimeSpan.FromSeconds(_options.RequestedHeartbeat),
            AutomaticRecoveryEnabled = _options.AutomaticRecoveryEnabled,
            TopologyRecoveryEnabled = _options.TopologyRecoveryEnabled
        };

        return factory.CreateConnectionAsync(cancellationToken);
    }

    private async Task EnsureDelayedExchangeExistsAsync(IConnection connection, CancellationToken cancellationToken)
    {
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        try
        {
            var arguments = new Dictionary<string, object?>
            {
                { "x-delayed-type", "direct" }
            };

            await channel.ExchangeDeclareAsync(
                exchange: _options.DelayedExchangeName,
                type: "x-delayed-message",
                durable: true,
                autoDelete: false,
                arguments: arguments,
                cancellationToken: cancellationToken);
        }
        catch (Exception)
        {
            // If the delayed message plugin is not available, we'll handle scheduling differently
            // or fall back to immediate delivery
        }
    }

    private static async Task<IChannel> GetOrCreateChannelAsync(
        ConcurrentDictionary<string, IChannel> channels,
        IConnection connection,
        string name,
        CancellationToken cancellationToken)
    {
        if (channels.TryGetValue(name, out var existingChannel) && existingChannel.IsOpen)
        {
            return existingChannel;
        }

        var newChannel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        channels[name] = newChannel;
        return newChannel;
    }

    private static byte[] CreateMessageBody<TMessage>(TMessage message) where TMessage : IMessage
    {
        var json = JsonConvert.SerializeObject(message);
        return Encoding.UTF8.GetBytes(json);
    }

    private static BasicProperties CreateBasicProperties<TMessage>(TMessage message) where TMessage : IMessage
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            MessageId = Guid.NewGuid().ToString(),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Persistent = true
        };

        if (message.Properties.Count > 0)
        {
            properties.Headers = new Dictionary<string, object?>();
            foreach (var property in message.Properties)
            {
                properties.Headers[property.Key] = property.Value;
            }
        }

        properties.Headers ??= new Dictionary<string, object?>();
        properties.Headers.Add("AssemblyQualifiedName", message.GetType().AssemblyQualifiedName);

        return properties;
    }

    private static string GetRoutingKey<TMessage>(TMessage message) where TMessage : ITopicMessage
    {
        if (message.Properties.TryGetValue("RoutingKey", out var routingKeyObj) && routingKeyObj is string routingKey)
        {
            return routingKey;
        }

        return "message";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            foreach (var channel in _queueChannels.Values)
            {
                channel.Dispose();
            }

            foreach (var channel in _exchangeChannels.Values)
            {
                channel.Dispose();
            }

            _connection?.Dispose();

            _queueChannels.Clear();
            _exchangeChannels.Clear();
            _connecting.Dispose();
        }
        catch (Exception ex)
        {
            var tagList = new TagList { { "Operation description", "Disposing RabbitMQ messaging provider" } };
            Activity.Current?.AddException(ex, tagList, DateTimeOffset.UtcNow);
        }
        finally
        {
            _disposed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            foreach (var channel in _queueChannels.Values)
            {
                await CloseAsync(channel);
            }

            foreach (var channel in _exchangeChannels.Values)
            {
                await CloseAsync(channel);
            }

            if (_connection is not null)
            {
                if (_connection.IsOpen)
                {
                    await _connection.CloseAsync();
                }

                await _connection.DisposeAsync();
            }

            _queueChannels.Clear();
            _exchangeChannels.Clear();
            _connecting.Dispose();
        }
        catch (Exception ex)
        {
            var tagList = new TagList { { "Operation description", "Disposing RabbitMQ messaging provider" } };
            Activity.Current?.AddException(ex, tagList, DateTimeOffset.UtcNow);
        }
        finally
        {
            _disposed = true;
        }
    }

    private static async Task CloseAsync(IChannel channel)
    {
        if (channel.IsOpen)
        {
            await channel.CloseAsync();
        }

        await channel.DisposeAsync();
    }
}
