# Manual database migrations

## Initial admin account

In Dokploy's Compose **Environment** editor, supply these private values before deploying:

```dotenv
ADMIN_SEED_EMAIL=your-unused-admin-email@example.com
ADMIN_SEED_PASSWORD='replace-with-a-strong-unique-password'
ADMIN_SEED_FIRST_NAME=YourFirstName
ADMIN_SEED_LAST_NAME=YourLastName
```

Do not register this email through the public API first. Startup creates the Identity
account with a hashed password, confirmed email, patient profile/MRN, and admin profile/number
in one transaction. It sends no confirmation email. The configured password must satisfy
the application's Identity password policy. Keep real values out of Git and logs.

Deploy the commit containing the Compose mappings. Ensure database migrations have been
applied first; the recovery migrator skips admin bootstrap if the API cannot start.
Log in through `/api/Auth/login` using the configured credentials after deployment.

Later startups leave the same admin and password unchanged. Remove `ADMIN_SEED_PASSWORD`
from Dokploy after successful creation and redeploy to remove it from the container environment;
retain the email to check the admin identity on subsequent startups. Password changes use
the account's password-reset flow, not deployment settings.

An existing non-admin account with that email logs an error and skips admin creation;
the API still starts. Use an unused email and redeploy to create the admin.
A different existing admin or multiple admins still causes startup to fail.
No existing user is silently promoted, deleted, or demoted.
An empty admin email disables bootstrap. Existing volumes and account records are preserved.

## Apply migrations

Deploy the latest code through Dokploy, open the **api** container terminal (working
directory `/app`), and run:

```sh
dotnet PatientBooking.Api.dll --migrate
```

No VPS SSH session, Compose project name, or `.env` path is needed in that terminal.
The command inherits the API container's environment, including the database connection
string and protection keys. It applies pending migrations included in the deployed image,
then exits without starting another web server or running the admin-seed block.
Normal deployment and API startup do not apply migrations automatically.

Success prints `Database migrations completed successfully.` Failure logs the exception
and exits with code 1. Running it again applies only migrations still pending. With the
current SQL Server `sa` connection it can also create the missing `PatientBookingDb`.
Check `/api/clinic` after the command succeeds.

Create new migrations locally using EF tooling and commit them before deployment. This
command only applies migrations; it does not generate them or support rollback targets.
Review schema changes before running them, and avoid serving requests during changes
incompatible with the running API.

If the API cannot stay running (for example, configured admin seeding needs the missing
database), use the optional `migrator` Compose service from the deployment directory with
the same project and environment as Dokploy. It runs the same manual command. Redeploy
or restart the API after that succeeds.

The command uses EF Core's [MigrateAsync API](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying#apply-migrations-at-runtime).
The standard [dotnet ef CLI](https://learn.microsoft.com/en-us/ef/core/cli/dotnet)
requires SDK/tooling and project files that are not included in this runtime image.

## RabbitMQ

Supply `RABBITMQ_USER` and `RABBITMQ_PASSWORD` in Dokploy's Compose **Environment** editor before
first deploy - Compose maps them straight to the broker's `RABBITMQ_DEFAULT_USER`/`RABBITMQ_DEFAULT_PASS`
and to the API's `RabbitMq__UserName`/`RabbitMq__Password`. AMQP (5672) has no host mapping, same as
SQL Server - the API reaches the broker over the internal Compose network by service name and never
needs a published port. The management UI (15672) is published loopback-only
(`127.0.0.1:15672:15672`) so it's reachable through an SSH tunnel but never from the public
interface - binding to `127.0.0.1` rather than `0.0.0.0` is what keeps it non-public even though a
port is technically published. For ad hoc inspection of queues, SSH-tunnel instead of opening the
port publicly:

```sh
ssh -L 15672:localhost:15672 <user>@<vps-host>
```

Then browse `http://localhost:15672` and sign in with a broker operator account that has
a management tag and the required vhost permissions. An untagged application account
cannot log into this UI, even when its API connection works. See
[management permissions](https://www.rabbitmq.com/docs/management#permissions).

### Existing-broker account setup

Changing default-user environment variables does not update users in an existing broker volume.
If only `guest` exists, run these commands in Dokploy's **RabbitMQ container terminal**:

```sh
rabbitmqctl list_users
rabbitmqctl add_user patientbooking 'REPLACE_WITH_GENERATED_PASSWORD'
rabbitmqctl set_permissions -p / patientbooking ".*" ".*" ".*"
```

Use a long, randomly generated alphanumeric password to avoid shell quoting mistakes.
Run `add_user` only if that user is missing. This creates an application account without
management privileges, with configure/write/read access to all resources in `/`.

Set the matching values in Dokploy's Compose **Environment** editor:

```dotenv
RABBITMQ_USER=patientbooking
RABBITMQ_PASSWORD='REPLACE_WITH_THE_SAME_PASSWORD'
```

Save and redeploy. Check API logs for successful broker connectivity and verify a booking
notification. Account setup is one-time while `rabbitmq-data` persists; do not delete the
volume to fix credentials. Never paste the real password into committed documentation.
See [RabbitMQ user management](https://www.rabbitmq.com/docs/access-control#user-management).

The API starts and keeps serving bookings and patient registrations even if the broker is down or
not yet deployed - every kind of background email (booking confirmation, registration confirmation,
and any future kind) queues up in one shared database outbox (`EmailOutboxMessages`) and gets
delivered once the broker's reachable. Nothing needs the broker to be healthy before the API starts.

### One shared pipeline for every email kind

As of the 2026-09-10 consolidation, booking confirmation and patient-registration confirmation share
a single delivery pipeline instead of two separate ones: one outbox table
(`EmailOutboxMessages`), one dedup table (`SentEmailNotifications`), one queue (`email-delivery`),
one failed queue (`email-delivery.failed`), one dispatcher (`EmailOutboxDispatcher`), and one
consumer (`EmailDeliveryConsumer`). `BookingServices`/`UsersService` each compose their own
Subject/HtmlBody before staging a row - the shared consumer never inspects what kind of email it's
sending, so adding another ordinary email type does not require another table, queue, dispatcher, or
consumer. See `.claude/knowledge/rabbitmq-email-delivery.md` for the contributor walkthrough of
adding a new email type.

### Retry/dead-letter policy (one-time, per broker)

The `email-delivery` queue's redelivery limit and dead-letter routing are applied as a broker
**policy**, not as queue arguments - policies attach to an existing queue without redeclaring it,
so there's nothing to conflict with if the queue already exists. This only needs to be run once per
broker (it's stored with the broker's own metadata, alongside `rabbitmq-data`, and survives
restarts/redeploys); a fresh broker with an empty volume needs it run again. From the `rabbitmq`
container's terminal:

```sh
rabbitmqctl set_policy email-delivery-retry-limit "^email-delivery$" \
  '{"delivery-limit":3,"dead-letter-exchange":"email-delivery.dlx","dead-letter-routing-key":"email-delivery.failed","dead-letter-strategy":"at-least-once","overflow":"reject-publish"}' \
  --apply-to queues
```

`dead-letter-strategy: at-least-once` (quorum queues only) keeps a dead-lettered message in
`email-delivery` until the broker confirms `email-delivery.failed` actually accepted it,
retrying periodically if the failed queue is temporarily unreachable, instead of the default
`at-most-once` behavior where a message can be lost during the hand-off. This mode requires
`overflow: reject-publish` - the default `drop-head` overflow strategy silently defeats
`at-least-once` even without a queue length limit set - and the `stream_queue` feature flag, which
is enabled by default on a fresh `rabbitmq:4-management-alpine` broker (confirm with
`rabbitmqctl list_feature_flags | grep stream_queue` if in doubt). See
[quorum queue dead-lettering](https://www.rabbitmq.com/docs/quorum-queues#dead-lettering).

Consider also setting `max-length` or `max-length-bytes` in the same policy body if
`email-delivery.failed` is ever unreachable for an extended period - RabbitMQ recommends this to
bound how much `email-delivery` can retain while retrying delivery to it. Not set here; pick a
value against your actual message volume before adding it.

Verify with `rabbitmqctl list_policies`, or per-queue via `rabbitmqctl list_queues name policy` or
the management UI's queue detail page ("Effective policy definition"). To change an existing
broker's policy later, re-run `set_policy` with the same name and the new JSON body - it replaces
the definition in place; no queue redeclaration or restart is needed, and Compose's `--build`
redeploys never touch this since it lives in the broker's own persisted metadata, not the image.

Messages that exhaust their 3 delivery attempts, or that fail to deserialize at all, land in the
durable `email-delivery.failed` queue (declared automatically by the app, same as the main
queue) with full `x-death` history for diagnosis - see
[RabbitMQ's dead-lettering docs](https://www.rabbitmq.com/docs/dlx) and
[quorum queue delivery limits](https://www.rabbitmq.com/docs/quorum-queues#poison-message-handling).

### One-time migration cutover from the old per-domain pipelines (this deploy only)

This deploy replaces the previous two separate pipelines (`booking-confirmed` /
`registration-confirmation`, each with their own outbox and dedup table) with the single
`email-delivery` pipeline above. Booking and registration confirmation used to each run their own
consumer/dispatcher; this deploy retires both of those in favor of the shared ones. Because two
consumers cannot safely run against the same in-flight work, and because this is a solo, low-volume
application, the chosen transition is a **documented maintenance-window cutover**, not a
dual-running compatibility shim (rolling upgrade support would be meaningfully more complex than
this app's traffic justifies).

**Before deploying this version**, on the currently-running (old) deployment:

1. Confirm both old outboxes have drained to zero pending rows (they normally do within seconds,
   since `BookingOutboxDispatcher`/`RegistrationOutboxDispatcher` poll every 5s):
   ```sql
   SELECT COUNT(*) FROM BookingOutboxMessages WHERE DispatchedAtUtc IS NULL;
   SELECT COUNT(*) FROM RegistrationOutboxMessages WHERE DispatchedAtUtc IS NULL;
   ```
   If either is non-zero, wait and recheck rather than proceeding - do not deploy while a row could
   still be in flight, since nothing will dispatch it after this deploy replaces those dispatchers.
2. Confirm both old queues show 0 **ready** messages in the management UI (or
   `rabbitmqctl list_queues name messages_ready` filtered to `booking-confirmed` and
   `registration-confirmation`). Under normal operation this is also near-instant, since the old
   consumers process messages continuously.
3. If either old **failed** queue (`booking-confirmed.failed` / `registration-confirmation.failed`)
   holds messages you still intend to recover, replay them now using the existing procedure this
   section used to document (peek non-destructively, republish to the *old* main queue, confirm the
   old dedup table gained the row, only then remove the original) - **before** deploying, while the
   old consumers are still running. After this deploy, nothing consumes `booking-confirmed` or
   `registration-confirmation` any more, so a message republished to either old queue after this
   point will not be delivered.

**Then deploy this version and apply the migration** (`dotnet PatientBooking.Api.dll --migrate`, per
the "Apply migrations" section above). The migration:

- Creates `EmailOutboxMessages` and `SentEmailNotifications`.
- Copies every row from `SentBookingNotifications` and `SentRegistrationNotifications` into
  `SentEmailNotifications` (matched by `Id`, safe to re-run) - so a legacy notification ID that
  somehow gets replayed later is still recognized as already-sent and skipped, not resent.
- Does **not** drop `BookingOutboxMessages`, `RegistrationOutboxMessages`,
  `SentBookingNotifications`, or `SentRegistrationNotifications`. They are retained as an inert
  historical/audit record - nothing in the codebase reads or writes them after this migration. This
  is deliberate: even if the preflight checks above are skipped, no data is silently destroyed. A
  future migration may drop these four tables once you have confirmed on the actual deployment
  target that they are empty and no longer needed for audit - that is a follow-up you choose to make,
  not something this migration does automatically.

**Apply the new broker policy** from the "Retry/dead-letter policy" section above
(`email-delivery-retry-limit`). The old `booking-confirmed-retry-limit` /
`registration-confirmation-retry-limit` policies can be left in place harmlessly (they apply to
queues nothing publishes to any more) or removed with `rabbitmqctl clear_policy`; neither is required
for the new pipeline to work.

### Recovery from `email-delivery.failed` is manual, not automatic

If SQL Server stays unavailable long enough for a delivery to exhaust its 3 attempts (each retry
waits 1 second plus reject/redeliver latency, so this takes on the order of seconds, not minutes),
the confirmation lands in the failed queue even though the underlying cause was transient and would
have succeeded on a later retry. Nothing consumes the failed queue - `Program.cs` registers two
hosted services for email delivery, `EmailDeliveryConsumer` (reads `email-delivery` only) and
`EmailOutboxDispatcher` - so a message that lands in `email-delivery.failed` stays there until
someone replays it by hand.

The procedure below never removes a message from `email-delivery.failed` until its replay is
confirmed to have worked. Get Message(s) with Ack Mode "Nack message requeue false" deletes on read
- using that as the first step, before you know the republish succeeded, means a failed republish
loses the message for good. Never use the queue's **Purge Messages** action for this: it discards
every message in the queue with no replay at all.

To replay a message once the underlying problem (SQL Server, SMTP, etc.) is fixed, from the
management UI (`http://localhost:15672` through the SSH tunnel above):

1. Open the `email-delivery.failed` queue's page and use **Get Message(s)** with **Ack Mode**
   set to "Nack message requeue true" - a non-destructive peek. The payload is shown and the
   message is immediately put back on the queue, so nothing is lost if you stop here.
2. Copy the **Payload** value exactly, and separately note its `notificationId` field. That field
   is the exact value `HandleDeliveryAsync` writes as `SentEmailNotifications.Id`, so it is what
   step 4 checks for and what step 5 uses to identify the message safely.
3. Open the `email-delivery` queue's page and use **Publish message**, leaving **Exchange**
   blank (the default exchange) and setting **Routing key** to `email-delivery`, then paste the
   unmodified payload back in and publish.
4. Confirm the republish actually worked before touching the failed queue: poll
   `SentEmailNotifications` for a row whose `Id` equals the `notificationId` from step 2 (or
   confirm the email itself arrived). Do not proceed to step 5 on a hunch - until that row appears,
   the message is still safe in `email-delivery.failed` precisely because you haven't removed it.
5. Only now remove the original. Use **Get Message(s)** with Ack Mode "Nack message requeue true"
   once more to confirm the message currently at the head of the queue is still the one whose
   `notificationId` you just confirmed processed - a different failure may have arrived at the head
   in the meantime. If it matches, repeat **Get Message(s)**, this time with Ack Mode "Nack message
   requeue false", to remove just that one message. If it doesn't match, leave it alone and handle
   whichever message actually failed on its own turn instead of removing the wrong one.

This is a manual, one-message-at-a-time procedure appropriate for this project's volume - it does
not use the Shovel plugin or `rabbitmqadmin` scripting, since neither is otherwise part of this
deployment. Redelivering through the same dedup path is safe even if the original send actually did
go through before failing later in the pipeline: `HandleDeliveryAsync`'s `SentEmailNotifications`
check skips sending again, so a message that turns out to already be a duplicate produces no second
email even after being replayed.
