using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.Messaging;
using PatientBooking.Api.Domain;

namespace PatientBooking.Api.BackgroundServices;

// Relays EmailOutboxMessage rows (written transactionally alongside the business operation they
// belong to - see BookingServices.EnqueueConfirmationAsync / UsersService.EnqueueConfirmationEmailAsync)
// to RabbitMQ. A row is marked dispatched only once the publish is broker-confirmed; anything else -
// broker down, nack, unroutable - is retried on the next tick. No per-row backoff: at this message
// volume, retrying an undispatched row every 5s while the broker is down isn't a hot loop, and
// tracking per-row backoff is complexity this app doesn't need yet.
public sealed class EmailOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<EmailOutboxDispatcher> logger
) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(PollInterval, clock);
        do
        {
            try
            {
                await DispatchPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.EmailOutboxDispatchLoopFailed(ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DispatchPendingAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PatientBookingDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEmailEventPublisher>();

        List<EmailOutboxMessage> pending = await db
            .EmailOutboxMessages
            .Where(m => m.DispatchedAtUtc == null && m.ExpiredAtUtc == null)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        DateTimeOffset now = clock.GetUtcNow();

        foreach (EmailOutboxMessage message in pending)
        {
            EmailEnvelope? evt;
            try
            {
                evt = JsonSerializer.Deserialize<EmailEnvelope>(message.Payload);
            }
            catch (JsonException ex)
            {
                logger.EmailOutboxMessageMalformed(ex, message.Id);
                message.DispatchAttempts++;
                message.LastError = ex.Message;
                continue;
            }

            if (evt is null)
            {
                message.DispatchAttempts++;
                message.LastError = "Payload deserialized to null.";
                continue;
            }

            // An expired envelope must never be recorded as sent - mark it abandoned instead of
            // publishing, and stop it from being polled again (ExpiredAtUtc joins DispatchedAtUtc in
            // the WHERE clause above).
            if (evt.ExpiresAtUtc is { } expiresAtUtc && expiresAtUtc <= now)
            {
                logger.EmailOutboxMessageExpired(message.Id);
                message.ExpiredAtUtc = now;
                message.LastError = "Expired before dispatch.";
                continue;
            }

            bool published = await publisher.PublishEmailAsync(evt, ct);
            message.DispatchAttempts++;

            if (published)
            {
                message.DispatchedAtUtc = now;
                message.LastError = null;
            }
            else
            {
                message.LastError = "Publish was not confirmed by the broker.";
            }
        }

        if (pending.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
