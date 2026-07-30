# PatientBooking.App

Patient booking API — solo, personal/learning project. Governed by dotnet-claude-kit
(`.claude/rules/*.md`, `.claude/skills/*`, `.claude/agents/*` — see `AGENTS.md` for routing).
This file adds project-specific decisions the generic rules don't know about.

## Architecture: Clean Architecture + MVC Controllers

Four-project layer split, dependencies point inward:

```
PatientBooking.Api.Domain        → Domain (currently also hosts EF Core/SQL Server — see note below)
PatientBooking.Api.Application   → Application (currently also hosts infra clients — see note below)
PatientBooking.Api.Common        → shared contracts/DTOs/enums, no business logic
PatientBooking.Api               → Presentation — MVC Controllers
```

**Presentation is traditional MVC (Controllers), by explicit choice — not Minimal API.**
`Program.cs`'s `AddControllers()`/`MapControllers()` is the target state, not scaffold debt to
migrate away from. Deliberate call: this matches what most established C# enterprise shops
actually run (a lot of hiring-relevant .NET codebases predate Minimal API and stay on
Controllers), even though Microsoft's own default guidance since .NET 6 leans Minimal API for
greenfield work. Controllers stay thin — delegate to Application-layer use cases/handlers, no
business logic in the action method (same "thin endpoint" discipline as Minimal API, just with
`[ApiController]` + attribute routing instead of `IEndpointGroup`).

**Known conflict with `.claude/rules/architecture.md` — left unresolved on purpose.**
That file currently states Controllers are an anti-pattern and "IEndpointGroup + auto-discovery
is the only accepted pattern." This project deliberately overrides that for the Presentation
layer. Consequence: the `code-reviewer` agent and `arch-check` skill will likely flag Controllers
as a rule violation until `architecture.md` is either updated or given an explicit exception for
this project. This file (`CLAUDE.md`) is the source of truth for this project's actual choice —
if a future session sees that conflict, defer to this file, not `architecture.md`, for the
Controllers-vs-Minimal-API question specifically.

**Known layer-naming drift (flagged, not yet fixed):** `Api.Domain` currently holds
`PatientBookingDbContext`, SQL Server/EF Core packages, and an Identity/Migrations/Security
folder set — that's Infrastructure-layer content living in a project named Domain. `Api.Application`
holds Dapper, RabbitMQ.Client, MailKit/MimeKit, and Google.Apis.Auth — external service clients,
also Infrastructure concerns. There is no dedicated Infrastructure project. This is fine to leave
as-is for a solo/learning project, but if the layer split ever needs to be enforced by project
references (per `architecture.md`'s "module boundaries via project references" rule), the fix is
extracting a `PatientBooking.Api.Infrastructure` project rather than renaming in place. Don't do
this extraction unprompted — it's a deliberate, explicit ask.

**Domain complexity:** moderate business rules (double-booking prevention, cancellation
windows, provider availability) — not a rich domain model. Entities should carry behavior for
these rules (see `clean-architecture` skill's `Order.Create`/`Cancel` pattern), but don't invent
aggregates or domain events beyond what a given rule actually needs.

## Tech Stack (as configured)

| Concern | Choice | Notes |
|---|---|---|
| Database | SQL Server via EF Core | `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.Data.SqlClient` |
| Raw SQL | Dapper | Alongside EF Core in `Api.Application` — use for reporting/complex reads only, not CRUD |
| Auth | JWT Bearer + ASP.NET Identity (EF Core store) + Google OAuth | `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Google.Apis.Auth`, `System.IdentityModel.Tokens.Jwt` |
| Caching | `Microsoft.Extensions.Caching.Hybrid` (HybridCache) | Matches `performance.md` — use over `IMemoryCache` directly |
| Messaging | `RabbitMQ.Client` (raw) | No Wolverine/MassTransit wrapper yet — if outbox/saga patterns become necessary, evaluate the `messaging` skill before hand-rolling |
| Mapping | Riok.Mapperly (source generator) | Not AutoMapper — compile-time, no reflection |
| Validation | FluentValidation | Per `error-handling.md` — validate at the API boundary |
| Email | MailKit / MimeKit | |
| Observability | Serilog (console + file + Seq) + OpenTelemetry (traces + metrics, OTLP export) | Already wired in `Program.cs` |
| API docs | Built-in `Microsoft.AspNetCore.OpenApi` + Scalar | Not Swashbuckle |
| Versioning | `Asp.Versioning.Mvc` + `Asp.Versioning.Mvc.ApiExplorer` | Correct package family for Controllers — no migration needed |

## Compliance Scope

Treated as a **personal/learning project**, not HIPAA-regulated, despite handling patient-shaped
data. Apply the correctness/security fundamentals from the global and project rule files
(nullable, parameterized queries, no secrets in source, `[Authorize]`/`[AllowAnonymous]` explicit)
but skip the full Enterprise Constraints tier (audit trails, PII-at-rest encryption mandates,
requirements traceability) unless the project's purpose changes. Revisit this if the project
moves toward a real deployment with real patient data.

## Team Size

Solo. Optimize for velocity over process — no PR ceremony, no multi-reviewer gates. Architecture
conformance (`arch-check` skill) is still worth running periodically since there's no second
reviewer to catch drift.

## Testing

No test project exists yet. Per `testing.md`: xUnit v3 + `WebApplicationFactory` + Testcontainers
(real SQL Server container, not `UseInMemoryDatabase`) is the default — confirm this is still
correct when the first test project is scaffolded rather than assuming silently.

## MCP Setup

`CWM.RoslynNavigator` is installed as a global `dotnet tool` (confirmed via
`dotnet tool list -g`) — no `.mcp.json` needed in this repo; it's configured outside the repo by
design (see `.claude/dotnet-claude-kit-SOURCE.md`).
