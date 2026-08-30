# Mode: Written Lessons

Prose tutorial files built around one running example — the Mukesh Kumar deep-dive-blog voice, or
a well-produced course transcript. This is the mode `SKILL.md`'s shared skeleton was written
against directly, so it needs the least translation; this file adds the parts that are specific to
full narrative lessons: the running-example rule, numbering/grouping, the two Grounding sub-modes,
and the Obsidian-vs-plain-Markdown formatting toolkit.

## The running example

Pick **one** running example — one entity, feature, or program (e.g. "Orders", "the shopping
cart") — and reuse it in every lesson. The earliest lessons build its core pieces from scratch;
every later lesson that adds a capability edits those same files instead of building a disposable
parallel example. The reader should be working in the same handful of files lesson over lesson,
the same way the real thing came together.

When designing a new full track, the first lesson covers environment setup — clone/install,
restore dependencies, run — and a tour of the project's folder structure before any
pattern-specific lesson begins. Do not insert another onboarding lesson when adding to an
established track that already has one.

## Establish the numbered checkpoint

A numbered lesson is not generated from the repository tip in isolation. Its starting point is the
last completed checkpoint on the selected course path.

Before writing:

1. Read the canonical plan, index, immediate prerequisite, and earlier lessons that last touched
   the same files.
2. Apply `SKILL.md`'s mandatory course-state preflight and symbol-provenance audit.
3. Build a compact before/after manifest for the lesson. The before side contains only material
   already introduced on the selected numbered path; the after side contains only this lesson's
   planned delta.
4. Use the current repository or reference implementation to verify the after state, but strip
   partial future work and unexplained scaffolding from the reader's starting assumptions.
5. If an old lesson is being rewritten or renumbered, treat the old file as source material, not
   as a prerequisite, unless the canonical path explicitly retains it.

Each required numbered lesson should begin from a buildable or otherwise successful prior
checkpoint and end at another one. Do not normalize an accidentally broken working tree into
course history. An intentional debugging lesson may start broken only when the exact failure is
the declared input and is reproducible from its prerequisite.

## Grounding and narrative voice

Every grounding mode uses the shared skeleton and running-example rule. Narrative voice is a
separate track-level decision: discover it from the existing course contract, or ask when a new
plan-driven track leaves it unresolved.

- **Greenfield voice** applies to existing-codebase greenfield, from-scratch, conceptual, and
  plan-driven tracks whose contract presents the implementation as newly built. The full *Voice:
  it was always built this way* rule applies.
- **Real-history voice** applies to accumulated-history runsheets, including plan-driven tracks
  whose contract deliberately recreates historical phases. The Voice rule's ban on history is
  lifted. Everything else in that rule (no deference, no hedging, no process narration) still
  applies. The *multi-touch-artifacts* rule matters most here: every phase that touches a given
  file shows its complete current shape at the end of that phase.

Do not switch narrative voice within one track unless the canonical plan explicitly defines
separate paths with different voices.

## File structure — additions to the shared skeleton

Nothing is dropped from `SKILL.md`'s shared unit skeleton; this mode uses all of it, plus:

- The **Prerequisites** section always states which earlier-numbered lesson built every non-obvious
  contract assumed here, via a link. If no earlier lesson owns an assumed symbol, introduce it in
  this lesson instead of calling it pre-existing.
- **Examples** for a multi-touch artifact end with one complete code block pulled from a verified
  source snapshot opened this session. For a plan-driven addition, first implement and verify the
  snapshot against the reconstructed before state. Do not assemble it from memory across several
  diffs or copy future members from repository tip.

## Numbering and grouping

- `000` is reserved for the Index, `001` for the Glossary. Content lessons start at `010`; the
  onboarding lesson (environment setup + project tour) takes that lowest slot
  (`010-Getting-Started.md`).
- Number every significant topic in tens; sub-topics that expand on it get the ones right after —
  `110-Pagination.md`, `111-Filtering.md`, `112-Sorting.md` is one related group. The gap inside
  each ten is deliberate: room to insert more sub-lessons later if a topic turns out bigger than
  expected.
- Group by whatever logical clustering actually fits the material — the numbers above are the
  *pattern*, not a fixed sequence to copy literally.
- One concept per file. If a reader at the chosen Register would have to scroll past an unrelated
  topic, split it into the next open number in that group instead of overloading one file.
- Build `000-Index.md` last, once the final grouping and order is known — every lesson listed in
  learning order with a one-line description, linked.
- Build `001-Glossary.md` alongside it: every domain or pattern term defined the first time it's
  introduced, one line each, no essays. Link to it from each lesson the first time a term appears.

### Deterministic core lessons and companion guides

All-numeric lessons form the numbered curriculum — the linear path a reader follows start to
finish. Along any selected numbered path, a reader must reach the next required numbered lesson
without choosing between variants, performing unrelated repair work, or reading advice that
doesn't apply to a clean run-through.

Put every optional, conditional, or additional item in a lettered companion file for the related
numbered lesson: `74-Account-Profiles.md` has `74B-Account-Profile-Variations.md`. A companion file
is the field guide for readers who want to adapt the tutorial — alternate policies, expanded
troubleshooting, production-operational advice, legacy-data repair. It is never part of the linear
path or a prerequisite for the next numbered lesson.

A numbered lesson may depend only on an earlier numbered lesson; never make its verification,
conclusion, or checkpoint depend on a companion file. A companion file may build on an earlier
companion file only when its optional procedure genuinely requires it — state that dependency
explicitly, and keep that optional chain isolated from the numbered path. An independently
executable feature with its own setup, implementation, and verification stays a numbered lesson
even when marked optional — companion files are for guidance attached to a numbered lesson, not a
substitute for a planned optional feature.

## Package instructions and verification-artifact-first

Apply `SKILL.md`'s universal rules as written — this mode is their primary use case. When the
project already has a verification tool (an `.http` file, a committed collection, a test suite),
that's the primary path; `curl` or an equivalent is the documented fallback, never the required
one.

## Scope callouts

When a lesson intentionally leaves something out that a later lesson adds, drop a one-line callout
right where a reader would notice the gap — what's coming, and which lesson adds it. Name what's
coming, not why it isn't here yet; that's scope-setting, not justification.

## Checkpoints

Follow `SKILL.md`'s DIY-with-hidden-solution pattern exactly. Default to one checkpoint per file,
placed after the reader has seen the complete worked example — not scattered after every step.
Exception: a file large enough to contain a genuinely separate sub-skill can get a second
checkpoint, placed right after that sub-skill's own worked example. Two checkpoints total, at
most, never one per paragraph.

## Formatting toolkit

**Output format = Obsidian vault:**
- Callouts: `> [!abstract]`, `> [!note]`, `> [!info]`, `> [!tip]`, `> [!warning]`, `> [!failure]`,
  `> [!success]`, `> [!question]` — each has a defined job above; don't introduce a new type
  without giving it one.
- Code fences with language tags.
- No diagrams. For anything with a sequence or flow (a request pipeline, a dispatch chain,
  lifetimes), use a nested outline instead of an image.
- `[[wikilinks]]` only for navigation between lessons — never to justify a decision.
- Consistent `H2` for every major section — it drives the table of contents.

**Output format = Plain Markdown:** same shape, different mechanics — no wikilinks, no callout
syntax:
- Replace `[!note]`/`[!warning]`/etc. with a bold lead-in on a blockquote line: `> **Note:** …`,
  `> **Warning:** …`. Keep the same seven jobs (abstract, note, info, tip, warning, failure,
  success/question), just without Obsidian's rendering.
- Replace `[[wikilinks]]` with relative Markdown links: `[Getting Started](010-getting-started.md)`.
- Everything else (numbering, TOC via anchor links, code fences, H2 structure) carries over
  unchanged.

## Self-check additions

- [ ] The running example is the same entity used in every other lesson in the arc
- [ ] The lesson's before state matches the end of its numbered prerequisites, not an unrelated
      working-tree snapshot or superseded lesson
- [ ] Every symbol assumed before its first instruction has a linked earlier owner; everything
      else is created completely here
- [ ] The canonical plan, lesson outcome, implementation, and verification agree on the same
      baseline policy and scope
- [ ] No current-repository scaffolding, future member, or broken partial method became a hidden
      prerequisite
- [ ] New terms are linked to the Glossary the first time they appear
- [ ] Every Do It Yourself checkpoint comes after its worked example is complete, not before, and
      has a collapsed solution directly beneath it
- [ ] Any deliberately incomplete code has a one-line scope callout pointing to the lesson that
      completes it
- [ ] Real-history sub-mode only: every lesson that touches a multi-touch artifact shows its
      complete current shape without future members; greenfield sub-mode has zero "replaces/used
      to" language anywhere
- [ ] A junior at the stated Register could go install → working example with zero outside context
