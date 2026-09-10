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

### Locating the Dokploy project directory (VPS SSH)

The `migrator` service needs the real Compose project name and the directory holding
`docker-compose.yml` and `.env` - guessing these (for example, assuming they sit in the
SSH user's home directory) produced `couldn't find env file: /home/jpcvillalon/.env` on
2026-09-08. Dokploy's on-disk layout is undocumented and version-dependent, so discover
these values from Docker's own Compose metadata instead of guessing a path:

```sh
docker ps --filter "name=api" --format '{{.Names}}\t{{.Image}}\t{{.Status}}'
```
Expected: one row for the running API container (e.g. `patientbooking-api-qns9o0-api-1`).
Substitute its name for `<api-container>` below.

```sh
docker inspect <api-container> --format \
  'project={{ index .Config.Labels "com.docker.compose.project" }}
working_dir={{ index .Config.Labels "com.docker.compose.project.working_dir" }}
config_files={{ index .Config.Labels "com.docker.compose.project.config_files" }}'
```
Expected: three non-empty lines. `project` is the exact value for `-p`. `working_dir` is
the directory containing `docker-compose.yml` and Dokploy's generated `.env` (Dokploy
writes `.env` next to the compose file it runs). `config_files` confirms the compose
file's exact name/path. These are standard Compose-assigned container labels, not a
Dokploy-specific convention, so this keeps working across Dokploy versions.

Verify both required files actually exist before using them:
```sh
cd <working_dir>
ls -la docker-compose.yml .env
```
Expected: both listed with a non-zero size. If `.env` is missing or empty, stop - do not
run the migrator, since it would start without the database password/JWT/lookup-protection
keys and either fail immediately or, worse, resolve them from the wrong values. Do not
print the file's contents; `ls -la` confirms existence and size without exposing secrets.

Every VPS-SSH command in this document that references `<project-name>`, `<working_dir>`,
or `<compose-file>` assumes this discovery has been run once for the current session.

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

This is a one-time consolidation procedure, not the shape of a routine future deploy - a normal
code-only or schema-compatible release still just goes through Dokploy's **Deploy** button per
"For later code-only deployments" above. Steps 6-8 below deliberately bypass that button only
because this specific cutover needs the old API stopped, the new migration and broker policy
applied, and only then the new API started, in that exact order, and Dokploy's Deploy button
cannot express that ordering for a Compose app (see step 7).

**Terminal labels used below:** **local PowerShell** (your own machine, outside the VPS),
**VPS SSH** (an SSH session on the VPS host itself, not inside any container), **RabbitMQ
terminal** / **SQL Server terminal** (Dokploy's container-terminal feature for the `rabbitmq` /
`sqlserver` service), **Dokploy UI** (the web panel for anything other than a container terminal).

**Placeholders, defined once:** `<domain>` is the API's public domain
(`api.industrialhinterlands.cloud` per the Compose file's network comment - confirm in Dokploy's
**Domains** tab). `<vps-host>` / `<vps-public-ip>` are the VPS's SSH hostname and public IP.
`<project-name>`, `<working_dir>`, `<compose-file>` come from "Locating the Dokploy project
directory" above. `<branch>` is whatever branch Dokploy's **General** tab shows configured for
this app - confirm it there; do not assume `master`.

**Step 1 - block incoming traffic, without touching the running container.** Disabling the API's
domain in Dokploy's **Domains** tab does **not** work here. Per
[Dokploy's domain documentation](https://docs.dokploy.com/docs/core/domains), Docker Compose
services (unlike single-image Applications) "must redeploy... for domain changes to take effect,"
because Compose "does not support hot reloading of domain configurations." A redeploy here would
rebuild and recreate the `api` container - killing `BookingOutboxDispatcher`/
`RegistrationOutboxDispatcher` and the old consumers before they finish draining (they run
in-process inside the API, not as separate worker containers), and risks starting the new
consolidated code before the migration and broker policy are ready. Domain removal is unsuitable
for this app's deployment shape; block at the VPS firewall instead, which never touches the
container.

Before blocking, confirm how you currently reach the Dokploy panel itself. If it's
`http://<vps-public-ip>:3000` (Dokploy's own default port), the block below doesn't affect it. If
the panel is only reachable through a custom domain proxied by the same Traefik, keep the `:3000`
address as a fallback for the container-terminal steps below (steps 2, 3, and 7 still go through
Dokploy's panel for the SQL Server and RabbitMQ terminals) - **not independently verified for this
VPS**, confirm it once before relying on it.

*(VPS SSH)* Confirm the firewall is actually enforcing anything, and note the current rules so
step 9 can reverse exactly what this step adds:
```sh
sudo ufw status verbose
```
Expected: `Status: active`. **Stop here if it says `inactive`** - enabling ufw for the first time
during a maintenance window risks locking out SSH if port 22 isn't already allowed; fix that as
its own prerequisite first.

*(local PowerShell)* Record the baseline for both public entry points this app exposes - the
domain through Traefik, and the API's directly-published port 8080 (`docker-compose.yml`'s `api`
service publishes `8080:8080`; a comment there claims this is "safe... as long as the VPS
firewall doesn't allow inbound 8080," which this check verifies rather than assumes):
```powershell
curl.exe -m 8 -s -o NUL -w "%{http_code}`n" https://<domain>/api/clinic
curl.exe -m 8 -s -o NUL -w "%{http_code}`n" http://<vps-public-ip>:8080/api/clinic
```
Expected before blocking: the first prints `200`. The second should already fail to connect. If
it instead prints `200`, that is a pre-existing public exposure independent of this cutover -
add a permanent `sudo ufw deny 8080/tcp` now, not just for this maintenance window.

*(VPS SSH)* Block the domain's path (Dokploy's shared Traefik listens on 80/tcp and 443/tcp - see
[Dokploy's Traefik integration](https://docs.dokploy.com/docs/core/domains)):
```sh
sudo ufw deny 80/tcp
sudo ufw deny 443/tcp
```
*(local PowerShell)* Re-run both `curl.exe` commands above. Expected now: both fail to connect
(e.g. `curl: (7) Failed to connect...`, or a timeout) - no `200` for either. **Stop and
investigate instead of proceeding to step 2 if the domain check still succeeds** - traffic is not
actually blocked, and continuing risks a booking or registration landing after you've started
treating the outboxes as drained.

**Step 2 - drain the old outboxes.** *(SQL Server terminal)* They normally clear within seconds,
since `BookingOutboxDispatcher`/`RegistrationOutboxDispatcher` poll every 5s:
```sql
SELECT COUNT(*) FROM BookingOutboxMessages WHERE DispatchedAtUtc IS NULL;
SELECT COUNT(*) FROM RegistrationOutboxMessages WHERE DispatchedAtUtc IS NULL;
```
Expected: both `0`. **If either is non-zero, wait and recheck rather than proceeding** - do not
continue while a row could still be in flight, since nothing will dispatch it once step 6 stops
these dispatchers for good.

**Step 3 - confirm both old queues are actually empty, not just "ready" empty.** *(RabbitMQ
terminal, or the management UI via the SSH tunnel in the [RabbitMQ](#rabbitmq) section)* Check
both `messages_ready` **and** `messages_unacknowledged` for `booking-confirmed` and
`registration-confirmation`:
```sh
rabbitmqctl list_queues name messages_ready messages_unacknowledged
```
Expected: both columns `0` for both queue names. `messages_ready` alone can read 0 while a
message is still `messages_unacknowledged` - delivered to an old consumer but not yet acked -
which is not yet safe to treat as drained. **Stop and recheck rather than proceeding if either
column is non-zero for either queue.**

**Step 4 - resolve failed messages.** *(RabbitMQ management UI via the SSH tunnel)* If either old
**failed** queue (`booking-confirmed.failed` / `registration-confirmation.failed`) holds messages
you still intend to recover, replay them now using the same non-destructive procedure documented
under "Recovery from `email-delivery.failed` is manual" below (peek with requeue, republish to
the *old* main queue, confirm the old dedup table gained the row, only then remove the original) -
the old consumers must still be running for a replay to be processed at all. After step 6, nothing
consumes `booking-confirmed` or `registration-confirmation` any more, so a message republished to
either old queue after that point will never be delivered.

**Step 5 - repeat steps 2 and 3 once more, immediately before stopping the old API.** Replaying
failed messages in step 4 itself publishes to the old main queues, and any earlier check result is
already stale by the time you act on it. This second pass should show the exact same zeros as the
first. **Stop and do not proceed to step 6 if it doesn't** - something published or redelivered
between the two passes, and step 1's traffic block needs re-verifying before you go further.

**Step 6 - stop the old API.** *(VPS SSH)*, using the project identity from "Locating the Dokploy
project directory" above:
```sh
docker compose -p <project-name> --env-file .env -f <compose-file> stop api
docker compose -p <project-name> --env-file .env -f <compose-file> ps api
```
Expected: the second command shows state `exited`. Using `stop`, not `down` or `rm`, is
deliberate - it ends the process (so the dispatcher/consumer definitely stop, matching what
steps 2-5 just confirmed is safe) while leaving the container in place as a fallback: since this
migration is additive only (see "The migration" below - it drops nothing), restarting this same
stopped container is a safe, reversible way to keep serving traffic if step 8 doesn't go cleanly,
without touching the migration or the broker policy. **Do not proceed to step 7 until `ps` shows
`exited`.**

**Step 7 - update the checked-out code, then run the migration and broker policy with no API
process running.** This app builds its image from source on every deploy (the Dockerfile, not a
pulled registry image - see the Compose file's own top comment), and the `migrator` service
(`profiles: ["tools"]`, `depends_on: sqlserver` only - confirmed by reading `docker-compose.yml`,
so running it cannot start `api` as a side effect) builds from whatever commit is currently
checked out in `<working_dir>`, not from whatever Dokploy's UI happens to show as "last
deployed." Dokploy's own **Deploy** button re-clones the repo and *also* starts every
non-tool-profile service (`sqlserver`, `rabbitmq`, `api`) in one action - there is no separate
Dokploy action that fetches new code without starting `api`. Reaching a state where the migrator
has the new commit but `api` hasn't started yet means advancing that checkout directly, with the
same `git` Dokploy itself uses, rather than inventing a new deployment mechanism:

*(VPS SSH)*
```sh
cd <working_dir>
git status
git fetch origin
git log -1 --oneline origin/<branch>
git reset --hard origin/<branch>
git log -1 --oneline
```
Expected: the final commit is the one containing `ConsolidateEmailDelivery`
(`PatientBooking.Api.Domain/Migrations/20260910135147_ConsolidateEmailDelivery.cs` should now
exist under `<working_dir>`). **Not verified against the actual VPS** - confirm `<working_dir>`
really is a git checkout (it should be; Dokploy clones the repo there) before relying on this. If
`git status` fails or reports unexpected local changes, stop and reconcile manually rather than
forcing anything.

Now build and run the migrator against that checkout:
```sh
docker compose -p <project-name> --env-file .env -f <compose-file> run --build --rm migrator
```
Expected: log line `Database migrations completed successfully.`, exit code 0. **Stop here - do
not proceed to step 8 - on any other outcome.** It is safe to just fix the cause and re-run this
exact command: the migration only applies pending changes and is documented as safe to re-run
(see "The migration" below), and no API instance is running yet for a partial state to affect.

*(RabbitMQ terminal)* Apply the shared policy from the [RabbitMQ](#rabbitmq) section above
(`email-delivery-retry-limit`), then confirm it:
```sh
rabbitmqctl list_policies
```
Expected: one row named `email-delivery-retry-limit` whose body matches that section exactly.
**Stop before step 8 if it doesn't match** - starting the new API against an unpolicied
`email-delivery` queue means a burst of retries has no delivery limit or dead-letter route yet.

**Step 8 - start the matching new API, then check it before restoring traffic.** *(VPS SSH)*,
same checkout, same project identity:
```sh
docker compose -p <project-name> --env-file .env -f <compose-file> up -d --build api
docker compose -p <project-name> --env-file .env -f <compose-file> logs --tail 100 api
```
Expected in the logs: normal Serilog startup lines, no unhandled exception, and (if
`ADMIN_SEED_EMAIL` is configured) the existing admin-bootstrap line, not a new error. Then, still
over SSH - this reaches the container over the VPS's own loopback interface, which ufw leaves
open by default even while step 1's rules block the public interface, so it exercises the real
API without yet exposing it publicly:
```sh
curl -m 8 -s -o /dev/null -w '%{http_code}\n' http://localhost:8080/api/clinic
```
Expected: `200`. **Stop and do not restore traffic if this fails or the logs show exceptions.**
The old container is only stopped, not removed (step 6), and this migration adds tables without
touching the old ones, so falling back is safe and reversible: stop the broken new container
(`docker compose ... stop api`) and start the old one again (`docker compose ... start api` -
Compose still has its prior container since step 6 used `stop`, not `rm`) while you investigate.
Do not purge queues, drop the new tables, or roll the migration back to do this.

**Step 9 - restore traffic and verify a real email.** *(VPS SSH)*:
```sh
sudo ufw delete deny 80/tcp
sudo ufw delete deny 443/tcp
```
*(local PowerShell)* Re-run the two `curl.exe` checks from step 1. Expected: the domain check
prints `200` again; the 8080 check still fails to connect unless step 1 found it already open,
in which case it should now be blocked by the permanent rule added there.

Create one real booking and one real patient registration through the live API and confirm both
confirmation emails actually arrive at a real inbox within a few minutes - a clean migration and
a correct policy are necessary but not sufficient, and this is the only check that proves the
whole pipeline end to end. If either does not arrive, check `SentEmailNotifications` *(SQL Server
terminal: `SELECT TOP 5 * FROM SentEmailNotifications ORDER BY SentAtUtc DESC;`)* and the
`email-delivery`/`email-delivery.failed` queues in the RabbitMQ management UI before assuming the
whole cutover failed - a single delivery hiccup doesn't necessarily mean the pipeline is broken.

**The migration** (`dotnet PatientBooking.Api.dll --migrate`, applied in step 7):

- Creates `EmailOutboxMessages` and `SentEmailNotifications`.
- Copies every row from `SentBookingNotifications` and `SentRegistrationNotifications` into
  `SentEmailNotifications` (matched by `Id`, safe to re-run) - so a legacy notification ID that
  somehow gets replayed later is still recognized as already-sent and skipped, not resent. Verified
  locally against populated old tables (including an `Id` present in both old dedup tables at once),
  not just an empty schema - see `EmailDeliveryMigrationCompatibilityTests` in
  `PatientBooking.Api.Tests`.
- Does **not** drop `BookingOutboxMessages`, `RegistrationOutboxMessages`,
  `SentBookingNotifications`, or `SentRegistrationNotifications`. They are retained as an inert
  historical/audit record - nothing in the codebase reads or writes them after this migration. This
  is deliberate: even if the preflight checks above are skipped, no data is silently destroyed. A
  future migration may drop these four tables once you have confirmed on the actual deployment
  target that they are empty and no longer needed for audit - that is a follow-up you choose to make,
  not something this migration does automatically.

The old `booking-confirmed-retry-limit` / `registration-confirmation-retry-limit` broker policies can
be left in place harmlessly after step 7 (they apply to queues nothing publishes to any more) or
removed with `rabbitmqctl clear_policy`; neither is required for the new pipeline to work.

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
