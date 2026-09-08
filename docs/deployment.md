# Manual database migrations

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
