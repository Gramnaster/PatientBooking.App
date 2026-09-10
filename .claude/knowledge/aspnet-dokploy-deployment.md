# ASP.NET Core deployment with Dokploy

Last checked against local source: 2026-09-09. Scope: PatientBooking portfolio app,
ASP.NET Core/.NET 10, Docker Compose, Dokploy on a Hostinger VPS, SQL Server.
Future PostgreSQL applications can reuse the workflow, with provider-specific changes.

## Read this before deployment work

The user runs remote commands and deployments. Routine schema updates must remain
manually triggered from the Dokploy **api container terminal**. Do not replace this
with automatic migrations on every API startup without a new user request.

Use [the deployment runbook](../../docs/deployment.md) for executable project instructions.
This note records decisions and troubleshooting evidence; keep the runbook as the single
source for the full procedure.

Inspect [Program.cs](../../PatientBooking.Api/Program.cs), [Dockerfile](../../Dockerfile),
and [Compose](../../docker-compose.yml) before changing advice. Local source does not
prove which image or configuration is currently deployed.

## Manual migration design

- The application implements `--migrate` itself. It is not a standard .NET CLI option
  automatically available in other applications.
- From the API container terminal, change directory with `cd /app`, then run
  `dotnet PatientBooking.Api.dll --migrate`. The command resolves the configured
  DbContext through host DI and calls `MigrateAsync`, then exits before seeding or
  starting another web listener. Normal API startup does not apply migrations.
- Author and commit migrations locally, then deploy the image containing them before
  invoking the command. It applies pending migrations; it does not generate migrations
  or expose rollback targets. See the [EF migration API documentation](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying#apply-migrations-at-runtime).
- The final Docker stage contains the ASP.NET runtime and published application, while
  the SDK is in the build stage. Do not prescribe `dotnet ef database update` in this
  container: its SDK, EF tool, and project files are absent. See [EF CLI prerequisites](https://learn.microsoft.com/en-us/ef/core/cli/dotnet).
- Migration mode builds the host, so database configuration alone is insufficient:
  preserve required JWT and lookup-protection configuration. Do not print secret values.

## Execution locations and deployment stages

Admin bootstrap now creates one new account from `ADMIN_SEED_EMAIL`,
`ADMIN_SEED_PASSWORD`, `ADMIN_SEED_FIRST_NAME`, and `ADMIN_SEED_LAST_NAME` supplied
through Dokploy's Environment editor and mapped explicitly by Compose. It no longer
promotes a publicly registered account by email. See the runbook's initial-admin section.
Creation is transactional (Identity user, patient/MRN, admin/number); the email is marked
confirmed by operator provisioning. Existing non-admin email collisions or other admins
stop startup. The same existing admin is left unchanged, including its password.
Remove the bootstrap password after provisioning; retain the email for startup checks.
Migration mode still skips bootstrap and normal startup still does not apply migrations.

| Location | Responsibility |
|---|---|
| Local repository terminal | Author migrations, build, review and commit changes. |
| Dokploy API container terminal | Apply migrations from the deployed application in `/app`. Routine migrations need no separate VPS SSH session. |
| Database container terminal | Diagnose database existence/readiness with database tools. |
| VPS host terminal | Recovery using the actual Dokploy Compose project, files and environment. |

First deployment requires the application database/schema as well as a healthy database
server. The current SQL healthcheck runs `SELECT 1`; it does not establish that
`PatientBookingDb` exists or contains the required schema.

If the API cannot remain running, its terminal is unavailable. The optional `migrator`
service in the `tools` profile supports manual recovery using the same runtime command.
Use the actual deployed Compose project identity and environment, then restart/redeploy
the API. Do not invent a Dokploy checkout path or reuse an old project name blindly.
The current startup seed queries the database only when an admin seed email is configured.

For later code-only deployments, deploy normally. For schema changes, review migration
compatibility and backups, deploy the required image and deliberately apply migrations;
coordinate downtime if the running API cannot tolerate the schema transition. Verify a
database-backed endpoint afterward. A success log alone does not verify the public API.

## Observed failures and evidence

| Observation from the 2026-09-08 session | Finding and lesson |
|---|---|
| `/api/clinic` returned HTTP 500; logs included SQL error 4060 for `PatientBookingDb`. | This request reached application/database code. It was not evidence of a missing route. |
| User queried `sys.databases` for `PatientBookingDb` and received zero rows. | The database was absent on the queried SQL instance, supporting the missing-database diagnosis. |
| Compose reported `/home/jpcvillalon/.env` missing. | The command was run from the home directory using a relative env-file path. Recover using verified deployment files and project identity. |
| Running the DLL from `/$` reported application missing and no SDK. | The relative DLL path was wrong. `cd /app` selects the directory; entering `/app` alone attempts to execute a directory. This error did not justify adding an SDK. |
| User subsequently reported successful migration. | User-reported success; the agent did not independently inspect the remote migration history or verify endpoint recovery. |

For a separate 404, inspect the exact route, HTTP method, deployed version and proxy
mapping. Do not automatically attribute every 404 to the database failure. Earlier
Development-only Scalar/OpenAPI advice is stale: current local `Program.cs` maps both
outside an environment guard. Deployment of that change has not been verified here.

## CI Docker restore artifacts (2026-09-09)

The supplied GitHub Actions log showed a successful container restore followed by
`NETSDK1064` (AsyncFixer 2.1.0 missing) during `dotnet publish --no-restore`, after
`COPY . .`. The workflow builds on the runner before passing its working directory
as Docker's context. Root-only `bin/` and `obj/` exclusions allowed nested project
artifacts to overwrite the container's restore metadata with host package paths.

Use `**/bin/` and `**/obj/` in the root `.dockerignore` to exclude these directories
at every depth. Preserve the cached restore stage; adding a package or removing
`--no-restore` does not address the leaking build artifacts. See
[Docker ignore syntax](https://docs.docker.com/build/concepts/context/#dockerignore-files)
and [Microsoft's NETSDK1064 guidance](https://learn.microsoft.com/en-us/dotnet/core/tools/sdk-errors/netsdk1064).

## Configuration caveats to recheck

- Compose connects the API to `default` and external `dokploy-network`; SQL uses the
  default network. Verify the Dokploy domain targets the API on port 8080 with the intended
  path handling. This note does not certify the remote domain/proxy settings.
- The external network must exist; the Compose comment claiming unmodified local startup
  should not be treated as a verified prerequisite-free procedure.
- SQL has no published host port. The existing SSH example targeting host `localhost:1433`
  is incomplete for this configuration; establish a reachable destination before using it.
- API port `8080:8080` is published. Do not assume UFW alone prevents public access or
  justifies trusting every forwarded-header sender. Inspect actual networking and proxy
  trust. [Docker documents its interaction with host firewall rules](https://docs.docker.com/engine/network/packet-filtering-firewalls/).
- SQL data uses a named volume and logs use a bind mount. Preserve the deployed volume
  identity during recovery; never suggest deleting volumes to fix a missing database.
  Compose now mounts `api-keys` at `/app/keys`. The Dockerfile prepares that directory
  for the non-root app user with mode 700, inherited by a new empty named volume.
  This fixes the reported registration failure (`Access to the path '/app/keys' is denied`).
  Preserve existing keys before the first deployment of this change. Existing volumes
  retain their own permissions; rebuilding the image does not repair those permissions.
  Keys are excluded from Docker's build context and must stay out of source control.
  Filesystem permissions do not encrypt the key files or prevent VPS/Docker administrators
  from reading them. Back up the keys securely alongside the database.

## PostgreSQL for future applications

The manual migration entry point can follow the same host/DI pattern. Configure
`Npgsql.EntityFrameworkCore.PostgreSQL` and `UseNpgsql` with versions compatible with the
application's EF Core version; see [Npgsql's provider documentation](https://www.npgsql.org/efcore/).

SQL Server migrations are not a portable PostgreSQL schema. Generate and review migrations
for the selected provider; applications supporting both need provider-specific migration
sets. See [EF Core migrations with multiple providers](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers).

Recheck connection strings, container image/version, initialization variables, data-volume
paths, readiness checks, permissions, backup/restore tools, raw SQL and type mappings.
Starting a new PostgreSQL application and moving existing SQL Server data are separate tasks.
No PostgreSQL deployment or data conversion has been tested in this project.

## Verification boundary and maintenance

Earlier work recorded a passing Release API build and Compose configuration validation.
The initial knowledge capture rechecked local source only. On 2026-09-09, after the
recursive `.dockerignore` fix, a local Docker image build passed, including restore
and Release publish with existing host `obj` files present. Existing analyzer warnings
remained. GitHub Actions has not been rerun by the agent; no database integration test,
VPS inspection or public endpoint recovery check was performed. Record later checks
with dates and distinguish agent-observed results from user reports.

Update this note when deployment decisions change. Keep credentials out, link to the
runbook instead of duplicating full procedures, and recheck current official documentation
before prescribing version-sensitive commands or Dokploy UI steps.

## RabbitMQ added to Compose (2026-09-09)

`docker-compose.yml` gained a `rabbitmq` service (`rabbitmq:4-management-alpine`, named volume
`rabbitmq-data`, healthcheck via `rabbitmq-diagnostics check_port_connectivity`, credentials via
`RABBITMQ_USER`/`RABBITMQ_PASSWORD`) - this is the first time RabbitMQ enters this file, so there is
no existing-queue-argument conflict to account for on the deploy path. AMQP (5672) has no host
mapping; the management port (15672) is published loopback-only (`127.0.0.1:15672:15672`, added
2026-09-10) so an SSH tunnel from the VPS's own terminal has a real destination without exposing
anything on the public interface - the original doc's tunnel command targeted a port nothing
published, so it could never have connected as written.

The retry/dead-letter behavior for `booking-confirmed` (delivery-limit 3, dead-letter to
`booking-confirmed.failed`) ships as a broker **policy**, applied once manually per broker via
`rabbitmqctl set_policy` (see the runbook's RabbitMQ section) - deliberately not automated via a
`definitions.json` boot-time import. Verified locally: importing any `definitions.json` at boot
(via `definitions.local.path`/`definitions.import_backend`) suppresses RabbitMQ's default
vhost/user seeding entirely, including the `RABBITMQ_DEFAULT_USER`/`RABBITMQ_DEFAULT_PASS`
env-var path - the broker booted with **zero users** and every credential silently stopped
working until the vhost/users were also declared in the definitions file. Baking real credentials
into a git-committed definitions file to work around that isn't acceptable, and there's no
official post-boot hook for running `rabbitmqctl` once the server is actually up (confirmed via
search - only third-party forks of the image offer one). A one-time manual policy step matches
this repo's own existing precedent for migrations (`--migrate`, deliberately manual, not run on
every startup) more than it looks like a shortcut.

Verified locally only: `rabbitmq:4-management-alpine` boots cleanly with `RABBITMQ_DEFAULT_USER`/
`RABBITMQ_DEFAULT_PASS` and no definitions file, `rabbitmqctl set_policy` applies and is visible
via `rabbitmqctl list_policies`. Not verified: this Compose service on the actual VPS/Dokploy, or
the policy step run against a deployed broker.

### Policy gained at-least-once dead-lettering (2026-09-10)

The policy body now also sets `dead-letter-strategy: at-least-once` and `overflow: reject-publish`
(see `docs/deployment.md`'s RabbitMQ section for the full command and reasoning) - without these,
the default `at-most-once` dead-lettering can silently lose a message during the hand-off to
`booking-confirmed.failed` if that queue is briefly unreachable, which defeats the point of a
poison-message queue. Verified against current RabbitMQ docs (quorum-queues#dead-lettering).
`PatientBooking.Api.Tests` covers retry exhaustion into `booking-confirmed.failed` end-to-end, and
now also the "dead-letter target unavailable, then recovers" path itself
(`HandleDeliveryAsync_DeadLetterDestinationUnavailableThenRecovers_MessageSurvivesAndEventuallyArrives`)
- simulated by putting a 0-length `overflow: reject-publish` policy on `booking-confirmed.failed`
itself rather than taking any container down, since the failed queue lives on the same broker
process as `booking-confirmed` and can't be independently stopped. This is RabbitMQ's own
documented way to force a target queue to reject every enqueue
(quorum-queues#dead-lettering's "push back" wording).

Recovery from `booking-confirmed.failed` is manual in all cases, including a SQL Server outage long
enough to exhaust the 3-attempt delivery limit - nothing in `Program.cs` consumes that queue. See
`docs/deployment.md`'s "Recovery from booking-confirmed.failed is manual, not automatic" section for
the exact management-UI replay procedure (get-message-then-republish, no Shovel plugin).

### RabbitMQ image pinned to 4.3.4 (2026-09-10)

`rabbitmq:4-management-alpine` is a floating tag - it moved from resolving to RabbitMQ 4.3.4 to
4.3.5 during this project's own development, confirmed by re-pulling it and running
`rabbitmqctl version` before and after. All three references (`docker-compose.yml`,
`docker/dev.compose.yaml`, `MessagingTestFixture.cs`) now pin `rabbitmq:4.3.4-management-alpine`
explicitly, so deploy and tests stay on the exact version they were verified against until someone
deliberately bumps it.

### Booking and registration queues consolidated into `email-delivery` (2026-09-10)

Everything above in this section describing `booking-confirmed`/`booking-confirmed.failed`
described the state before this date - kept as-is since it documents real history (the
`definitions.json` rejection reasoning, the loopback port-binding fix, the `at-least-once`
dead-lettering addition all still apply unchanged). What changed: booking confirmation and
patient-registration confirmation, previously two separate same-shaped pipelines, now share one
queue pair (`email-delivery` / `email-delivery.failed`) instead of two (`booking-confirmed` /
`registration-confirmation`, each with its own `.failed` queue). The policy name changed to
`email-delivery-retry-limit`; the `at-least-once`/`overflow: reject-publish` reasoning and the
manual (no Shovel plugin) replay procedure both carry over unchanged, just pointed at the new
queue names. See
[RabbitMQ email delivery knowledge](rabbitmq-email-delivery.md) for the full pipeline and
[the deployment runbook](../../docs/deployment.md#one-time-migration-cutover-from-the-old-per-domain-pipelines-this-deploy-only)
for the exact one-time cutover steps (drain both old outboxes/queues before deploying, apply the
new policy, old tables retained as inert history). Not verified on the VPS - only locally.

### Cutover procedure tightened: write-blocking, unacked counts, migrator-first sequencing (2026-09-10)

The nine-step cutover in the runbook now does three things the original draft didn't:
disables the old API's Dokploy domain first (not the container - the container must stay
up so its own dispatcher/consumer finish draining), checks `messages_unacknowledged` on
both old queues alongside `messages_ready` (a message delivered-but-unacked to an old
consumer wouldn't show up in a ready-only check), and repeats the drain/queue checks a
second time immediately before actually stopping the old API, since replaying old failed
messages (step 4) itself changes queue state after the first check pass.

It also changes *how* the migration and broker policy get applied: via the `migrator`
Compose service (`docker compose ... run --build --rm migrator`, already defined for the
"API cannot stay running" case) with the API container stopped, rather than running
`--migrate` in the already-started new container's terminal. `Program.cs` has no gate that
pauses `EmailOutboxDispatcher`/`EmailDeliveryConsumer` until migration completes - if the
new container starts normally first, those hosted services immediately try to poll
`EmailOutboxMessages` (which won't exist until migration runs) and consume `email-delivery`
under whatever policy (or lack of one) the broker currently has. Running the migration and
policy step with no API process running at all closes that gap entirely, using only tooling
that already existed rather than a new code-level readiness gate. Not verified on the VPS -
only locally, and only as a documented procedure (the actual `migrator` invocation against
the real Dokploy Compose project has not been run by the agent).

### Domain-tab traffic block replaced with a VPS firewall block (2026-09-10)

The previous entry above assumed disabling the API's Dokploy domain "immediately stops new
inbound HTTP requests" for this Compose-deployed app. Checked against
[Dokploy's own domain documentation](https://docs.dokploy.com/docs/core/domains) and found
wrong: Docker Compose services "must redeploy... for domain changes to take effect" because
Compose "does not support hot reloading of domain configurations" - the opposite of
Applications, where domain changes apply immediately via Traefik's file provider. A redeploy
here would rebuild and recreate the `api` container, killing the still-draining old
dispatcher/consumer (they run in-process inside the API, not as separate workers) and
risking the new consolidated code starting before the migration and broker policy are ready
- exactly what the cutover exists to avoid.

The runbook's step 1 now blocks at the VPS firewall (`sudo ufw deny 80/tcp`/`443/tcp`, the
ports Dokploy's shared `dokploy-traefik` container publishes) instead, verified from outside
the VPS with `curl` against both the domain and the API's directly-published port 8080
(`docker-compose.yml`'s `api` service publishes `8080:8080` - a `Program.cs` comment claims
ufw already blocks external access to it, which this check verifies rather than assumes).
Whether that port is actually closed had never been independently confirmed before this
pass. Not verified on the real VPS - only against Dokploy's and RabbitMQ's official docs and
this repo's own Compose file.

Also clarified: the `migrator` service builds from whatever commit is checked out in
Dokploy's own Compose working directory on disk, not from whatever Dokploy's UI shows as
"last deployed," and Dokploy's Deploy button for a Compose app always fetches new code and
starts every non-tool-profile service (including `api`) together - there is no way to
decouple the two through Dokploy alone. The runbook now advances that checkout directly with
`git fetch`/`git reset --hard` before invoking the migrator, then starts `api` with a direct
`docker compose up -d --build` rather than through Dokploy's Deploy button, so the
migrate-then-start ordering is guaranteed rather than assumed. Not verified on the real VPS -
the working-directory/project-name discovery via `docker inspect`'s Compose labels, and the
git/`docker compose` sequence built on it, are documented procedure only.
