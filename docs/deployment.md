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
these values instead of guessing a path.

**Four things to keep distinct**, because they are not the same thing and a fix aimed at
the wrong one silently does nothing:

1. **Repository Compose configuration** - this repo's `docker-compose.yml`. It has no
   `labels:` block for Traefik on the `api` service.
2. **Dokploy's effective deployment configuration** - what Dokploy actually runs. For a
   Compose app, [Dokploy adds domain routing as Traefik Docker labels, generated and
   applied automatically](https://docs.dokploy.com/docs/core/docker-compose) - "All label
   generation... is handled automatically by Dokploy." Dokploy does not support a
   `docker-compose.override.yml`; it works from the one compose file, meaning it injects
   those labels into the spec it deploys rather than layering a second file on top. This
   is why the repo's own file shows no labels even though routing works.
3. **The currently running container's actual configuration** - inspectable read-only
   right now via `docker inspect`. This is the only one of the four you can check directly
   without guessing what Dokploy or git will do next.
4. **Facts that require VPS inspection** - the real project name, working directory, and
   full compose file list; whether any other app shares this VPS's Traefik; whether the
   host has public IPv6 exposure. None of these should be assumed.

**Primary discovery - Dokploy's own Advanced tab (Dokploy UI, read-only).** Open this
app in Dokploy, go to **Advanced**, and read the command shown there (a "Custom Command"
toggle, off by default, reveals the exact default command Dokploy runs when you turn it
on to edit it - read it, then turn it back off without saving if you only wanted to look).
This string is Dokploy's own authoritative project name (`-p <name>`) and compose file
list (`-f <file>`, possibly more than one `-f`) - not a guess, and not something `docker
inspect` can be wrong about. **Record the exact current toggle state and, if on, the exact
current command text before changing anything** - step 7 below needs to restore this.

**Cross-check - Docker's own Compose labels (VPS SSH).** Confirms the Advanced tab's
values from the running container's actual side, and is the only option if the Advanced
tab's command differs from what's actually running (for example, after a manual change):

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
file's exact name(s)/path(s) - if it lists more than one file, every `-f <compose-file>`
below stands for all of them, in the same order, not just `docker-compose.yml`. These are
standard Compose-assigned container labels, not a Dokploy-specific convention, so this
keeps working across Dokploy versions. If this disagrees with the Advanced tab's command,
stop and reconcile why before proceeding - one of the two is stale.

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
**Do not use `git reset --hard` or any other command that rewrites `docker-compose.yml`
inside `<working_dir>` by hand** - see step 7 below for why that specifically destroys
Dokploy's injected Traefik labels, and how the cutover advances the code instead.

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
code-only or schema-compatible release still just goes through Dokploy's **Deploy** button,
unmodified, per "For later code-only deployments" above. Steps 6-8 below still use that same
Deploy button (so Dokploy's own git-pull and Traefik-label generation keep running normally) but
temporarily swap its Compose **Advanced** tab **Custom Command** for one deploy, so the sequence
becomes old API stopped → new migration and broker policy applied → new API started, in that
exact order - something the button's own default command can't express for a Compose app, since
its default always starts every non-tool-profile service together (see step 7).

**Terminal labels used below:** **local PowerShell** (your own machine, outside the VPS),
**VPS SSH** (an SSH session on the VPS host itself, not inside any container), **RabbitMQ
terminal** / **SQL Server terminal** (Dokploy's container-terminal feature for the `rabbitmq` /
`sqlserver` service), **Dokploy UI** (the web panel for anything other than a container terminal).

**Placeholders, defined once:** `<domain>` is the API's public domain
(`api.industrialhinterlands.cloud` per the Compose file's network comment - confirm in Dokploy's
**Domains** tab). `<vps-host>` / `<vps-public-ip>` are the VPS's SSH hostname and public IP.
`<project-name>`, `<working_dir>`, `<compose-file>` come from "Locating the Dokploy project
directory" above. `<branch>` is whatever branch Dokploy's **General** tab shows configured for
this app - confirm it there; do not assume `master`. `<ext-if>` is the VPS's public network
interface name, from `ip route show default` (the `dev <name>` field) - **VPS SSH**, read-only.

**Step 0 - prerequisites, pinned revision, and rollback artifact (VPS SSH + Dokploy UI).**

Dokploy has no built-in way to deploy an exact commit SHA - it always deploys whatever is
currently at the tip of the configured branch
([confirmed open feature request, Dokploy#4147](https://github.com/Dokploy/dokploy/issues/4147)).
The only way to make "the reviewed commit" and "what Dokploy deploys" the same thing is to make
sure nothing else moves `<branch>` during this procedure:

1. Confirm the exact commit you intend to deploy is really at the tip of `<branch>` right now:
   *(local PowerShell)* `git ls-remote origin <branch>` - compare the printed SHA against the
   commit you reviewed. **Stop and reconcile if they differ.**
2. *(Dokploy UI)* Open this app's **General** tab and turn **Auto Deploy** off, noting that it
   was on (so you can restore it after step 9). This is a real, documented toggle - without it, a
   webhook-triggered deploy from any push to `<branch>` (including one from a CI job or a stray
   push during this window) could start `api` mid-cutover using whatever Custom Command is
   currently configured, out of sequence with steps 6-8 below.
3. *(VPS SSH)* Confirm no other app on this host shares the ports you're about to block:
   ```sh
   docker ps --format '{{.Names}}\t{{.Ports}}'
   ```
   Note every container publishing `80`, `443`, or `8080`. If only `dokploy-traefik` and this
   app's `api` container appear, blocking those ports affects only this app (and Dokploy's own
   panel, handled below). If another app's container also appears, step 1's domain/Traefik block
   affects it too - there is no per-domain firewall block, only per-port - state that to yourself
   before proceeding, since there's no way around it at this layer without a redeploy (which is
   exactly what step 1 exists to avoid).
4. **Capture the current, working image as a rollback artifact** *(VPS SSH)*, before anything is
   rebuilt - once step 8 rebuilds `api`, this exact image may no longer have any tag pointing to
   it, and the old *container* being merely `stop`ped (step 6) does not by itself preserve a
   rebuildable, taggable reference to it (see step 8's rollback note for why):
   ```sh
   docker inspect <api-container> --format '{{.Image}}'
   docker tag $(docker inspect <api-container> --format '{{.Image}}') patientbooking-api:pre-cutover-rollback
   ```
   Expected: the second command prints nothing on success. Verify with
   `docker images patientbooking-api:pre-cutover-rollback` - expect one row. This tag is a local
   safety net only, independent of whatever Dokploy or git does next; keep it until step 9's
   verification passes.
5. **Record the pre-cutover commit SHA** *(VPS SSH)*, the "old commit" the rollback section below
   points back at:
   ```sh
   cd <working_dir>
   git log -1 --oneline
   ```
   Write this SHA down somewhere outside the VPS (it's not a secret) - once step 7 advances the
   checkout, this is the only record of exactly what was running before.

**Step 1 - block incoming traffic, without touching the running container.** Two things ruled
out first, both confirmed against official documentation rather than assumed:

- Disabling the API's domain in Dokploy's **Domains** tab does **not** work here. Per
  [Dokploy's domain documentation](https://docs.dokploy.com/docs/core/domains), Docker Compose
  services (unlike single-image Applications) "must redeploy... for domain changes to take
  effect," because Compose "does not support hot reloading of domain configurations." A redeploy
  here would rebuild and recreate the `api` container - killing `BookingOutboxDispatcher`/
  `RegistrationOutboxDispatcher` and the old consumers before they finish draining (they run
  in-process inside the API, not as separate worker containers).
- A plain `ufw deny` on 80/443/8080 does **not** reliably work either, because Docker manages its
  own `iptables`/`ip6tables` rules for published ports and routes that traffic through the `nat`
  table and the `FORWARD` chain, ahead of - and bypassing - the `INPUT`/`OUTPUT` chains ufw
  controls. This is Docker's own documented behavior, not a misconfiguration:
  ["Docker routes container traffic in the `nat` table, which means that packets are diverted
  before it reaches the `INPUT` and `OUTPUT` chains that ufw uses."](https://docs.docker.com/engine/network/packet-filtering-firewalls/)
  Docker explicitly provides a different, documented hook for exactly this case: the
  `DOCKER-USER` chain, which is consulted *before* Docker's own forwarding rules and is never
  flushed by Docker itself. See
  [Docker's iptables documentation](https://docs.docker.com/engine/network/firewall-iptables/).

Before blocking, confirm how you currently reach the Dokploy panel itself. If it's
`http://<vps-public-ip>:3000` (Dokploy's own default port), the block below doesn't affect it -
**verify this now, it's a prerequisite, not a nice-to-have**, since steps 2, 3, 7, and step 0's
Auto Deploy toggle all need continued Dokploy panel access during the blocked window. If the
panel is only reachable through a domain proxied by the same Traefik, it goes down along with
`<domain>` when you block 443 - reconsider before proceeding, since you'd lose the ability to run
the remaining steps through the UI.

*(local PowerShell)* Record the baseline for both public entry points this app exposes - the
domain through Traefik, and the API's directly-published port 8080 (`docker-compose.yml`'s `api`
service publishes `8080:8080`):
```powershell
curl.exe -m 8 -s -o NUL -w "%{http_code}`n" https://<domain>/api/clinic
curl.exe -m 8 -s -o NUL -w "%{http_code}`n" http://<vps-public-ip>:8080/api/clinic
```
Expected before blocking: the first prints `200`. The second should already fail to connect - if
it instead prints `200`, that is a pre-existing public exposure independent of this cutover; add
a permanent block for it separately from this maintenance window.

*(VPS SSH)* Check whether this VPS has any public IPv6 exposure at all, so you know whether the
`ip6tables` step below is needed:
```sh
ip -6 addr show <ext-if>
sudo ip6tables -L DOCKER-USER 2>&1 | head -1
```
If the first prints no global (non-`fe80`) address, this host has no public IPv6 - skip the
`ip6tables` commands below entirely, there is nothing for them to block. If the second reports
"No chain/target/match by that name," Docker is not managing `ip6tables` on this host, and the
plain `ip6tables` commands below would need a different (host-level) approach - **stop and
reassess rather than guessing** if you have a global IPv6 address *and* no `DOCKER-USER` chain in
`ip6tables`.

*(VPS SSH)* Block all three public entry points at once, matched only on traffic arriving from
the external interface (loopback and Docker's internal bridge traffic are untouched, so SSH, the
health check in step 8, and the RabbitMQ SSH tunnel keep working):
```sh
sudo iptables  -I DOCKER-USER -i <ext-if> -p tcp --dport 80   -j DROP
sudo iptables  -I DOCKER-USER -i <ext-if> -p tcp --dport 443  -j DROP
sudo iptables  -I DOCKER-USER -i <ext-if> -p tcp --dport 8080 -j DROP
```
Only if the IPv6 check above found a global address and an existing `DOCKER-USER` chain:
```sh
sudo ip6tables -I DOCKER-USER -i <ext-if> -p tcp --dport 80   -j DROP
sudo ip6tables -I DOCKER-USER -i <ext-if> -p tcp --dport 443  -j DROP
sudo ip6tables -I DOCKER-USER -i <ext-if> -p tcp --dport 8080 -j DROP
```

*(local PowerShell)* Re-run both `curl.exe` commands above. **What "blocked" looks like:** a
timeout within the `-m 8` window (curl exit code `28`, `Operation timed out`) - the `DROP` target
silently discards the packet rather than returning an HTTP response, so there's no status code to
compare, only "no response arrived." A fast `curl: (7) Failed to connect` is also acceptable
evidence (something else on the path refused it outright); a `200`, or any other actual HTTP
response, is not. **Stop and investigate instead of proceeding to step 2 if either check still
returns a real HTTP response** - traffic is not actually blocked, and continuing risks a booking
or registration landing after you've started treating the outboxes as drained.

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
docker compose -p <project-name> --env-file .env -f <compose-file> ps -a api
```
Expected: the second command shows state `exited` (`-a` is required here - `ps` without it can
omit stopped containers). Using `stop`, not `down` or `rm`, is deliberate - it ends the process
(so the dispatcher/consumer definitely stop, matching what steps 2-5 just confirmed is safe)
without touching `sqlserver`, `rabbitmq`, their networks, or volumes. **This alone is not the
rollback plan** - step 8 rebuilds the `api` service, and Compose recreating a service removes its
prior container as part of that (see step 8's rollback note); the real fallback is the image
tagged in step 0. **Do not proceed to step 7 until `ps -a` shows `exited`.**

**Step 7 - advance the checkout through Dokploy itself, then run the migration and broker policy
with no API process running.** This app builds its image from source on every deploy (the
Dockerfile, not a pulled registry image - see the Compose file's own top comment), and the
`migrator` service (`profiles: ["tools"]`, `depends_on: sqlserver` only - confirmed by reading
`docker-compose.yml`, so running it cannot start `api` as a side effect) builds from whatever
commit is currently checked out in `<working_dir>`.

Advancing that checkout must go through Dokploy's own deploy pipeline, **not** a manual
`git reset --hard` in `<working_dir>`: Dokploy injects the running `api` container's Traefik
routing labels into the compose spec it deploys, generated fresh on every deploy from the
Domains-tab configuration (see "Locating the Dokploy project directory" above). A manual
`git fetch`/`git reset --hard` only touches git and the working tree - it does not re-run
Dokploy's label-generation step, so the *next* time `api` is actually started (step 8), whatever
process starts it needs to be Dokploy's own pipeline again, or those labels never get written and
the domain silently stops routing to the new container. Dokploy's own **Deploy** button normally
starts every non-tool-profile service (`sqlserver`, `rabbitmq`, `api`) together, with no separate
action that fetches new code without starting `api` - but Dokploy's Compose **Advanced** tab
(from "Locating the Dokploy project directory" above) exposes a **Custom Command** override that
fully replaces the command Dokploy runs at the end of its normal deploy pipeline (git pull, build,
label generation still happen first), which is the documented, supported way to make one Deploy
action run the migrator instead of `up`:

*(Dokploy UI)* On this app's **Advanced** tab, enable **Custom Command** (or edit the existing
one - you recorded its prior state in step 0) and set it to:
```
compose -p <project-name> -f <compose-file> run --build --rm migrator
```
using the exact project name and compose file(s) from "Locating the Dokploy project directory"
above (repeat `-f` once per file if more than one was listed). Save, then trigger **Deploy**.

Expected in the deploy log: the normal git-pull/build steps, then the migrator's own log line
`Database migrations completed successfully.`, and no `api`/`sqlserver`/`rabbitmq` container
restart reported. **Stop here - do not proceed further - on any other outcome.** It is safe to
just fix the cause and trigger **Deploy** again: the migration only applies pending changes and
is documented as safe to re-run (see "The migration" below), and no API instance is running yet
for a partial state to affect.

*(VPS SSH)* Confirm the checkout actually advanced to the reviewed commit (this is a read-only
check on what Dokploy just did, not a step that changes anything):
```sh
cd <working_dir>
git log -1 --oneline
```
Expected: the commit containing `ConsolidateEmailDelivery`
(`PatientBooking.Api.Domain/Migrations/20260910135147_ConsolidateEmailDelivery.cs` should now
exist under `<working_dir>`) - the same SHA confirmed against `<branch>`'s tip in step 0. **Not
verified against the actual VPS** - confirm `<working_dir>` really is a git checkout (it should
be; Dokploy clones the repo there) before relying on this.

*(RabbitMQ terminal)* Apply the shared policy from the [RabbitMQ](#rabbitmq) section above
(`email-delivery-retry-limit`), then confirm it:
```sh
rabbitmqctl list_policies
```
Expected: one row named `email-delivery-retry-limit` whose body matches that section exactly.
**Stop before step 8 if it doesn't match** - starting the new API against an unpolicied
`email-delivery` queue means a burst of retries has no delivery limit or dead-letter route yet.

**Step 8 - start the matching new API through Dokploy, then check it before restoring traffic.**
*(Dokploy UI)* On the same **Advanced** tab, restore the Custom Command to whatever you recorded
in step 0 (turn the toggle back off if it was off before; Dokploy's own built-in default is the
normal `up -d --build` equivalent, and going through it again is what re-runs label generation for
`api`). Trigger **Deploy**.

Expected in the deploy log: the normal git-pull/build steps (same commit as step 7 - nothing
should have moved `<branch>` since step 0 disabled Auto Deploy), then `api` reported as started.
*(VPS SSH)*:
```sh
docker compose -p <project-name> --env-file .env -f <compose-file> logs --tail 100 api
```
Expected: normal Serilog startup lines, no unhandled exception, and (if `ADMIN_SEED_EMAIL` is
configured) the existing admin-bootstrap line, not a new error. Then, still over SSH - this
reaches the container over the VPS's own loopback interface, which the `DOCKER-USER` rules from
step 1 never touch (they match only `-i <ext-if>`, not loopback), so it exercises the real API
without yet exposing it publicly:
```sh
curl -m 8 -s -o /dev/null -w '%{http_code}\n' http://localhost:8080/api/clinic
```
Expected: `200`. **Stop and do not restore traffic if this fails or the logs show exceptions** -
go to "Rollback" below instead of guessing at a fix live.

### Rollback

Three different points in this procedure need different responses - treat them differently
rather than reaching for one blanket rollback:

**Failure in step 7 (migration itself fails), before `api` starts at all.** No user-facing state
changed. Fix the cause and trigger Deploy again with the same Custom Command - the migration is
documented as safe to re-run (see "The migration" below).

**Failure in step 8 (new `api` fails its health check or logs exceptions), before traffic is
restored.** No real request has been served by the new code yet - this is a clean rollback.
`docker compose ... stop api` no longer gets you back to working code: Compose's own recreate
behavior for `up --build` removes the *previous* container for a service once the replacement is
running - [confirmed against Compose's `up` reference](https://docs.docker.com/reference/cli/docker/compose/up/) -
so the container `stop`ped in step 6 does not exist anymore for `docker compose start api` to
restart, even though that container object still existed when step 6 ran. Use the image tagged in
step 0 instead:
1. *(VPS SSH)* `docker compose -p <project-name> --env-file .env -f <compose-file> stop api`
2. Point Dokploy's source at the old commit: push a disposable ref at the pre-cutover SHA you
   recorded in step 0 (`git push origin <old-commit-sha>:refs/heads/rollback-pre-cutover`
   *(local PowerShell)*), then set that as the branch in this app's Dokploy **General** tab
   temporarily, and confirm the Custom Command override from the Advanced tab is off (using
   Dokploy's normal deploy path, not the migrator-only override).
3. Trigger **Deploy**. This rebuilds and starts `api` from the old, known-good commit through
   Dokploy's own pipeline - preserving the Traefik labels the same way any normal deploy does,
   which a hand-rolled `docker run` reconstructing env vars/volumes/networks/labels by hand would
   risk getting subtly wrong.
4. Restore the branch field to `<branch>` once resolved. The `patientbooking-api:pre-cutover-rollback`
   tag from step 0 is a faster, riskier alternative only if you're confident you can reconstruct
   every env var, volume mount, network attachment, and label the running container needs by hand -
   prefer the Dokploy redeploy above unless speed is critical.

The migration adds tables without touching the old ones, so the schema itself tolerates the old
code running against it - do not purge queues, drop the new tables, or roll the migration back to
do this rollback.

**Failure discovered after step 9 restores traffic and the new API has already accepted real
work.** This is not a clean rollback, and rolling back here has a real cost: only the *new* code
(`EmailOutboxDispatcher`/`EmailDeliveryConsumer`) reads `EmailOutboxMessages` - the old, retired
dispatchers never did and won't be restored. Rolling back to the old commit does **not** delete
any booking/registration confirmation queued during the new API's uptime (the additive migration
keeps the table), but it does **pause** delivery of anything queued during that window until the
new code is running again - nothing will dispatch those rows while the old code is active. Prefer
fixing forward (diagnose the new API's actual problem and redeploy the same new commit again)
over rolling back once real traffic has been accepted, specifically because rolling back doesn't
undo the problem for already-queued rows, it just delays them further. If the new API must come
down urgently regardless, use the same three-step rollback above, then treat draining
`EmailOutboxMessages` rows created during the new API's window as the first thing to check once
the new code is back - they resume automatically once `EmailOutboxDispatcher` is running again, no
manual replay needed for that class of row.

**Step 9 - restore traffic and verify a real email.** *(VPS SSH)*, reversing exactly the rules
step 1 added:
```sh
sudo iptables  -D DOCKER-USER -i <ext-if> -p tcp --dport 80   -j DROP
sudo iptables  -D DOCKER-USER -i <ext-if> -p tcp --dport 443  -j DROP
sudo iptables  -D DOCKER-USER -i <ext-if> -p tcp --dport 8080 -j DROP
```
Only if step 1 also applied the `ip6tables` versions:
```sh
sudo ip6tables -D DOCKER-USER -i <ext-if> -p tcp --dport 80   -j DROP
sudo ip6tables -D DOCKER-USER -i <ext-if> -p tcp --dport 443  -j DROP
sudo ip6tables -D DOCKER-USER -i <ext-if> -p tcp --dport 8080 -j DROP
```
*(Dokploy UI)* Turn **Auto Deploy** back on for this app (step 0 turned it off).

*(local PowerShell)* Re-run the two `curl.exe` checks from step 1. Expected: the domain check
prints `200` again; the 8080 check still fails to connect unless step 1 found it already open,
in which case block it permanently as its own follow-up, separate from this maintenance window.

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
