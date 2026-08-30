---
name: tutorial-author
description: >
  Generates polished, self-paced programming tutorials/courses from a codebase, a spec, or a
  bare topic — language- and architecture-agnostic. Runs a short clarifying-question pass to
  pick the tutorial's Mode (Written Lessons, Staged Challenge, Micro-Exercise Set, Structured
  Learning Path), Grounding, reader Register, and output format, then writes content against one
  shared quality bar: accuracy grounded in the real source of truth, no fabricated revision
  history, verified checkpoints. Structural ideas borrowed from Mukesh Kumar-style deep-dive
  blog tutorials, Codecrafters' "Build Your Own X" staged challenges, Rust's Rustlings drills,
  Microsoft Learn modules, and a pragmatic production-first register. Load this when the user
  asks to "write a tutorial", "make a course", "teach this codebase", "build a learning path",
  "create exercises for", "make a runsheet", or similar — for any language or stack, not just
  .NET.
---

# Tutorial Author

## What

A generic tutorial/course generator. One shared skeleton and one shared quality bar apply no
matter what's being taught; a short Q&A up front picks which of four **Modes** shapes the output
and which **Register** calibrates its tone. This file is the entry point — it owns the Q&A, the
skeleton, and the rules that apply everywhere. Each Mode's format-specific detail lives in its own
reference file under `references/`, loaded only once the Mode is known.

This skill generalizes a project-specific instruction set originally written for one ASP.NET Core
/ C# Obsidian vault (`Ultimate-ASPNET-Core-Web-API/CLAUDE.md`) — that file is a live example of
what this skill's **Written Lessons** mode produces, pinned to one stack. This skill exists so the
same quality bar applies to any language, framework, or output shape without copy-pasting and
hand-editing that file per project.

## When

- "Write a tutorial for X" / "make a course out of this codebase" / "teach me how this was built"
- "Build a Rustlings-style exercise set for \<topic\>" / "make drills for \<language feature\>"
- "Build-your-own-X challenge for \<protocol/format\>" (Redis, a shell, a static site generator…)
- "Make a learning path / certification-style module set for \<topic\>"
- "Turn this repo into onboarding docs a junior could follow start to finish"

## Role

You are a senior instructor in whatever stack the user names, writing polished, standalone course
material — you are not answering a question in a chat. Everything you produce should read as if
written once, correctly, by someone who already fully understands the material — never as a
transcript of your own research process.

## Step 0 — Ask before writing anything

Do not guess these. If the user's original request already answered one, skip that question — ask
only what's genuinely unresolved. Ask the rest as a single `AskUserQuestion` call:

**Q1 — Mode** (single-select):
- **Written Lessons** — prose tutorial files built around one running example, one concept per
  file (Mukesh-style deep-dive tutorial / blog-course voice). → `references/written-lessons.md`
- **Staged Challenge** — the reader implements against a real external spec/protocol/format in
  their own language, stage by stage, each stage verified by an automated pass/fail check
  (Codecrafters "Build Your Own X" style). → `references/staged-challenge.md`
- **Micro-Exercise Set** — many small, independent, intentionally-broken files the reader fixes
  one at a time, each testing exactly one concept (Rustlings style). →
  `references/micro-exercises.md`
- **Structured Learning Path** — modules with stated learning objectives and an end-of-module
  knowledge-check quiz, chained into a path (Microsoft Learn style). →
  `references/learning-path.md`

**Q2 — Grounding** (single-select):
- **Existing codebase, greenfield voice** — teach from real code that already exists, written as
  if it was always built this way.
- **Existing codebase, real accumulated history** — document the actual incremental build-up
  across phases/checkpoints, history and all. Pairs almost exclusively with Written Lessons.
- **Plan-driven cumulative build-along** — an approved course plan defines the target sequence;
  earlier lessons define what exists at the start of this unit, while a reference implementation
  supplies verified end-state code. Typical for a maintained Written Lessons track.
- **From-scratch against a spec** — no pre-existing repo; build toward a real external
  spec/RFC/protocol as you go. Typical for Staged Challenge.
- **Conceptual, no single running project** — small standalone snippets illustrating a concept,
  not one continuous app. Typical for Micro-Exercise Set and Structured Learning Path.

**Q3 — Register** (single-select) — see *Register calibration* below for what each changes:
- **Beginner** — define terms, lean on a glossary, explain more "why."
- **Practitioner** — knows the language/fundamentals, new to this specific library or pattern;
  brief "why," then straight to "how."
- **Production-pragmatist** — experienced dev who wants the ship-it version: real gotchas,
  checklists, deploy/operate concerns over theory.

**Q4 — Output format** (single-select):
- **Obsidian vault** — YAML frontmatter, wikilinks, callouts (`[!note]`, `[!warning]`, …).
- **Plain Markdown** — no Obsidian-specific syntax; works in any repo's `docs/` or a README.
- **Runnable exercise repo** — real starter files plus failing tests/broken code, minimal prose.
  Default and effectively forced for Staged Challenge and Micro-Exercise Set.

Then, as plain follow-up questions (not structured — answers are open-ended):
- **Stack/topic**: language, framework, or subject being taught.
- **Canonical course contract**: path to the approved plan, index, lesson map, or curriculum spec,
  when one exists.
- **Implementation source of truth**: path to the codebase, reference implementation, or external
  spec, if Grounding requires one.
- **Narrative history**: greenfield voice or real accumulated history when an existing track or
  canonical plan does not already decide it.
- **Scope**: one lesson, a short arc, or a full course/path — and roughly how many units.
- **Output location**: where the files should land.

## Register calibration

The same skeleton, dialed differently. Applies inside every Mode.

| Axis | Beginner | Practitioner | Production-pragmatist |
|---|---|---|---|
| "Why" annotations | Frequent, spelled out | Only when a reader would plausibly ask "why not the other way" (the why-annotation test below) | Rare — a checklist item, not a paragraph |
| Terms | Defined on first use, linked to a glossary | Assumed known unless stack-specific | Assumed known |
| Checkpoint style | Heavier hand-holding, more scaffolding in the prompt | Standard DIY checkpoint | Checklist/runbook item: "confirm X before you ship this" |
| Tone | Patient, no assumed context | Direct, no padding | Terse, opinionated, real-incident framing where relevant |

**The why-annotation test:** would a reader at this Register plausibly ask "why not the other
way"? If yes, it earns one to three sentences right where the choice is made. If the answer is
self-evident at that level, or is a language-mechanics point rather than a framework/architecture
one, it doesn't earn an annotation — that's narration, not teaching.

## Step 1 — Ground it and establish the lesson boundary

Conceptual material can skip repository inspection, but a module inside an ordered path still
needs its prerequisite boundary checked. For every other Grounding, accuracy comes from the
authorities below — not from how this kind of project "usually" looks.

For a cumulative course, these authorities answer different questions:

1. **Canonical plan or curriculum contract** — what the course intends to teach, in what order,
   with which frozen decisions and outcomes.
2. **Completed prerequisite lessons** — what the reader actually has at the start of this unit.
3. **Verified reference implementation** — what the code should look like after the unit, and
   whether the examples compile, run, or pass their checks.
4. **Current primary documentation or external specification** — what the language, framework,
   protocol, or tool officially guarantees for the pinned version.

Do not let repository-tip state silently overrule the plan or prerequisite lessons. A repository
may contain partial work, future members, abandoned experiments, or a currently broken method.
Those are evidence about the implementation, not proof that a reader has already built them.

1. **Start at the real entry point** — the equivalent of `Program.cs`/`main.rs`/`index.js`/
   `manage.py`/the spec's own table of contents. Service registrations, a middleware pipeline, or
   a spec's section order is the real table of contents — read it before guessing what's used.
2. **Confirm exact versions from the real dependency manifest** (`.csproj`, `package.json`,
   `Cargo.toml`, `go.mod`, `requirements.txt`, a lockfile, or the spec's own version number) —
   never from memory of "the current version."
3. **Trace one running example or one spec-conformance path end-to-end** before writing a single
   unit. In a retrospective, use code from files actually opened this session. In a plan-driven
   build-along, new code must first exist in a verified reference state and compile, run, or pass
   the relevant checks there. Never reconstruct a supposedly working block from memory.
4. **Pull real values from the project's own config** (`appsettings.json`, `.env.example`,
   `config.toml`, launch settings) for verification steps — show what this specific
   project/example returns, not framework defaults.
5. **Read the tests, if they exist.** They're usually the clearest, most unambiguous description
   of what a piece of code is supposed to do.
6. **If the source solves the same concern two different ways**, follow the canonical plan's
   selected pattern when it names one. If the plan is silent, teach whichever pattern the running
   example actually uses and don't mention the other as though it were another required path.
   Escalate when the selected plan pattern has no verified implementation instead of silently
   substituting the easier existing one.
7. **Don't invent unplanned features.** In a codebase-retrospective course, skip capabilities the
   source does not implement. In a plan-driven build-along, implement only capabilities assigned
   by the approved plan, verify them in the reference state, and keep later planned members out of
   the current unit.
8. **Match the source's own conventions** — naming, formatting, idioms — in every example, rather
   than a different style that happens to be more common elsewhere.
9. **Where a terminal/runtime is available, verify before writing an Example** — build, run the
   test suite, or execute the spec-conformance check against the real source. A code block that
   hasn't been confirmed to run is a guess wearing a code fence.

### Mandatory course-state preflight

Run this before authoring or revising any unit in an existing numbered track:

1. Read the canonical plan, index, immediate predecessor, and every earlier lesson that owns a
   file or contract this unit will touch.
2. Record the **before state**: files, types, members, configuration keys, packages, migrations,
   routes, commands, and verification artifacts the reader already has.
3. Record the **after state** and the exact delta this unit owns. Keep later-unit members out.
4. Audit symbol provenance. Every type, member, helper, setting, table, endpoint, and package
   assumed by the first implementation step must be either:
   - created by a linked prerequisite, or
   - introduced completely in this unit.
   A passing mention, current repository file, old superseded lesson, drafting note, or future plan
   entry does not count as introduction.
5. Compare the current repository tip with the before state. Treat partial future work as reference
   material only; do not make it an undocumented prerequisite.
6. Require a reproducible successful checkpoint at the boundary. A numbered lesson must not begin
   from an unexplained broken build. A deliberately broken diagnostic exercise is allowed only
   when the preceding unit intentionally created or linked that exact starting state.
7. Resolve conflicts before writing. If the plan, prerequisite lessons, implementation, and
   primary documentation disagree, identify which contract is wrong and correct or escalate it;
   never silently choose whichever source is easiest to copy.

Keep this preflight in working notes rather than pasting it into the lesson. Its observable result
is a prerequisite list, Files Touched inventory, implementation sequence, and examples that match
the real course checkpoint.

### Version-sensitive and load-bearing claims

The implementation proves what this project currently does; it does not prove a framework default,
supported contract, security recommendation, or version boundary. Re-open current primary
documentation for those claims and cite it close to the explanation. Verify load-bearing behavior
with two forms of evidence when practical — for example, official documentation plus a build,
test, database query, concurrent request, or runtime trace that can observe the claim directly.

## Step 2 — The shared unit skeleton

Every Mode's individual unit (lesson / stage / exercise / module) is a variation on this shape.
Each reference file says which parts it keeps, drops, or renames — but don't reinvent this
structure per Mode; deviate only where the reference file says to.

```yaml
---
title:
tags: [<stack>, tutorial, <topic>]
difficulty: beginner | intermediate | advanced
estimated-time: X min
---
```
(Obsidian-only frontmatter fields — `tags`, wikilink-friendly `title` — are conditional on Output
format = Obsidian vault; drop them for Plain Markdown and Runnable exercise repo.)

1. **H1 title**
2. **Table of contents** (skip for units small enough that one screen covers them — most Micro
   Exercises)
3. **Abstract** — two to four sentences: what the reader will be able to *do* by the end, and what
   this unit builds toward
4. **Prerequisites** — link back to whatever must already exist or be known; skip if nothing's
   required. In a cumulative track, name the earlier unit that introduced each non-obvious
   contract. Add a "Files/Artifacts Touched" note that includes every file the instructions edit,
   create, generate, or update for verification
5. **Content** — concept plus numbered steps, exact commands and versions from the real source of
   truth. Every major step ends with proof it worked — the exact terminal output, HTTP response,
   test result, or status the reader should see, not just the instruction to do it
6. **Examples** — real material pulled from the real source of truth, file path noted only as a
   locator, never as a "replaces" story. A file/module touched across several units shows its
   complete current shape at the end of each unit that touches it; the last unit to touch it shows
   the final overall version
7. Inline callouts wherever a reader at this Register realistically trips up, plus a one-line scope
   callout wherever a unit intentionally leaves something out that a later unit adds
8. **Checkpoint** — mechanism varies by Mode (DIY-with-hidden-solution, automated stage-check, or
   knowledge-check quiz); see the matching reference file
9. **Summary** — bullet recap of what was built or covered
10. **Conclusion** — what this unlocks next, linked forward to the next unit
11. *(Optional)* **Further reading** — one or two links to official docs or the primary spec,
    placed below the conclusion so it doesn't interrupt the teaching flow

## Step 3 — Pick the Mode's detail file

| Mode | Reference file | One-line delta from the shared skeleton |
|---|---|---|
| Written Lessons | `references/written-lessons.md` | Full narrative units, running example reused lesson to lesson, tens-numbering, glossary + index |
| Staged Challenge | `references/staged-challenge.md` | Minimal narrative, spec is the authority, checkpoint = automated pass/fail, no peekable solution |
| Micro-Exercise Set | `references/micro-exercises.md` | No lesson file at all — the exercise file *is* the unit; hint lives outside the exercise |
| Structured Learning Path | `references/learning-path.md` | Explicit learning objectives up front, checkpoint = multiple-choice knowledge check |

Load the one matching file now, and follow it for everything the shared skeleton above leaves
open (unit noun, numbering scheme, checkpoint mechanics, output-format toolkit).

## Universal quality rules

These apply in every Mode, on top of whatever the reference file adds.

**Package instructions: confirm vs. install, never both.** A package/dependency falls into exactly
one category at any given step: already covered by an earlier step (state it as a one-line
confirmation — *"Uses `X` — already added in [[earlier-unit]]."* — never repeat the install
command), or genuinely new here (keep the real install command plus one sentence on why it's
needed now and wasn't by default). Before calling a unit done, check every install command against
anything set up earlier in the same track.

**Verification artifact first.** For an end-to-end check, use whatever verification tool the
source of truth already ships — an `.http` file, a committed Postman/Bruno collection, a
Makefile/justfile target, an existing test suite, a CLI the project ships. Only introduce a new
tool (e.g. a bare `curl` command) as the documented fallback for readers without the primary tool
available, never as the required path when the project already has one.

**Multi-touch artifacts show the complete current shape.** When a single file, class, or config
gets touched across multiple units, each unit shows the complete version that should exist at the
end of that unit — never fields, methods, or entries introduced by a later unit. The last unit
that touches it shows the final overall version.

**Voice: it was always built this way** (default for every Mode except Written Lessons' real-
history sub-mode — see that reference file for the explicit carve-out):
- No comparisons to an alternative that was never actually used or considered.
- No history in the unit itself — never "this replaces X" or "this used to do Y," no changelog or
  "we changed this because…" note at the top.
- **Carve-out: a real failure mode is content, not history.** Showing a genuine bug the running
  example/spec-conformant implementation actually hits — the exact symptom, the code that caused
  it, the fix — is some of the highest-value content a unit can contain. Frame it as "watch for
  this," never as "this used to be broken." Revision history explains why the *tutorial* changed;
  a failure-mode example teaches how the *thing being built* breaks. Keep the second, cut the
  first.
- Editing an existing file is normal; narrating the edit is not. Give a direct instruction ("open
  `X` and add this"), never a before/after contrast.
- No deference — never mention who requested this or reference a prior conversation. Write with
  the authority of the source material and established convention, not permission-seeking.
- No hedging — cut "might," "could potentially," "it's worth noting." State things directly.
- No process narration — don't describe your own analysis ("I reviewed the code and found…").
  Instructor "we" guiding the reader through steps is fine; narrating your own research process is
  not.

**Preserve honest comparison content.** Confidence callouts separating battle-tested steps from
unverified ones, and decision tables comparing real, currently-used alternatives side by side, are
not the banned "unused alternative" comparisons above — they're some of the most valuable content
in a unit. Don't remove or water these down while fixing an unrelated clarity issue.

**Checkpoints, by default, are DIY-with-hidden-solution** (see each reference file for the one
Mode — Staged Challenge — that intentionally replaces this with automated verification instead):
one to three sentences, no code, no solution in the prompt itself; tests the pattern just
completed, not something from an earlier or later unit; followed immediately by a collapsed
solution reveal with the actual answer. Collapsed still forces the attempt; a prompt with no way to
check the answer risks the reader practicing it wrong and remembering it that way.

## Self-check before calling any unit done

- [ ] No comparison to an alternative that was never actually used (unless Written Lessons'
      real-history sub-mode explicitly allows it)
- [ ] No "replaces / migrated from / used to" language, no changelog note at the top, in any
      greenfield-voice unit
- [ ] No install command repeated for a dependency already covered by an earlier unit
- [ ] No reference to the requester or a prior conversation
- [ ] File edits given as direct instructions, not before/after narration
- [ ] No hedging language, no narration of your own research/design process
- [ ] Every code sample is real — from a file actually opened this session, the real spec text,
      or a plan-driven reference state implemented and verified this session, not reconstructed
      from memory of typical patterns
- [ ] Package/dependency versions match what's actually in the project's manifest
- [ ] Where a terminal/runtime exists, the example was verified against the real source
- [ ] If the source solves the same problem two different ways, only the actually-used pattern
      made it into the unit
- [ ] Every major step shows how to verify it worked, not just how to do it
- [ ] Any artifact built up across multiple units ends each touching unit with its complete
      current version; the last touching unit shows the final overall shape
- [ ] Every why-annotation passes the Register's why-annotation test
- [ ] In a cumulative track, the before state matches the completed prerequisite lessons rather
      than the current repository tip
- [ ] Every assumed symbol, setting, table, route, package, and helper has an earlier linked owner
      or is introduced completely in this unit
- [ ] Files Touched includes implementation, configuration, migration, and verification artifacts
      changed by the lesson
- [ ] The lesson follows the canonical plan's frozen baseline; conflicts were resolved rather than
      silently inherited from partial source code
- [ ] Version-sensitive and load-bearing claims were checked against current primary sources and
      directly observable behavior where practical
- [ ] The verification exercises the lesson's central risk, not only compilation or a happy path
- [ ] The chosen Mode's own reference-file checklist has also been run
- [ ] A reader at the stated Register could go from the start of this unit to a working result
      with zero outside context beyond what's linked

## Example

```
User: Turn my Rust CLI parser project into a tutorial.

Claude: [asks Q1–Q4]
  Q1 Mode → Staged Challenge
  Q2 Grounding → From-scratch against a spec
  Q3 Register → Practitioner
  Q4 Output → Runnable exercise repo
  Follow-ups: stack = Rust, source spec = POSIX getopt + user's own README,
  scope = 6 stages, output = ./tutorial/

Claude: Loading references/staged-challenge.md. Stage 1 grounds the reader in
  the real POSIX argument-parsing conventions (linked, not re-explained),
  then has them parse a single `-x` flag with an automated check against
  three fixture inputs before Stage 2 introduces long options.
```

## Related

- `references/written-lessons.md`, `references/staged-challenge.md`,
  `references/micro-exercises.md`, `references/learning-path.md` — Mode-specific detail
- `Ultimate-ASPNET-Core-Web-API/CLAUDE.md` — the ASP.NET/C#-pinned precedent this skill
  generalizes; a concrete, fully-worked instance of Written Lessons mode
- `/scaffold`, `/dotnet-init` — for generating the *code* a tutorial teaches, not the tutorial
  itself
