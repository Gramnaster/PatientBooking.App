# Session Handoff

> Generated: 2026-07-29 | Source session: HotelListing.App (this work is documentation-only, no C# touched)
> Branch: master (PatientBooking.App)

## What this handoff is about

Not a code task. The prior session used HotelListing.App's working directory but the actual
subject was an Obsidian vault: `Obsidian Vault/notes/Programming/Csharp/Ultimate-ASPNET-Core-Web-API/`
— the user's personal ASP.NET Core tutorial notes, and a new `Vertical-Slice/CLAUDE.md` instructing
how to author a from-scratch VSA + Minimal API rewrite of that tutorial series. The user's complaint:
existing tutorials are disorganized, force too much file-jumping, and some destinations have no
actionable content once you jump there ("a book that tells you random facts you don't actually need").

## Completed

- Verified `HotelListing.Vsa` and `PatientBooking.App` both received the full dotnet-claude-kit import
  (skills incl. `arch-check`/`outdated`, `agents/`, `hooks/`, `AGENTS.md`, `settings.json`) — copied
  byte-identical from `HotelListing.App`'s already-verified import, documented in each project's own
  `.claude/dotnet-claude-kit-SOURCE.md`.
- Confirmed via grep/read that `HotelListing.Vsa` actually demonstrates the `IEndpointGroup` +
  `MapGroup` + `TypedResults` Minimal API pattern across all 5 feature areas (Auth/Bookings/Countries/
  Hotels/Reports), zero Controllers anywhere, plus `Mediator`-based CQRS dispatch (`ISender.Send`).
- Studied the tutorial-authoring rules in `Vertical-Slice/CLAUDE.md` and critiqued them against the
  user's own study-science notes: `attention.md` (interruption cost, ~20min to return to depth),
  `anki-mastery.md` (minimum-information principle, one-fact-per-card), `struggle-ladder.md` (docs
  before help), `programming-study-guide.md` (vertical slices over isolated trivia), `peak-performance-
  developer-guide.md` (interleaving, retrieval before re-reading), `growth.md` (break things
  deliberately — real bugs are pedagogy).
- Audited the **existing, pre-VSA flat tutorial vault** (`Ultimate-ASPNET-Core-Web-API/*.md`, ~78
  files) against that critique. Confirmed the complaint is real but **bimodal**, not uniform:
  - **Good cluster** (33/78 files have a Table of Contents; dated 2026-07-09 onward — `110-Paging.md`,
    `111-Filtering.md`, `113-Paging-Keyset.md`, `160-Logging.md`, `161-Seq.md`): TL;DR abstract, TOC,
    real code from the actual project, "verified against the running API" output blocks, honest
    tradeoff tables, "See also" sections. Already inlines a one-sentence recap of prerequisites before
    linking to them — good instinct, not something the written rules require yet.
  - **Thin cluster** (~45 files, e.g. `50-Validation.md`, likely `30-RESTful-API.md`/`35-Std-vs-API.md`/
    `38-Service-Layer.md`/`65`–`85`/`100`/`101`/`120`/`121`): bare `####` bullet lists, no frontmatter
    beyond `aliases`, no TOC, no code walkthrough, no verification — this is what "random facts, no
    payoff" looks like in practice.
- Found two concrete, reproducible defects:
  1. `112-Paging-Filtering-Sorting-Best-Practices.md` — an 8-line stub, no header/code/verification —
     that `111-Filtering.md` and `113-Paging-Keyset.md` both point to as if it were a real lesson.
  2. A broken anchor, replicated twice: `111-Filtering.md` links to
     `[[50-Validation#Cross-field validation (IValidatableObject)]]` (no trailing colon), and
     `50-Validation.md` self-links the same way — but the actual heading in `50-Validation.md` is
     `#### Cross-field validation (IValidatableObject):` (with a colon). No mechanism in the CLAUDE.md
     self-check list catches this class of bug.
- Applied **1 of 5** planned punch-list edits to `Vertical-Slice/CLAUDE.md`:
  - Added a "real failure mode is content, not history" carve-out to the **Voice** section. The
    existing "no history in the file" rule, read literally, would have banned the single most
    pedagogically valuable pattern actually seen in the good cluster — `111-Filtering.md`'s "a real
    bug this project hit early" callout (a filtered `IQueryable` built correctly, then a different,
    unfiltered variable executed by mistake — compiles fine, silently ignores every filter). The
    carve-out distinguishes banned tutorial-revision narration ("we migrated from AutoMapper") from
    required real-failure-mode teaching ("watch for this exact bug").

## Pending

**4 more punch-list edits, drafted but NOT applied** (paused mid-edit when the user redirected):

1. **Prerequisites** (File structure item 4) — require a 2–4 bullet inline recap of what the reader
   already has, not just a `[[link]]`. The link becomes an "I forgot" fallback, never a forced
   mid-lesson read.
2. **Glossary rule** (Task item 6) — add restraint: gloss minor/already-seen terms inline in 3–6
   words instead of forcing a jump; reserve the actual `001-Glossary.md` link for genuinely
   load-bearing terms. Every jump is a context-switch tax on the reader.
3. **Grouping & naming** — add two rules: (a) "no stub lessons" — a numbered file needs its own
   TL;DR + real code example + verification step or it doesn't earn a number, fold it into the
   parent instead (kills the `112`-stub problem); (b) a sub-lesson (`111`, `112`...) must open its
   abstract by naming the parent ten's throughline it extends, so a `110`/`111`/`112` split reads as
   one idea, not three disconnected facts.
4. **Self-check list** — add 3 items: wikilink/anchor integrity (verify exact heading text including
   punctuation, not "close enough"), no-stub-file check, sub-lesson tie-back check.

**User's actual next step (their call, explicitly not mine to decide):**

- Build **one vertical slice** in PatientBooking (Patients CRUD + one cross-cutting concern, e.g.
  paging) using the **current, already-diagnosed-as-flawed** flat tutorial vault as course material —
  deliberately scoped down from "finish the whole project first" after pushback (see Learned).
- While doing that slice, keep a struggle log per `struggle-ladder.md`'s own template, tagging every
  entry as **domain friction** (Patients/Appointments vs. Hotels/Countries translation work) or
  **tutorial friction** (the vault's actual organization/content problems). Only tutorial-friction
  entries should drive the CLAUDE.md redesign — domain friction is just normal build work.
- The full `Vertical-Slice/CLAUDE.md` redesign — explicit target: "5-star, 100k-review tutorial blog"
  quality, and the user is fine drawing on outside tutorial-writing craft, not just this vault's own
  notes — is intentionally deferred until the scoped slice produces a concrete, tagged complaint list.
  Don't restart that redesign from a cold start; pick up the 4 pending punch-list edits above once
  there's real evidence to weigh them against.

## Learned

- **The user explicitly asked to be disagreed with, not agreed with by default.** Direct quote: "One
  downside with AI is all you do is agree with me. I need you to disagree with me too." Treat this as
  standing guidance for this collaboration generally, not just for the tutorial-redesign thread —
  surface real tradeoffs and pushback rather than validating plans by default.
- The user's own Studies vault notes are a legitimate, citable design authority for tutorial/course
  structure decisions — worth grounding structural claims in specific named notes (interruption cost
  from `attention.md`, minimum-information from `anki-mastery.md`, "break things deliberately" from
  `growth.md`) rather than generic tutorial-writing advice.
- The existing tutorial vault's quality is bimodal by era, not uniformly bad — treat "fix the
  instructions" and "retire/rewrite the thin-cluster files" as two separate work items, not one.

## Context

- No C# code was touched this session in `HotelListing.App`, `HotelListing.Vsa`, or `PatientBooking.App`.
- Edited file: `Obsidian Vault/notes/Programming/Csharp/Ultimate-ASPNET-Core-Web-API/Vertical-Slice/CLAUDE.md`
  (1 of 5 punch-list edits applied — see Pending for the other 4, drafted in the prior conversation
  turn if that transcript is available, otherwise redraft from the Pending descriptions above).
- Files referenced during the audit: `Ultimate-ASPNET-Core-Web-API/50-Validation.md`, `110-Paging.md`,
  `111-Filtering.md`, `112-Paging-Filtering-Sorting-Best-Practices.md`, `113-Paging-Keyset.md`,
  `160-Logging.md`, `161-Seq.md`.
- `PatientBooking.App` repo state at handoff time: branch `master`, 1 commit ahead of `origin/master`.
  Uncommitted: `.claude/dotnet-claude-kit-SOURCE.md` modified; untracked `.claude/agents/`,
  `.claude/hooks/`, `.claude/settings.json`, `.claude/skills/arch-check/`, `.claude/skills/outdated/`,
  `AGENTS.md`, `CLAUDE.md` — this is the user's own `/dotnet-init` output, not yet committed. Nothing
  here should be committed without asking first per standing git-workflow rules.
