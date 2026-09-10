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

An existing non-admin account with that email, a different existing admin, or multiple
admins causes startup to fail. No existing user is silently promoted, deleted, or demoted.
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

Then browse `http://localhost:15672` and sign in with `RABBITMQ_USER`/`RABBITMQ_PASSWORD`.

The API starts and keeps serving bookings even if the broker is down or not yet deployed -
confirmation emails queue up in the database outbox and get delivered once the broker's reachable.
Nothing needs the broker to be healthy before the API starts.

### Retry/dead-letter policy (one-time, per broker)

The `booking-confirmed` queue's redelivery limit and dead-letter routing are applied as a broker
**policy**, not as queue arguments - policies attach to an existing queue without redeclaring it,
so there's nothing to conflict with if the queue already exists. This only needs to be run once per
broker (it's stored with the broker's own metadata, alongside `rabbitmq-data`, and survives
restarts/redeploys); a fresh broker with an empty volume needs it run again. From the `rabbitmq`
container's terminal:

```sh
rabbitmqctl set_policy booking-confirmed-retry-limit "^booking-confirmed$" \
  '{"delivery-limit":3,"dead-letter-exchange":"booking-confirmed.dlx","dead-letter-routing-key":"booking-confirmed.failed","dead-letter-strategy":"at-least-once","overflow":"reject-publish"}' \
  --apply-to queues
```

`dead-letter-strategy: at-least-once` (quorum queues only) keeps a dead-lettered message in
`booking-confirmed` until the broker confirms `booking-confirmed.failed` actually accepted it,
retrying periodically if the failed queue is temporarily unreachable, instead of the default
`at-most-once` behavior where a message can be lost during the hand-off. This mode requires
`overflow: reject-publish` - the default `drop-head` overflow strategy silently defeats
`at-least-once` even without a queue length limit set - and the `stream_queue` feature flag, which
is enabled by default on a fresh `rabbitmq:4-management-alpine` broker (confirm with
`rabbitmqctl list_feature_flags | grep stream_queue` if in doubt). See
[quorum queue dead-lettering](https://www.rabbitmq.com/docs/quorum-queues#dead-lettering).

Consider also setting `max-length` or `max-length-bytes` in the same policy body if
`booking-confirmed.failed` is ever unreachable for an extended period - RabbitMQ recommends this to
bound how much `booking-confirmed` can retain while retrying delivery to it. Not set here; pick a
value against your actual message volume before adding it.

Verify with `rabbitmqctl list_policies`, or per-queue via `rabbitmqctl list_queues name policy` or
the management UI's queue detail page ("Effective policy definition"). To change an existing
broker's policy later, re-run `set_policy` with the same name and the new JSON body - it replaces
the definition in place; no queue redeclaration or restart is needed, and Compose's `--build`
redeploys never touch this since it lives in the broker's own persisted metadata, not the image.

Messages that exhaust their 3 delivery attempts, or that fail to deserialize at all, land in the
durable `booking-confirmed.failed` queue (declared automatically by the app, same as the main
queue) with full `x-death` history for diagnosis - see
[RabbitMQ's dead-lettering docs](https://www.rabbitmq.com/docs/dlx) and
[quorum queue delivery limits](https://www.rabbitmq.com/docs/quorum-queues#poison-message-handling).

### Recovery from `booking-confirmed.failed` is manual, not automatic

If SQL Server stays unavailable long enough for a delivery to exhaust its 3 attempts (each retry
waits 1 second plus reject/redeliver latency, so this takes on the order of seconds, not minutes),
the confirmation lands in `booking-confirmed.failed` even though the underlying cause was transient
and would have succeeded on a later retry. Nothing consumes `booking-confirmed.failed` - `Program.cs`
registers exactly two hosted services, `BookingConfirmationConsumer` (reads `booking-confirmed` only)
and `BookingOutboxDispatcher` - so a message that lands there stays there until someone replays it by
hand.

The procedure below never removes a message from `booking-confirmed.failed` until its replay is
confirmed to have worked. Get Message(s) with Ack Mode "Nack message requeue false" deletes on read
- using that as the first step, before you know the republish succeeded, means a failed republish
loses the message for good. Never use the queue's **Purge Messages** action for this: it discards
every message in the queue with no replay at all.

To replay a message once the underlying problem (SQL Server, SMTP, etc.) is fixed, from the
management UI (`http://localhost:15672` through the SSH tunnel above):

1. Open the `booking-confirmed.failed` queue's page and use **Get Message(s)** with **Ack Mode**
   set to "Nack message requeue true" - a non-destructive peek. The payload is shown and the
   message is immediately put back on the queue, so nothing is lost if you stop here.
2. Copy the **Payload** value exactly, and separately note its `notificationId` field. That field
   is the exact value `HandleDeliveryAsync` writes as `SentBookingNotifications.Id`, so it is what
   step 4 checks for and what step 5 uses to identify the message safely.
3. Open the `booking-confirmed` queue's page and use **Publish message**, leaving **Exchange**
   blank (the default exchange) and setting **Routing key** to `booking-confirmed`, then paste the
   unmodified payload back in and publish.
4. Confirm the republish actually worked before touching the failed queue: poll
   `SentBookingNotifications` for a row whose `Id` equals the `notificationId` from step 2 (or
   confirm the email itself arrived). Do not proceed to step 5 on a hunch - until that row appears,
   the message is still safe in `booking-confirmed.failed` precisely because you haven't removed it.
5. Only now remove the original. Use **Get Message(s)** with Ack Mode "Nack message requeue true"
   once more to confirm the message currently at the head of the queue is still the one whose
   `notificationId` you just confirmed processed - a different failure may have arrived at the head
   in the meantime. If it matches, repeat **Get Message(s)**, this time with Ack Mode "Nack message
   requeue false", to remove just that one message. If it doesn't match, leave it alone and handle
   whichever message actually failed on its own turn instead of removing the wrong one.

This is a manual, one-message-at-a-time procedure appropriate for this project's volume - it does
not use the Shovel plugin or `rabbitmqadmin` scripting, since neither is otherwise part of this
deployment. Redelivering through the same dedup path is safe even if the original send actually did
go through before failing later in the pipeline: `HandleDeliveryAsync`'s `SentBookingNotifications`
check skips sending again, so a message that turns out to already be a duplicate produces no second
email even after being replayed.
