using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PatientBooking.Api.Application.Contracts;
using PatientBooking.Api.Application.Messaging;
using PatientBooking.Api.Domain;

namespace PatientBooking.Api.BackgroundServices;

// Relays RegistrationOutboxMessage rows (written transactionally alongside the account they belong
// to - see UsersService.EnqueueConfirmationEmailAsync) to RabbitMQ. A row is marked dispatched only
// once the publish is broker-confirmed; anything else - broker down, nack, unroutable - is retried
// on the next tick. Mirrors BookingOutboxDispatcher's polling/retry design.
public sealed class RegistrationOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<RegistrationOutboxDispatcher> logger
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
                logger.RegistrationOutboxDispatchLoopFailed(ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DispatchPendingAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PatientBookingDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IRegistrationEventPublisher>();

        List<RegistrationOutboxMessage> pending = await db
            .RegistrationOutboxMessages
            .Where(m => m.DispatchedAtUtc == null)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (RegistrationOutboxMessage message in pending)
        {
            RegistrationConfirmationEvent? evt;
            try
            {
                evt = JsonSerializer.Deserialize<RegistrationConfirmationEvent>(message.Payload);
            }
            catch (JsonException ex)
            {
                logger.RegistrationOutboxMessageMalformed(ex, message.Id);
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

            bool published = await publisher.PublishRegistrationConfirmationAsync(evt, ct);
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
