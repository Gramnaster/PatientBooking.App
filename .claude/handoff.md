# Session Handoff

> Generated: 2026-08-17 | Branch: master (PatientBooking.App)
> Two unrelated threads live in this file: today's auth/JWT session (below), and an older,
> still-unresolved tutorial-vault redesign thread from 2026-07-29 (carried over at the bottom —
> merged in rather than dropped, per explicit user choice).

## Today's Session (2026-08-17) — Auth/JWT hardening + tooling troubleshooting

### Completed

- Verified `85-Custom-Authorization-Filters.md`'s applicability against current code instead of
  relying on stale memory: the role-claim precondition is now satisfied (`ResolveRoleAsync` is
  live in `UsersService.GenerateTokenAsync`, no longer commented out), but no resource-scoped
  endpoint exists yet — `PatientBooking.Api/Controllers/` still only has `AuthController`
  (Register/Login, both `[AllowAnonymous]`) and `BaseApiController`.
- Diagnosed a false-positive Visual Studio Error List (74 errors, all phantom) as a stale
  Roslyn/IntelliSense workspace desync after 3 `.csproj` edits in this session's earlier staged
  changes — confirmed via a clean `dotnet build` (0 warnings, 0 errors) on the identical tree.
  Root tell: a nonsensical `CS1503` claiming `new Claim(ClaimTypes.Role, role)` couldn't convert
  `string` to `System.IO.BinaryReader` — no code path makes that a real error.
- Diagnosed a real `CS0119` compile error in `UsersService.cs:103` — `Expires = TimeProvider,` used
  the bare type as a value instead of calling `TimeProvider.GetUtcNow()`. Explained the fix
  (inject `TimeProvider` via primary constructor, `clock.GetUtcNow().UtcDateTime.AddMinutes(...)`,
  register `TimeProvider.System` in `Program.cs` DI). **User implemented and committed it
  themselves**: `c0627b6` "Adds TimeProvider singleton because DateTime.UtcNow is an anti-pattern".
- User set a new standing rule, now recorded in memory (`feedback_user_implements_own_code.md`,
  updated today): never `Edit`/`Write` ANY PatientBooking.App project file — including config/infra,
  which was previously exempted as "low judgment" — without an explicit ask first. Suggest and
  explain fixes only.

### Pending

- [ ] User intends to build the `HotelOrSystemAdminAttribute`-equivalent custom authorization
      filter soon, ahead of having a resource-scoped endpoint to attach it to — an explicit call
      made after pushback (see Learned below). When it lands, note the mapping already worked out:
      `Employee.ClinicId` is a direct FK on `Employee` (no separate `HotelAdmins`-style join table
      needed), so the membership check collapses to
      `dbContext.Employees.AnyAsync(e => e.UserId == userId && e.ClinicId == clinicId)`. The bypass
      role check must use `IsInRole("Admin")`, not `"Administrator"` — `ResolveRoleAsync` emits the
      literal string `"Admin"`. Full detail in memory `project_pacing_decision.md` (updated today).
- [ ] No domain controllers exist yet (`ClinicController`/`EmployeeController`/etc.) — still the
      actual blocker for a *tested* filter, even once the attribute class itself is written.
- [ ] Auth-hardening arc (lockout, refresh tokens, 2FA, external login, PII encryption — runsheet
      items 200-209) not started; still correctly sequenced after this per `project_pacing_decision`.

### Learned

- User pushed back on the recommendation to wait on the custom-auth-filter until a real endpoint
  exists — decided to build it now regardless ("I can just use it later"). Pushback was given once
  (route-shape guesswork, untestable until a real endpoint exists), then deferred to their call —
  their explicit decision to make on their own learning project, not something to keep re-litigating.
- Standing "user implements their own code" rule broadened: config/infra edits (DI registrations,
  appsettings entries, csproj tweaks) were previously exempted as low-judgment busywork safe to just
  do. That exemption is now revoked — ALL files, no exceptions, unless explicitly asked. See memory
  `feedback_user_implements_own_code.md`.
- A Visual Studio Error List full of "type not found" errors for types that obviously exist (plus
  any outright nonsensical type-mismatch error) is a strong signal of a stale Roslyn/IntelliSense
  workspace after `.csproj` edits, not a real compile problem — cross-check with a clean CLI
  `dotnet build` before chasing phantom errors in source.

### Context (today)

- Branch: `master` | Last commit: `c0627b6` "Adds TimeProvider singleton because DateTime.UtcNow is
  an anti-pattern"
- Uncommitted changes: none — working tree clean as of this handoff
- Solution: `PatientBooking.App.slnx`
- No file edits were made by Claude this session — advisory only, per the standing rule above.

---

## Carried over from 2026-07-29 (tutorial-vault redesign — separate, still-unresolved thread)

> Original generation note: prior session used HotelListing.App's working directory but the actual
> subject was the Obsidian vault at
> `Obsidian Vault/notes/Programming/Csharp/Ultimate-ASPNET-Core-Web-API/`, plus a new
> `Vertical-Slice/CLAUDE.md` instructing how to author a from-scratch VSA + Minimal API rewrite of
> that tutorial series. This thread has had no activity since 2026-07-29 and none today — carried
> forward untouched rather than dropped.

**What that handoff was about:** not a code task. Complaint: existing tutorials are disorganized,
force too much file-jumping, and some destinations have no actionable content once you jump there
("a book that tells you random facts you don't actually need").

**Completed (as of 2026-07-29):**

- Verified `HotelListing.Vsa` and `PatientBooking.App` both received the full dotnet-claude-kit
  import (skills incl. `arch-check`/`outdated`, `agents/`, `hooks/`, `AGENTS.md`, `settings.json`).
- Confirmed `HotelListing.Vsa` demonstrates the `IEndpointGroup` + `MapGroup` + `TypedResults`
  Minimal API pattern across all 5 feature areas, zero Controllers, `Mediator`-based CQRS dispatch.
- Audited the existing flat tutorial vault (~78 files) against the user's own study-science notes
  (`attention.md`, `anki-mastery.md`, `struggle-ladder.md`, `programming-study-guide.md`,
  `peak-performance-developer-guide.md`, `growth.md`). Confirmed the complaint is real but
  **bimodal**: a good cluster (33/78 files, dated 2026-07-09+, has TOC/TL;DR/verified output/tradeoff
  tables) vs. a thin cluster (~45 files, bare bullet lists, no verification).
- Found two concrete defects: `112-Paging-Filtering-Sorting-Best-Practices.md` is an 8-line stub
  that two other files link to as if real; a broken wikilink anchor (missing colon) replicated in
  both `111-Filtering.md` and `50-Validation.md`.
- Applied **1 of 5** planned punch-list edits to `Vertical-Slice/CLAUDE.md`: added a "real failure
  mode is content, not history" carve-out to the Voice section, distinguishing banned
  tutorial-revision narration from required real-bug teaching moments.

**Pending — 4 more punch-list edits, drafted but NOT applied:**

1. **Prerequisites** — require a 2–4 bullet inline recap of what the reader already has, not just a
   `[[link]]`. The link becomes an "I forgot" fallback, never a forced mid-lesson read.
2. **Glossary rule** — add restraint: gloss minor/already-seen terms inline in 3–6 words instead of
   forcing a jump; reserve `001-Glossary.md` links for genuinely load-bearing terms.
3. **Grouping & naming** — two new rules: (a) "no stub lessons" — a numbered file needs its own
   TL;DR + real code example + verification step or it doesn't earn a number, fold it into the
   parent instead; (b) a sub-lesson must open its abstract by naming the parent ten's throughline it
   extends.
4. **Self-check list** — add 3 items: wikilink/anchor integrity (exact heading text incl.
   punctuation), no-stub-file check, sub-lesson tie-back check.

**User's actual next step on this thread (their call, not mine to decide):**

- Build **one vertical slice** in PatientBooking (Patients CRUD + one cross-cutting concern, e.g.
  paging) using the current, already-diagnosed-as-flawed flat tutorial vault as course material —
  deliberately scoped down from "finish the whole project first" after pushback.
- While doing that slice, keep a struggle log per `struggle-ladder.md`'s template, tagging entries as
  **domain friction** (Patients/Appointments vs. Hotels/Countries translation work, ignore for
  redesign purposes) vs. **tutorial friction** (the vault's actual organization/content problems,
  drives the redesign).
- The full `Vertical-Slice/CLAUDE.md` redesign is intentionally deferred until that scoped slice
  produces a concrete, tagged complaint list. Don't restart it cold — pick up the 4 pending
  punch-list edits above once there's real evidence to weigh them against.

**Learned (2026-07-29):**

- User explicitly asked to be disagreed with, not agreed with by default: "One downside with AI is
  all you do is agree with me. I need you to disagree with me too." Standing guidance for this
  collaboration generally.
- The user's own Studies vault notes are a legitimate, citable design authority for tutorial/course
  structure decisions.
- The tutorial vault's quality is bimodal by era, not uniformly bad — "fix the instructions" and
  "retire/rewrite the thin-cluster files" are two separate work items.

**Status check needed:** this thread has had zero activity in ~19 days while the auth/JWT track
(above) moved forward instead. Worth asking the user directly at next session start whether this is
still active or superseded before assuming either way.
