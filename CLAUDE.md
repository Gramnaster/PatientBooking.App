# PatientBooking.App

Patient booking API — solo, personal/learning project. Governed by dotnet-claude-kit
(`.claude/rules/*.md`, `.claude/skills/*`, `.claude/agents/*`). This file is the canonical
instruction document for every coding agent working in this repository: it contains both the
shared agent routing and the project-specific decisions the generic rules don't know about.

## Shared Agent Routing & Orchestration

These instructions apply to Claude Code, Codex, and any other coding agent. Reuse the Markdown
under `.claude/rules/`, `.claude/skills/`, `.claude/agents/`, and `.claude/knowledge/` directly;
do not create parallel copies for another runtime.

Project-specific choices in this file override generic kit defaults. In particular, this
application uses ASP.NET Core MVC controllers even where a generic kit rule prefers Minimal APIs.

### Runtime-specific integration

- Claude Code uses the existing `.claude` settings, hooks, agents, skills, and slash-command wiring.
- Codex uses `.codex/config.toml`, `.codex/agents/*.toml`, and the discovery adapter under
  `.agents/skills/`. These files only register or point to the shared instructions; they must not
  become a second source of project policy.
- Claude-only frontmatter, model aliases, memory paths, worktree isolation, tool names, hook
  wiring, and slash-command registration are integration metadata. Codex should use its native
  equivalents while following the shared Markdown guidance.
- Claude model-selection guidance lives in `.claude/rules/agents.md`. Codex should ignore
  Claude-specific model aliases there and use the current Codex session configuration.

### Agent roster

| Agent | Canonical playbook | Primary domain |
|---|---|---|
| dotnet-architect | `.claude/agents/dotnet-architect.md` | Architecture, project structure, module boundaries |
| api-designer | `.claude/agents/api-designer.md` | APIs, OpenAPI, versioning, rate limiting |
| ef-core-specialist | `.claude/agents/ef-core-specialist.md` | Database, queries, migrations, EF Core patterns |
| test-engineer | `.claude/agents/test-engineer.md` | Test strategy, xUnit, WebApplicationFactory, Testcontainers |
| security-auditor | `.claude/agents/security-auditor.md` | Authentication, authorization, OWASP, secrets |
| performance-analyst | `.claude/agents/performance-analyst.md` | Benchmarks, memory, async patterns, caching |
| devops-engineer | `.claude/agents/devops-engineer.md` | Docker, CI/CD, Aspire, deployment |
| code-reviewer | `.claude/agents/code-reviewer.md` | Multi-dimensional code review |
| build-error-resolver | `.claude/agents/build-error-resolver.md` | Autonomous build-error fixing |
| refactor-cleaner | `.claude/agents/refactor-cleaner.md` | Systematic dead-code removal and cleanup |

### Routing table

Match user intent to the first applicable primary agent.

| User intent | Primary agent | Support agent |
|---|---|---|
| Project setup, structure, architecture, modules, bounded contexts | dotnet-architect | — |
| Feature scaffolding or architecture-appropriate feature creation | dotnet-architect | api-designer, ef-core-specialist |
| API route, controller/endpoint, OpenAPI, versioning, rate limiting, CORS | api-designer | — |
| Database, migration, query, DbContext, EF, NuGet/package upgrade | ef-core-specialist | — |
| Tests, strategy, coverage, xUnit, WebApplicationFactory, Testcontainers | test-engineer | — |
| Security, authentication, JWT, OIDC, authorization | security-auditor | — |
| Performance, benchmark, memory, profiling, caching | performance-analyst | — |
| Docker, containers, CI/CD, deployment, Aspire, service discovery | devops-engineer | — |
| Code/PR review, project health, conventions, consistency, refactoring advice | code-reviewer | dotnet-architect when architectural |
| Build or test failures, bounded automatic repair loop | build-error-resolver | — |
| Dead/unused code and systematic cleanup | refactor-cleaner | — |

Architecture questions take precedence over implementation routing. Specific domain expertise
takes precedence over general expertise. Surface security concerns even when another role is
primary. Code review should load architecture and domain context based on the reviewed files.

### Skill loading

Load skills in dependency order:

1. Read `.claude/skills/modern-csharp/SKILL.md` for all .NET work.
2. Read the selected agent's playbook.
3. Read the relevant agent-specific skills below and any files they directly reference.

| Agent | Skills |
|---|---|
| dotnet-architect | modern-csharp, architecture-advisor, project-structure, scaffold, project-setup; conditionally vertical-slice, clean-architecture, ddd |
| api-designer | modern-csharp, minimal-api, api-versioning, authentication, error-handling |
| ef-core-specialist | modern-csharp, ef-core, configuration, migrate |
| test-engineer | modern-csharp, testing |
| security-auditor | modern-csharp, authentication, configuration |
| performance-analyst | modern-csharp, caching |
| devops-engineer | modern-csharp, docker, ci-cd, aspire |
| code-reviewer | modern-csharp, code-review, convention-learner; contextual clean-architecture and ddd |
| build-error-resolver | modern-csharp, build-fix; contextual ef-core and dependency-injection |
| refactor-cleaner | modern-csharp, de-sloppify; contextual testing and ef-core |

Cross-agent skills:

- `instinct-system`: user corrections, recurring non-obvious discoveries, status/export/import.
- `wrap-up`: session handoff lifecycle using `.claude/handoff.md`.
- `checkpoint`: explicit mid-session save before risky changes or task switches.
- `workflow-mastery`: context pressure, large-codebase navigation, or parallel workflows.
- `convention-learner`: detect and enforce project-specific conventions.

### Roslyn MCP preferences

Prefer available Roslyn MCP operations over broad file scanning for symbol definitions,
references, implementations, type hierarchies, project graphs, public APIs, diagnostics, dead
code, circular dependencies, call chains, test coverage maps, anti-patterns, callers, overrides,
NuGet packages, endpoint maps, and DI registrations. Use `rg`, focused source inspection, and
normal `dotnet` commands when the corresponding MCP operation is unavailable.

### Shared workflow names

The following names identify the shared kit workflows. Claude Code may expose them as slash
commands; other runtimes should treat them as named workflows and load the corresponding skill:

| Workflow | Supporting skill/role | Purpose |
|---|---|---|
| `/dotnet-init` | project-setup, dotnet-architect | Interactive project initialization |
| `/spec` | shared workflow | Questioning to an agreed spec under `docs/specs/` |
| `/plan` | architecture-advisor, dotnet-architect | Architecture-aware planning |
| `/verify` | shared workflow | Seven-phase verification pipeline |
| `/tdd` | testing, test-engineer | Red-green-refactor workflow |
| `/scaffold` | dotnet-architect | Architecture-aware feature scaffolding |
| `/code-review` | convention-learner, code-reviewer | Blast-radius-prioritized review |
| `/build-fix` | build-error-resolver | Bounded build/test repair loop |
| `/checkpoint` | checkpoint | Mid-session commit and handoff |
| `/security-scan` | security-auditor | OWASP, secrets, and dependency audit |
| `/migrate` | ef-core, ef-core-specialist | EF Core, schema, .NET, and NuGet migrations |
| `/health-check` | code-reviewer | Graded project health report |
| `/de-sloppify` | refactor-cleaner | Systematic cleanup |
| `/wrap-up` | wrap-up, instinct-system | Session handoff lifecycle |
| `/outdated` | outdated | Dependency staleness, CVEs, and licenses |
| `/arch-check` | architecture-advisor, dotnet-architect | Architecture conformance verification |

### Context and response defaults

- Small task: load one or two relevant skills and use focused Roslyn/source inspection.
- Medium feature: load three or four relevant skills and inspect existing structure first.
- Large architecture review: load all relevant skills and start with the project graph.
- Start with the recommended approach, show implementation before extended explanation, surface
  relevant anti-patterns, and reference the shared skill used for deeper methodology.

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

For deployment, Docker/Dokploy troubleshooting, or deployed database migrations, read
[ASP.NET Core deployment knowledge](.claude/knowledge/aspnet-dokploy-deployment.md)
and the [deployment runbook](docs/deployment.md). Preserve the documented manual migration
workflow and distinguish verified local configuration from remote deployment assumptions.

For RabbitMQ, outbox, consumer retries, or background email work, read
[RabbitMQ email delivery knowledge](.claude/knowledge/rabbitmq-email-delivery.md).
Preserve its failure-handling lessons and distinguish booking notifications from the
still-separate Identity email flows.

| Concern | Choice | Notes |
|---|---|---|
| Database | SQL Server via EF Core | `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.Data.SqlClient` |
| Raw SQL | Dapper | Alongside EF Core in `Api.Application` — use for reporting/complex reads only, not CRUD |
| Auth | JWT Bearer + ASP.NET Identity (EF Core store) + Google OAuth | `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Google.Apis.Auth`, `System.IdentityModel.Tokens.Jwt` |
| Caching | `Microsoft.Extensions.Caching.Hybrid` (HybridCache) | Matches `performance.md` — use over `IMemoryCache` directly |
| Messaging | `RabbitMQ.Client` (raw) | Booking transactional outbox, confirmed publishing, and consumer deduplication; see RabbitMQ knowledge above for guarantees and limits |
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

`PatientBooking.Api.Tests` contains xUnit v3 messaging tests using Testcontainers with
real SQL Server, RabbitMQ, and smtp4dev. See RabbitMQ knowledge above for coverage,
runner lessons, and verification gaps. These worker tests do not establish HTTP endpoint
coverage; use `WebApplicationFactory` when endpoint-level verification is needed.

## MCP Setup

`CWM.RoslynNavigator` is installed as a global `dotnet tool` (confirmed via
`dotnet tool list -g`) — no `.mcp.json` needed in this repo; it's configured outside the repo by
design (see `.claude/dotnet-claude-kit-SOURCE.md`).
