# RabbitMQ email delivery knowledge

Last reviewed: 2026-09-10. Scope: this project's booking notifications, using raw
RabbitMQ.Client, EF Core/SQL Server, and MailKit SMTP. Read this before modifying
publishing, consumers, retry policy, or background email delivery.

## Evidence and scope

- Source inspected on the review date: publisher, consumer, dispatcher, booking and
  user services, test project, Compose, and deployment runbook.
- Claude reported eight real-infrastructure tests passing against local SQL Server,
  RabbitMQ, and smtp4dev, including a later-added test that blocks the failed queue with
  a `max-length: 0`/`overflow: reject-publish` policy and confirms a message survives the
  outage and eventually arrives once the policy is lifted. This knowledge-writing session
  did not rerun them.
- Local results do not verify the VPS, its effective broker policy, or its volumes.
- Booking notifications use the outbox. Registration, confirmation resend, password
  reset, and login notifications must be traced separately. `UsersService` still
  awaits email calls; registration currently does so before its transaction commits.
- The user's intended outcome is background delivery for all email flows. That is a
  requirement, not a claim that this implementation already covers every flow.

## Current flow and ownership

1. `BookingServices` saves the booking and `BookingOutboxMessage` in the same explicit
   database transaction. It does not publish directly to RabbitMQ.
2. `BookingOutboxDispatcher` polls pending rows every five seconds, up to 20 per batch.
3. `RabbitMqBookingEventPublisher` opens a channel with publisher confirmations and
   tracking enabled, publishes a persistent message with `mandatory: true`, and
   accounts for returned/unroutable messages. Only a successful publish allows the
   dispatcher to set `DispatchedAtUtc`.
4. `BookingConfirmationConsumer` consumes `booking-confirmed` with manual
   acknowledgements and prefetch 5. It checks `SentBookingNotifications`, sends through
   `IBookingNotificationSender`, records the notification ID, then acknowledges.
5. Malformed messages and exhausted retries go to `booking-confirmed.failed` through
   `booking-confirmed.dlx`, subject to the deployed broker policy.

Source locations (relative to the repository root):

- `PatientBooking.Api.Application/Services/BookingServices.cs`
- `PatientBooking.Api.Application/Services/UsersService.cs`
- `PatientBooking.Api.Application/Messaging/BookingConfirmedEvent.cs`
- `PatientBooking.Api.Application/Messaging/RabbitMqBookingEventPublisher.cs`
- `PatientBooking.Api.Application/Messaging/RabbitMqConnectionProvider.cs`
- `PatientBooking.Api/BackgroundServices/BookingOutboxDispatcher.cs`
- `PatientBooking.Api/BackgroundServices/BookingConfirmationConsumer.cs`
- `PatientBooking.Api.Tests/BookingConfirmationConsumerTests.cs`
- `PatientBooking.Api.Tests/Infrastructure/MessagingTestFixture.cs`

An async method is not automatically a background job. Awaiting SMTP still keeps the
request pending. Durable background delivery requires committing the queued work and
letting a worker send it later. The five-second poll affects email arrival, not a
promise of any particular API response time. See Microsoft's
[hosted service guidance](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services).

## Failure cases we must not reintroduce

| Failure | Lesson |
|---|---|
| SQL fails before SMTP | Scope resolution and dedup lookup belong inside the delivery try/catch. An escaping exception can leave deliveries unacknowledged and fill all five prefetch slots. |
| Saving the sent record fails | Do not acknowledge every `DbUpdateException` as a duplicate. Current SQL Server classification accepts only inner `SqlException` numbers 2627/2601 in the dedup insert scope. Other failures retry. Revisit classification if that save gains other writes. |
| Legacy event has no notification ID | Never use `Guid.Empty` as a shared dedup key. Current compatibility code derives a stable, namespaced SHA-256 key from `BookingId`. Preserve that algorithm for persisted old messages. It assumes one confirmation event per booking. |
| Required payload fields are missing | Reject without requeue; retries cannot repair malformed input. |
| Broker cancels a consumer without disconnecting | `UnregisteredAsync` wakes the outer loop to redeclare topology and consume again. Connection recovery alone does not cover every cancellation scenario. |
| Broker is unavailable during booking | Commit the outbox with the booking; let the dispatcher retry. Do not restore a synchronous broker dependency to booking creation. |

For RabbitMQ 4.3 behavior, explicit `basic.nack(requeue: true)` does not increment the
quorum delivery counter. The current retry path uses `BasicRejectAsync(requeue: true)`
after a one-second delay. Do not replace it with nack and assume the delivery limit
still bounds attempts. `basic.nack(requeue: false)` remains appropriate for immediate
dead-lettering of malformed input. Verify semantics against the actual broker version:
[quorum poison-message handling](https://www.rabbitmq.com/docs/quorum-queues#poison-message-handling).

The earlier local verification reported RabbitMQ 4.3.4. `docker-compose.yml`,
`docker/dev.compose.yaml`, and `MessagingTestFixture.cs` now pin `rabbitmq:4.3.4-management-alpine`
instead of the floating `4-management-alpine` tag, after that floating tag was observed
moving to 4.3.5 mid-development of this test suite. Bump the pin in all three places
together, deliberately, rather than re-pulling the floating tag.

## Delivery guarantees and limits

- Publisher confirmation means broker acceptance, not that SMTP sent the email.
  Mandatory routing and publisher confirmations solve different failure cases.
  See [publisher confirms](https://www.rabbitmq.com/docs/confirms).
- A crash after publishing but before saving `DispatchedAtUtc` can cause republishing.
  Preserve the same notification ID across retries.
- Sequential redelivery after a committed sent record is skipped. Deduplication does
  not guarantee exactly-once email: SMTP may succeed before the sent-record save fails,
  and concurrent consumers can both send before either saves. A unique-key catch after
  sending cannot undo either email.
- A prolonged SQL or SMTP outage can exhaust retries and send messages to the failed
  queue. Restoring the dependency does not automatically replay that queue in this app.
  A bounded, non-destructive replay procedure is now documented in the deployment
  runbook: peek with requeue enabled, republish, confirm the `SentBookingNotifications`
  row by `notificationId` before removing the original. It has not been exercised against
  a real failed message. Preserve IDs when replaying and acknowledge the possibility of
  duplicate email after an ambiguous send.
- Review coordination before scaling API replicas: each instance hosts workers, and
  the current dispatcher does not claim rows with a distributed lease.

## Deployment decisions

Use the [deployment runbook](../../docs/deployment.md#rabbitmq) for exact commands.
Keep command maintenance there rather than copying a second runsheet into this file.

- Both queues are durable quorum queues. Mutable retry/DLX settings are broker policy,
  avoiding incompatible hardcoded queue-argument redeclarations.
- The policy sets delivery limit 3, the DLX/routing key, `dead-letter-strategy:
  at-least-once`, and `overflow: reject-publish`. Verify the effective policy and required
  feature flags. Do not infer the applied policy from source code alone.
- At-least-once dead-lettering retains messages until the target confirms acceptance.
  It does not mean retries from the failed queue back to the main queue. See
  [dead-letter prerequisites and caveats](https://www.rabbitmq.com/docs/quorum-queues#dead-lettering).
- Policy survives with broker metadata in its volume. A new empty broker needs setup;
  an existing broker needs the updated policy applied when its definition changes.
- Boot-time definitions import was rejected after local testing showed default user
  seeding was suppressed. Do not reintroduce it without deliberately handling users and
  vhosts securely. See [deployment knowledge](aspnet-dokploy-deployment.md) for history
  and [RabbitMQ definitions](https://www.rabbitmq.com/docs/definitions).
- Credentials come from Dokploy Environment through Compose. Never commit credentials.
  AMQP has no host mapping; management maps to `127.0.0.1:15672:15672` for an SSH tunnel.
  A tunnel to the VPS host needs that mapping; container-only exposure is insufficient.
- API startup uses `service_started` for RabbitMQ so broker health is not a prerequisite
  to serving bookings with a working database/outbox.

## Verification lessons and remaining checks

The eight checked-in tests cover outbox dispatch, duplicate redelivery, recovery after
a short SQL outage with five messages, duplicate-key exception classification, legacy
deduplication, missing required fields, permanent SMTP failure dead-lettering, and the
dead-letter destination becoming unavailable and then recovering (message survives the
outage and eventually reaches `booking-confirmed.failed`). The exception classification
test performs successive inserts with two contexts; it does not prove concurrent
full-pipeline behavior. Do not describe every test as an HTTP end-to-end test: these
tests also exercise workers directly.

Reported harness lessons: under the selected xUnit v3/Microsoft.Testing.Platform runner,
Claude used `--parallel none`; do not assume a copied runner JSON file enforces the
desired execution mode. Verify the active runner's help/configuration when changing it.
The fixture also applies exact recipient matching after smtp4dev's substring-based
`deliveredTo` filter. Preserve that check to avoid false duplicate-email failures.

Still unverified by the reported suite:

1. SQL outage long enough to exhaust retries is confirmed by source inspection (no
   hosted service reads `booking-confirmed.failed`) and its replay procedure is now
   documented, but the procedure itself has not been exercised against a real failed
   message.
2. Genuine concurrent duplicate-key race through the complete send/persist pipeline.
3. Effective policy, port binding, migrations, and notification delivery on the VPS.

Keep tests isolated from normal development data and real recipients. When PostgreSQL
replaces SQL Server, preserve the delivery design but replace provider-specific error
classification, migrations, and test infrastructure; SQL Server error numbers are not
portable. Avoid introducing a messaging framework or rewriting this working flow solely
to capture these lessons.
