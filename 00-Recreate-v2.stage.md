# ASP.NET Core Web API Recreation Plan v2

> [!abstract]
> This is the canonical build order and lesson-authoring contract for recreating a production-shaped
> ASP.NET Core Web API from an empty solution. It frontloads reusable foundations, makes every
> prerequisite explicit, and keeps every numeric lesson independently buildable and verifiable.
> The old lesson names are evidence sources only; they are never instructions to leave this sequence.

## Status and scope

- **Status:** sequence and execution contract. The numbered tutorial files have not all been written.
- **Target reader:** a junior C# developer with basic ASP.NET Core experience.
- **Target implementation:** .NET 10, MVC controllers, a five-project Clean Architecture solution,
  one selected relational provider, and the local ASP.NET Core Identity learning branch unless step
  04 records a different identity authority.
- **Primary writing rules:**
  `C:\Users\jocvi\Documents\Collection-of-Folders\Obsidians\Obsidian Vault\notes\Programming\Csharp\Ultimate-ASPNET-Core-Web-API\CLAUDE.md`.
- **Regression ledger:** `RECREATE/01-Recreation-Notes.md`. It records defects to prevent, not a
  second build order.
- **Historical runsheet:** `RECREATE/00-Recreate.md`. It supplies source material and proven code,
  but this file owns the new order.

This plan is intentionally not a collection of copy-paste implementation snippets. An agent must
author or revise exactly one numbered lesson from its contract, verify that lesson against the
selected clean solution and primary documentation, and only then use the finished lesson to change
the application. A request to “implement this plan” never authorizes skipping directly from these
summaries to guessed application code.

The notation is:

```text
New lesson number and name (old source lesson names): responsibility owned by the new lesson
```

The labels below are logical plan ids, not final filename prefixes. Finished lesson filenames obey
`CLAUDE.md`'s `000` Index / `001` Glossary / content-starts-at-`010` rule through this fixed mapping:

| Logical plan ids | Final filename prefixes |
|---|---|
| 00–05 | 010–015 |
| 10–16 | 020–026 |
| 20–24 | 030–034 |
| 30–37 | 040–047 |
| 60–67 | 060–067 |
| 200–501 | unchanged |

A companion keeps the mapped number plus `B`: logical `05B` becomes `015B`; logical `203B` remains
`203B`. Prose inside this plan uses logical ids (`step 20`); prerequisites, conclusions, filenames,
and Obsidian wikilinks in authored lessons use the mapped prefix (`[[030-...]]`). The author also
maintains `000-Index.md` and `001-Glossary.md`; the Index is finalized only after the lesson set is
stable.

---

## Sequencing rules

- The number is an ordered, expandable slot in the new recreation plan. The old name in parentheses is source material, not a prerequisite link to follow.
- Number ranges group related work and deliberately leave room for later lessons: 00 project/runtime and architecture decisions, 10 host behaviour, 20 database, 30 API delivery, 60 account identity, 200 account hardening, 300 feature/data access, 400 performance/integrations, and 500 deployment. Do not fill a gap merely to make the list consecutive.
- Each lesson must end in a buildable, verifiable state. A later lesson may extend a type, but an earlier lesson must never show fields, methods, registrations, or enforcement points that do not exist yet.
- The target architecture is a five-project Clean Architecture solution with MVC controllers and
  thin services/controllers: `PatientBooking.Api`, `.Application`, `.Domain`, `.Infrastructure`,
  and `.Common`. Do not reintroduce a Vertical Slice or Minimal API track into this run.
- Step 00 teaches the complete project responsibility and dependency map; step 01 creates that exact
  graph. Every later lesson lists exact project-relative paths for its delta and gives a one-sentence
  ownership reason when it introduces a new kind of artifact. Repeat only the relevant branch of the
  tree, not the full solution tour, so placement stays explicit without becoming noise.
- Each rewrite uses the real current project as implementation evidence, not as an architectural
  template to copy blindly. The current `UsersService`, four-project layer drift, and any other debt
  already identified by this plan are negative evidence: preserve verified behavior while applying
  the new ownership contract. HotelListing-specific choices are explicitly marked as project
  decisions, never presented as universal Identity requirements.
- Supporting correction notes are merged into their owning lesson. The new run must not require readers to discover a Mapperly correction, a patch callout, or a future auth lesson on their own.
- Before authoring or revising a lesson, consult `RECREATE/01-Recreation-Notes.md` as a historical defect and regression ledger for its old source lesson. Convert relevant observations into current acceptance checks, and classify each as carried forward, already resolved, not applicable, or rejected. Do not treat old numbering, HotelListing-specific choices, or a proposed fix from those notes as requirements when they conflict with this plan, the current project, or current primary documentation.
- Every selected linear path uses all-numeric lessons only. The plan may label an entire numbered
  lesson as core, optional, or an alternative path, but the next selected numbered lesson must build
  and verify without any `B` companion having been read or executed.
- A same-number `B` companion holds optional, conditional, or additional guidance attached to its
  numbered lesson: product-policy variations, alternate implementation options, legacy-data
  backfills, repair procedures, production operations, and expanded troubleshooting. For example,
  `65-Account-Profiles-and-Default-Assignment` may have
  `65B-Account-Profile-Variations-and-Operations`.
- `B` companions are not curriculum steps, do not consume a numbered slot, and are never prerequisites
  for a later numbered lesson. A numbered lesson's implementation, verification, exercise, and
  conclusion stay on the numeric path; its conclusion may include one short link to the companion.
  An optional chain may depend on an earlier `B` companion only when that dependency is explicit and
  remains isolated from every numeric path.
- An independently executable feature with its own setup, implementation, and verification remains a
  numbered lesson even when it is optional or one of several deployment paths. Do not turn features
  such as Dapper reporting, SignalR, RabbitMQ, broader PII encryption, or a deployment target into a
  `B` file merely because a project may skip them.
- Frontloaded packages are installed once. A later lesson that uses one confirms the earlier installation instead of repeating the command; a genuinely new package includes its trigger condition.
- The identity/authentication model and relational database provider are explicit project decisions before infrastructure or persistence code. Later lessons execute the selected branches; they do not silently assume that every project owns passwords or that every EF Core project uses SQL Server.
- Database flexibility belongs to the runsheet, not automatically to the generated application. Choose exactly one provider for an ordinary project—SQL Server/Azure SQL or PostgreSQL—then use its package, EF registration, migrations, container, tests, health verification, operational guidance, and deployment target consistently. Supporting both providers at runtime is a separate product requirement that owns separate migrations and a full test matrix.
- End-to-end API verification updates the project's `.http` file first and provides an equivalent `curl` request as the fallback. Tokens and secrets come from the selected local secret/environment mechanism, never from committed literal values.
- A security lesson is not complete when only its happy path works. It must enumerate every authentication entry path it affects, state what proof the caller has before and after the operation, attach its endpoint-specific abuse controls in the same lesson, and verify invalid, replayed, concurrent, cross-account, revoked, locked, and deleted-account cases that apply.
- An existing bearer session is not fresh proof for a sensitive account change. Adding, replacing, or removing an authenticator; linking or unlinking an external identity; changing a password or email; deleting an account; and performing security-sensitive administration require the current password, an assertion from an already-enrolled passkey, or the currently enrolled MFA factor as specified by the owning lesson.
- `SecurityStamp` behavior must be described narrowly. It invalidates Identity tokens and periodically validated Identity sessions; it does not retroactively revoke this plan's self-contained access JWTs or custom refresh-token rows. Every password reset, account deletion, and comparable security event states its explicit refresh-session revocation behavior and the accepted residual access-token window.
- Anonymous account endpoints must not disclose account existence through status, body, log content, or materially different response timing. Registration, login, confirmation resend, password recovery, refresh, pending-MFA verification, and external login own their named rate-limit policy when introduced; step 420 may tune those policies but cannot be their first enforcement point.
- Browser, native/mobile, and machine clients do not share one token-storage answer. Any lesson that issues a refresh token must state the supported client profile: browser clients use a same-origin BFF or `HttpOnly; Secure; SameSite` cookie design with CSRF protection; native clients use platform secure storage; tutorial `.http`/`curl` requests may receive the token in a response body but must not teach `localStorage` or `sessionStorage` as production storage.
- Security-event notifications never contain credentials, raw tokens, TOTP seeds, recovery codes, or reset links. Notify on password reset, new authenticator/passkey, authenticator removal or reset, external-login link/unlink, recovery-code use, refresh-token reuse, and account deletion. Store only the minimum session/device metadata needed to help the user recognize activity.
- When a test project exists, every lesson runs the relevant tests. The first test-baseline lesson uses the selected project's verified framework and real database provider rather than silently substituting an in-memory database.
- Use nested outlines and tables for flows and relationships. Do not add diagrams.

## Source precedence and execution-agent protocol

Use this precedence when sources disagree:

1. The selected branch and ownership rules in this plan.
2. The vault-wide tutorial contract in `CLAUDE.md`.
3. Current primary framework, protocol, provider, and package documentation.
4. The current PatientBooking implementation and its executable behavior.
5. `RECREATE/01-Recreation-Notes.md` as a regression ledger.
6. Old tutorial files and HotelListing source as historical evidence.

The current project wins questions about proven behavior and exact failure modes. This plan wins
questions about the clean target architecture, lesson order, service boundaries, and defects that
must not be reproduced. Old tutorial prose never overrides code, a later primary API, or this plan.

An agent working from this file follows one of two explicit modes:

### Author mode: create or revise one tutorial lesson

1. Receive one exact numeric lesson id. Never author a range, its `B` companion, or the next lesson
   in the same task unless the request names it. Required glossary additions and the eventual Index
   entry are supporting edits, not permission to author another lesson.
2. Read the complete plan contract for that lesson, `CLAUDE.md`, the relevant regression-ledger
   section, every old source lesson named in parentheses, and every directly affected current source
   file. Read current project/package/configuration files before selecting versions or paths.
3. Write the dependency manifest below before writing prose or code. If any prerequisite is supplied
   only by a later lesson, stop and repair this plan instead of inserting a stub or forward call.
4. Create the lesson in the voice and structure required by `CLAUDE.md`. Show the complete current
   shape of every small or multi-touch file at this lesson's checkpoint. Never use `...`, pseudocode,
   an undeclared helper, or a final DTO/service shape from a later lesson.
5. Rehearse the lesson against a clean checkpoint of the selected branch. Run every stated command,
   build, migration, test, `.http` request, and failure case that the environment permits. Do not
   print expected output that was not observed; label externally blocked proof as unverified.
6. Update the lesson, its `.http` requests, and the regression classification until the manifest and
   proof matrix agree. Finish with the exact next numeric lesson and no hidden companion dependency.

### Execution mode: apply one finished tutorial lesson

1. Require an authored numeric lesson, not only this sequence summary. Confirm that the repository
   matches the lesson's stated starting checkpoint and selected database/identity branch.
2. Snapshot `git status`, project graph, package inventory, options/configuration, DI registrations,
   endpoints, migrations, and relevant tests. Preserve unrelated user changes.
3. Apply only the files and behavior owned by that lesson. Run each local proof before continuing to
   the next numbered section. A failed prerequisite returns control to author mode; it is not filled
   in from memory or copied from the finished reference project.
4. Complete the lesson's build, tests, migration verification, `.http` path, negative cases, and
   final diff audit. Stop after that lesson and report the exact next checkpoint.

### Mandatory dependency manifest for every numeric lesson

The finished lesson includes this information near its prerequisites. Empty categories say `None`;
they are never silently omitted.

| Category | Required content |
|---|---|
| Starting checkpoint | Previous numeric lesson and the observable state that proves it is complete |
| Selected branches | Identity authority, client profile, database provider, and optional feature path |
| New files | Exact project-relative path, namespace, type, and ownership reason |
| Modified files | Exact path plus members/sections changed in this lesson |
| Current-shape types | Complete end-of-lesson properties and public method signatures for every multi-touch type |
| Packages | `Install now` with verified version and owner, or `Already installed in step NN`; never both |
| Configuration/options | Section name, complete options type, safe committed values, secret source, binding, validation, and startup-failure proof |
| Dependency injection | Service, implementation, lifetime, registration owner, and lifetime justification |
| Database | Entities/configuration/DbSet/indexes/concurrency, migration name, generated files, and clean-plus-upgrade proof |
| HTTP surface | Method, versioned route, request/response DTO, auth posture, policy, success status, and Problem Details failures |
| Observability/security | Stable events/metrics, redaction, enumeration behavior, abuse control, and threat cases owned here |
| Verification | Build/tests, primary `.http` sequence, `curl` fallback, database assertion, and applicable negative/replay/concurrency cases |
| Handoff | Exact files/types later lessons may extend and the next numeric lesson |

Before writing code, the author adds a delta table in this form:

```text
Artifact | Before this lesson | After this lesson | Created/modified here | Verified by
```

This table is the guard against the recurring “finished type copied too early” defect. A field or
method appears in the `After` column only when every dependency it calls is also present and tested
in the same checkpoint.

## Definition of implementation-ready

A numeric lesson is implementation-ready only when all of the following are true:

- every file path, namespace, type, method, helper, option, configuration key, registration, route,
  migration, and test it consumes is either an explicit prerequisite or created earlier in the same
  lesson;
- its source files and package APIs were opened and checked during the authoring session;
- no code block contains ellipses, undeclared helpers, future response fields, or calls completed by
  a later lesson;
- small and multi-touch files end with their complete current shape;
- the selected branch builds with zero errors at each stated compile checkpoint;
- runtime behavior has a primary `.http` proof, a matching `curl` fallback where possible, concrete
  database assertions when persistence changes, and relevant failure diagnostics;
- security work covers every affected entry path plus the applicable invalid, expired, replayed,
  concurrent, cross-account, revoked, locked, and deleted cases;
- the Files Touched list, dependency manifest, implementation steps, code listings, DI map, endpoint
  map, migration list, verification matrix, and conclusion all describe the same delta.

## Primary security references for the rewrites

The security lessons cite primary framework/protocol guidance beside the exact rule they implement rather than relying on a generic "best practices" claim:

- [ASP.NET Core Identity passkeys (.NET 10+)](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/?view=aspnetcore-10.0)
- [ASP.NET Core multifactor authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/mfa?view=aspnetcore-10.0)
- [Microsoft JWT bearer-token guidance](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication)
- [`JsonWebTokenHandler.CreateToken`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.jsonwebtokens.jsonwebtokenhandler.createtoken)
- [`JsonWebTokenHandler.ValidateTokenAsync`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.jsonwebtokens.jsonwebtokenhandler.validatetokenasync)
- [NIST SP 800-63B-4 authenticators and phishing resistance](https://pages.nist.gov/800-63-4/sp800-63b/authenticators/)
- [NIST SP 800-63B-4 password requirements](https://pages.nist.gov/800-63-4/sp800-63b/passwords/)
- [OAuth 2.0 Security Best Current Practice, RFC 9700](https://www.rfc-editor.org/rfc/rfc9700.html)
- [Google ID-token verification for a backend](https://developers.google.com/identity/sign-in/web/backend-auth)
- [Google OpenID Connect claims reference](https://developers.google.com/identity/openid-connect/reference)
- [`GoogleJsonWebSignature.ValidateAsync` .NET API](https://docs.cloud.google.com/dotnet/docs/reference/Google.Apis/latest/Google.Apis.Auth.GoogleJsonWebSignature)
- [OWASP multifactor-authentication guidance](https://cheatsheetseries.owasp.org/cheatsheets/Multifactor_Authentication_Cheat_Sheet.html)
- [OWASP password-recovery guidance](https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html)
- [OWASP session-management guidance](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html)
- [ASP.NET Core Identity recovery-code store source](https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Extensions.Stores/src/UserStoreBase.cs) — use this to avoid the incorrect claim that the default store hashes each recovery code.

## Primary database references for the rewrites

- [EF Core database providers](https://learn.microsoft.com/en-us/ef/core/providers/)
- [EF Core DbContext provider configuration](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)
- [EF Core migrations with multiple providers](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers)
- [Npgsql EF Core provider](https://www.npgsql.org/efcore/)
- [Npgsql optimistic concurrency and PostgreSQL `xmin`](https://www.npgsql.org/efcore/modeling/concurrency.html)

## Primary foundation references for steps 00–29

- [NuGet Central Package Management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)
- [ASP.NET Core configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0)
- [Options binding and startup validation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-10.0)
- [Safe storage of app secrets in development](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0)
- [ASP.NET Core Data Protection key storage providers](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers?view=aspnetcore-10.0)
- [ASP.NET Core API error handling and Problem Details](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0)
- [Serilog.AspNetCore](https://github.com/serilog/serilog-aspnetcore)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/dotnet/)
- [Built-in OpenAPI support in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0)
- [ASP.NET API Versioning](https://github.com/dotnet/aspnet-api-versioning)
- [CORS in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/cors?view=aspnetcore-10.0)
- [Proxy and forwarded-header configuration](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0)
- [ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
- [.NET HTTP resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience)
- [ASP.NET Core health checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0)

## Global tutorial structure

These writing and implementation rules apply to every numeric lesson. The detailed sections below
add topic-specific requirements without weakening this global structure. This plan deliberately
stops short of duplicating the verified code listings that belong in each finished lesson.

Every numeric tutorial must follow the quality standard established by
`74-Account-Profiles-and-Default-Assignment` and `204-Email-Sending-v2`:

1. Add frontmatter, a table of contents, a one-paragraph outcome, prerequisites, the exact starting
   state, the mandatory dependency manifest and delta table, files touched, and package status.
   Package versions are resolved and verified when the lesson is authored, then pinned centrally;
   stale versions from an older note are never copied.
2. Explain why the lesson occurs at this point and identify the later steps that depend on it.
3. Show complete final contents for small files and focused edits with enough surrounding context
   for large files. Never use `...` where the learner must infer executable code.
4. Put commands beside the change they verify. State the working directory and use the actual
   solution/project names. PowerShell is primary on Windows; portable `dotnet`, Docker, `.http`, and
   `curl` equivalents are included where useful.
5. End each numbered implementation section with a concrete success check and at least one relevant
   failure diagnosis. A compile-only check is insufficient when the step changes runtime behavior.
6. Finish with end-to-end verification, one reasoning exercise with its answer, a summary, the exact
   next lesson, and primary documentation links cited beside the decisions they support. Do not
   claim success from unexecuted commands.
7. Keep the reusable decision separate from the PatientBooking example. The example may select MVC,
   local Identity, SQL Server, and the current project layout; a new project executes the branches
   selected in steps 00, 04, and 05 and deletes the unused instructions.

## Full recreation sequence

### 00 — Project and runtime foundation

- **00-Architecture-and-Project-Contract** (100-Advanced-Architecture-Concepts, 102-Layered-architecture-pattern, CLAUDE.md): Record the MVC Clean Architecture-style project shape, dependency direction, project-specific deviations, and the definition of done for every later lesson.
- **00B-Existing-Solution-Architecture-Adaptation** (new companion): Adapt the contract to an established repository, including project-name/layout mapping, documented debt, and staged boundary repair without making that migration part of the clean numeric path.
- **01-Solution-Skeleton-and-Package-Management** (00-Recreate Phase 0): Create the solution, projects, references, central package management, and the minimal host. Do this before any provider, middleware, or feature code.
- **02-Analyzers-and-Build-Baseline** (230-Analyzers-Setup): Enable nullable, analyzers, warning policy, formatting, and the first clean build before application code accumulates.
- **02B-Analyzer-Policy-and-Legacy-Warning-Burn-Down** (230-Analyzers-Setup companion): Cover alternate severity policies, justified local suppressions, generated-code exclusions, and a bounded warning-reduction plan for an existing codebase.
- **03-Configuration-Secrets-Options-and-Data-Protection-Keys** (101-Options-Pattern): Establish configuration binding, startup validation, user secrets, environment-specific overrides, and the ASP.NET Core Data Protection key-ring policy before any settings class or Identity token provider is consumed. Development may use the local profile-backed key ring; containers and production must persist, restrict, back up, and protect keys outside the image so confirmation/reset tokens and protected Identity values survive restarts and rotations.
- **03B-Production-Key-Stores-Rotation-and-Migration** (101-Options-Pattern companion): Compare approved external key/secret stores and document provider-specific custody, rotation overlap, migration, backup, and recovery operations without changing step 03's configuration contract.
- **04-Identity-and-Authentication-Architecture-Decision** (new): Before installing Identity or issuing tokens, record whether the project has human users, machine clients, or both; whether credential ownership belongs to local ASP.NET Core Identity, an established external OIDC provider, platform identity, or no user identity at all; and whether each client uses a server cookie/BFF, bearer tokens, or OAuth client credentials. Record account-recovery, MFA/passkey, authorization, deployment, and compliance requirements. The default tutorial continues through the local Identity/JWT learning branch, but future projects execute only the applicable branch. Step 490 confirms the production choice; it is not the first time the choice is made.
- **05-Database-Provider-Decision-and-Local-Development-Services** (02-Docker-Compose-Dev-Infra, 00-Docker-SqlServer-Setup, 01-Docker-PostgreSQL-Setup): Select exactly one relational provider for the project—SQL Server/Azure SQL or PostgreSQL—after checking provider support for the selected EF Core major version. Start only its Docker Compose profile, establish one provider-neutral connection-string key, and prove readiness with the provider's native health check. PatientBooking selects SQL Server; the reusable runsheet gives PostgreSQL an equal alternative path rather than installing both.
- **05B-Optional-Local-Development-Service-Profiles** (02-Docker-Compose-Dev-Infra companion): Provide convenience profiles and operational notes for frontloading smtp4dev, Seq, Aspire Dashboard, or RabbitMQ. No numbered lesson assumes this companion ran; step 13, step 200, and step 440 must still introduce or confirm their own required service deterministically.

#### 00 — Architecture and project contract

- **Outcome:** create the canonical project contract before scaffolding. It records the application
  type, actors, domain complexity, expected lifetime, team/compliance constraints, MVC-controller
  presentation style, chosen Clean Architecture variant, dependency direction, and explicit
  deviations. The tutorial explains why the architecture is being selected instead of presenting
  Clean Architecture as a universal default.
- **Required artifacts:** `AGENTS.md` (or the runtime's canonical instruction file) and a short
  architecture decision record such as `docs/decisions/0001-architecture.md`. Include a table for
  each project/layer: responsibility, allowed dependencies, forbidden content, and composition-root
  owner. Record that controllers are thin and that business rules are not controller/service glue.
- **Project-shape decision:** this course uses Domain, Application, Infrastructure, Common, and Api,
  with MVC controllers in Api. Domain owns framework-independent business concepts. Application
  owns use-case contracts, framework-neutral use cases, DTOs, validation, mapping, and ports.
  Infrastructure owns EF Core, Identity persistence and Identity-bound contract implementations,
  migrations, and external-service implementations. Common is a small solution-project-
  independent leaf for genuinely cross-cutting contracts used by multiple projects; it is not a catch-all for
  every enum, helper, or DTO. Api owns HTTP translation and the composition root.
- **Dependency contract:** Domain and Common reference no solution project; Application references
  Domain and Common; Infrastructure references Application, Domain, and Common; Api references
  Application, Infrastructure, and Common. A test project references only the projects required by
  the behavior it verifies. Api never becomes a source of reusable application or domain types.
- **Ownership test:** place a type with the concept that owns it, not with other files sharing its C#
  syntax. For example, `ErrorCodes` may live in `Common/Enums` because several boundaries consume
  the error vocabulary, while `RefreshTokenRevocationReason` belongs in `Domain/Enums` because it
  describes refresh-session state. Infrastructure supplies the EF mapping for that domain value.
- **Implementation order:** inventory requirements; answer the architecture-advisor questions;
  compare the simplest viable alternatives; record the decision and evolution triggers; define
  naming, nullable/time conventions, controller choice, test strategy, database/identity decisions
  still deferred to steps 04–05, and the definition of done for all later lessons.
- **Guardrails:** no package installation, database choice, entity scaffolding, speculative domain
  events, generic repositories, or Minimal API endpoint-group track. An architecture document that
  contradicts the generated project references is a failed step.
- **Verify and hand off:** a later agent can answer where a controller, use case, entity, DbContext,
  email sender, configuration type, and integration test belong without guessing. Review every
  planned project reference against the dependency table before continuing to step 01.

#### 01 — Solution skeleton and central package management

- **Outcome:** produce the smallest runnable solution that enforces step 00's boundaries. The host
  starts with MVC registered through `AddControllers()`/`MapControllers()` even though no feature
  controller exists yet; an expected `404` is preferable to adding a disposable Minimal API probe.
- **Files/artifacts:** `global.json`, `.slnx`, `Directory.Build.props`,
  `Directory.Packages.props`, `.editorconfig`, `.gitignore`, source project files, and the initial
  `Program.cs`. Use the existing PatientBooking flat layout consistently and create all five
  projects explicitly; do not mix a later `src/` layout into the same tutorial.
- **Implementation order:** verify the installed stable SDK; pin it with an intentional roll-forward
  policy; create the `.slnx`; create projects using the correct SDK (`Microsoft.NET.Sdk.Web` only
  for Api); add them to the solution; add only the project references allowed by step 00; enable
  Central Package Management; move shared target-framework/language/nullable properties to
  `Directory.Build.props`; then reduce `Program.cs` to the MVC composition root.
- **Required project tree:** show the empty `PatientBooking.Api`, `.Application`, `.Domain`,
  `.Infrastructure`, and `.Common` directories and their `.csproj` files before adding feature
  folders. Show the exact `dotnet add reference` commands and verify the graph matches step 00.
  Explain that folders organize files while project references enforce architectural boundaries.
- **Package rule:** `Directory.Packages.props` owns versions and project files own references without
  `Version`. Do not pre-install the entire future stack. Add only packages required by the current
  step or deliberately marked as a frontloaded baseline, and verify every provider/tool supports
  the selected .NET/EF major.
- **Verify:** run `dotnet --info`, `dotnet restore`, `dotnet sln <name>.slnx list`,
  `dotnet list <name>.slnx reference`, `dotnet build --no-restore`, and the API launch profile.
  Confirm all five projects exist, zero illegal references exist, and the host starts. Include
  failure callouts for SDK pin mismatch, a package version left in a `.csproj`, and a circular/
  outer-to-inner reference error.
- **Handoff:** step 02 receives a clean build, a deterministic SDK, one package-version source, and
  no provider/auth/feature code.

#### 02 — Analyzer and build baseline

- **Outcome:** make the compiler/analyzer policy explicit while the solution is empty enough to fix
  cleanly. Preserve the learning-project decision from `230-Analyzers-Setup`: nullable enabled,
  SDK analyzers enabled, `AnalysisLevel=latest`, `AnalysisMode=recommended`, code style enforced in
  build, and warnings visible but not globally promoted to errors unless step 00 selected a stricter
  production policy.
- **Files/artifacts:** update `Directory.Build.props` and `Directory.Packages.props`; add only
  justified analyzer packages; add `.editorconfig` severities, naming/style rules, generated-code
  handling, and the chosen formatting command. Keep one owner for each property rather than
  redundantly restating global values in every project.
- **Analyzer selection:** PatientBooking may retain its reviewed seven-analyzer suite. A future
  project must resolve current stable versions, check maintenance/license status and overlap, and
  document why each package earns its build cost. Do not copy old version numbers or suppressions.
  Each suppression names the diagnostic, scope, reason, and condition that would re-enable it.
- **Generated code:** exempt EF-generated migration bodies through a narrow migrations glob only;
  migration snapshots and generated SQL are still reviewed for schema correctness. Do not mark an
  entire project as generated to silence real application warnings.
- **Verify:** restore, build, and run `dotnet format --verify-no-changes` (or the selected equivalent).
  Add one temporary, harmless analyzer violation to prove the analyzer fires, then remove it before
  completing the lesson. Record the clean warning/error counts rather than merely saying “builds.”
- **Handoff:** every later tutorial inherits one documented analysis policy and may add a targeted
  suppression only with a local explanation.

#### 03 — Configuration, secrets, options, and Data Protection keys

- **Outcome:** establish how configuration enters the process and how protected state survives the
  environments that need it. This lesson teaches the pattern before database, JWT, SMTP, or OIDC
  options exist; later lessons add their own option types to this established mechanism.
- **Required conventions:** committed `appsettings.json` contains non-secret structure/defaults;
  environment files contain safe overrides; Development secrets use user-secrets or an ignored
  local environment file; deployment secrets use environment variables or the selected managed
  secret store. Services inject typed options, not `IConfiguration`; options validate on startup
  with data annotations or focused custom validators.
- **Implementation order:** document configuration-source precedence; initialize user-secrets on
  the Api project; demonstrate one non-secret bootstrap option with `BindConfiguration`, validation,
  and `ValidateOnStart`; document environment-variable key syntax; configure Data Protection's
  application name and environment-specific key persistence; then remove any demonstration secret.
- **Data Protection decision:** development can use the profile-backed default key ring. Containers
  and multi-instance deployments require a durable shared repository outside the image. If an
  explicit repository is selected, also select key encryption/access control because explicit
  persistence can remove the default encryption-at-rest behavior. Record backup, restore, rotation,
  previous-key retention, and application-name isolation; never log key material.
- **Verify:** start with valid configuration, then prove missing/invalid required values fail during
  startup with a useful option name and no secret value. Protect/unprotect a development-only probe,
  restart the host, and prove the same key ring can unprotect it; prove a different application name
  cannot. Remove the probe from production code after verification.
- **Handoff:** steps 20, 21, and later security lessons can add validated option classes and rely on
  a durable key-ring policy rather than inventing configuration or secret handling again.

#### 04 — Identity and authentication architecture decision

- **Outcome:** produce a decision, not authentication code. List every actor (human, administrator,
  service, scheduled job), client type (browser, native, trusted first-party tool, machine), and
  trust boundary. For each client, identify the authority, credential owner, protocol, transport,
  session/token lifetime model, and authorization source.
- **Decision table:** compare local ASP.NET Core Identity, external OIDC, platform identity, and no
  human identity. State who owns registration, password policy, email verification, recovery,
  lockout, passkeys/MFA, breach response, session revocation, account deletion, and support. Machine
  clients use an established OAuth/OIDC authority's client-credentials flow when required; the
  tutorial's JWT issuer is not expanded into a custom OAuth server.
- **Client-security decision:** browser applications choose a same-origin BFF/cookie design or a
  deliberately justified bearer design; native clients use system browser authorization and secure
  storage when an external authority is selected; tutorial `.http` calls are a test client, not a
  production storage recommendation. Record CSRF, CORS, redirect URI, token audience, signing-key,
  and logout/revocation consequences.
- **Authorization decision:** define stable subject mapping, roles versus permissions/policies,
  tenant/resource ownership, administrative boundaries, and which checks require live database
  state rather than potentially stale claims. Authentication proves identity; it does not replace
  resource authorization.
- **Deliverable and verify:** write an ADR with selected and rejected alternatives, assumptions,
  threat cases (phishing proxy, session theft, recovery abuse, push fatigue where applicable), and
  later lesson branches. Walk every planned login/recovery/account-change path and confirm it has an
  owner. If any path says “the app or provider will handle it” without naming which, the decision is
  incomplete.
- **Handoff:** the default PatientBooking learning run selects local Identity plus first-party access
  JWTs and later passkey/TOTP hardening. Other projects cross out inapplicable local-credential
  lessons and retain local subject mapping/authorization/session duties identified here.

#### 05 — Database provider decision and local development services

- **Outcome:** select one database and create reproducible local infrastructure before EF packages
  or migrations exist. SQL Server and PostgreSQL are equal runsheet branches; an individual project
  executes one.
- **Decision record:** compare hosting/managed-service availability, team operational familiarity,
  required features/extensions, licensing/cost, backup/restore tooling, collation/case behavior,
  and confirmed compatibility between the selected provider and EF Core major. Record the selected
  image tag and upgrade policy; do not use floating `latest` tags.
- **Files/artifacts:** `docker/dev.compose.yaml`, `docker/.env.dev.example`, ignored
  `docker/.env.dev`, and a short selected-database table containing profile, image, ports,
  persistent volume, health command, and purpose. The database has provider-specific profiles, but
  the project enables only the branch selected here.
- **Implementation order:** create the example environment file with placeholders; create only the
  selected database service/profile; add a named volume and native readiness check; bind to
  localhost for development unless network access is explicitly needed; start it; inspect status
  and logs; connect using the provider's native client; stop and restart without losing the test
  database.
- **Secret rule:** commit neither real database passwords nor a populated `.env.dev`. Explain that
  Compose interpolation and container environment variables are development plumbing, not a
  production secret store.
- **Verify and failure cases:** prove clean first start, readiness, authenticated native connection,
  persistence across restart, and clean shutdown. Diagnose port collision, weak/missing password,
  unhealthy container, unsupported CPU/image, stale volume credentials, and profile not selected.
- **Handoff:** step 20 receives one ready engine, one provider-neutral connection-string key name,
  and no second provider package/container to keep accidentally in sync.

#### 05B — Optional local development service profiles

- **Scope:** extend the step 05 Compose file with independently selectable smtp4dev, Seq, Aspire
  Dashboard, or RabbitMQ profiles for a developer who wants to frontload local tooling. This is a
  convenience field guide, not part of the numeric path.
- **Ownership rule:** keep each service isolated behind its own profile, pinned image, local-only
  ports, health check, and named volume only when persistence is useful. Record the numeric lesson
  that owns its application integration: step 13 for Seq/Aspire telemetry, step 200 for smtp4dev,
  and step 440 for RabbitMQ. Do not add application packages or registrations here.
- **Verify:** start, health-check, inspect, restart, and stop each profile independently and in the
  combinations a developer actually uses. Confirm the selected database still starts alone.
  Numbered owning lessons must work when this companion was skipped and must treat an already-running
  companion service as a confirmation, not a prerequisite.

### 10 — Cross-cutting host behaviour

- **10-Result-Error-Codes-and-Base-Api-Controller** (new; currently scattered through 40-ASP-Controller-Service-Architecture): Create Result, ResultError, error-code conventions, and BaseApiController before any auth or resource controller is written.
- **11-Global-Exception-Handling** (163-GlobalExceptionHandler): Add the ProblemDetails-shaped exception boundary immediately after the base host exists, not after features have already been built.
- **11B-Application-and-Provider-Exception-Mappings** (163-GlobalExceptionHandler companion): Add narrowly reviewed mappings for application or database-provider exceptions, compatibility aliases, and expanded failure diagnostics without broad catch-all translation.
- **12-Structured-Logging-and-Log-Levels** (160-Logging, 162-LogLevel): Configure Serilog, application log levels, correlation-friendly fields, and the logging conventions later services will use.
- **12B-Additional-Log-Sinks-Retention-and-Redaction-Operations** (160-Logging companion): Configure optional sinks, environment-specific retention, PII redaction, access controls, and operational troubleshooting while preserving the numeric event contract.
- **13-Local-Logs-Traces-and-Metrics** (161-Seq, 220-OpenTelemetry): Connect the local Seq and OTLP/Aspire-dashboard paths after logging exists so the first database and auth requests are observable.
- **13B-Telemetry-Backends-and-Collector-Topologies** (161-Seq, 220-OpenTelemetry companion): Cover alternate OTLP collectors/backends, sampling, buffering, authentication, multi-instance topology, and backend migration without becoming required for local verification.
- **14-API-Versioning-and-Scalar-Documentation** (180-Versioning-Documenting, 181-Scalar-API-Docs): Configure the MVC versioning and native OpenAPI/Scalar pipeline before controllers appear. Exclude the obsolete Swashbuckle path.
- **15-CORS-Security-Headers-and-Default-Rate-Limit-Policy** (210-CORS, 150-Rate-Limiting): Configure the required named CORS policy, forwarded-header/known-proxy trust, HTTPS/HSTS production behavior, baseline security headers, request-size limits, and a partitioned global rate-limit baseline. Allowed origins remain configuration-driven and never combine credentials with `AllowAnyOrigin`. Later security lessons attach their own stricter named policies when each endpoint appears.
- **15B-Proxy-CORS-and-Distributed-Rate-Limiter-Topologies** (210-CORS, 150-Rate-Limiting companion): Provide deployment-specific trusted-proxy chains, origin matrices, shared limiter storage, failover policy, and troubleshooting without weakening the baseline policy.
- **16-Resilient-Outbound-HTTP** (151-Resilience-Polly): Register the named or typed HTTP client, timeout, and resilience rules before the breached-password check or any other external HTTP integration calls it.
- **16B-Integration-Specific-Resilience-Tuning** (151-Resilience-Polly companion): Tune retry, timeout, circuit-breaker, concurrency, and fallback behavior for a named external dependency only after its idempotency and failure semantics are known.

#### 10 — Result, error codes, and BaseApiController

- **Outcome:** define the single expected-failure contract used by services and MVC controllers
  before any feature invents its own booleans, null conventions, or exception-to-status mapping.
- **Required model:** `Result`, `Result<T>`, and `ResultError` must make success/failure states
  unambiguous; errors carry stable application codes and safe messages, not `Exception` objects or
  HTTP types. Define the finite error categories and their HTTP mapping in one table. Expected
  validation/not-found/conflict/forbidden outcomes return Results; unexpected faults remain
  exceptions for step 11.
- **Placement:** put the result primitives in the contract location selected by step 00 and the HTTP
  translation in Api. `BaseApiController.ToActionResult` maps success and each failure category to
  `ActionResult`/Problem Details without business logic. Do not make Application reference MVC.
- **Implementation order:** specify invariants; implement non-generic and generic factories; add
  safe multi-error support; implement the controller translator; add one development-only example
  in documentation rather than a permanent fake feature; then show how a future controller returns
  the translator's result.
- **Verify:** build and exercise every factory/mapping through focused tests when the test project
  exists. Until step 37, include a deterministic verification table and compile-time example. Check
  success with/without value, multiple validation errors, unknown error category fail-safe behavior,
  and that internal exception details can never be serialized through `ResultError`.
- **Handoff:** step 11 handles only unexpected exceptions; steps 30–36 use this one contract without
  creating parallel response wrappers.

#### 11 — Global exception handling

- **Outcome:** add the final safety boundary for unhandled failures using `AddProblemDetails`, an
  `IExceptionHandler`, and `UseExceptionHandler`. MVC validation and step 10 expected failures must
  produce compatible Problem Details shapes without being routed through exception control flow.
- **Implementation order:** define the public Problem Details fields and trace/correlation id;
  register the problem-details service; implement ordered handling for the few exception types that
  genuinely cross the boundary; log unexpected faults once; configure environment-safe detail
  output; put middleware in the documented order; and add status-code handling only where it will
  not overwrite an existing body.
- **.NET 10 diagnostic rule:** explicitly decide whether handled exceptions emit framework
  diagnostics because .NET 10 suppresses diagnostics for exceptions handled by
  `IExceptionHandler` by default. Avoid duplicate Serilog/OpenTelemetry records and preserve one
  error event for unexpected faults.
- **Security/guardrails:** never return stack traces, SQL/provider messages, connection strings,
  file paths, or raw request bodies outside Development. Cancellation caused by the disconnected
  caller is not logged as an application error. Do not catch exceptions in every controller after
  installing the global boundary.
- **Verify:** use a temporary Development-only throw path or an integration test, then remove/disable
  the route. Confirm status, `application/problem+json`, stable type/title, trace id, one log event,
  no sensitive detail in Production, and correct behavior for unsupported `Accept` headers.
- **Handoff:** all later runtime failures have a consistent safe boundary and observable trace id.

#### 12 — Structured logging and log levels

- **Outcome:** configure Serilog as the application logging provider and define an event schema that
  later database/auth code can reuse. Console is the required baseline; Seq ingestion belongs to
  step 13 and a file sink remains a deliberate local requirement, not an automatic production
  default.
- **Implementation order:** add centrally managed Serilog packages; configure bootstrap logging so
  host-start failures are visible; read final logger configuration from validated non-secret
  settings; enrich with service, environment, trace/span, and request identifiers; add one concise
  HTTP request-completion event; set framework overrides; flush on shutdown.
- **Logging contract:** use message templates and stable property names; distinguish operational
  information, recoverable warnings, and faults; do not interpolate structured values into strings.
  Define redaction/exclusion rules for authorization headers, cookies, passwords, tokens, email
  links, TOTP/passkey material, connection strings, request/response bodies, and unnecessary PII.
- **Guardrails:** do not log the same exception in service, controller, and global handler. Do not
  enable broad EF command payload or HTTP-body logging by default. Health probes and static docs
  traffic may use lower verbosity to avoid noise.
- **Verify:** trigger startup, a normal request, a `404`, and the step 11 failure path. Confirm fields
  are queryable, log-level overrides work, exactly one completion event is written per request, the
  trace id matches Problem Details, and a seeded fake secret never appears in captured output.
- **Handoff:** step 13 exports the same structured events/traces rather than introducing a second
  unrelated observability vocabulary.

#### 13 — Local logs, traces, and metrics

- **Outcome:** make the host observable before database/auth requests arrive. Serilog continues to
  own application logs; OpenTelemetry owns distributed traces and metrics. Export destinations are
  configuration-driven and local exporters can be disabled without changing application code.
- **Files/packages:** centralize current stable OpenTelemetry hosting, ASP.NET Core, HTTP client,
  runtime, and OTLP packages actually used; add Seq and Aspire Dashboard Compose services only for
  the selected local experience. Do not install every instrumentation package speculatively.
- **Implementation order:** define a stable service/resource name and environment attributes;
  register ASP.NET Core and HttpClient tracing, runtime/ASP.NET metrics, sampling, and OTLP export;
  filter health/docs noise intentionally; configure local endpoints; start one dashboard path;
  generate traffic and correlate it with the Serilog request event.
- **Data policy:** review tags and baggage as exported data. Never attach credentials, raw SQL with
  values, tokens, full request bodies, or unbounded user-provided values. Record the production
  sampling/export decision as deferred to deployment, not silently “always on.”
- **Verify:** confirm the service appears once, a request produces one server span and expected
  metrics, trace/span ids join logs to traces, exporter loss does not take down the API, and stopping
  the local dashboard produces bounded retries rather than log flooding.
- **Handoff:** steps 16 and 20 will automatically add outbound-client and database evidence to an
  already working telemetry path.

#### 14 — API versioning, built-in OpenAPI, and Scalar

- **Outcome:** establish one MVC API surface and documentation mechanism before real controllers
  appear. Use `Asp.Versioning.Mvc`/ApiExplorer, `Microsoft.AspNetCore.OpenApi`, and Scalar; do not
  retain a parallel Swashbuckle pipeline.
- **Decision:** record the versioning strategy (URL segment is the tutorial default), default-version
  behavior, deprecation/reporting policy, version-neutral endpoints, and how route names/documents
  are grouped. Do not advertise multiple versions until behavior actually differs.
- **Implementation order:** install compatible centrally managed packages; register controllers and
  API versioning; configure explorer substitution/group naming; register the built-in OpenAPI
  document; add transformers only for real metadata needs; map JSON documents and Scalar in the
  selected environments; add authorization-scheme metadata later when auth exists.
- **Guardrails:** generated OpenAPI is the contract to inspect, not proof the runtime works. Scalar
  is a client UI, not an authentication system. Production exposure is an explicit deployment
  decision, and docs routes must not bypass endpoint authorization.
- **Verify:** add a tiny tutorial-only controller or use a focused test fixture, confirm the versioned
  route executes, unsupported versions fail predictably, the OpenAPI document contains the MVC
  action/status/Problem Details schema once, and Scalar loads that document. Remove any placeholder
  controller before the lesson hands off if it is not part of the product.
- **Handoff:** step 30 can define real resources without revisiting host documentation plumbing.

#### 15 — CORS, proxy trust, headers, request limits, and baseline rate limiting

- **Outcome:** establish safe host defaults while clearly separating browser CORS, reverse-proxy
  trust, response headers, transport security, input-size controls, and abuse throttling. None is
  described as a substitute for authentication or authorization.
- **Configuration:** bind exact allowed origins and trusted proxy/network values from validated
  options. Credentialed CORS never uses `AllowAnyOrigin`; reject wildcard subdomain improvisation.
  Configure forwarded headers only from known infrastructure and retain a finite forward limit so
  client IP, scheme, link generation, logs, and rate-limit partitions cannot be spoofed.
- **Middleware order:** document and implement forwarded headers before scheme/host-dependent work;
  production exception/HSTS/HTTPS behavior; routing; CORS; rate limiting; later authentication and
  authorization; controller mapping. Keep `AllowedHosts` meaningful for the deployment topology.
- **Headers/limits:** add only headers useful to this API/docs surface, such as
  `X-Content-Type-Options` and an explicit referrer policy. If Scalar is served, define a compatible
  CSP rather than breaking it blindly. Set request/body limits from actual endpoint needs and allow
  later upload endpoints to own narrower overrides.
- **Rate limiting:** add a modest partitioned global safety net with a `429` Problem Details response
  and `Retry-After`. Use authenticated subject when available later and a trusted remote IP fallback
  now. Named auth/recovery policies remain owned by their later lessons; a single in-memory limiter
  is documented as per-instance rather than falsely global in a scaled deployment.
- **Verify:** test allowed/disallowed/no-Origin requests, preflight, credential rules, direct spoofed
  forwarding headers, a configured trusted proxy case, HTTP-to-HTTPS/HSTS behavior in the right
  environment, host rejection, over-limit/body-limit responses, and limiter partition isolation.
- **Handoff:** later account endpoints attach stricter policies without weakening this baseline.

#### 16 — Resilient outbound HTTP

- **Outcome:** provide one correct `IHttpClientFactory` pattern before breached-password, OIDC
  metadata, or other remote integrations appear. The tutorial creates infrastructure, not a fake
  universal API client.
- **Implementation order:** install the current compatible `Microsoft.Extensions.Http.Resilience`
  package; register a named or typed client with a configured base address and total timeout; add a
  standard resilience handler; tune attempt timeout, total timeout, circuit breaker, and retry from
  the dependency's contract; pass cancellation tokens through every call; define safe response
  parsing and disposal.
- **Retry rule:** retries are enabled only for transient failures and operations known to be
  idempotent. Because the standard handler can retry all HTTP methods by default, explicitly disable
  or constrain retries for unsafe `POST`/mutation calls unless the downstream protocol supplies an
  idempotency key. Do not stack multiple retry handlers or retry caller cancellation.
- **Observability/security:** emit dependency name, outcome, duration, and trace context without
  URLs/query values that contain secrets. Credentials live in options/secret stores, not default
  headers copied into logs. No fallback returns fabricated “success.”
- **Verify:** use a controlled local test handler/server to produce success, transient failure then
  recovery, permanent `4xx`, timeout, open/half-open circuit, caller cancellation, and an unsafe
  method. Assert attempt counts and that the API remains responsive. Integration-test helpers may
  be finalized in step 37, but the runtime behavior is demonstrated here.
- **Handoff:** later integrations request a configured client by name/type and add only their
  dependency-specific contract.

### 20 — Database, Identity persistence, and schema

- **20-Selected-Database-Connection-and-EF-Registration** (10-EFCore-Database-Setup, 00-Docker-SqlServer-Setup, 01-Docker-PostgreSQL-Setup): Implement the decision from step 05 using only the selected provider package and registration: `Microsoft.EntityFrameworkCore.SqlServer`/`UseSqlServer` or `Npgsql.EntityFrameworkCore.PostgreSQL`/`UseNpgsql`. Bind and validate the shared connection-string/configuration contract, keep the secret outside source, configure provider-appropriate retries/timeouts deliberately, and prove the application reaches the selected local database. Do not add a runtime provider switch merely to make the tutorial look portable.
- **20B-Provider-Tuning-and-Cloud-Database-Authentication** (10-EFCore-Database-Setup companion): Add SQL Server/PostgreSQL connection-pool, retry, timeout, managed-identity/token, TLS, and managed-service variations after the selected provider path works.
- **21-ApplicationUser-IdentityDbContext-and-Initial-Migration** (60-Authentication-v-Authorization Identity setup): When step 04 selected local Identity, add ApplicationUser, the .NET-version-appropriate IdentityDbContext schema (including .NET 10 passkey storage when that track is selected), Identity registrations, unique normalized-email requirements, protected Identity-store policy, and the first migration before defining any account endpoint. Generate and inspect that migration with the selected provider; do not copy SQL Server column types, defaults, or index filters into the PostgreSQL branch or vice versa. Verify which `AspNetUserTokens` values are reversible secrets and never describe the default recovery-code store as hashed. When external identity owns credentials, replace this with only the local subject/profile mapping the application actually needs.
- **21B-Identity-Schema-Customization-and-Legacy-User-Migration** (60-Authentication-v-Authorization companion): Cover custom keys/tables/columns, existing-user import and normalization, migration reconciliation, and rollback without changing the greenfield Identity schema required later.
- **22-Database-Model-Design-Relationships-and-Indexes** (15-EFCore-Relationship-Types, Database-Modelling, Mermaid-ERD-Todolist-Example): Design the project's initial entities and Fluent configurations, including uniqueness, foreign-key, and expected lookup/filter/sort indexes. Keep the conceptual model provider-neutral, but explicitly resolve provider differences for UUID/`uniqueidentifier`, UTC timestamps, decimal precision, case-sensitive/case-insensitive comparisons, generated values, identifier lengths, filtered/partial indexes, JSON, and optimistic concurrency. Prefer portable EF mappings unless a measured or required provider feature earns a clearly labelled provider-specific branch. The actual entities remain project decisions.
- **22B-Legacy-Schema-Adoption-and-Data-Repair** (Database-Modelling companion): Map an existing schema, inventory incompatible constraints/data, stage repair and backfill work, and preserve provider behavior without contaminating the clean model-design lesson.
- **23-Selected-Provider-Migration-Workflow-and-Database-Verification** (10-EFCore-Database-Setup): Teach add/list/review/script/update/remove migrations and verify the live schema against the provider selected in step 05. Treat migrations and generated SQL as provider-specific artifacts. An ordinary project maintains one migration set; a product that intentionally supports both providers maintains separate migration sets, generates every model change for both, and tests both before either is called supported. Never treat changing only the connection string as a database migration strategy.
- **23B-Zero-Downtime-Migrations-Rollback-and-Repair** (10-EFCore-Database-Setup companion): Add expand/contract deployment, reviewed scripts, resumable backfills, rollback limits, failed-migration repair, and multi-instance rollout operations.
- **24-Database-Health-Checks** (170-HealthChecks): Add the DbContext/provider health check only after the selected provider, DbContext, migration, and real connection exist. Verify healthy, unavailable, bad-credential, and cancellation/timeout behavior without exposing connection details.
- **24B-Orchestrator-and-Cloud-Health-Probe-Variations** (170-HealthChecks companion): Map liveness/readiness/startup probes to Docker, Kubernetes, Azure, or another host and add deployment-specific timing and troubleshooting without changing probe semantics.

#### Steps 04–05 and 20–24 — Required decision and provider branch details

The tutorial is reusable because it makes an early choice and carries it consistently, not because
every application contains two providers. Each rewrite records these decisions in the project
contract/ADR and removes instructions belonging only to an unselected branch.

| Concern | SQL Server / Azure SQL branch | PostgreSQL branch |
|---|---|---|
| EF Core package | `Microsoft.EntityFrameworkCore.SqlServer` | `Npgsql.EntityFrameworkCore.PostgreSQL` compatible with the selected EF Core major |
| Registration | `UseSqlServer` | `UseNpgsql` |
| Local service | SQL Server container/profile | PostgreSQL container/profile |
| Integration-test module | `Testcontainers.MsSql` | `Testcontainers.PostgreSql` |
| Database-managed concurrency option | SQL Server `rowversion` | PostgreSQL `xmin`; an application-managed token is also acceptable |
| Migration output | SQL Server migration and reviewed SQL script | PostgreSQL migration and reviewed SQL script |

- **Identity decision deliverable:** list actors and clients, identity authority, accepted
  authentication methods, session/token transport, recovery/MFA requirements, authorization
  boundary, and the reason local credentials are or are not owned. A human-user project using an
  external OIDC provider does not recreate local passwords, password reset, or TOTP merely because
  those lessons exist; it still owns local authorization and subject/profile mapping where needed.
- **Provider decision deliverable:** record hosting availability, operational familiarity,
  extensions/features actually required, licensing/cost constraints, backup/restore tooling, and
  confirmed EF Core provider-version compatibility. Provider selection happens before Compose,
  package installation, migrations, or Testcontainers configuration.
- **Portable-model baseline:** use explicit lengths and decimal precision; store timestamps with a
  documented UTC convention; generate application identifiers portably unless a provider-specific
  default is intentional; verify normalized Identity uniqueness and string-comparison semantics on
  the real provider; and label provider-specific computed columns, filtered/partial indexes, JSON,
  collations, and raw SQL beside the code that needs them.
- **Migration rule:** scaffold with the selected provider active, inspect destructive operations,
  review the generated SQL, apply it to a clean container and an upgraded prior schema, and verify
  rollback/forward recovery as appropriate. If both providers become a real requirement, create
  separate migrations assemblies/directories and CI jobs rather than branching unpredictably
  throughout one migration history.
- **Verification:** both tutorial branches must be executable in isolation. For the selected branch,
  restore/build succeeds with only its provider package, the container becomes ready, migrations
  apply from empty and upgrade states, the DbContext health check behaves correctly, and integration
  tests run against that same engine. The unselected branch is documentation, not a second runtime
  dependency.

#### 20 — Selected database connection and EF registration

- **Outcome:** connect the application to the engine selected in step 05 through EF Core without
  creating schema or Identity endpoints. Infrastructure is the course's persistence owner; Domain
  remains free of EF Core packages and provider annotations.
- **Files/packages:** add `Microsoft.EntityFrameworkCore` plus exactly one provider package to the
  persistence-owning project, with matching major versions in Central Package Management. Add the
  design/tools package only to the project that needs design-time commands with appropriate private
  assets. Create the DbContext under `PatientBooking.Api.Infrastructure/Persistence`, its design-
  time/host discovery path if required, database options, and the `AddInfrastructure` persistence
  registration extension. Migrations and EF configurations stay under the same persistence owner.
- **Configuration:** keep one `ConnectionStrings:DefaultConnection` contract and validated
  provider-neutral timeout/retry settings. The composition root may read configuration to build EF
  options; application services do not inject raw `IConfiguration`. Provider-specific delegates
  call `UseSqlServer` or `UseNpgsql`, never a runtime string switch that installs both packages.
- **Execution strategy:** set command timeout deliberately and enable transient retries only where
  supported and appropriate. Do not combine a retrying execution strategy with ad hoc user
  transactions without following the provider's execution-strategy pattern. Never auto-apply
  migrations during ordinary application startup or pool a DbContext without measured need.
- **Verify:** restore/build, inspect package references, start the selected container, run EF design-
  time context discovery, and execute a Development-only `CanConnectAsync` smoke check that is
  removed or superseded by step 24. Test missing connection string, wrong credentials, unavailable
  host, timeout, and cancellation without printing the connection string.
- **Handoff:** step 21 receives a working empty context using one engine and one configuration path.

#### 21 — ApplicationUser, identity persistence, and initial migration

- **Outcome:** implement only the identity-persistence branch selected in step 04 and establish its
  first provider-specific schema before any public account endpoint exists.
- **Local Identity placement:** keep the ASP.NET Core Identity user, stores, and
  `IdentityDbContext` in Infrastructure because they are framework persistence models. Domain
  models reference a stable application user identifier only when required and never depend on the
  Identity user type.
- **Local Identity implementation:** define only required user fields; register Identity stores,
  token providers, password/lockout/confirmed-email options, and `TimeProvider`; require unique email
  in application behavior and enforce normalized-email uniqueness in the actual database model;
  inspect .NET 10 passkey schema when that branch is selected. Configure personal-data protection
  only with the durable Data Protection policy from step 03 and inventory which Identity token-store
  values are reversible secrets.
- **External-authority implementation:** do not add local password hashes, reset tokens, TOTP, or
  passkey tables. Persist the minimum local account/profile plus a unique `(issuer, subject)` mapping,
  display/contact claims with their trust status, local authorization state, and lifecycle flags.
  Never use mutable email as the stable external identity key.
- **Migration:** generate the initial identity/local-subject migration with the selected provider
  active. Review tables, keys, lengths, delete behaviors, normalized indexes, passkey tables where
  applicable, provider annotations/types, and accidental plaintext columns before applying to the
  clean local database.
- **Verify:** schema applies from empty; duplicate normalized email or `(issuer, subject)` is rejected
  at the database boundary; Data Protection-backed values survive restart; the context resolves from
  DI; and no login/register route or default Identity endpoint surface was accidentally exposed.
- **Handoff:** step 22 can model application data against a stable local user/subject boundary;
  steps 60+ later add the chosen account flows.

#### 22 — Database model design, relationships, and indexes

- **Outcome:** turn project requirements into a reviewed conceptual/relational model before letting
  migrations define it accidentally. The lesson uses a compact entity/relationship table and nested
  flow outline, not a diagram.
- **Modelling workflow:** list use cases and invariants; identify entities and ownership; choose keys;
  define required/optional fields; state cardinality and navigation direction; choose delete behavior;
  define value generation, concurrency, UTC timestamp, soft-delete/retention, and sensitive-data
  policy; then derive indexes from known lookup, uniqueness, filter, sort, and foreign-key paths.
- **Layering/code:** domain entities own real invariants and avoid EF/HTTP concerns. Infrastructure
  owns `IEntityTypeConfiguration<T>` mappings, the DbContext, and migrations, with mappings applied
  by assembly scanning. Avoid generic repository
  scaffolding, lazy loading, public collection setters, and one-to-one relationships inferred from
  ambiguous navigations. DTOs/controllers are not created in this step.
- **Provider review table:** resolve Guid/UUID storage and generation, timestamp type/UTC convention,
  decimal precision, string length/collation/case semantics, enum conversion, identity/sequence
  generation, filtered versus partial indexes, JSON use, identifier length, and concurrency token.
  Label any SQL Server-only or PostgreSQL-only feature and its fallback.
- **Indexes:** every unique business rule has an enforcing constraint/index; every index names the
  query it supports; composite order follows equality then range/order needs; avoid speculative
  indexes on every column. Verify nullable uniqueness and normalized Identity/profile rules on the
  selected engine rather than assuming cross-provider semantics.
- **Verify and handoff:** build the model; inspect EF's model/debug output; confirm relationship and
  delete behavior through focused tests or a temporary clean database; run the provider's pending-
  model-change check and expect the new application model to require the step 23 migration. The
  handoff includes the model table, provider decisions, and named migration scope.

#### 23 — Selected-provider migration workflow and database verification

- **Outcome:** make schema change a repeatable reviewed operation. Migrations are provider-specific
  source artifacts, not an opaque command or an application-startup side effect.
- **Command contract:** every command shows solution-root working directory and explicit
  `--project` persistence owner plus `--startup-project` Api. Teach migration add/list, generated
  file/snapshot review, SQL generation, update to a named migration/latest, rollback in disposable
  development data, remove only an unapplied last migration, and pending-model-change detection.
- **Review checklist:** inspect destructive drops/renames, nullability changes, defaults/backfills,
  computed/generated values, index filters, unique data preconditions, foreign-key cascade behavior,
  provider annotations, long locks/table rewrites, and whether `Down` can honestly reverse the
  change. A safe data migration may require hand-authored reviewed SQL specific to the selected
  provider.
- **Verification matrix:** create a fresh selected-provider container/database and apply from zero;
  create a second database at the previous schema/data state and upgrade it; verify constraints and
  representative queries; generate and inspect the deployment script or migration bundle supported
  by the provider; test the documented rollback/forward-recovery path on disposable data.
- **Dual-provider rule:** ordinary projects keep one migration history. A product that truly supports
  both creates separate migration assemblies/directories, runs scaffold/apply/upgrade tests for both,
  and reviews two SQL outputs. Changing only the connection string is never presented as support.
- **Production guardrail:** deployment applies a reviewed artifact under operational control; the
  web process does not call `Database.Migrate()` on every startup. Backup/restore and zero/low-
  downtime strategy are revisited with the real deployment in steps 490–501.
- **Handoff:** step 24 receives a known-good selected-provider schema and a reproducible clean/upgrade
  verification procedure.

#### 24 — Database health checks

- **Outcome:** expose separate liveness and readiness semantics after the real DbContext and schema
  exist. Liveness proves the process can respond and never depends on the database; readiness may
  use `AddDbContextCheck<TContext>`/`CanConnectAsync` to show whether this instance can serve
  database-backed traffic.
- **Files/packages:** centrally manage
  `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` when using the EF check;
  register named checks with `live`/`ready` tags; map stable version-neutral endpoints such as
  `/health/live` and `/health/ready`; and define the minimal response contract expected by the local
  orchestrator and future deployment.
- **Security/operations:** return aggregate status without connection strings, SQL, credentials,
  exception messages, or topology. Set bounded timeouts and cancellation. Keep health routes out of
  noisy request logs/traces where appropriate, and decide at deployment whether detailed readiness
  is restricted to a management port/network.
- **Guardrails:** a health check does not apply migrations, repair schema, run an expensive business
  query, or prove backup integrity. Do not make liveness fail because a dependency is down; doing so
  can cause an orchestrator restart loop that cannot repair the database.
- **Verify:** healthy database yields live/ready success; stopped container keeps liveness healthy
  while readiness fails; bad credentials and timeout fail readiness without leaking detail; restart
  returns readiness to healthy; concurrent probes remain cheap; cancellation stops promptly. Confirm
  the endpoint status/body works with Docker/orchestrator probing, not only a browser.
- **Handoff:** the 00–29 foundation ends with a buildable, observable host connected to one migrated
  provider, plus identity persistence appropriate to the selected authority. Step 30 can now create
  the reusable public API delivery pattern.

#### 25–29 — Reserved expansion slots

These numbers stay intentionally unused. Do not create filler lessons. A future topic may occupy one
only when it is a real prerequisite after database health and before API delivery, can end in an
independently buildable state, and cannot be owned more coherently by steps 00–24 or 30–37.

### 30 — Reusable API delivery pattern

- **30-REST-Resources-Status-Codes-and-API-Standards** (30-RESTful-API, 35-Std-vs-API): Set the request/response, status-code, resource-naming, and error-contract standards before the first public endpoint.
- **30B-Conditional-Requests-Content-Negotiation-and-Bulk-Standards** (30-RESTful-API companion): Extend the API standard with ETags/preconditions, additional representations, or bulk-operation contracts only when a concrete consumer requires them.
- **31-Layer-Boundaries-and-Dependency-Rules** (102-Layered-architecture-pattern, 100-Advanced-Architecture-Concepts): Apply the chosen project boundaries to the actual solution. Keep MVC controllers; do not teach endpoint groups for this project.
- **31B-Existing-Solution-Infrastructure-Extraction-and-Layer-Repair** (102-Layered-architecture-pattern companion): For an existing four-project PatientBooking-style solution only, move EF Core and external clients into the Infrastructure project through staged references and verification. The clean numeric recreation already created Infrastructure in step 01 and does not execute this companion.
- **32-DTO-Contracts-and-Response-Shaping** (45-Data-Shaping): Define request and response DTOs before any mapper, service, or controller consumes them. Prefer explicit summary/detail representations; dynamic field selection is an optional consumer requirement, not a default API feature.
- **32B-Dynamic-Field-Selection** (45-Data-Shaping companion): Add allow-listed `?fields=` shaping only for a named consumer requirement. This companion never changes the response contracts required by steps 33–37.
- **33-Mapperly-Mapping** (21-Mapperly-Mapping, 41-Mapperly-For-Controller-Service-Architecture, 46-Mapperly-For-Data-Shaping, 103-Mapperly-For-Layered-Architecture): Use Mapperly as the first and only object-mapping approach. Fold every former AutoMapper correction into this lesson's final examples.
- **33B-Advanced-Mapperly-Conversions-and-Diagnostics** (Mapperly companion): Cover custom conversions, factories, nested/reference handling, diagnostic policy, and generated-code troubleshooting without introducing a second mapper or moving step 400's query projections earlier.
- **34-Service-Layer-Contracts-and-Implementations** (38-Service-Layer): Establish the service, domain, persistence-port, mapping, and DTO responsibilities that both account and resource features use.
- **35-Request-Validation** (50-Validation): Add boundary validation after request DTOs exist and before a controller accepts them.
- **35B-Explicit-Per-Action-Validation** (50-Validation companion): Show explicit `ValidateAsync` orchestration for the rare action whose validation lifecycle cannot use the standard MVC validation filter. Later numbered controllers depend only on step 35's filter.
- **36-MVC-Controller-Service-Composition** (40-ASP-Controller-Service-Architecture): Build thin versioned controllers on top of BaseApiController, Result, validators, and services rather than introducing a one-off pattern for auth.
- **37-Integration-Test-Baseline** (new): After the first MVC request path works, create the verified test project and cover one successful request plus its main failure case. Database integration tests use the matching selected-provider module—`Testcontainers.MsSql` or `Testcontainers.PostgreSql`—rather than EF Core's in-memory provider, SQLite, or the other production engine. Start the real container, apply the selected migration set, and use a provider-compatible cleanup strategy. Add reusable test helpers for time, authentication principals, rate-limit isolation, email capture, and database cleanup so later security lessons can test expiry, replay, concurrency, and cross-user access without weakening production registrations.
- **37B-Dual-Provider-Test-Matrix** (new companion): Explain the separate fixtures, migration histories, CI jobs, cleanup strategies, and provider-sensitive assertions required only when a product explicitly supports both SQL Server and PostgreSQL. Ordinary SQL Server and PostgreSQL paths do not execute this companion.

#### 30-series execution contract and primary references

The 30-series proves one reusable MVC request path without creating a disposable tutorial entity.
At the start of step 30, select the smallest already-modelled, non-sensitive resource from step 22
that is genuinely safe to expose at this point. PatientBooking uses a read-only Clinic directory;
another project records its chosen reference resource in `docs/api/standards.md`. If no real resource
is safe before authorization exists, use a controller loaded only by the integration-test host and
do not ship a fake `WeatherForecast`, `Todo`, or `SampleResource` in the production assembly.

Keep the reference slice deliberately narrow: one collection query and one detail query are enough
to prove routing, DTOs, mapping, services, expected failures, validation, documentation, database
access, and tests. Do not add anonymous create/update/delete actions merely to demonstrate every
HTTP verb. Those mutations wait for a real feature and its authorization policy.

The tutorials use these primary references and resolve current compatible package versions when
each lesson is executed:

- [HTTP Semantics, RFC 9110](https://www.rfc-editor.org/rfc/rfc9110)
- [Problem Details for HTTP APIs, RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)
- [Controller action return types](https://learn.microsoft.com/en-us/aspnet/core/web-api/action-return-types?view=aspnetcore-10.0)
- [OpenAPI metadata in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/include-metadata?view=aspnetcore-10.0)
- [ASP.NET API versioning by URL path](https://github.com/dotnet/aspnet-api-versioning/wiki/Versioning-via-the-URL-Path)
- [Mapperly usage and configuration](https://mapperly.riok.app/docs/category/usage-and-configuration/)
- [FluentValidation ASP.NET Core integration](https://docs.fluentvalidation.net/en/latest/aspnet.html)
- [ASP.NET Core integration tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
- [Testcontainers for .NET](https://dotnet.testcontainers.org/)

Every step must leave the solution buildable and must update the same reference slice instead of
starting another example. The final tutorial shows the complete current contents of every small
contract, mapper, validator, service, controller, DI extension, and test-fixture file.

Use this as the concrete file-placement target, adapting only the root project names chosen in step
00. The tutorial states every final path before its first code listing:

| Concern | PatientBooking course path | Ownership reason |
|---|---|---|
| API standards | `docs/api/standards.md` | Repository-wide public contract |
| Domain model and enums | `PatientBooking.Api.Domain/{Entities,Enums}/*` | Business concepts and state |
| DTOs | `PatientBooking.Api.Application/DTOs/Clinics/*` | Use-case input/output contracts |
| Mapper/validator/service | `PatientBooking.Api.Application/{Mappers,Validators,Services}/*` | Application orchestration |
| Application interfaces | `PatientBooking.Api.Application/Contracts/*` | Ports required by use cases |
| DbContext, EF configuration, query implementation, migrations | `PatientBooking.Api.Infrastructure/Persistence/*` | Framework/provider implementation |
| Identity persistence | `PatientBooking.Api.Infrastructure/Identity/*` | ASP.NET Core Identity storage model |
| External-service implementations | `PatientBooking.Api.Infrastructure/{Email,ExternalServices}/*` | Outbound adapters implementing Application ports |
| Cross-cutting shared contracts | `PatientBooking.Api.Common/{Results,Enums,Models/Config}/*` | Used unchanged by multiple projects |
| MVC controller | `PatientBooking.Api/Controllers/ClinicsController.cs` | HTTP boundary |
| Integration tests | `PatientBooking.Api.IntegrationTests/{Fixtures,Clinics}/*` | Host and provider verification |

#### 30 — REST resources, status codes, and API standards

- **Outcome:** create the public HTTP contract before implementation details make accidental choices
  permanent. Add `docs/api/standards.md` and name the real reference resource selected above. This
  document is normative for account endpoints in steps 60–209 and feature endpoints in step 300+.
- **Resource and route contract:** use plural noun resources, lowercase path segments, opaque route
  identifiers, and explicit URL-segment versions such as `/api/v1/clinics/{clinicId}`. Routes do not
  contain verbs except when an operation is not honestly representable as resource state. `GET`
  never accepts a request body. Query names, maximum lengths/counts, repeated-value behavior, and
  unknown-parameter policy are documented rather than left to model-binding accidents.
- **Representation contract:** successful responses return the documented resource DTO or a named
  collection/page contract—never EF/domain entities and never a universal `ApiResponse<T>` wrapper.
  JSON uses the configured `System.Text.Json` camel-case/null/enum policy consistently. IDs are not
  silently converted between numeric and string forms; timestamps are UTC ISO 8601 values; money
  and other precision-sensitive values are not represented as binary floating point.
- **Status-code table:** record at least `200` for successful reads/updates, `201` plus `Location` for
  synchronous creation, `202` only for accepted asynchronous work with a status resource, `204` for
  successful no-body operations, `400` for malformed/boundary validation, `401` for missing/invalid
  authentication, `403` for an authenticated but unauthorized caller, `404` for missing or
  deliberately concealed resources, `409` for state/concurrency conflicts, `415` for unsupported
  media types, `429` with retry information, and `500` for unexpected faults. Do not return `200`
  with an error object or `404` for every failure.
- **Error and evolution contract:** all non-success bodies use step 10/11 Problem Details with stable
  application error codes in extensions and safe field errors for validation. The internal
  `Result<T>` wrapper is never serialized. Define which changes are backward-compatible and require
  a new API version; adding an optional response field is normally compatible, while removing or
  retyping a field is not. Do not advertise v2 before v2 behavior exists.
- **Security and transport contract:** record JSON media types, request-size limits, cache behavior,
  explicit anonymous/authorized posture, safe `Location` generation, and the ban on secrets or PII
  in URLs. CORS does not define API authorization. Documentation examples use synthetic data.
- **Verify and hand off:** review the proposed collection/detail examples against the table, including
  malformed id, unknown id, invalid query, unavailable database, unauthenticated, forbidden, and
  throttled cases. Each has one unambiguous status/body owner. Step 31 then assigns each concern to
  a project without changing the HTTP contract.

#### 31 — Layer boundaries and dependency rules

- **Outcome:** turn step 00's architecture decision into an executable placement map for the first
  request slice. Update the architecture ADR if the generated project graph differs from the
  intended dependency direction; do not hide drift behind namespaces.
- **Course placement:** Domain owns the resource and business invariants; Application owns
  request/response contracts, service interfaces, framework-neutral use-case implementations,
  Result usage, validators, and purpose-specific ports; Infrastructure owns EF Core, Identity/
  provider-bound account implementations, the selected database provider, queries, migrations, and
  port implementations; Api owns MVC controllers, HTTP translation, filters, and composition. Tests
  reference Api and only the projects needed for fixture/seed support.
- **Common boundary:** place a type in Common only when at least two projects require the same stable
  contract and neither project clearly owns it. Common must not become a dumping ground for feature
  DTOs, entity-specific enums, EF helpers, or arbitrary utility classes.
- **Reference-slice inventory:** list the exact planned types and owner before creating them—for
  example `Clinic`, `ClinicSummaryResponse`, `ClinicDetailResponse`, `IClinicService`,
  `ClinicService`, the selected persistence port/implementation, `ClinicMapper`,
  `ListClinicsRequestValidator`, `ClinicsController`, registrations, and integration tests. Each type
  gets one reason to exist; no generic repository, generic service, or base DTO is introduced.
- **Dependency and lifetime rules:** Application must not reference MVC, OpenAPI, provider packages,
  or concrete infrastructure. Domain must not reference EF/HTTP. The API
  composition root calls explicit `AddApplication`/`AddInfrastructure` registrations. DbContext and
  request services are scoped; pure stateless mappers may be static; no scoped dependency is captured
  by a singleton.
- **Guardrails:** controllers never query DbContext, persistence code never returns `ActionResult`,
  DTOs contain no entity navigation graphs, and domain entities contain no serialization attributes.
  Do not create a repository abstraction that merely copies the full `DbSet<T>` API.
- **Verify and hand off:** run the solution project/reference inventory and a clean build. Inspect the
  DI graph with validation enabled and prove there are no circular or forbidden references. Record
  any deliberate PatientBooking exception in the ADR. Step 32 can now create contracts without
  guessing their project or namespace.

#### 32 — DTO contracts and response shaping

- **Outcome:** define stable, serializable request/response types before mapping, services, and MVC
  actions depend on them. Data shaping means deliberately choosing what each use case exposes; it
  does not mean returning arbitrary dictionaries by default.
- **Required contracts:** create a small query contract only when the collection actually has query
  inputs, plus separate `ClinicSummaryResponse` and `ClinicDetailResponse`-style records. The
  summary omits expensive/sensitive detail by design; the detail includes only public fields. Do
  not reuse one DTO for create, update, list, detail, persistence, and domain state.
- **Contract design:** make required versus optional fields truthful under nullable reference types;
  use immutable records/`init` members where they improve construction; bound every string and
  collection accepted from a caller; and define JSON names only where the public name intentionally
  differs from the C# name. Avoid circular graphs, navigation entities, provider types, and server-
  managed fields in request DTOs so over-posting is impossible by construction.
- **Examples and compatibility:** add representative JSON for collection, detail, and Problem Details
  responses to `docs/api/standards.md`. Identify which properties are stable, optional, nullable, or
  version-breaking. Examples use plausible synthetic values and exactly match configured serializer
  behavior; they are not hand-written shapes that the runtime cannot emit.
- **Verify and hand off:** compile the contracts, serialize representative values with the real JSON
  options, and compare names/null/enum/time behavior to the standards document. Confirm that no DTO
  exposes an Identity entity, EF proxy/navigation, concurrency token, password/security field, or
  internal database key without an explicit contract reason. Step 33 receives fixed source/target
  types and does not redesign them to satisfy the mapper.

#### 32B — Dynamic field selection companion

- **Scope:** build on the complete step 32 contracts only when a named consumer requires sparse
  response fields. This companion is adaptation guidance, not part of the numeric curriculum.
- **Implementation contract:** use a case-insensitive allow-list mapped to known response members;
  reject unknown, duplicate, and excessive fields with validation Problem Details; preserve
  deterministic output order; and never reflect over entity properties named by the caller. Keep
  the normal summary/detail DTOs as the documented schemas and authorization boundary.
- **Verify:** cover the omitted `fields` query, allowed subsets, casing, duplicates, unknown fields,
  excessive field counts, empty selection, deterministic serialization, and attempts to request
  sensitive/internal members. The same step 33 mapper and steps 34–37 numeric path must still build
  and pass when this companion is absent.

#### 33 — Mapperly mapping

- **Outcome:** add Riok.Mapperly as the sole object-mapping mechanism and produce compile-time checked
  mappings for the reference slice. This lesson replaces, rather than coexists with, all AutoMapper
  setup and the later correction notes listed in its source material.
- **Package and placement:** resolve a current stable Mapperly version compatible with the selected
  SDK, pin it in `Directory.Packages.props`, and reference it only from projects that declare mapper
  types. Put the reference-slice mappings beside the Application use case/contracts. Do not add
  runtime DI for a static mapper.
- **Required mappings:** implement explicit partial methods for resource-to-summary and resource-to-
  detail responses and for collections where needed. Request-to-entity mapping is not automatic:
  creation/update commands call domain factories/behavior so invariants and server-owned fields
  cannot be overwritten by a generated member copy.
- **Strictness:** keep Mapperly's unmapped-member diagnostics visible and choose a documented required-
  mapping strategy. Every ignored source/target member is named explicitly with a reason. Use
  `nameof`-based attributes, explicit enum conversion policy, and intentional null behavior. Do not
  suppress all Mapperly diagnostics or enable case-insensitive matching merely to silence mistakes.
- **Projection boundary:** step 33 uses ordinary in-memory mapping so its deterministic outcome does
  not depend on provider translation. Step 400 owns queryable projection, generated-expression review,
  selected-provider SQL inspection, and the rule against non-translatable custom methods in EF queries.
- **Generated-code review:** show how to inspect the `.g.cs` output without checking generated files
  into compilation twice. Verify that mapping is reflection-free, maps every intended public field,
  does not copy sensitive/internal members, and does not mutate tracked entities unexpectedly.
- **Verify and hand off:** build to force generator diagnostics; execute representative null/enum/
  nested/collection mappings; and, if projection is used, inspect SQL against the selected provider.
  Step 34 consumes only these canonical mapping methods—no controller-local `new Response(...)`,
  AutoMapper profile, or parallel manual mapper is introduced later.

#### 34 — Service-layer contracts and implementations

- **Outcome:** create the application boundary that coordinates persistence, mapping, expected
  failures, and cancellation while remaining independent of MVC. The reference slice implements
  only the collection/detail operations required to prove the pattern.
- **Public service contract:** define focused asynchronous methods such as collection and by-id reads
  returning `Result<IReadOnlyList<TSummary>>` and `Result<TDetail>`. Accept typed request values and
  `CancellationToken`; do not accept `HttpContext`, `ClaimsPrincipal`, `IActionResult`, raw query
  dictionaries, or provider-specific connection objects.
- **Persistence boundary:** the course defines a purpose-specific read port in Application
  and implements it with EF Core in Infrastructure using `AsNoTracking`, bounded queries, and the
  selected provider. Do not inject the concrete DbContext into Application or add a generic
  repository/unit-of-work façade over EF Core merely for this one slice.
- **Implementation responsibilities:** normalize application-level inputs, call the persistence port,
  map with step 33, and translate expected absence/state conflicts into stable step 10 errors. Let
  unexpected provider faults reach step 11. Do not catch `Exception`, log the same fault twice, or
  return an empty successful resource when an id is missing.
- **Mutation boundary:** no anonymous mutation is added for tutorial completeness. When later features
  create/update state, services call domain behavior, persist one coherent unit of work, handle the
  selected provider's concurrency mechanism, and return enough information for the controller to
  produce `Location`/ETag/status metadata without learning persistence details.
- **Registration:** add explicit Application and Infrastructure registration extensions with lifetimes
  justified beside the code. Enable service-provider validation in Development/tests. Avoid assembly
  scanning for a handful of services unless the project contract already selected and tests it.
- **Verify and hand off:** exercise found, not-found, empty-collection, duplicate/invalid persisted
  state where applicable, database cancellation, and unexpected database failure. Assert Result
  category/code and mapped DTO values rather than repository call counts. Step 35 validates caller
  input before this service is invoked; step 36 exposes it over HTTP.

#### 35 — Request validation

- **Outcome:** establish one asynchronous-capable boundary-validation path for MVC request DTOs.
  FluentValidation checks caller-controlled shape and inexpensive cross-field rules; domain behavior
  and database constraints remain authoritative for business invariants and races.
- **Packages and registration:** pin `FluentValidation` and
  `FluentValidation.DependencyInjectionExtensions`; register validators by a known assembly marker.
  Do not use the legacy `FluentValidation.AspNetCore` automatic MVC pipeline as the new-project
  default; that pipeline is synchronous and cannot safely run async rules. Implement one
  application-owned async MVC action filter that resolves the bound request validator, calls
  `ValidateAsync`, and maps failures through the established Problem Details contract. Show and test
  the complete filter and registration rather than leaving the validation mechanism as an agent choice.
- **Reference validator:** validate the collection query's bounded strings, counts, enum/allow-list
  values, and cross-field combinations. Route constraints handle syntactic route matching, while the
  service owns whether a well-formed id exists. Use stable FluentValidation error codes and map
  property paths/messages into the existing RFC 9457 validation response without leaking attempted
  secret values.
- **Execution contract:** validation runs once, before expensive service/database/external work, and
  receives `HttpContext.RequestAborted`. Async rules are rare and use `ValidateAsync`; uniqueness or
  authorization checks that can race do not masquerade as final validation. Malformed JSON/model-
  binding failures and FluentValidation failures produce the same documented media type and compatible
  extensions, but retain useful field keys.
- **Guardrails:** do not duplicate all rules in data annotations, controllers, services, and entities.
  Do not throw a validation exception for expected HTTP input failures unless the established global
  contract intentionally owns that path. Cap error counts and values so hostile input cannot create
  unbounded validation work or response bodies.
- **Verify and hand off:** unit-test each boundary/cross-field rule with FluentValidation's test helper,
  then integration-test valid, missing, malformed, over-limit, unknown, and multiple-error inputs.
  Prove the service is not called on failure, async cancellation propagates, errors are stable Problem
  Details, and no attempted sensitive value appears in logs. Step 36 can now remain free of rule logic.

#### 35B — Explicit per-action validation companion

- **Scope:** use this only when a specific action has an orchestration lifecycle the standard step 35
  filter cannot represent. Do not offer it as a second default or mix both paths for equivalent DTOs.
- **Implementation contract:** inject the applicable `IValidator<TRequest>`, call `ValidateAsync` with
  `RequestAborted` before service work, and send failures through the same stable validation Problem
  Details translator. Document why the action is exceptional and keep rules in the validator.
- **Verify:** prove validation executes exactly once, cancellation propagates, the service is not called
  on failure, and the response matches step 35. Steps 36–37 remain complete without this companion.

#### 36 — MVC controller/service composition

- **Outcome:** expose the reference slice through a thin, versioned MVC controller that composes the
  contracts, validator path, service, BaseApiController translation, and OpenAPI metadata already
  built in steps 10, 14, and 30–35.
- **Controller shape:** create a plural resource controller deriving from `BaseApiController`, using
  constructor injection, `[ApiController]`, the explicit URL-version route, and the actual v1 API
  version metadata selected in step 14. Actions accept bound DTO/route values and cancellation, call
  one service operation, and return `ActionResult<T>`/the established Result translator.
- **Action contract:** add collection and detail `GET` actions with stable route/action names. State
  `[AllowAnonymous]` or `[Authorize(Policy = ...)]` explicitly; PatientBooking's Clinic directory may
  be anonymous only if step 30 records that product decision. If the selected resource is not safely
  public before step 60, load the controller only in the integration-test host instead of shipping an
  unprotected route.
- **Response metadata:** use `[ProducesResponseType]`/`[Produces]` as needed so the built-in OpenAPI
  document describes success DTOs and Problem Details for validation/not-found/failure cases. Keep
  metadata synchronized with executable branches. Future creation uses `CreatedAtAction` or an
  equivalent trusted route-generated `Location`, not string concatenation from an untrusted host.
- **Thin-controller guardrails:** no DbContext/Dapper queries, mapping logic, validation rules,
  password/token logic, transaction management, broad try/catch, or service registration in the
  controller. Do not return entities, anonymous objects, raw strings, or a second response envelope.
  The controller does not convert every exception into `400`.
- **Pipeline verification:** request the supported version, an unsupported/missing URL version, an
  existing id, a missing id, and an invalid query. Inspect route selection, status, content type,
  body, version headers, trace id, one request log/span, rate-limit behavior, and the generated OpenAPI
  operation/schema. Scalar must call the same route; it is not separate proof of correctness.
- **Handoff:** step 37 receives one real or test-host-only MVC path whose production layers are complete.
  Later account and feature controllers copy this composition pattern and replace only their owned
  contracts, validators, services, mappings, authorization policies, and status branches.

#### 37 — Integration-test baseline

- **Outcome:** create the first executable proof of the HTTP pipeline and selected database, then leave
  a fixture that later account-security tutorials can extend without swapping production behavior for
  weaker fakes. Use xUnit v3, `WebApplicationFactory<Program>`, and one real Testcontainers database.
- **Projects/packages:** create a dedicated integration-test project, reference Api, and pin compatible
  `Microsoft.AspNetCore.Mvc.Testing`, xUnit v3 runner, and exactly one selected-provider module:
  `Testcontainers.MsSql` or `Testcontainers.PostgreSql`. Add a unit-test project only when pure domain/
  validator tests justify it. Never add EF InMemory or substitute SQLite for provider-backed tests.
- **Factory lifecycle:** expose `Program` to the test assembly intentionally; start one pinned database
  container; replace only database/options/external-edge registrations through the test host; apply the
  selected migration history from zero; create clients with explicit redirect/cookie behavior; and
  dispose host/container cleanly. Production `Program.cs` contains no `IsTest` branch.
- **Isolation:** choose and document one provider-compatible reset strategy—transaction boundaries where
  the entire test can share them, ordered deletes/truncation, or a reviewed reset library. Preserve
  migration/Identity metadata, reset mutable rows and sequences as required, and prevent parallel tests
  from sharing mutable account/session/limiter state. A test passes in isolation and in the full suite.
- **Baseline tests:** seed the reference resource through a fixture/DbContext boundary; assert collection
  and detail success with exact status/media type/contract values; assert not-found Problem Details;
  assert the main invalid-query validation response; assert unsupported API version behavior; and fetch
  the OpenAPI JSON to prove the operations and response schemas exist. At least one assertion proves the
  selected real provider and migrations are active rather than an accidental in-memory store.
- **Security-ready helpers:** add a replaceable `TimeProvider`/`FakeTimeProvider`, a test authentication
  scheme that creates explicit subject/claim sets without accepting forged production tokens, clean
  client factories for limiter partition tests, selected-provider database reset/seed helpers, and—on
  the local Identity branch—a thread-safe captured `IEmailSender<ApplicationUser>`. Helpers never relax
  production JWT validation, authorization policies, Data Protection, hashing, or concurrency rules.
- **Selected-provider rule:** PatientBooking runs SQL Server tests. A PostgreSQL-selected project runs
  PostgreSQL tests. The fixture, migrations, reset strategy, SQL assertions, and CI service all match
  the provider selected in step 05; connecting the fixture to the other engine is not support.
- **Verify and hand off:** run restore, build, format/analyzers, and the integration suite twice, including
  parallel execution where allowed. Confirm container startup failure is diagnosed clearly, cancellation
  stops promptly, secrets do not enter test output, and resources are disposed. Steps 60+ must add their
  first success, enumeration/failure, expiry, replay, concurrency, and cross-user tests to this fixture in
  the same lesson that introduces each security behavior.

#### 37B — Dual-provider test-matrix companion

- **Scope:** this companion applies only after a product explicitly commits to supporting SQL Server
  and PostgreSQL at the same time. It does not turn the ordinary step 05 provider choice into a runtime
  switch and is not required by any later numbered lesson.
- **Implementation contract:** maintain two provider-specific fixture registrations and migration
  histories, use each engine's real Testcontainers module and cleanup/concurrency behavior, and run
  every provider-sensitive integration test in separate CI jobs. Share only provider-neutral test
  assertions and seed intent; do not force provider-specific SQL or migration operations behind a
  misleading common implementation.
- **Verify:** create both databases from zero, run their complete migration histories and suites,
  exercise provider-specific concurrency/index/query behavior, and restore-test both operational
  paths. If later numbered lessons require dual-provider support, plan a separate all-numeric
  alternative path instead of making this companion a hidden prerequisite.

### 60 — Account identity and base authorization

Steps 60–67 execute the local ASP.NET Core Identity/first-party learning branch selected by step
04. A project using an external OIDC authority replaces registration, credential verification,
token issuance, confirmation, and recovery with that provider's supported flows while retaining
the local authorization, subject/profile mapping, abuse controls, and integration tests it needs.

#### Account service ownership and current-shape contract

The current reference project's `UsersService` has grown to roughly 782 lines and 16 public methods
because the old lessons repeatedly appended unrelated account operations. It is behavior evidence,
not the service boundary for the new run. The clean recreation never creates `IUsersService` or a
single replacement `AccountService`.

Application owns the focused contracts and auth DTOs. Infrastructure owns their local-Identity
implementations because those classes directly consume `UserManager`, `SignInManager`, EF Identity
stores, Google token verification, SMTP, and signing-key material. Api owns HTTP/request metadata
translation and injects only the service required by each action. Domain owns refresh-session and
other persistent business state that is independent of ASP.NET Core Identity.

| Contract | Introduced with complete current shape | Later numeric extensions |
|---|---|---|
| `IRegistrationService` | Step 63 declares it; step 64 implements only `RegisterAsync` | Step 200 adds `ConfirmEmailAsync` and `ResendConfirmationEmailAsync` after email delivery exists |
| `IAuthenticationService` | Step 63 declares it; step 66 implements only password `LoginAsync` | Step 201 changes lockout behavior; step 202 adds refresh/session operations; step 206 adds Google sign-in/linking orchestration |
| `IPasswordRecoveryService` | Step 203 creates `ForgotPasswordAsync` and `ResetPasswordAsync` together | No later lesson appends unrelated session, MFA, or deletion methods |
| `IPasskeyService` | Step 204 creates registration, authentication, list/rename/remove operations and ceremony DTOs | No TOTP or Google methods |
| `ITwoFactorService` | Step 205 creates setup, enable, login verification, disable/reset, and recovery-code operations | No Google, password-recovery, or refresh-management methods |
| `IJwtTokenGenerator` | Step 66 creates access-JWT generation and validation parameters | Step 205 adds the separately typed pending-authentication JWT; it never stores refresh tokens |
| `ISessionTokenIssuer` | Step 202 creates access-plus-opaque-refresh issuance and rotation orchestration | Steps 205 and 206 reuse it after TOTP or Google proof succeeds |
| `IGoogleIdTokenValidator` | Step 206 creates the provider-specific validation port and implementation | Additional providers receive their own validator and lesson rather than branches in this type |
| `IAccountLifecycleService` | Step 207 creates reauthenticated soft deletion and session cutoff | Purge scheduling and storage cleanup remain step 208 infrastructure, not methods on this service |

The controller-to-service map is fixed before the first action is written:

| Route group/action | Service dependency |
|---|---|
| register, confirm email, resend confirmation | `IRegistrationService` |
| password login, refresh, logout, session list/revoke, Google sign-in/linking | `IAuthenticationService` |
| forgot/reset password | `IPasswordRecoveryService` |
| passkey register/sign-in/manage | `IPasskeyService` |
| TOTP setup/enable/login/reset/disable/recovery codes | `ITwoFactorService` |
| reauthenticated account deletion | `IAccountLifecycleService` |

Each lesson shows the complete interface at that checkpoint. The method delta is fixed as follows;
names may be adjusted once while authoring for project conventions, but ownership may not drift:

| Step | Contract delta owned here |
|---|---|
| 64 | `IRegistrationService.RegisterAsync` |
| 66 | `IAuthenticationService.LoginAsync`; `IJwtTokenGenerator.CreateAccessTokenAsync` |
| 200 | `IRegistrationService.ConfirmEmailAsync`; `ResendConfirmationEmailAsync` |
| 202 | `IAuthenticationService.RefreshTokenAsync`, `RevokeRefreshTokenAsync`, `GetActiveSessionsAsync`, `RevokeSessionAsync`; `ISessionTokenIssuer.IssueAsync`/rotation internals |
| 203 | `IPasswordRecoveryService.ForgotPasswordAsync`; `ResetPasswordAsync` |
| 204 | `IPasskeyService` register/assert/list/rename/remove ceremony operations |
| 205 | `ITwoFactorService` setup/enable/verify-login/disable/reset/recovery operations; pending-token methods on `IJwtTokenGenerator` |
| 206 | `IAuthenticationService.ExternalLoginAsync`, `LinkGoogleAsync`, `UnlinkGoogleAsync`; `IGoogleIdTokenValidator.ValidateAsync` |
| 207 | `IAccountLifecycleService.DeleteAsync` with explicit fresh-proof input |

One `AuthController` may expose this surface while it remains thin; split it by route responsibility
if constructor dependencies or file size obscure the map. `IJwtTokenGenerator`,
`ISessionTokenIssuer`, provider validators, `UserManager`, `SignInManager`, DbContext, and email
senders are never injected into the controller. Logger extensions are named per implementation
(`RegistrationServiceLoggerExtensions`, `AuthenticationServiceLoggerExtensions`, and so on); the
new run never creates `UsersServiceLoggerExtensions`.

`IHttpContextAccessor` is not a blanket Application-service prerequisite. Api passes bounded request
metadata such as trusted client IP and user agent through a typed input when the use case needs it;
Identity/Infrastructure may register `IHttpContextAccessor` for framework components that actually
consume it. No Application contract accepts `HttpContext`, `HttpRequest`, or `ClaimsPrincipal`.

The following table is normative for the incremental DTO/service shape. A lesson must show exactly
the listed checkpoint and no later fields:

| Step | Type or contract | Complete shape owned at this checkpoint |
|---|---|---|
| 62 | `RegisterUserDto` | `Email`, `Password`, `FirstName`, `LastName`; no `Role`, profile id, hotel id, admin flag, or email-confirmed field |
| 64 | `RegisteredUserDto` | Public account id and the safe registration fields actually returned; no confirmation token, JWT, refresh token, role selector, or MFA state |
| 66 | `LoginUserDto` / `LoginResponseDto` | Request has email and password; response has the access `Token` only |
| 200 | `ResendConfirmationDto` | Email only; confirmation uses query `userId` plus opaque encoded token and never adds token data to registration output |
| 202 | refresh/session DTOs and `LoginResponseDto` | Create `RefreshTokenRequestDto` and `RefreshTokenSessionDto`; add nullable `RefreshToken` only when successful login begins issuing a pair |
| 203 | `ForgotPasswordDto` / `ResetPasswordDto` | Forgot has email; reset has `UserId`, `Token`, and `NewPassword`; both receive complete validators in the same lesson |
| 205 | TOTP DTOs and `LoginResponseDto` | Create setup/code/recovery request/response types; add `RequiresTwoFactor` and `PendingToken` only when pending login exists |
| 206 | `ExternalLoginDto` / `GoogleAuthSettings` | The request contains the declared Google provider contract and ID token; options contain the configured Google client id and exist before validation service registration |

Options are always declared, bound, validated with `ValidateOnStart`, and failure-tested before the
first implementation that consumes them. This applies to JWT options in step 66, email/frontend
options in step 200, refresh-session configuration in step 202, TOTP/pending-challenge options in
step 205, and `GoogleAuthSettings` in step 206.

- **60-Authentication-and-Authorization-Concepts** (60-Authentication-v-Authorization): Explain the identity, authentication, authorization, role, and ownership vocabulary without pretending that it is a completed login implementation.
- **61-Roles-Role-Constants-Seeding-and-Indexes** (80-Roles): Create role constants, seed the required roles, configure their database constraints/indexes, and migrate them before any registration DTO chooses a role or default profile.
- **61B-Role-Hierarchy-Reassignment-and-Legacy-Role-Repair** (80-Roles companion): Add product-specific role hierarchy, reassignment, renamed-role migration, duplicate repair, and operational controls without changing the baseline role set.
- **62-Registration-Contracts-and-Modern-Password-Policy** (60-Authentication-v-Authorization, 206-Breach-Password-Check): Create the initial request/response DTOs and one authoritative password policy: at least 15 characters while password-only accounts are permitted, at least 64 accepted characters, Unicode/whitespace/password-manager paste support, no character-class composition rules, and rejection of common/context-specific/breached values. Use HIBP's padded range API without transmitting the password or full hash, plus a small local blocklist so an outage does not remove the mandatory baseline; introduce the identical reset-password validation later when that DTO exists.
- **62B-Passkey-Only-Password-Policy-and-Breach-Check-Variations** (206-Breach-Password-Check companion): Document the policy change when passwords are not authenticators and alternate reviewed breach-check infrastructure such as an internal mirror, without weakening password accounts.
- **63-Focused-Account-Service-Boundaries-and-Shared-Prerequisites** (60-Authentication-v-Authorization, 202-Account-Lockout prerequisites): Create the focused contracts and implementation locations above, register only the framework prerequisites needed by steps 64 and 66, and establish per-service logging events. Do not create `IUsersService`, placeholder methods for later lessons, or application-layer dependencies on `HttpContext`.
- **64-Public-Registration-Service-and-Endpoint** (60-Authentication-v-Authorization): Implement `RegistrationService.RegisterAsync` and its endpoint with only the DTO fields and result fields this step owns. Public callers cannot choose privileged roles. Apply the registration-specific per-IP and normalized-account partition limits here, cap request sizes, and choose a duplicate-account response that does not become an email-enumeration oracle. Do not add confirmation methods or require email confirmation yet.
- **64B-Invitation-Only-and-Administrator-Created-Registration** (60-Authentication-v-Authorization companion): Add invite issuance/redemption, administrator-created accounts, expiry, replay protection, audit, and notification policies without becoming a prerequisite for public registration.
- **65-Account-Profiles-and-Default-Assignment** (74-Account-Profiles-and-Default-Assignment): Add the profile schema, configuration, migration, and default-profile provision inside the existing `RegistrationService` transaction immediately before JWT role resolution. In PatientBooking, public registration creates a Patient; that is a clearly labelled project-specific decision, not a generic Identity rule. Show the complete post-step registration implementation and never copy the old monolithic `UsersService` around it.
- **65B-Account-Profile-Variations-and-Operations** (74B companion): Document alternate default-profile policies, administrative reassignment rules, legacy-account backfills, and repair procedures without making any of them prerequisites for login or step 66.
- **66-First-Party-JWT-Login-and-Token-Issuance** (75-Basic-Authentication-03-JWT): Declare, bind, and startup-validate the complete JWT options first; implement `IJwtTokenGenerator` and `AuthenticationService.LoginAsync` with `JsonWebTokenHandler`, asymmetric signing and `kid`-based rotation where supported, strict issuer/audience/lifetime/signature/algorithm/type validation, minimal non-PII claims, `jti`, `auth_time`, and a ten-minute-or-shorter access lifetime. Login uses a generic failure response and owns its per-IP plus per-account limiter. State prominently that this is not an OAuth/OIDC authorization server and is not the default production issuance recommendation; refresh and MFA fields arrive only in their owning steps.
- **66B-Development-Signing-Key-Variations** (75-Basic-Authentication-03-JWT companion): Cover ephemeral or symmetric local-only signing, developer key generation, key identifiers, and rotation exercises while keeping production asymmetric-key requirements unchanged.
- **67-Self-Record-Authorization** (new; extracted from 90-Booking-Feature): Teach the ordinary “a user may access only their own record” query/service scope explicitly, using the profile introduced in step 65. This is the default ownership pattern, not the heavier resource-scoped role filter.

#### Security tutorial execution contract

This section is normative for the rewrites of steps 62–66 and 200–209. A future agent must not
copy the old lesson's final code when it conflicts with these requirements. Each tutorial starts
with its threat model and owned state transitions, implements only the fields and services that
exist at that point, and ends with the listed verification. It is the complete execution contract
for the default local-account branch; an external-identity branch maps each applicable threat and
control to provider configuration or local application responsibility instead of copying local
credential code.

#### Authentication-path contract

Every path that can result in a local access/refresh session must converge on one issuance service
and apply the same confirmed/active/deleted checks. A refresh is continuation of an existing
session, not fresh authentication, and cannot upgrade the authentication method or freshness.

| Entry path | Proof accepted before full token issuance | Required result |
|---|---|---|
| Password, no enrolled TOTP | Confirmed account + correct password + lockout/active checks | Issue the ordinary token/session pair |
| Password, enrolled TOTP | Correct password first | Issue only a short one-time pending challenge; TOTP or one recovery code completes issuance |
| Passkey | Valid, fresh WebAuthn assertion with required user verification, correct RP/origin/challenge, and active-account checks | Issue the ordinary token/session pair and record passkey as the authentication method |
| Google, new or linked user without required local TOTP | Valid Google ID token plus local active-account checks | Create/resolve by provider subject and issue the ordinary pair |
| Google, linked user with required local TOTP | Valid Google ID token first | Issue only the same pending challenge; never bypass local TOTP |
| Refresh | One active refresh token bound to an active session/family | Atomically rotate it; do not treat it as password, passkey, or MFA reauthentication |
| Recovery code | Valid pending challenge plus one unused code | Consume the code exactly once, issue the ordinary pair, and send a security notification |

The local access token records the authentication method and original authentication time for
auditing and authorization decisions. Those claims do not replace re-prompting for a credential
when an authenticator is being added, replaced, or removed.

#### 60 — Authentication and authorization concepts

- **Outcome:** establish the vocabulary and selected local-account flow without creating a fake
  working login. Define identity, credential, authentication, authorization, role, permission,
  claim, subject, profile, ownership, session, access token, refresh token, and fresh proof in the
  glossary and connect them to the decision from step 04.
- **Required boundary:** authentication answers who presented valid proof; authorization answers
  whether that subject may perform this operation on this resource. A role is not a profile row, a
  JWT is not a database session, and `[Authorize]` does not enforce resource ownership by itself.
- **Verify:** walk the planned register, confirm, password login, refresh, passkey, TOTP, Google,
  recovery, and deletion paths and name the authority, proof, local state, service owner, and
  authorization check for each. This lesson changes documentation/glossary only and ends with a
  clean build proving no speculative authentication code was introduced.

#### 61 — Roles, constants, seed data, and indexes

- **Prerequisites:** step 21's selected local-Identity schema already created `AspNetRoles`,
  `AspNetUserRoles`, primary/foreign keys, and the normalized-role-name index. This lesson verifies
  that migration instead of pretending to create Identity's role tables a second time.
- **Owns:** the exact PatientBooking baseline roles, `RoleNames` authorization vocabulary, an
  idempotent deterministic seeding strategy, any explicit role-store configuration delta, the data
  migration when seed data is migration-owned, and verification of normalized-name uniqueness and
  user-role foreign keys. The default roles are `Patient`, `Employee`, and `Admin`; a new project
  replaces them with roles justified by step 04.
- **Placement:** keep reusable role names in Application's authorization contract so Api policies
  and Infrastructure seeding share one definition. Keep `IdentityRole` configuration and seed
  implementation in Infrastructure. Public DTOs never reference this constant to accept a role.
- **Default seed path:** use stable ids and normalized names with EF configuration plus a reviewed
  migration, or choose one equally deterministic `RoleManager` deployment/startup seeder and state
  its concurrency behavior. Do not mix both. The PatientBooking numeric lesson uses one path and its
  complete files; `61B` owns alternate/legacy role operations.
- **Verify:** apply from empty and upgrade step 21, assert exactly one row per normalized role,
  duplicate normalized name rejection, valid user-role foreign keys, idempotent second execution,
  and absence of privileged role fields from anonymous OpenAPI request schemas.

#### 62 — Registration contracts and modern password policy

- **Owns:** registration/password DTO limits, Identity password options, the local common-password
  blocklist, HIBP checker, and matching FluentValidation rules. Later password-change/reset DTOs
  reuse the same policy rather than reproducing a divergent regex chain.
- **Required artifacts and wiring:** create `RegisterUserDto` and its complete validator in
  Application, define `IBreachedPasswordChecker` plus one reusable password-policy component there,
  implement `HaveIBeenPwnedPasswordChecker` and its logger events in Infrastructure with the typed
  resilient client from step 16, and register the interface/implementation and validators before
  registration consumes them. Show the exact DI additions and an integration test that proves the
  real checker is resolved. Do not create `ForgotPasswordDto`, `ResetPasswordDto`, or their
  validators in this lesson; step 203 creates those complete types and reuses this policy.
- **Required implementation:**
  - Set the password minimum to 15 while any password-only account can exist. Accept at least 64
    characters; use a finite upper bound such as 128 to limit hashing denial-of-service without
    silently truncating.
  - Disable required uppercase/lowercase/digit/symbol composition rules and allow Unicode,
    whitespace, paste, autofill, and generated passphrases.
  - Normalize accepted Unicode passwords to NFC at every password-entry boundary before length,
    blocklist, breach, hash, and verification operations. Never normalize only at registration or
    existing users can be locked out.
  - Reject a focused local list of common, context-specific, username-derived, and product-name
    passwords before the network check. Query HIBP by SHA-1 five-character prefix with
    `Add-Padding: true`; SHA-1 is only the service's lookup format, never the stored password hash.
  - A HIBP outage may fail open only because the local mandatory blocklist remains active. Record a
    metric/structured event without the password, hash, prefix, email, or username.
  - Bound email, password, name, and every other anonymous DTO field before expensive work.
- **Verify:** boundary values at 14/15/64/128/129 characters, Unicode and spaces, composition-free
  passphrases, local-blocklist rejection with HIBP unavailable, padded HIBP requests, cancellation,
  timeout behavior, and proof that no password-derived material reaches logs.

#### 63 — Focused account boundaries and shared prerequisites

- **Owns:** `IRegistrationService.RegisterAsync`, `IAuthenticationService.LoginAsync`, their typed
  request-context input where bounded client metadata is required, contract-level cancellation and
  `Result` semantics, marker types/DI extension locations, and stable per-service logging event ids.
  It declares no confirmation, refresh, recovery, passkey, TOTP, Google, or deletion method.
- **Framework placement:** the interfaces and DTO references stay in Application. Their local-
  Identity implementations will live in Infrastructure and may consume `UserManager`,
  `SignInManager`, Identity stores, and DbContext. Api controllers will consume only the Application
  interfaces. This is the explicit target; do not copy the current Application-layer
  `UsersService` constructor into a newly named class.
- **HTTP boundary:** define a small `AuthenticationRequestContext` only for values a use case
  actually needs, such as a trusted client IP and a bounded user-agent label. Api constructs it
  after forwarded-header trust is established. No service contract accepts framework HTTP types.
- **Verify:** inspect the project graph/public API, build with the new contracts, and prove no
  Application reference to MVC, Identity EF stores, Google, SMTP, or the concrete DbContext was
  introduced. Do not register a placeholder implementation merely to make DI pass before step 64.

#### 64 — Public registration

- **Owns:** `IRegistrationService`/`RegistrationService.RegisterAsync`, public registration result
  semantics, the controller action, and its named abuse policy. The request never owns a role/admin
  flag; Patient is assigned server-side for PatientBooking.
- **Required implementation:** use the same public status/body for a new and already-registered
  email where enumeration resistance is selected. Partition limits by IP and a non-reversible
  server-side representation of the normalized account key; do not place raw email addresses in
  limiter diagnostics. Keep creation/profile/role assignment transactional or compensate every
  committed step explicitly.
- **Verify:** public role escalation is impossible, duplicate requests do not create duplicate
  Identity/profile rows, parallel registration of one email has one winner, rate limits return
  `429` with the documented retry behavior, and the external response does not reveal existence.

#### 65 — Account profiles and default assignment

- **Prerequisites:** step 64 commits a local Identity account through `RegistrationService`; step 22
  established the domain modelling/index rules. No JWT role resolution exists yet.
- **Owns:** the Patient profile entity/configuration/DbSet delta, one-to-one unique user index,
  nullable unique medical-record-number rule for unassigned Patients, selected-provider migration,
  and the complete updated `RegistrationService.RegisterAsync` transaction that creates exactly one
  server-selected Patient profile before commit.
- **Project-specific boundary:** PatientBooking public registration assigns Patient. Employee/Admin
  creation remains a trusted administrative flow. Other projects state their default-profile policy
  and do not copy Patient, hotel, or role relationships as generic Identity fields.
- **Verify:** migration from empty and prior checkpoint, filtered/partial unique behavior on the
  selected provider, one user/one Patient under parallel duplicate registration, rollback when
  profile persistence fails, no orphan user/profile, and no privileged profile selected from input.
  Then show the complete current registration implementation that step 66 will query.

#### 66 — First-party JWT login and token issuance

- **Owns:** validated `JwtSettings`, `IJwtTokenGenerator`,
  `IAuthenticationService`/`AuthenticationService.LoginAsync`, the bearer authentication scheme,
  login endpoint, and initial access-only response. It does not own refresh, passkey, TOTP, or
  Google fields.
- **Required implementation:**
  - Create the complete options type and safe configuration skeleton, put signing secrets outside
    committed files, bind the selected section, validate issuer/audience/lifetime/key material, call
    `ValidateOnStart`, then register the generator and authentication service. Prove missing or
    malformed values stop startup before sending a login request.
  - Prefer asymmetric signing. Load the private signing material from user secrets/a protected
    production store, publish/use only public validation material outside the issuer, include a
    `kid`, and document active-plus-previous-key rotation. If a symmetric development branch is
    shown, label it local-only and require at least 256 random bits.
  - Pin the accepted algorithm and token type; require signature, issuer, audience, expiry, and
    lifetime validation. Use `JsonWebTokenHandler`, `TimeProvider`, a unique `jti`, zero or
    deliberately small clock skew, and a ten-minute-or-shorter access lifetime.
  - Set inbound claim mapping deliberately. The numeric path uses `MapInboundClaims = false`, emits
    the standard `sub` claim, and reads `sub` consistently; do not retain comments that assume the
    legacy handler remaps it to `ClaimTypes.NameIdentifier`.
  - Include only required authorization claims. Do not place patient data or other unnecessary PII
    in a readable JWT. Record authentication method/time without treating those claims as fresh
    proof for later authenticator changes.
  - Use `CheckPasswordSignInAsync(..., lockoutOnFailure: true)` once step 201 lands. Unknown,
    incorrect, unconfirmed, locked, and deleted accounts share the public failure contract.
  - Remove the legacy `System.IdentityModel.Tokens.Jwt` using when adopting
    `Microsoft.IdentityModel.JsonWebTokens`; keeping both makes `JwtRegisteredClaimNames`
    ambiguous. Treat `JsonWebTokenHandler.ValidateTokenAsync` as result-based asynchronous control
    flow: check `TokenValidationResult.IsValid` instead of expecting invalid input to throw. See the
    official [`ValidateTokenAsync` contract](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.jsonwebtokens.jsonwebtokenhandler.validatetokenasync).
  - State that this issuer is for the first-party learning path. It does not implement OAuth
    authorization code + PKCE, discovery, consent, scopes, client registration, or token exchange.
- **Verify:** valid token, wrong signature, wrong algorithm/type/issuer/audience, missing expiry,
  expired token, future-not-before, tampered claim, distinct `jti` values across repeated issuance,
  generic login failures, limiter partitions, and absence of secrets/PII from logs and tokens.

#### 67 — Self-record authorization

- **Owns:** the ordinary self-ownership pattern, a stable current-subject extraction boundary, the
  `/me` or equivalent self endpoint, explicit administrator policy when cross-user lookup exists,
  service/query scoping, OpenAPI auth metadata, and success/cross-account tests. It does not create
  the resource-scoped role filter from step 301.
- **Required implementation:** configure JWT claim mapping deliberately in step 66 and read the one
  documented subject claim here. Prefer a self route that does not accept a caller-supplied user id.
  When one service supports both self and authorized administration, pass the trusted subject id
  and an explicit authorized scope/filter; never infer administration from a nullable id alone.
- **Guardrails:** `[Authorize]` proves an authenticated principal only. The query still scopes rows
  to the trusted subject or applies a specific admin policy. Do not expose another user's record by
  accepting a route/query id and comparing only a role string from unvalidated input.
- **Verify:** missing/invalid token, own record, another user's id, administrator allowed/denied
  cases, missing profile, deleted/locked subject as applicable, generated SQL scope, OpenAPI bearer
  requirement, and stable `404` versus `403` concealment policy.

### 200 — Account hardening in executable order

These lessons harden the local-account branch; they are not a mandate for every project to own
passwords, TOTP seeds, recovery codes, or refresh tokens. An external-identity project documents
which controls the provider owns, configures them there, and implements only the application-side
session, authorization, notification, and account-lifecycle responsibilities that remain local.

- **200-Email-Delivery-and-Account-Confirmation** (204-Email-Sending-v2, replacing 204-Email-Sending): Bind and startup-validate `EmailSettings` and trusted frontend-link options before creating the sender; extend `IRegistrationService` with confirm/resend only here; configure smtp4dev; use the configured trusted frontend origin rather than the request `Host` header when building HTTPS links; give confirmation tokens an explicit lifetime/purpose; and prevent resend from exposing account existence through response or timing. Attach its limiter, notification-safe logging, `Cache-Control: no-store`, and referrer-leakage guidance here. Finish with register → inbox → confirm → login.
- **200B-Email-Provider-Template-Localization-and-Delivery-Operations** (204-Email-Sending-v2 companion): Add alternate SMTP/API providers, templates, localization, branding, delivery diagnostics, bounce handling, provider migration, and operational runbooks without changing the confirmation contract.
- **201-Account-Lockout-and-Login-Abuse-Controls** (202-Account-Lockout password-login portion): Configure Identity lockout and update only `AuthenticationService.LoginAsync` to use `CheckPasswordSignInAsync(..., lockoutOnFailure: true)`, while returning the same external failure for unknown, wrong-password, locked, deleted, and not-confirmed cases. Combine per-account lockout with the login endpoint's per-IP/per-account limiter, notify the real user of lockout/security events, and verify that distributed guessing is bounded without turning response details into enumeration. Do not add TOTP fields, refresh helpers, or soft-deletion methods here.
- **201B-Lockout-and-Distributed-Abuse-Control-Tuning** (202-Account-Lockout companion): Tune thresholds/windows, distributed limiter storage, degraded-mode behavior, notification suppression, unlock operations, and incident diagnostics without changing the generic failure contract.
- **202-Rotating-Refresh-Tokens-and-Recognizable-Sessions** (201-Refresh-Tokens-Sessions): Add 256-bit opaque refresh tokens stored only as SHA-256 hashes, a unique token-hash index, explicit session/family identifiers, rotation lineage, logout, list/revoke endpoints, minimal recognizable session metadata, idle/absolute expiry, and client-profile-specific transport/storage. Make rotation concurrency-safe with a transaction plus the selected provider's optimistic-concurrency mechanism—SQL Server `rowversion`, PostgreSQL `xmin`, or an explicit application-managed token—and a conditional update so only one successor can be committed. Reuse or a lost concurrency race revokes the affected family and raises a security event. Extend `IAuthenticationService`, create `ISessionTokenIssuer`, and add `RefreshToken` to `LoginResponseDto` only here.
- **202B-Session-Policy-Variations-and-Operations** (201-Refresh-Tokens-Sessions companion): Cover an optional all-sessions-on-reuse policy, richer recognizable-device policy, administrative session operations, retention cleanup, and incident-response procedures. Step 202's affected-family policy remains the deterministic baseline.
- **203-Password-Recovery-and-Session-Cutoff** (203-Password-Recovery): Create `IPasswordRecoveryService`, the complete forgot/reset DTOs and validators, email/reset-token configuration, controller actions, and account-wide refresh-session revocation in one buildable lesson after email and refresh-session persistence both exist. Use equal public response shape/timing, strict request limits, and no automatic login after reset. Send a security notification, invalidate other Identity reset tokens, revoke all active refresh sessions, and document that existing access JWTs remain usable only until their short expiry unless a deployment chooses online session validation.
- **204-Passkeys-as-the-Preferred-Phishing-Resistant-Sign-In** (new; ASP.NET Core Identity .NET 10+): Add built-in Identity passkey registration and passwordless authentication as the preferred browser-capable path, not as a hand-written WebAuthn implementation and not as Identity's built-in second-factor feature. Pin the RP/server domain and allowed origin behavior, require HTTPS and user verification, protect and single-use the attestation/assertion state, allow multiple named passkeys with resource limits, and require proof from an existing authenticator before adding or removing one. Issue the same local token/session pair after success and include a development-only same-origin browser harness because `.http`/`curl` cannot perform WebAuthn ceremonies.
- **204B-Custom-Passkey-Ceremony-State-and-Policy** (new companion): Specify the integrity, binding, expiry, one-time consumption, distributed storage, and operations required only when Identity's protected ceremony state cannot satisfy a documented deployment need. It never authorizes hand-written WebAuthn verification.
- **205-TOTP-Fallback-MFA-and-Recovery-Codes** (200-Two-Factor-Authentication plus the TOTP portion of 202-Account-Lockout): Keep TOTP as a supported fallback for clients that cannot use passkeys, while stating that manually entered OTP is not phishing-resistant. Use a short, one-time pending-authentication challenge with a separate audience/type and no authorization claims; issue no access/refresh token until it succeeds. Enrollment, reset, recovery-code regeneration, and disable require fresh proof from existing factors; never return an enrolled TOTP seed. Protect the reversible seed and default-store recovery-code value at rest, describe their actual storage accurately, attach the OTP limiter/lockout, and define the deliberate no-email-only MFA-bypass recovery policy.
- **205B-Custom-Recovery-Code-Storage** (new companion): Replace the protected stock Identity recovery-code value with an individually hashed, concurrency-safe code store only when the product accepts the extra implementation and migration cost. No numeric lesson depends on this custom store.
- **206-Google-ID-Token-Sign-In-and-Explicit-Account-Linking** (205-External-Login-Google): Create and startup-validate `GoogleAuthSettings` before registering `IGoogleIdTokenValidator`; add the Google operation to `IAuthenticationService`, its DTO/controller action, and its `.http` plus browser-token acquisition proof in the same lesson. Verify Google ID tokens with the supported library and strict signature/issuer/audience/expiry checks, resolve returning users by provider `sub`, and never anonymously attach Google to an existing local account solely because email strings match. Existing-account link/unlink is an authenticated, freshly reauthenticated action; non-authoritative third-party Google email addresses remain unconfirmed until the application verifies them. A linked user whose local TOTP policy requires a second factor receives only the pending challenge, never a full token pair. This remains sign-in federation, not delegated OAuth API access.
- **206B-Google-Workspace-and-Provisioning-Policy-Variations** (205-External-Login-Google companion): Add hosted-domain restrictions, invite-only or just-in-time provisioning, organization offboarding, provider migration, and account-linking operations without permitting anonymous email-based merging.
- **207-Reauthenticated-Account-Deletion** (207-Account-Deletion plus the soft-delete portion of 202-Account-Lockout): Create `IAccountLifecycleService.DeleteAsync` and its focused implementation; add soft-delete and retention state, but require current password/passkey proof and the enrolled MFA factor where applicable rather than trusting the bearer session. Revoke all refresh sessions, update Identity security state, notify the user, reject deleted users on password/passkey/Google/refresh paths, and document the bounded residual access-JWT lifetime. Admin deletion requires a specific policy plus recent phishing-resistant/MFA proof and audit data; role membership alone is insufficient.
- **207B-Account-Retention-Restoration-Legal-Hold-and-Admin-Deletion** (207-Account-Deletion companion): Cover product-specific retention periods, restoration, legal hold, privileged deletion workflows, audit operations, and legacy-state repair without weakening reauthentication.
- **208-Background-Account-Purge** (208-Background-Purge-Service): Add the hosted purge worker only after soft-deleted rows and retention rules exist. Make it idempotent, bounded, cancellation-aware, observable without logging deleted PII, and safe under multiple application instances.
- **208B-Distributed-Purge-Scheduling-and-Operations** (208-Background-Purge-Service companion): Add leader election or external scheduling, sharding, pause/resume, dry-run, batch tuning, recovery, and multi-instance operational procedures while keeping purge idempotent.
- **209-Optional-Broader-PII-Encryption-at-Rest** (209-PII-Encryption-At-Rest): Optional independently executable hardening for display/lookup fields after the required authentication-secret protection above. Add deterministic lookup protection only where equality lookup demands it, randomized protection for display-only data, persistent rotatable key management, greenfield column sizing/migrations, and backup/restore implications without breaking normalized Identity lookups.
- **209B-Legacy-PII-Encryption-Migration-and-Key-Operations** (209-PII-Encryption-At-Rest companion): Add resumable legacy-data backfill, mixed-version deployment, repair, key-provider variation, rotation runbooks, and rollback procedures for an existing populated system. The clean numeric recreation does not depend on legacy repair.

#### Sensitive-action proof contract

| Action | Minimum fresh proof | Mandatory side effects |
|---|---|---|
| Add the first passkey or begin first TOTP enrollment | Current password, or an assertion from an already-enrolled passkey | Notify after successful enrollment |
| Add another passkey | Assertion from an existing passkey, or current password plus current TOTP when configured | Bind ceremony state to the same user; notify |
| Reset/disable TOTP or regenerate recovery codes | Current password/passkey plus current TOTP or recovery code | Rotate/remove the seed/codes as appropriate, revoke other sessions when factor ownership is uncertain, notify |
| Remove a passkey | Assertion from a different enrolled passkey, or current password plus current TOTP; never let a passkey authorize its own removal as the last recovery path | Prevent accidental removal of every usable authenticator unless the password/recovery policy permits it; notify |
| Link/unlink Google | Authenticated local account plus current password/passkey and current TOTP when enabled | Never link from anonymous email equality; notify |
| Reset/change password | Valid reset token or current credential | Revoke custom refresh sessions, update Identity security state, notify; do not auto-login |
| Delete account | Current password/passkey and current TOTP when enabled | Revoke sessions, soft-delete, notify, audit |
| Security-sensitive admin action | Specific authorization policy plus recent passkey/MFA proof | Audit actor, target, action, result, and correlation id without secrets |

#### 200 — Email confirmation

- **Owns:** complete validated `EmailSettings` and frontend-link options,
  `IEmailSender<TUser>` integration, the confirm/resend additions to `IRegistrationService` and its
  implementation, email-specific logger events, confirmation token provider/lifetime,
  confirmation/resend DTO/actions, and their named limiters.
- **Required implementation:**
  - Declare the options types before sender/service code, bind their exact sections with
    `AddOptions`, validate host/port/sender and an allowed HTTPS frontend origin, call
    `ValidateOnStart`, then register the sender and registration implementation. Prove missing
    binding fails startup instead of producing an empty SMTP host and port `0` at first send.
  - Build links only from configured allowed HTTPS origins; never derive a reset/confirmation host
    from an untrusted `Host` or forwarded header. Base64Url-encode opaque token material.
  - Give confirmation tokens a distinct purpose and explicit lifetime (for example 24 hours), and
    ensure the Data Protection key ring survives the target hosting restart/rotation model.
  - Resend returns the same public response for missing, already-confirmed, and pending accounts.
    Avoid materially different response time: dispatch behind an abstraction/queue or apply and
    test a bounded equalization strategy. Do not call it non-enumerable based only on response text.
  - Do not log full URLs, query strings, tokens, or SMTP credentials. Responses containing account
    token material use `Cache-Control: no-store`; the future frontend sets a no-referrer policy.
- **Verify:** complete register → receive → confirm → login; malformed/expired/replayed token;
  resend existence/timing contract; host-header injection; SMTP outage behavior; rate limits; and
  restart behavior with persisted versus deliberately ephemeral development keys.

#### 201 — Lockout and login abuse controls

- **Owns:** Identity lockout options, the lockout-aware current shape of
  `AuthenticationService.LoginAsync`, password failure counting, login-specific limiter, external
  failure contract, per-service logger events, and lockout notification. TOTP failure counting
  remains step 205.
- **Required implementation:** use Identity's lockout APIs rather than editing counters directly.
  Combine account-scoped lockout with caller-scoped throttling because either one alone is
  bypassable. Keep the HTTP result generic even after lockout so the state cannot confirm account
  existence; expose recovery guidance through a separate generic flow. Treat aggressive lockout as
  a denial-of-service tradeoff and document the chosen threshold/window.
- **Verify:** threshold boundary, successful reset of failure count, distributed-IP attempts,
  password spraying across accounts, parallel failures, generic unknown/wrong/locked responses,
  and notification/log redaction.

#### 202 — Refresh rotation and sessions

- **Prerequisites:** step 66 access-token issuance and step 201's final password-login checks are
  working. This lesson does not depend on password recovery, passkeys, TOTP, Google, or deletion.
- **Owns:** `RefreshToken` persistence, `RefreshTokenConfiguration`, validated
  `RefreshSessionOptions`, migration, DTOs,
  `ISessionTokenIssuer`, and the refresh/session additions to `IAuthenticationService`; token
  generation/hash, pair issuance, rotation/reuse handling, client transport, logout, session
  list/revoke endpoints, security events, and the first `LoginResponseDto.RefreshToken` field.
- **Required placement and files:**
  - Domain: `Entities/RefreshToken.cs` and `Enums/RefreshTokenRevocationReason.cs`. The entity exposes
    its stable `UserId` and session/family state; it never references Infrastructure's Identity user.
  - Application: `RefreshTokenRequestDto`, `RefreshTokenSessionDto`, the complete current
    `LoginResponseDto`, `ISessionTokenIssuer`, session persistence/notification ports, and the exact
    new `IAuthenticationService` signatures.
  - Infrastructure: EF `RefreshTokenConfiguration`, DbContext/DbSet edits, EF persistence, hashing
    and session-issuance implementation, `RefreshSessionOptions` binding/validation, DI
    registrations, and the selected-provider migration.
  - Api: refresh/logout/session actions, selected browser cookie/CSRF behavior, and `.http` requests.
  If helpers such as `BuildRefreshToken`, `GenerateRawToken`, or `HashToken` are used, the lesson
  defines their complete signatures and bodies before the first call; prose never names an absent
  helper as though the reader already owns it.
- **Configuration and DI:** create the complete `RefreshSessionOptions` type before the services,
  bind its section, validate positive idle/absolute lifetimes and their ordering with
  `ValidateOnStart`, then register services with lifetimes compatible with the scoped DbContext.
  `LoginResponseDto.RefreshToken` is nullable because the value is returned only on a successful
  full session issuance; raw tokens are never added to entities, logs, or exception messages.
- **Required persistence model:** session/family id, unique token hash, public stable user id,
  created/last-used/idle-expiry/absolute-expiry/revoked timestamps, revocation reason, replacement
  relationship, bounded recognizable client metadata, and a selected-provider concurrency token:
  SQL Server `rowversion`, PostgreSQL `xmin`, or a deliberate application-managed equivalent.
- **Required rotation:** generate 256 random bits, expose the Base64Url raw value once, store only
  its SHA-256 hash, load the active row and active user, then revoke-old/create-successor in one
  transaction. A conditional update plus optimistic concurrency makes one simultaneous request win.
  Replay or a concurrency loser revokes the affected family, returns the same generic failure as a
  missing/expired token, and emits a bounded security event.
- **Client profiles:** `.http`/`curl` may receive the token in the response for tutorial proof;
  native apps use platform secure storage. Browser production uses a same-origin BFF or a
  `__Host-RefreshToken` cookie with `HttpOnly`, `Secure`, explicit `SameSite`, `Path=/`, and no
  `Domain`, plus CSRF defense for cookie-authenticated mutations. Never recommend web storage.
- **Migration and verification:** show the exact migration command/name, inspect the unique hash
  index, user/session indexes, foreign keys, and concurrency mapping, then apply from empty and
  upgrade the prior checkpoint. Verify login pair issuance, hash/raw-value separation, expiry
  boundaries, one winner from two simultaneous refreshes, replay-family revocation, locked/security-
  changed user rejection, ownership-scoped session listing/revocation, logout idempotency, cookie/
  CSRF behavior when selected, and token/log redaction. Step 207 later adds the deleted-account case
  because deletion state does not exist yet.

#### 202B — Session policy variations and operations

- **Scope:** extend step 202 for a product that deliberately revokes every user session after refresh
  reuse, stores richer recognizable-device metadata, exposes privileged session administration, or
  needs retention and incident-response runbooks. None changes the numeric baseline.
- **Security contract:** justify metadata and retention; never use a user-agent string as proof of
  device identity. Administrative access requires a specific policy, fresh proof, ownership-safe
  queries, audit data, and no raw token/hash disclosure.
- **Verify:** affected-family versus all-session behavior, notification deduplication, metadata bounds,
  cleanup, privileged cross-user access, bulk-revocation concurrency, and redaction. No numeric lesson
  depends on this companion.

#### 203 — Password recovery and session cutoff

- **Prerequisites:** step 62's authoritative password policy and `IBreachedPasswordChecker`, step
  200's working email sender/frontend-link configuration, step 201's active-account checks, and step
  202's refresh-session revocation port are all present and integration-tested.
- **Owns:** `ForgotPasswordDto`, `ResetPasswordDto`, both complete validators,
  `IPasswordRecoveryService`/implementation, the forgot/reset controller actions, password-reset-
  specific token lifetime/purpose, reset-email sender method, named rate-limit policies, security
  notification, and actual all-session cutoff after success. Nothing is deferred to another lesson.
- **Required files and registrations:** the lesson lists and shows the complete DTOs, validators,
  service contract/implementation, email contract/current sender shape, controller/current action
  shape, options binding, DI changes, `.http` requests, and tests. It confirms the HIBP client,
  `IBreachedPasswordChecker`, FluentValidation, Identity token providers, email sender, and session
  revoker from their owning earlier steps instead of recreating or silently assuming them.
- **Required behavior:**
  - Forgot-password accepts an email, always returns the same public status/body, uses account-plus-
    IP throttling, and has tested timing behavior. It never locks an account merely because reset was
    requested.
  - The reset link carries a Base64Url-encoded opaque token and user id under the configured trusted
    HTTPS frontend origin. `ResetPasswordDto` has exactly `UserId`, `Token`, and `NewPassword`.
  - Validate the new password with the same normalization, bounds, local blocklist, contextual check,
    and breached-password checker as registration. Do not copy a second regex-based policy.
  - An arbitrary/missing user id and an invalid/expired/replayed token receive one generic reset-
    failure contract. Show detailed password-policy feedback only after the token/account proof is
    valid enough to evaluate the replacement password without becoming an account oracle.
  - A successful reset calls Identity, revokes every custom refresh session through step 202's real
    store, updates the relevant Identity security state, sends the security notification, and never
    signs the caller in automatically. Existing self-contained access JWTs remain valid only for the
    short residual lifetime from step 66 unless the product adopts online validation.
- **Primary `.http` proof:** request reset for a known address; repeat for an unknown address and
  compare the public result; open smtp4dev; copy and decode the real `userId`/token from the link;
  submit the exact reset JSON body; prove the old password and every pre-reset refresh token fail;
  prove the new password succeeds; then prove the same link cannot be reused. Include malformed,
  expired, second-link-invalidated, password-policy, SMTP-outage, limiter, and notification cases.
- **Handoff:** passkeys, TOTP, Google, and deletion can rely on one complete password-recovery path.
  No future lesson edits password recovery merely to add a prerequisite that should have existed here.

#### 204 — Passkeys

- **Owns:** `IPasskeyService`/implementation and logger events, complete ceremony/manage DTOs,
  .NET 10 Identity passkey schema/options, registration/authentication ceremonies, list/rename/remove
  management, controller actions, authentication-method recording, resource limits, notifications,
  and a development-only same-origin browser harness.
- **Required implementation:**
  - Use Identity/`SignInManager` passkey APIs. Do not implement WebAuthn parsing, challenge signing,
    attestation, or assertion verification manually.
  - Explicitly configure the RP/server domain and allowed-origin behavior, require HTTPS outside
    localhost, validate proxy/host configuration, require user verification, and do not host
    untrusted content beneath the credential's RP domain.
  - Use Identity's protected temporary ceremony state, bind completion to the initiating
    user/session and ceremony type, expire it quickly, and consume it once. Never accept a user id
    supplied only by the completion request. The numeric path does not add custom state storage.
  - Allow several named passkeys but cap count and display-name length. Adding/removing requires the
    sensitive-action proof contract; prevent silent cross-account registration and unsafe removal
    of the last usable authenticator.
  - Treat a passkey as a phishing-resistant primary/passwordless authentication path. Do not claim
    the built-in Identity feature is automatically the app's TOTP-style second factor.
  - Keep the test harness disabled outside Development. It exists because Scalar, `.http`, and
    `curl` cannot call `navigator.credentials` or prove the real RP/origin ceremony.
- **Verify:** real browser register/sign-in on the configured origin, wrong origin/RP/challenge,
  expired/replayed state, cross-user completion, missing user verification, duplicate credential,
  count/name limits, remove authorization, deleted/locked account, session issuance, and build/tests
  without enabling the development harness in Production.

#### 204B — Custom passkey ceremony state and policy

- **Scope:** use this companion only when a documented distributed-deployment or ceremony-lifecycle
  requirement cannot use Identity's protected state. Continue using Identity/`SignInManager` for
  WebAuthn creation and verification; this companion does not authorize custom cryptography.
- **Implementation contract:** integrity-protect state, bind it to RP, origin, ceremony type, user or
  discoverable-login context, initiating session, and challenge; store only necessary data; set a
  short expiry; enforce one-time atomic consumption; cap outstanding ceremonies; and share the
  persistence mechanism safely across instances. Define cleanup, key rotation, and deployment overlap.
- **Verify:** wrong user/session/type/RP/origin, expiry, replay, concurrent completion, instance
  failover, key rotation, cleanup, and resource-exhaustion bounds. Step 205 remains independent.

#### 205 — TOTP fallback and recovery

- **Prerequisites:** confirmed-email login, lockout, step 202's `ISessionTokenIssuer`, step 204's
  sensitive-action proof path, persistent Data Protection keys, and the security-ready test fixture.
- **Owns:** `ITwoFactorService`/implementation, its logger events, complete TOTP/recovery DTOs,
  pending-authentication entity/configuration/DbSet/migration, the pending JWT extension to
  `IJwtTokenGenerator`, TOTP setup/enable/login/reset/disable, recovery-code generation/redemption/
  regeneration, factor-change proof, controller actions, options/DI, limiter/lockout, at-rest
  protection, notifications, and the `RequiresTwoFactor`/`PendingToken` response fields.
- **Required flow and endpoints:**
  - `POST /api/auth/2fa/setup` requires an authenticated user plus current password or existing
    passkey assertion. It creates/returns a seed only while TOTP is not enabled. It is not a cacheable
    `GET`, and it never returns the existing seed after enrollment.
  - `POST /api/auth/2fa/enable` verifies a current TOTP before enabling and returns recovery codes
    exactly once. `POST /api/auth/2fa/login` accepts only a valid unconsumed pending challenge plus
    TOTP or one recovery code.
  - Reset/disable/regenerate endpoints implement the sensitive-action proof table. Reset rotates the
    seed; disable removes/invalidates it and recovery codes. A bearer access token alone is never
    sufficient.
- **Pending challenge:** use a five-minute-or-shorter audience/type distinct from access tokens,
  minimal `sub`/`jti`/source-method claims, no roles or authorization claims, pinned validation
  parameters, and a persisted consumed/expiry state with concurrency control. Success consumes it
  once before issuing the token pair; failure increments the appropriate OTP attempt control.
- **Options, DI, and migration:** declare and startup-validate pending audience, lifetime, issuer,
  and allowed attempt bounds before registering `ITwoFactorService`. Register it as scoped with the
  scoped Identity/DbContext dependencies; keep `IJwtTokenGenerator` free of mutable request state.
  Generate and inspect the pending-challenge migration before exposing login verification. Prove the
  unique `jti`, user/expiry lookup index, consumed-state concurrency token, and cleanup path.
- **Secret storage and recovery:** the server must recover the TOTP seed, so protect it with the
  configured Identity personal-data protection/key ring rather than claiming it is hashable. Stock
  Identity recovery codes are stored as a joined token value, not individually hashed; protect that
  value at rest with the same reviewed key-ring policy. The baseline has no email-only/help-desk
  bypass: if all passkeys/TOTP/recovery codes are lost, the account is unrecoverable until a
  separately specified high-assurance recovery lesson exists.
- **Primary `.http` proof:** the lesson supplies a send-in-order request block and explains the IDE
  state, not only the raw requests:
  1. Register, open smtp4dev, confirm the email, and send the named password-login request once.
  2. Send `POST /api/auth/2fa/setup`. Copy the returned shared secret into an authenticator app with
     **Enter a setup key manually**; setup does not accept a TOTP code and QR scanning is optional.
  3. Generate one live code and send it to `POST /api/auth/2fa/enable`. Store the returned recovery
     codes once; do not expect the setup secret to be returned after enrollment.
  4. Wait for the next TOTP time window, send the named password-login request again, and confirm it
     returns only `requiresTwoFactor` plus `pendingToken`, never access/refresh tokens.
  5. In Visual Studio's `.http` file, send every `# @name` request before consuming its response and
     assign chained values on their own lines, for example
     `@pendingToken = {{twoFactorPasswordLogin.response.body.$.pendingToken}}`. Do not inline an
     unsent response expression inside a header; that produces HTTP0012.
  6. Generate a new live code and send `POST /api/auth/2fa/login` with the pending token. Confirm the
     full access/refresh pair appears only now. Repeat with one recovery code and prove one-time use.
- **Verify:** seed shown only before enable, no seed on enrolled setup, correct/incorrect/expired
  TOTP, recovery code one-time use, pending challenge wrong audience/type/expiry/replay/concurrency,
  OTP limiter and lockout, stolen-access-token attempts to reset/disable, at-rest value not equal to
  plaintext, notifications, and proof that no secret appears in logs/cache.

#### 205B — Custom recovery-code storage

- **Scope:** replace the stock joined recovery-code token value only when individually hashed codes
  are an explicit product requirement and the team accepts ownership of generation, redemption,
  regeneration, migration, and concurrency behavior. High-assurance account recovery is a separate
  independently executable feature and must receive its own numeric lesson if later planned.
- **Implementation contract:** generate high-entropy one-time codes, store only a slow or keyed hash
  appropriate to the code entropy/threat model, identify candidate rows without exposing the raw
  code, redeem atomically, invalidate prior sets on regeneration, and preserve the step 205 proof,
  limiter, notification, and no-enumeration contracts.
- **Verify:** raw-value absence, exact one-time use under concurrent redemption, regeneration cutoff,
  migration from the protected stock value, brute-force controls, backup/restore, and log redaction.
  Later numeric lessons remain compatible with the stock step 205 store.

#### 206 — Google sign-in and linking

- **Prerequisites:** step 03's options/secrets pattern, step 16's outbound-dependency conventions,
  step 202's `ISessionTokenIssuer`, step 205's pending-MFA path, and the active/locked checks shared
  by every authentication entry path.
- **Owns:** `ExternalLoginDto`, the complete `GoogleAuthSettings` type, Google options binding and
  startup validation, `IGoogleIdTokenValidator` plus its Infrastructure implementation, the Google
  extension to `IAuthenticationService`, external-login/link/unlink actions, provider-subject
  persistence, new-user/default-profile policy, local-MFA convergence, limiter, notifications,
  `.http` proof, and the browser-side procedure for obtaining a real test ID token. It does not
  request a Google API access token or implement delegated OAuth scopes.
- **Configuration must precede consumption:** create `GoogleAuthSettings` before the validator, give
  it the exact `Authentication:Google` section name and required `ClientId`, bind with `AddOptions`,
  validate non-empty values, and call `ValidateOnStart` before registering the validator or auth
  service. The OAuth client id is a public identifier, not a credential, but keep its environment-
  specific value in the selected configuration source. Never leave a `googleAuthOptions` constructor
  parameter whose type/binding was not declared. This ID-token-verification flow does not consume a
  client secret; do not add an unused secret property.
- **Required implementation:** reference `Google.Apis.Auth` only from Infrastructure. Validate with
  Google's maintained library and constrain signature, issuer, audience/client id, expiry, and
  `email_verified` as applicable, following [Google's backend ID-token verification
  contract](https://developers.google.com/identity/sign-in/web/backend-auth). Google's library may
  retrieve and cache Google's signing keys, so treat validation as an outbound dependency with a
  bounded adapter wait, failure behavior, metrics, and safe logging. The maintained static
  `ValidateAsync` overload has no cancellation-token parameter; do not claim caller cancellation
  aborts its underlying certificate refresh. Do not repeat the old claim that the server never
  contacts Google. Resolve a
  returning link by `(provider, sub)` before considering email. Never call `AddLoginAsync` on an
  anonymously found local email account. Linking is performed only by the signed-in owner after the
  sensitive-action proof contract.
- **Email rule:** Gmail addresses and verified Workspace-domain claims can be authoritative under
  Google's documented rules. A verified third-party address without Google's hosted-domain
  authority is not sufficient to mark the local email confirmed or merge accounts; send the
  application's own confirmation or keep it as untrusted display/contact data.
- **MFA rule:** after provider verification and local user resolution, apply the same active/deleted
  checks as password/passkey login. If the linked local account requires TOTP, return only the
  pending challenge and complete through step 205 before token issuance.
- **Primary `.http` proof:** provide a Development-only same-origin Google Identity Services harness
  or an exact documented client procedure configured with the same client id. Paste its short-lived
  ID token into a local `.http` variable sourced from secrets/environment, then send the external
  login request. `.http` cannot perform Google's interactive browser ceremony, and a token minted
  for another client id is an expected audience failure. Verify first-time provisioning, returning
  `(provider, sub)` resolution, and explicit authenticated linking separately.
- **Verify:** bad signature/issuer/audience/expiry, unknown provider, returning provider subject,
  anonymous same-email takeover attempt, explicit link/unlink proof, concurrent linking uniqueness,
  missing/invalid options startup failure, key-retrieval/provider outage, non-authoritative email
  handling, local-TOTP no-bypass, deleted/locked account, limiter, session issuance, event
  notification, and absence of ID tokens/provider claims from logs.

#### 207–209 — Deletion, purge, and broader encryption

- **Deletion verification:** stolen access token without fresh credentials cannot delete; password,
  passkey, TOTP, Google-linked, admin, already-deleted, last-admin, concurrent-delete, refresh-after-
  delete, and residual-access-JWT cases all have explicit expected results. Every login/refresh path
  rejects the deleted user rather than relying on password-login lockout alone.
- **Purge verification:** two worker instances cannot purge twice or exceed the batch; retention
  cutoff uses `TimeProvider`; failures retry safely; cancellation stops between records/batches;
  logs contain stable ids/counts rather than deleted PII.
- **Broader encryption boundary:** do not make the optional step responsible for the TOTP seed,
  recovery-code store, private signing material, SMTP password, or Data Protection key ring—those
  are already required secrets. This step covers additional application PII and must prove new-write
  protection, lookup behavior, key rotation, schema sizing, and backup/restore on the selected provider.

#### 209B — Legacy PII encryption migration and key operations

- **Scope:** adapt step 209 for a populated database, a different approved key provider, or an
  operational rotation/repair requirement. The clean numeric recreation contains no legacy backfill.
- **Implementation contract:** inventory columns and lookup dependencies; make schema expansion
  backward-compatible; deploy readers that tolerate the migration state; backfill in resumable,
  observable, bounded batches; verify before removing plaintext; and document rollback limits,
  key custody, rotation overlap, backup/restore, and disaster recovery. Never log plaintext or keys.
- **Verify:** mixed old/new rows, interrupted/resumed batches, concurrent writes, lookup parity,
  corrupted ciphertext isolation, old/new key overlap, rollback before and after plaintext removal,
  restore into a clean environment, and provider-specific transaction/locking behavior. No numeric
  lesson may depend on this companion.

### 300 — First feature, then reusable data-access capabilities

- **300-Reference-Domain-Feature** (90-Booking-Feature): Build one complete project-specific feature—model/configuration/migration, DTOs, validation, service, controller, ordinary ownership authorization, and verification—using all foundations above. Booking is the current reference feature, not a generic required domain.
- **300B-Reference-Feature-Policy-Variations** (90-Booking-Feature companion): Add alternate booking/cancellation/availability policies, administrative operations, seed/demo data, and legacy repair without changing the reference feature required by later lessons.
- **301-Resource-Scoped-Authorization** (85-Custom-Authorization-Filters): Add the project-specific “role within a particular resource” filter only now, when the reference feature supplies the protected resource relationship it requires.
- **302-EF-Core-Querying-and-Manual-Mapping** (20-EFCore-Querying-and-Manual-Mapping): Add query composition against a real feature model, then use the existing Mapperly rules rather than a parallel mapping style.
- **303-Dapper-Reporting** (22-Dapper-Reporting): Optional. Add Dapper only when a real read/report crosses EF Core’s practical boundary; it is not a CRUD default. Write and test SQL for the provider selected in step 05. If the product truly supports both engines, own two reviewed query implementations/tests where syntax or behavior differs rather than claiming one provider-specific query is portable.
- **310-Keyset-Pagination** (113-Paging-Keyset): Make keyset pagination the default for mutable collections. Define a stable, unique ordering and cursor contract, then verify that adjacent pages neither skip nor duplicate rows.
- **310B-Cursor-Signing-Versioning-and-Compatibility** (113-Paging-Keyset companion): Add opaque signed cursors, key rotation, contract versioning, expiry, backward compatibility, and troubleshooting when a public or hostile-client API requires them.
- **311-Filtering-and-Search** (111-Filtering): Add explicitly allowed filter and search fields, never arbitrary query-to-SQL projection.
- **312-Sorting-and-Query-Indexes** (112-Paging-Filtering-Sorting-Best-Practices): Add sortable-field allow-lists, require a unique tie-breaker compatible with the cursor, and teach the corresponding database indexes in the same step before calling the list endpoint complete.
- **312B-Provider-Specific-Indexing-and-Collation** (112-Paging-Filtering-Sorting-Best-Practices companion): Provide SQL Server/PostgreSQL index, collation, case/accent, partial/filtered-index, and query-plan variations while preserving the provider-neutral sort contract.
- **313-Offset-Pagination** (110-Paging): Optional alternative for endpoints that genuinely require page numbers or random page access. Keep keyset pagination as the default path.
- **320-PATCH-versus-PUT** (120-PATCH-vs-PUT): Decide update semantics and validation expectations before writing the patch endpoint.
- **321-PATCH-Implementation** (121-PATCH-Code): Implement the selected PATCH flow with the serializer, DTO, validation, and error contract already established.

### 400 — Measured performance and feature-triggered integrations

- **400-EF-Core-Performance** (130-EFCore-Performance, 131-Mapperly-For-EFCore-Performance): Measure actual generated SQL and query plans on the selected real provider, then apply no-tracking, projection, indexing, and Mapperly improvements. Provider-specific indexes or query rewrites must be labelled and verified on that engine. Merge the Mapperly correction into the canonical lesson.
- **400B-Provider-Specific-Query-Plan-and-Tuning-Field-Guide** (130-EFCore-Performance companion): Add SQL Server/PostgreSQL plan capture, parameterization, statistics, index, lock, and provider-specific query-rewrite guidance after measurements justify it.
- **410-Caching-with-HybridCache** (140-Caching-Performance, 141-Caching-Types): Apply cache-aside by default to eligible read endpoints using HybridCache. Partition user- or tenant-specific keys, exclude writes and unsuitable volatile or sensitive responses, and define invalidation beside every mutation that can stale the cached data.
- **410B-Distributed-HybridCache-Topology-and-Key-Migration** (140-Caching-Performance companion): Cover distributed backends, serializer/version changes, key migration, stampede behavior, regional topology, failover, and cache operations without making distributed caching mandatory.
- **420-Feature-and-Expensive-Endpoint-Rate-Limit-Refinement** (150-Rate-Limiting): Refine limits for expensive searches, reports, uploads, and external integrations and load-test `429`/retry behavior. Authentication, confirmation, recovery, refresh, MFA, and external-login policies already exist from their owning lessons; this step may tune measured values but must not be their first protection.
- **420B-Distributed-Rate-Limiter-Storage-and-Production-Tuning** (150-Rate-Limiting companion): Add shared-state algorithms, multi-region behavior, degraded-mode policy, partition migration, dashboards, alerts, and production load-tuning without weakening endpoint-owned security limits.
- **430-SignalR-Booking-Notifications** (91-SignalR-Booking-Notifications): Optional. Add real-time notifications only when the product has a live-update user experience.
- **430B-SignalR-Scale-Out-and-Operations** (91-SignalR-Booking-Notifications companion): Cover managed hubs/backplanes, user routing, reconnect/resume behavior, deployment draining, capacity, authorization diagnostics, and scale-out troubleshooting.
- **440-RabbitMQ-Booking-Confirmation** (92-RabbitMQ-Booking-Confirmation): Optional. Add durable asynchronous email delivery only when a committed feature has a side effect that must survive request/process failure. Step 200 shows the email sender as it exists at that point; this lesson ends with the sender's complete current contract after the RabbitMQ extension.
- **440B-RabbitMQ-Topology-Dead-Letter-Replay-and-Operations** (92-RabbitMQ-Booking-Confirmation companion): Add provider/topology variations, dead-letter inspection, bounded replay, poison-message handling, retention, monitoring, and disaster operations. A transactional outbox remains a separate numbered feature if later required.

### 500 — Deployment choices

- **490-Production-Security-Gate-and-Identity-Confirmation** (new; required before either deployment path): Reconfirm the identity architecture selected in step 04 and decide whether the first-party learning issuer is being replaced by an established OAuth 2.0/OIDC authorization server or deliberately retained for a constrained first-party deployment. Do not turn the tutorial token service into a home-grown OAuth server. Record browser/native client profiles, phishing-resistant authentication requirement, signing/Data-Protection key custody and rotation, immediate-vs-bounded token revocation, trusted proxy/host/origin settings, dependency/secrets scan results, selected-database support/lifecycle status, backup/restore tests, alert ownership, and the explicit ban on real patient data until this gate passes.
- **490B-Production-Security-Evidence-and-Incident-Response** (new companion): Provide evidence templates, review checklists, alert/incident ownership, compromise exercises, dependency exceptions, and audit retention for deployments that need a formal operational record.
- **500-Azure-Deployment** (190-Microsoft-Azure): Optional deployment path. Provision the selected compatible managed database target—Azure SQL for the SQL Server branch or an explicitly chosen PostgreSQL service for the PostgreSQL branch—without changing providers during deployment. Configure managed secrets/keys, persistent Data Protection storage, HTTPS/proxy trust, production identity integration, reviewed migrations, backup/restore testing, and provider-specific monitoring before verifying Azure resources.
- **500B-Azure-Topology-Rollout-and-Rollback-Variations** (190-Microsoft-Azure companion): Cover alternate Azure hosting/database/network topologies, slots or blue-green rollout, regional recovery, cost controls, rollback, and operational troubleshooting for the selected numeric Azure path.
- **501-Docker-VPS-CI-CD** (191-CICD-Docker-VPS): Alternative deployment path. Containerize the application and the selected database topology without baking secrets or private keys into images; do not deploy both database engines unless the product actually supports both. Persist/restrict the Data Protection key ring, configure CI/CD and the reverse proxy's trusted forwarded headers/TLS/security headers, integrate the selected production identity path, run reviewed provider-specific migrations, restore-test the chosen database, and verify the VPS deployment. Do not perform both 500 and 501 for one target.
- **501B-VPS-Proxy-Registry-Backup-and-Deployment-Operations** (191-CICD-Docker-VPS companion): Cover reverse-proxy and registry alternatives, host hardening, rollout/rollback, backup rotation, restore drills, certificate operations, monitoring, and failure recovery for the selected VPS path.

## Regression closure ledger

This table converts `RECREATE/01-Recreation-Notes.md` into enforceable plan outcomes. Lesson authors
still classify the observations relevant to their individual source lesson, but they may not reopen
an item already settled here without current primary evidence.

| Historical defect or proposal | Disposition in this plan | Enforcement point |
|---|---|---|
| Prerequisites were scattered across old notes and history | Carried forward and generalized | Source precedence, mandatory dependency manifest, delta table, and one-lesson author/execution modes |
| Base result/error/controller plumbing appeared after controllers needed it | Carried forward | Steps 10–11 precede every real controller and service flow |
| Generic host infrastructure was batched late without dependencies | Carried forward | Steps 10–16 frontload error handling, logging, telemetry, API docs, host security, rate limiting, and outbound HTTP |
| `RoleNames` and role tables appeared after registration DTOs referenced them | Carried forward | Step 61 creates roles, constraints, seed, indexes, and migration before step 62 DTOs |
| HotelListing let public registration choose `Role`/`AssociatedHotelId` | Rejected as project-specific and unsafe for the PatientBooking public path | Step 62's exact DTO shape and step 64's server-side Patient assignment |
| The ordinary “user can access only their own record” rule had no lesson | Carried forward | Step 67, separate from step 301's heavier resource-scoped role pattern |
| `LoginAsync` called a missing `GenerateTokenAsync` from a future JWT lesson | Carried forward | Step 66 creates validated JWT options, `IJwtTokenGenerator`, login, bearer registration, endpoint, and proof together |
| Legacy JWT code and `JwtRegisteredClaimNames` namespace ambiguity | Carried forward | Step 66 requires `JsonWebTokenHandler`, removal of the legacy using, and result-based async validation |
| Profile rows required by role resolution appeared after JWT | Carried forward | Step 65 creates/migrates/defaults the Patient profile before step 66 |
| One `UsersService` accumulated registration, login, email, recovery, refresh, TOTP, and Google | Carried forward with stronger evidence | Focused account ownership table; no `IUsersService`; per-service DI, logging, DTO, and controller map |
| `IHttpContextAccessor` was an undeclared dependency in old code | Adapted, not copied as a blanket dependency | Api passes typed bounded request metadata; only Identity/Infrastructure framework components that consume the accessor register it |
| `LoginResponseDto` showed refresh and MFA fields before their behavior existed | Carried forward | Normative DTO current-shape table: access at 66, refresh at 202, pending MFA at 205 |
| Email options existed but were never bound, leaving empty host/port values | Carried forward and strengthened | Step 200 declares, binds, validates, and startup-failure-tests SMTP plus trusted frontend options before sender DI |
| Account-lockout lesson previewed future TOTP and deletion enforcement | Carried forward | Step 201 changes password login only; steps 205 and 207 own their entry paths |
| Refresh-token lesson omitted entity configuration, DTOs, helpers, user id, migration, and concurrency proof | Carried forward | Step 202 artifact list, helper completeness rule, options/DI, migration, indexes, public stable user id, and real-provider concurrent rotation tests |
| Password recovery omitted DTOs, validators, email prerequisite, breach checker, DI, and executable proof | Carried forward | Step 203 owns all files and registrations and includes the smtp4dev → reset → old/new login → replay `.http` sequence |
| Password recovery preceded the refresh store but promised to revoke it later | Rejected as a hidden forward dependency | Refresh/session persistence is step 202; complete password recovery follows at step 203 and revokes real rows immediately |
| TOTP had no end-to-end verification and `.http` chained variables failed silently | Carried forward | Step 205's named-request send order, manual setup-key instructions, separate enable/login codes, explicit variable assignment, and HTTP0012 diagnosis |
| Old TOTP used a cacheable `GET` to create/return setup material | Rejected for the new API contract | Step 205 uses authenticated `POST /2fa/setup` and returns a seed only before enrollment |
| Google code consumed an undeclared `googleAuthOptions` value | Carried forward | Step 206 creates `GoogleAuthSettings`, binds `Authentication:Google`, validates on startup, then registers the validator/service |
| Old Google lesson claimed validation had no network dependency and anonymously linked matching email | Rejected as technically/security incorrect | Step 206 treats signing-key retrieval as outbound work and permits linking only for the signed-in freshly proven owner |
| Current partial auth work added a controller/implementation method without its interface and left an incomplete deletion code path | Carried forward as executable regression evidence | Every lesson's interface/implementation/controller delta and all return paths must compile together before its runtime proof or conclusion |
| Later edits were bolted onto headings/TOCs/code samples through patch callouts | Carried forward | Global lesson structure requires one coherent current shape; headings, TOC, files touched, code, DI, endpoints, and verification must agree |

## Material that is not a standalone step in the new run

- Terminal Commands is a command reference. Put each command beside the lesson that requires it instead of making the reader leave the sequence.
- Database-Modelling and Mermaid-ERD-Todolist-Example feed step 22; the final step should contain the required modelling workflow plus a compact relationship table and nested outline rather than cross-referencing two prerequisite notes.
- 41-Mapperly-For-Controller-Service-Architecture, 46-Mapperly-For-Data-Shaping, 103-Mapperly-For-Layered-Architecture, and 131-Mapperly-For-EFCore-Performance are corrections to merge into steps 31–36 and 400, not lessons a learner must discover later.
- 65-Basic-Authentication-01 and 70-Basic-Authentication-02-API are source material for independently
  executable alternative authentication mechanisms. If retained, assign them all-numeric
  alternative-path slots after step 67 rather than `B` companions; neither belongs in the default
  JWT-and-Identity path.
- 75-Basic-Authentication-03-JWT is source material for the first-party learning issuer, not proof that the result is an OAuth/OIDC authorization server or the preferred production token issuer. Step 04 owns the initial architecture decision; step 490 reconfirms it against the real deployment.
- Vertical-Slice/00-Index, Scaffolding-API, and OLD/* are comparison or historical material. They are not part of this MVC Clean Architecture recreation sequence.
- RECREATE/85-Custom-Auth-Filter is source material for step 301. Keep one canonical rewrite only.

## Required sequence checkpoints

1. After step 37, the host builds, reports errors consistently, logs/traces requests, documents a versioned API, connects to the database, has the reusable controller/service/DTO/validation pattern, and passes its first integration tests against the provider selected in step 05. Its package, registration, migrations, container, health check, and test fixture all target the same engine.
2. On the default local-Identity branch, after step 67 registration creates the project’s required profile, login issues a valid JWT, and ordinary self-ownership has an explicit pattern. An external-identity branch instead proves provider authentication, stable subject mapping, local authorization, and the selected session/token boundary without recreating provider-owned credential flows.
3. On the default local-account branch, after step 209 a new account can register without choosing a privileged role, receive and use a confirmation link, sign in with password/passkey/Google as configured, complete local TOTP where required, rotate and revoke recognizable sessions safely under concurrency, reset a password while cutting off prior refresh sessions, manage authenticators only after fresh proof, and be deleted without another login path bypassing that state. TOTP seeds/recovery values and key material already have required protection; only broader application PII encryption remains optional. An external-identity branch instead proves the provider-owned equivalents and every remaining local session/account-lifecycle control identified in step 04.
4. After step 301, the first project feature has a protected resource relationship and the scoped-role authorization pattern that depends on it.
5. After step 321, every subsequent CRUD feature can start from the complete baseline and only add its model/configuration, DTOs, service, controller, validation, and feature-specific policies.
6. Before step 500 or 501, step 490 confirms the production identity/token decision and selected database deployment, and the security gate passes. The self-issued learning JWT path, a successful local build, enabled TOTP, or a database that merely accepts a connection is not sufficient authorization to use real patient data.

This file owns the recreation sequence and execution contracts. It does not itself create, rename,
or rewrite the listed tutorial files or application source. The next task must name one numeric
lesson and choose Author mode or Execution mode explicitly.
