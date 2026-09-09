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
