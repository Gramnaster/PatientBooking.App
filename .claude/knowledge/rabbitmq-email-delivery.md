# RabbitMQ email delivery knowledge

Last reviewed: 2026-09-10. Scope: this project's booking notifications and patient
registration confirmation, using raw RabbitMQ.Client, EF Core/SQL Server, and MailKit
SMTP. Read this before modifying publishing, consumers, retry policy, or background
email delivery.

## Evidence and scope

- Source inspected on the review date: the shared publisher/dispatcher/consumer, both
  feature services (`BookingServices`, `UsersService`), the test project, Compose, and
  the deployment runbook.
- This session **consolidated** what was previously two separate, structurally
  identical pipelines (booking confirmation and patient registration confirmation - each
  with its own outbox, dedup table, publisher, dispatcher, consumer, queue, and broker
  policy) into one shared pipeline. See "Current flow and ownership" below.
- Confirmation resend, password reset, and login notifications are still traced
  separately and still send synchronously via `SmtpIdentityEmailSender` -
  `UsersService.SendConfirmationEmailAsync` (used only by `ResendConfirmationEmailAsync`),
  `SendPasswordResetCodeAsync`/`SendPasswordResetLinkAsync`, and
  `SendLoginNotificationAsync` were deliberately left unchanged; only booking and
  registration confirmation go through the outbox.
- **Employee registration (`EmployeeService.CreateEmployeeAsync`) sends no email at
  all today** - not synchronously, not through any outbox. This was true before this
  change and is still true after it; employee-invitation email work is explicitly out
  of scope for this consolidation. Do not assume an employee email exists yet.
- The user's intended outcome is background delivery for all email flows, with adding a
  new ordinary email type requiring no new table, queue, dispatcher, or consumer. That
  is a requirement, not a claim that every flow is on the outbox yet.

## Current flow and ownership

One shared pipeline delivers every background-sent email kind (booking confirmation,
registration confirmation, and any future kind):

1. A feature service (`BookingServices.EnqueueConfirmationAsync`,
   `UsersService.EnqueueConfirmationEmailAsync`) **composes** the email - Subject and
   HtmlBody, using its own domain data (a decrypted patient name, a confirmation link) -
   and stages an `EmailOutboxMessage` row in the exact same database transaction as the
   business operation it belongs to (the booking insert; the Identity user + Patient
   row). The consumer never composes anything; composition is entirely the feature's
   responsibility, matching "feature-specific code decides what the email says, shared
   infrastructure decides how it gets delivered."
2. `EmailOutboxDispatcher` polls pending `EmailOutboxMessages` every five seconds, up to
   20 per batch, deserializes each row's `Payload` as an `EmailEnvelope`, and publishes
   through `RabbitMqEmailPublisher`. A row that has passed its (optional) `ExpiresAtUtc`
   before being dispatched is marked `ExpiredAtUtc` instead of published - it must never
   be recorded as sent.
3. `RabbitMqEmailPublisher` opens a channel with publisher confirmations and tracking
   enabled, publishes a persistent message with `mandatory: true`, and accounts for
   returned/unroutable messages. Only a successful publish allows the dispatcher to set
   `DispatchedAtUtc`.
4. `EmailDeliveryConsumer` consumes `email-delivery` with manual acknowledgements and
   prefetch 5. It re-checks `ExpiresAtUtc` at send time too (a message can sit in the
   queue through a broker/consumer outage), checks `SentEmailNotifications`, sends
   through `IEmailTransportSender` (implemented by `SmtpIdentityEmailSender`), records
   the notification ID, then acknowledges.
5. Malformed messages and exhausted retries go to `email-delivery.failed` through
   `email-delivery.dlx`, subject to the deployed broker policy.

`EmailEnvelope` (`NotificationId`, `Recipient`, `Subject`, `HtmlBody`,
`PlainTextBody?`, `CreatedAtUtc`, `ExpiresAtUtc?`, `Kind?`) is the one wire contract for
every kind. `Kind` is diagnostic-only (a structured-log/query property, also
denormalized onto `EmailOutboxMessage.Kind`) - the consumer never switches on it to
rebuild per-feature logic, and it must stay that way. `EmailOutboxMessage` and
`SentEmailNotification` carry no per-domain foreign key; `EmailOutboxMessage.Recipient`
is the shared, non-domain-specific field used to look up "was this email queued" (by
recipient, optionally combined with `Kind` when more than one email kind might target
the same recipient).

### What was removed in this consolidation

Superseded by the shared pipeline above and deleted outright (not left "just in case"):
`BookingOutboxMessage`/`RegistrationOutboxMessage`/`SentBookingNotification`/
`SentRegistrationNotification` (Domain entities + configurations), `BookingConfirmedEvent`/
`RegistrationConfirmationEvent` (wire records), `IBookingEventPublisher`/
`IRegistrationEventPublisher`/`IBookingNotificationSender`/`IRegistrationNotificationSender`
(contracts), `RabbitMqBookingEventPublisher`/`RabbitMqRegistrationEventPublisher`,
`BookingOutboxDispatcher`/`RegistrationOutboxDispatcher`,
`BookingConfirmationConsumer`/`RegistrationConfirmationConsumer` (plus every one of
those files' `LoggerExtensions` companions), and `SmtpIdentityEmailSender`'s
`SendBookingConfirmationAsync`/`SendRegistrationConfirmationAsync` methods (composition
moved to the feature services). `RabbitMqConnectionProvider` was already queue-agnostic
and needed no change - it's still the one piece genuinely reused as-is.

Booking's own legacy-message dedup-key derivation (`TryResolveDedupKey`/
`DeriveLegacyDedupKey`/`LegacyDedupNamespace`, needed because `booking-confirmed`
predated `NotificationId` by about two hours - see git history around commit `3d7bf53`)
was retired along with `booking-confirmed` itself. `email-delivery` is a brand-new
queue with no messages predating `NotificationId`, so no equivalent logic exists in
`EmailDeliveryConsumer` and none should be added preemptively.

### Migrating existing data and queued messages (2026-09-10 consolidation, one-time)

The old per-domain tables (`BookingOutboxMessages`, `RegistrationOutboxMessages`,
`SentBookingNotifications`, `SentRegistrationNotifications`) are **not dropped** by the
`ConsolidateEmailDelivery` migration - they are retained as inert historical/audit
data. Nothing in the codebase reads or writes them after this migration. The migration
copies every row from both old `Sent*Notifications` tables into the new
`SentEmailNotifications` (matched by `Id`, idempotent) so a legacy notification ID that
somehow gets replayed later is still recognized as already-sent. See
[the deployment runbook](../../docs/deployment.md#one-time-migration-cutover-from-the-old-per-domain-pipelines-this-deploy-only)
for the exact step sequence: block new writes at the VPS's `DOCKER-USER` iptables/ip6tables
chain (not a plain `ufw deny` - Docker's published-port traffic bypasses ufw's `INPUT`/`OUTPUT`
chains entirely, confirmed against Docker's own firewall docs), not by disabling the API's
Dokploy domain and not by stopping the container - for a Compose-deployed app, Dokploy only
applies a domain change on redeploy (confirmed against Dokploy's own docs; it is not
hot-reloaded the way a single-image Application's domain is), and a redeploy would both
rebuild the `api` container (killing the old dispatcher/consumer still draining it - they run
in-process inside the API, not as separate workers) and risk starting the new pipeline's code
before the migration/policy are ready. Then drain both old outboxes and confirm both
`messages_ready` **and** `messages_unacknowledged` are zero on both old queues (not
`messages_ready` alone - a message can be delivered-but-unacked to an old consumer and still
not show up there), replay anything worth keeping in the old failed queues, repeat the
drain/queue checks once more immediately before stopping the old API, then run the migration
and apply the new broker policy with no API instance running at all before starting the new
one - the only way to guarantee `EmailOutboxDispatcher`/`EmailDeliveryConsumer` never run
against a not-yet-migrated schema or not-yet-policied queue, since nothing in `Program.cs`
gates their startup on either. Because Dokploy's Deploy button always fetches code and starts
`api` together with no way to decouple them, and because Dokploy generates that deploy's
Traefik labels itself (a manual `git reset --hard` plus a raw `docker compose up` bypasses that
generation entirely - see [deployment knowledge](aspnet-dokploy-deployment.md) for the
confirmation), both the migration step and the final API start go through Dokploy's own Deploy
button, temporarily swapping its Compose Advanced-tab Custom Command for the migrator-only step.
Dokploy also has no way to pin an exact commit SHA, so the runbook disables Dokploy's Auto
Deploy toggle for the cutover's duration instead. A documented maintenance-window cutover was
chosen over a dual-running compatibility shim, since this is a solo, low-volume application and
rolling-upgrade support would be meaningfully more complex than the traffic justifies. Do not
reintroduce that complexity without a concrete need.

### Contributor example: adding a new ordinary email type

Adding, say, a "booking rescheduled" email should look like this and nothing more:

```csharp
// In the feature service, alongside its own business operation, inside its own
// existing transaction:
private static (string Subject, string HtmlBody) ComposeRescheduledEmail(GetBookingDto dto) =>
    (
        $"Booking rescheduled - {dto.BookingNumber}",
        $"""Hi {WebUtility.HtmlEncode(dto.PatientFullName)}, your booking has moved to a new time."""
    );

private async Task EnqueueRescheduledEmailAsync(string patientEmail, GetBookingDto dto, CancellationToken ct)
{
    (string subject, string htmlBody) = ComposeRescheduledEmail(dto);
    EmailEnvelope evt = new(
        Guid.CreateVersion7(), patientEmail, subject, htmlBody,
        PlainTextBody: null, clock.GetUtcNow(), ExpiresAtUtc: null, Kind: "BookingRescheduled"
    );

    await patientBookingDbContext.AddAsync(new EmailOutboxMessage
    {
        Id = evt.NotificationId,
        Recipient = patientEmail,
        Kind = evt.Kind,
        Payload = JsonSerializer.Serialize(evt),
        CreatedAtUtc = clock.GetUtcNow(),
    }, ct);
}
```

Then add a focused test proving the new composition (readable names, correct HTML
encoding, staged in the same transaction) - the shared transport/dedup/retry/dead-letter
behavior is already covered by `EmailDeliveryConsumerTests` and needs no new coverage
per email type. No new table, migration, dispatcher, publisher, consumer, or queue
policy is needed.

Source locations (relative to the repository root):

- `PatientBooking.Api.Application/Services/BookingServices.cs`
- `PatientBooking.Api.Application/Services/UsersService.cs`
- `PatientBooking.Api.Application/Services/SmtpIdentityEmailSender.cs`
- `PatientBooking.Api.Application/Messaging/EmailEnvelope.cs`
- `PatientBooking.Api.Application/Messaging/RabbitMqEmailPublisher.cs`
- `PatientBooking.Api.Application/Messaging/RabbitMqConnectionProvider.cs`
- `PatientBooking.Api.Application/Contracts/IEmailEventPublisher.cs`
- `PatientBooking.Api.Application/Contracts/IEmailTransportSender.cs`
- `PatientBooking.Api/BackgroundServices/EmailOutboxDispatcher.cs`
- `PatientBooking.Api/BackgroundServices/EmailDeliveryConsumer.cs`
- `PatientBooking.Api.Domain/EmailOutboxMessage.cs`,
  `PatientBooking.Api.Domain/SentEmailNotification.cs`
- `PatientBooking.Api.Domain/Migrations/20260910135147_ConsolidateEmailDelivery.cs`
- `PatientBooking.Api.Tests/EmailDeliveryConsumerTests.cs`
- `PatientBooking.Api.Tests/PatientRegistrationEndpointTests.cs`
- `PatientBooking.Api.Tests/BookingConfirmationEmailEndpointTests.cs`
- `PatientBooking.Api.Tests/Infrastructure/MessagingTestFixture.cs`
- `PatientBooking.Api.Tests/Infrastructure/BookingEndpointTestFixture.cs`

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
| An envelope expires before it is sent | Check `ExpiresAtUtc` both at dispatch time (dispatcher) and at send time (consumer) - a message can sit queued through an outage. Mark it abandoned/ack it without ever recording `SentEmailNotifications`. Never invent an unrelated timeout; align it to the actual token/data lifetime that made expiry meaningful in the first place. |
| Required payload fields are missing | Reject without requeue; retries cannot repair malformed input. |
| Broker cancels a consumer without disconnecting | `UnregisteredAsync` wakes the outer loop to redeclare topology and consume again. Connection recovery alone does not cover every cancellation scenario. |
| Broker is unavailable during booking or registration | Commit the outbox with the business operation; let the dispatcher retry. Do not restore a synchronous broker dependency to either request. |
| Consolidating two pipelines into one | Do not silently drop the old tables/queues' data. Copy dedup records forward; retain old tables as inert history; require an explicit drain-and-verify preflight before cutover instead of inventing dual-running compatibility code this app's scale doesn't need. |

For RabbitMQ 4.3 behavior, explicit `basic.nack(requeue: true)` does not increment the
quorum delivery counter. The current retry path uses `BasicRejectAsync(requeue: true)`
after a one-second delay. Do not replace it with nack and assume the delivery limit
still bounds attempts. `basic.nack(requeue: false)` remains appropriate for immediate
dead-lettering of malformed input. Verify semantics against the actual broker version:
[quorum poison-message handling](https://www.rabbitmq.com/docs/quorum-queues#poison-message-handling).

The earlier local verification reported RabbitMQ 4.3.4. `docker-compose.yml`,
`docker/dev.compose.yaml`, and `MessagingTestFixture.cs` pin `rabbitmq:4.3.4-management-alpine`
instead of the floating `4-management-alpine` tag, after that floating tag was observed
moving to 4.3.5 mid-development of an earlier test suite. Bump the pin in all three
places together, deliberately, rather than re-pulling the floating tag.

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
  A bounded, non-destructive replay procedure is documented in the deployment runbook:
  peek with requeue enabled, republish, confirm the `SentEmailNotifications` row by
  `notificationId` before removing the original. It has not been exercised against a
  real failed message on a live deployment. Preserve IDs when replaying and acknowledge
  the possibility of duplicate email after an ambiguous send.
- Review coordination before scaling API replicas: each instance hosts workers, and
  the current dispatcher does not claim rows with a distributed lease.

## Sensitive content and retention

- Never log confirmation tokens, full email bodies, or full envelope payloads. The
  consumer's log statements carry only `NotificationId` and, on failure, the caught
  exception - never `Recipient`, `Subject`, or `HtmlBody`.
- Never include a password in `EmailEnvelope` or any composed email.
- `EmailOutboxMessage.Payload` persists the full rendered HTML (including, for
  registration, the confirmation link) until dispatched; it is not purged after
  dispatch today. This matches the prior per-domain outboxes' behavior - no new
  retention policy was introduced or removed by this consolidation. If retention
  becomes a requirement, treat it as its own scoped follow-up rather than folding it
  into a future unrelated change.
- Registration's confirmation-link expiry (`EmailEnvelope.ExpiresAtUtc`) is aligned to
  ASP.NET Core Identity's default `DataProtectionTokenProviderOptions.TokenLifespan`
  (1 day) via `UsersService.ConfirmationLinkLifespan` - not an arbitrary timeout. If
  that Identity default is ever configured explicitly in `Program.cs`, update the
  constant to match, or the queued email's expiry and the token's actual validity will
  drift apart.

## Deployment decisions

Use the [deployment runbook](../../docs/deployment.md#rabbitmq) for exact commands.
Keep command maintenance there rather than copying a second runsheet into this file.

- The queue is a durable quorum queue. Mutable retry/DLX settings are broker policy,
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

## Broker accounts and Dokploy credentials

- These accounts belong to the RabbitMQ broker on the VPS, not rabbitmq.com, Dokploy,
  or ASP.NET Identity. Generate a private password; there is no website signup.
- On 2026-09-10, the user reported `rabbitmqctl list_users` showing only
  `guest [administrator]`. Deployment logs also reported missing `RABBITMQ_USER` and
  `RABBITMQ_PASSWORD`. Account creation and successful API authentication were advised,
  but have not yet been confirmed by the user.
- Dokploy's Compose Environment supplies `RABBITMQ_USER`/`RABBITMQ_PASSWORD` to both
  broker default-user settings and API connection settings. Missing-variable warnings
  mean credentials need attention even when the image builds successfully.
- Default-user settings seed an empty broker. Changing them on an existing persisted
  broker does not create an account or rotate its password. Inspect users first; create
  the missing application user and grant permissions on `/` using the runbook below.
  Keep its password identical to the Dokploy value, then redeploy the API.
- Accounts and permissions persist in `rabbitmq-data`. Do not delete that volume to
  repair credentials: it also holds broker data. Rotate an existing user's password
  explicitly and update Dokploy together.
- The application account needs configure/write/read permissions for its topology,
  not an administrator tag. Untagged users cannot sign into the management UI.
  Use a separate operator account with the appropriate management tag and vhost
  permissions. An SSH tunnel provides connectivity, not authentication or permission.
- Do not use `guest` for the API or disable its default loopback restriction to make
  container-to-container login work. Never store actual passwords in knowledge files,
  source control, or chat.

Sources: [RabbitMQ access control](https://www.rabbitmq.com/docs/access-control),
[management permissions](https://www.rabbitmq.com/docs/management#permissions).
Commands: [existing-broker account setup](../../docs/deployment.md#existing-broker-account-setup).

## Verification lessons and remaining checks

Local test coverage after the 2026-09-10 consolidation: one shared-infrastructure suite
(`EmailDeliveryConsumerTests`) covering outbox dispatch, duplicate redelivery, recovery
after a short SQL outage with five messages, duplicate-key exception classification,
missing-field dead-lettering, permanent SMTP-failure dead-lettering, dead-letter
destination outage/recovery, and envelope expiry - exercised once, generically, rather
than once per email kind. Feature-specific composition (readable/encoded names, correct
subject lines, registration link semantics and expiry, no-SMTP-in-request timing) is
covered separately by `PatientRegistrationEndpointTests` and
`BookingConfirmationEmailEndpointTests`. Booking's prior legacy-message dedup test has
no equivalent here - `email-delivery` is a brand-new queue with no pre-`NotificationId`
messages, so there is nothing to reproduce.

**Root cause found and fixed - and the first fix attempt was wrong, corrected here rather
than left standing:** the previously-flaky "dead-letter destination unavailable then
recovers" test was first suspected to be racing RabbitMQ's own policy-application
latency (`PUT /api/policies/...` returning success before the broker attaches the policy
to the target queue). A fix polling the queue's `effective_policy_definition` until it
reflected the blocking policy was written and *believed* to resolve it, but retesting
showed the same failure with the policy confirmed attached - so that hypothesis was
wrong, not merely incomplete.

The actual root cause is a genuine, currently-unfixed RabbitMQ server behavior: the
internal publisher used for at-least-once dead-lettering ignores `overflow:
reject-publish` on the *target* queue and keeps enqueueing past its `max-length` limit -
confirmed by RabbitMQ's own issue tracker
(https://github.com/rabbitmq/rabbitmq-server/issues/8495, opened 2023, still open at time
of writing). The quorum-queues doc page's own prose ("dead-lettered messages have to
contribute to the queue resource limits...so the queue can refuse to accept more
messages") describes the *intended* behavior, which does not match what the broker
actually does for this delivery path. This is a broker limitation, not an application bug
and not a test-synchronization bug - `EmailDeliveryConsumer` needed no changes.

The fix: stop trying to block the destination queue via an overflow policy at all.
`MessagingTestFixture.BlockFailedQueueAsync` now deletes the `email-delivery.failed`
queue outright, which removes the DLX route entirely - the documented mechanism for an
unavailable dead-letter target
(https://www.rabbitmq.com/blog/2022/03/29/at-least-once-dead-lettering): a message with
no route is retained by the source queue in a "neither ready nor unacknowledged" state
and retried by an internal dead-letter consumer process on a fixed schedule, currently
every 3 minutes, which is not exposed as a policy setting. `UnblockFailedQueueAsync`
redeclares (and rebinds) the queue to restore the route. Because that retry interval is
real and not configurable, the recovery half of this test now waits up to 4 minutes
instead of 30 seconds - a widened timeout, but one backed by a cited broker interval and
a corrected blocking mechanism, not a blind pad over an unexplained flake.

Reported harness lessons: under the selected xUnit v3/Microsoft.Testing.Platform runner,
`dotnet test` fails outright on the .NET 10 SDK ("Testing with VSTest target is no
longer supported"); build the test project and run the produced `.exe` directly, with
`--filter-query "/*/*/<ClassName>/*"` to scope to one class (the filter-query language
does not accept `|` for OR - run one filtered invocation per class, or omit the filter
for the full suite). `xunit.runner.json` already enforces sequential execution
project-wide. The fixture also applies exact recipient matching after smtp4dev's
substring-based `deliveredTo` filter. Preserve that check to avoid false
duplicate-email failures.

Still unverified by the reported suite:

1. SQL outage long enough to exhaust retries is confirmed by source inspection (no
   hosted service reads `email-delivery.failed`) and its replay procedure is
   documented, but the procedure itself has not been exercised against a real failed
   message.
2. Genuine concurrent duplicate-key race through the complete send/persist pipeline.
3. Effective policy, port binding, migrations, and notification delivery on the VPS.
4. The migration's data-carry-forward SQL (`INSERT ... SELECT ... WHERE NOT EXISTS`) is
   now covered by a dedicated test, `EmailDeliveryMigrationCompatibilityTests` (its own
   fresh SQL Server container, migrated only up to the pre-consolidation schema, then
   deliberately populated with rows in all four old tables - including one `Id` present
   in both old dedup tables at once, to prove the `WHERE NOT EXISTS` guard skips it on
   the second insert rather than throwing a duplicate-key violation - before applying
   `ConsolidateEmailDelivery` and re-running the carry-forward SQL a second time to prove
   the re-run safety claim). Passed locally. Still not verified against an actual VPS
   database/backup - the populated rows are deliberately constructed, not a real
   production data shape.

Keep tests isolated from normal development data and real recipients. When PostgreSQL
replaces SQL Server, preserve the delivery design but replace provider-specific error
classification, migrations, and test infrastructure; SQL Server error numbers are not
portable. Avoid introducing a messaging framework or rewriting this working flow solely
to capture these lessons.
