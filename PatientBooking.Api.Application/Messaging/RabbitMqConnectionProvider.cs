using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PatientBooking.Api.Common.Models.Config;
using RabbitMQ.Client;

namespace PatientBooking.Api.Application.Messaging;

public sealed class RabbitMqConnectionProvider(
    IOptions<RabbitMqSettings> settings,
    TimeProvider clock,
    ILogger<RabbitMqConnectionProvider> logger
) : IAsyncDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan FailureCooldown = TimeSpan.FromSeconds(10);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private DateTimeOffset? _lastFailureUtc;

    public async Task<IChannel?> TryOpenChannelAsync(CancellationToken ct)
    {
        IConnection? connection = await GetOrConnectAsync(ct);
        if (connection is null)
        {
            return null;
        }

        try
        {
            return await connection.CreateChannelAsync(cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.RabbitMqChannelFailed(ex);
            return null;
        }
    }

    private async Task<IConnection?> GetOrConnectAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            // Previous attempt failed recently - fail fast instead of doing ConnectTimeout again
            // on every caller until cooldown resets
            if (_lastFailureUtc is { } lastFailure && clock.GetUtcNow() - lastFailure < FailureCooldown)
            {
                return null;
            }

            // _connection is null or a stale closed conn from earlier attempt. Dispose this stale conn.
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }

            ConnectionFactory factory = new()
            {
                HostName = settings.Value.HostName,
                Port = settings.Value.Port,
                UserName = settings.Value.UserName,
                Password = settings.Value.Password,
                VirtualHost = settings.Value.VirtualHost,
                RequestedConnectionTimeout = ConnectTimeout,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
            };

            _connection = await factory.CreateConnectionAsync(ct);
            _lastFailureUtc = null;
            return _connection;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.RabbitMqConnectFailed(ex);
            _lastFailureUtc = clock.GetUtcNow();
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}
