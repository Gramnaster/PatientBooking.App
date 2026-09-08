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
source for the full procedure. The [earlier guide handoff](../../docs/deployment-guide-handoff.md)
is historical context, not proof of current source or VPS state.

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
  Current Compose does not mount `/app/keys`; check Data Protection key persistence
  separately before treating container recreation as preserving all application state.

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
