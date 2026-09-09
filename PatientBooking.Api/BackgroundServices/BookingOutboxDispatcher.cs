using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.Messaging;
using PatientBooking.Api.Domain;

namespace PatientBooking.Api.BackgroundServices;

// Relays BookingOutboxMessage rows (written transactionally alongside the booking they belong to -
// see BookingServices.EnqueueConfirmationAsync) to RabbitMQ. A row is marked dispatched only once
// the publish is broker-confirmed; anything else - broker down, nack, unroutable - is retried on the
// next tick. No per-row backoff: at this message volume, retrying an undispatched row every 5s while
// the broker is down isn't a hot loop, and tracking per-row backoff is complexity this app doesn't need yet.
public sealed class BookingOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<BookingOutboxDispatcher> logger
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
                logger.BookingOutboxDispatchLoopFailed(ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DispatchPendingAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PatientBookingDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IBookingEventPublisher>();

        List<BookingOutboxMessage> pending = await db
            .BookingOutboxMessages
            .Where(m => m.DispatchedAtUtc == null)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (BookingOutboxMessage message in pending)
        {
            BookingConfirmedEvent? evt;
            try
            {
                evt = JsonSerializer.Deserialize<BookingConfirmedEvent>(message.Payload);
            }
            catch (JsonException ex)
            {
                logger.BookingOutboxMessageMalformed(ex, message.Id);
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

            bool published = await publisher.PublishBookingConfirmedAsync(evt, ct);
            message.DispatchAttempts++;

            if (published)
            {
                message.DispatchedAtUtc = clock.GetUtcNow();
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
