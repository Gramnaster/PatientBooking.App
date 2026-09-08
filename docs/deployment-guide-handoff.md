# Handoff: revise the reusable deployment guide

Prepared 2026-09-08 for Terra-High. This is a documentation task, not a request to deploy or change the application.

## Assignment

Edit this existing Obsidian note in place:

`C:\Users\jocvi\Documents\Collection-of-Folders\Obsidians\Obsidian Vault\notes\Programming\00Projects\Phase01-FinHealth\Complete-Deployment.md`

Make it a practical, reusable guide for preparing and deploying future ASP.NET Core / EF Core applications on a Hostinger VPS through Dokploy Docker Compose. Preserve PatientBooking as the concrete SQL Server example, and add clearly identified PostgreSQL guidance for future applications. The user will handle remote terminal commands themselves.

Read the existing note before editing: it already contains the Dockerfile, Compose services, manual migration mode, forwarded headers, environment variables, and a commit timeline. Do not duplicate these sections blindly. Preserve useful Obsidian links, especially `[[Connecting-To-VPS]]`; keep VPS provisioning in that linked note.

The target is outside the current repository's writable sandbox. Start the editing session with the vault available as a writable workspace, or use the applicable approval mechanism for that specific edit. This handoff does not imply the note has been edited.

## Completed and verified context

Repository: `C:\Users\jocvi\Documents\Collection-of-Folders\Programming\CSharp\PatientBooking.App`.

- The reported `/api/clinic` failure was HTTP 500. Supplied logs showed SQL error 4060: unable to open `PatientBookingDb` for login `sa`, connecting to `sqlserver,1433`.
- The user queried `sys.databases` inside the SQL Server container; the query returned zero rows for `PatientBookingDb`. The database was missing on that instance.
- The request reached the controller/database code. A separate routing 404 was not established by that evidence; do not describe every 404 as the same database failure.
- Local changes added explicit manual migration mode: `dotnet PatientBooking.Api.dll --migrate`.
- This command reuses the application's configuration and DI, invokes `Database.MigrateAsync()`, and exits before admin seeding or web-server startup. Normal startup does not migrate. Failures set exit code 1.
- The runtime Docker image supports the command without installing the SDK or `dotnet-ef`. The optional Compose `migrator` service runs the same command under the `tools` profile.
- Release API build and Compose configuration validation passed in the preceding implementation work. There has been no Docker image build or real database migration test by this agent, and no remote deployment verification.
- At handoff creation, branch is `master`; `Dockerfile`, `PatientBooking.Api/Program.cs`, and `docker-compose.yml` have uncommitted changes, and `docs/` is untracked. Recheck current state before describing anything as committed or deployed.

Read these files as implementation evidence:

- `Dockerfile`, `docker-compose.yml`, `.dockerignore`, `.env.example`
- `PatientBooking.Api/Program.cs`
- `PatientBooking.Api/appsettings.json` and `appsettings.Production.json`
- `Directory.Packages.props`, DbContext and migration files in `PatientBooking.Api.Domain`
- `docs/deployment.md`

Do not read or copy actual secret values into the guide. Do not modify application code during this documentation assignment.

## Required outcome

1. Explain the common deployment flow, then distinguish first deployment, routine code deployment, and a deployment containing schema changes. Include concise preparation and verification checklists with expected results.
2. Keep routine migrations manual and simple: after deploying an image containing the reviewed migrations, open Dokploy's **api container terminal**, work in `/app`, and run:

   ```sh
   dotnet PatientBooking.Api.dll --migrate
   ```

   Label this as a custom application command implemented in this repo, not a built-in .NET command. A future application must implement that mode and substitute its DLL name. State what the command supports (pending forward migrations) and what it does not (authoring migrations or selecting rollback targets).
3. Explain local migration authoring and committing the migration files before image deployment. Verify exact `dotnet ef` project/startup-project arguments from this repository. Explain why literal `dotnet ef database update` needs tooling and project files absent from this runtime image.
4. Cover first-deployment bootstrapping: database server readiness does not prove the application database/schema exists. If admin seeding prevents API startup, the API terminal is unavailable; use the optional migrator service with the correct deployed image/project/environment, then restart the API. Do not invent Dokploy UI actions or deployment paths; verify them against current documentation.
5. Put the longer Compose fallback in a recovery section. The user's earlier command failed because they ran it in `/home/jpcvillalon`, where `.env` did not exist. Explain shell working directories, actual deployment files, and Compose project identity. Do not present `patientbooking-api-qns9o0` or any inferred path as a universal value. Avoid accidentally creating another project's database volume. Ensure the fallback uses the intended current image (build when necessary).
6. Label every command's execution location: local repo terminal, Dokploy API terminal, database terminal, or VPS host terminal. Use copyable commands with explicit placeholders. Avoid requiring SSH for routine migrations.
7. Add a concise troubleshooting table distinguishing proxy routing/404, application 404, application 500 with DB error, database readiness versus schema readiness, missing `.env`, and unavailable API terminal. Check `/api/clinic` after migration. Production OpenAPI/Scalar routes are not automatically available; verify code before naming documentation URLs.
8. Include proportionate operational preparation: persistent database storage, backup/restore procedure before risky schema changes, writable/persistent application key storage where needed, secret configuration, proxy routing and port exposure. Distinguish currently implemented behavior from recommendations and unverified server settings. Do not expand into enterprise compliance or unrelated architecture refactors.

## PostgreSQL scope and differences

The overall build/deploy/manual-migrate workflow is reusable. PostgreSQL is not just a connection-string replacement. Npgsql uses `Npgsql.EntityFrameworkCore.PostgreSQL` and `UseNpgsql`; EF migrations are scaffolded for the active provider. See [Npgsql provider documentation](https://www.npgsql.org/efcore/) and [EF Core provider-specific migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers).

Add a short comparison and a verified PostgreSQL setup example covering:

- Provider/package compatibility with the chosen .NET/EF version; connection-string syntax and service/port settings.
- PostgreSQL container environment variables, initial database/user provisioning, persistent volume mount path for the selected major image version, and readiness check. Verify these against that image's official documentation; do not assume volume paths are identical across versions.
- Database CLI, backup and restore tooling, credentials and migration permissions.
- Provider-specific migration SQL and model configuration: inspect SQL Server defaults, filtered indexes, identity annotations, raw SQL, and data/time mappings before claiming portability.
- A fresh future PostgreSQL application versus moving an existing SQL Server database and its data. The latter is a separate migration project, not this documentation task. Do not delete or regenerate existing SQL Server migrations as part of this handoff.

Do not implement PostgreSQL support in PatientBooking or add dual-provider architecture just to illustrate the guide.

## Existing note claims to audit

These are review targets, not verified descriptions of the VPS:

- The note suggests `ssh -L 1433:localhost:1433 hostinger` while declaring no database host port mapping. Explain what endpoint the tunnel could actually reach; avoid presenting an unavailable host listener as working.
- It says blocking 8080 with UFW makes published `8080:8080` safe and justifies trusting all forwarded headers. Verify Docker firewall behavior and proxy trust requirements; do not rely on that assertion without evidence. Document safer intended network exposure without silently changing Compose.
- It claims local Compose works unmodified despite requiring external `dokploy-network`. Check and document the prerequisite or a suitable local configuration approach.
- It describes admin seeding as always querying at startup. Inspect the conditional seed configuration before restating that.
- Several references say “see §5” for migrations, which are actually in §6. Fix cross-references as the structure changes.
- The commit timeline includes `ec339d2` as the manual-migration implementation, while the current local worktree still contains the changes. Verify with Git or remove unnecessary historical claims. Do not invent commit/deployment history.
- Replace speculative reasons for “rejected alternatives” with the actual user preference: explicit manual migration with little terminal ceremony. Applying pending migrations does not imply every restart would invent a new schema change.

## Sources and validation

Use primary, current documentation for technical claims. Starting points:

- https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying
- https://learn.microsoft.com/en-us/ef/core/cli/dotnet
- https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers
- https://www.npgsql.org/efcore/
- https://docs.dokploy.com/docs/core/docker-compose
- https://docs.docker.com/compose/how-tos/profiles/
- https://docs.docker.com/engine/network/packet-filtering-firewalls/
- https://github.com/docker-library/docs/blob/master/postgres/README.md

Finish by checking the note against current repo files, command locations, placeholders, section links, and cited documentation. Clearly label examples not executed. No production commands, deployment, commits, or database changes are requested. Report the edited note's path and the meaningful corrections made.
