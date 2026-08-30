# Mode: Micro-Exercise Set

Rustlings' shape: dozens of small, independent, intentionally-broken files, each teaching exactly
one concept, fixed one at a time in a tight edit-run-fix loop. There is no separate lesson file —
the exercise file *is* the unit. This is the leanest mode; most of Written Lessons' apparatus
(abstract, prerequisites, table of contents, summary, conclusion) doesn't apply per-exercise.

## Exercise anatomy

Each exercise is one file:

- **A short comment at the top** — at most two or three sentences: what concept this exercise
  drills and, if not obvious, what "done" looks like. This is the entire lesson; there's no
  separate prose file per exercise.
- **An intentionally broken or incomplete body** — code that fails to compile/run, or that
  compiles/runs but fails an included assertion or test. The failure must be caused by exactly the
  concept the exercise is teaching, not an unrelated syntax slip the reader has to puzzle out
  first.
- **A single, unambiguous completion signal** — the file compiles and its included test(s) pass, or
  it runs and produces a stated expected output. If the language/stack has no natural pass/fail
  signal, adopt an explicit marker convention (a comment the reader removes when done, e.g. an
  `// I AM NOT DONE`-style line) and state the convention once in the exercise set's top-level
  README, not per exercise.

Skip `SKILL.md`'s Table of Contents, Prerequisites, Summary, and Conclusion sections entirely for
individual exercises — they add ceremony a two-minute file doesn't need. A per-exercise
Prerequisite is implicit in its position in the ordering (below); state one explicitly only when an
exercise genuinely can't be attempted out of order.

## Hint mechanism: reachable, never inline

A hint must exist for every exercise, but must not be visible while attempting it — visible-by-
default hints get read before the reader tries, which defeats the drill. Two acceptable shapes:

- A separate hints file (or one hints file per topic folder) keyed by exercise name, looked up
  deliberately.
- A collapsed reveal block at the very bottom of the exercise file, below the code, so it's never
  in the reader's eyeline while working.

Never put the hint inline in the body of the broken code itself, and never put the answer (as
opposed to a hint toward it) anywhere the reader can see without deliberately asking for it.

## Ordering

- Group exercises into topic folders in dependency order (fundamentals before things built on
  them) — mirror whatever the target language's own concept dependency graph actually is, not an
  arbitrary list.
- Within a topic, order easiest first.
- Leave gaps in the numbering scheme (topic-level and exercise-level) the same way Written Lessons
  leaves tens-gaps — room to insert a new exercise later without renumbering everything after it.
- One concept per exercise. If an exercise is teaching two things, split it — the point of this
  mode is that each failure has exactly one cause.

## Tooling: the watch loop

Recommend a file-watcher/auto-rerun command appropriate to the actual stack so the reader gets
instant feedback on save, and name it explicitly in the exercise set's README (not per exercise):
the language's native test-watch command where one exists (e.g. `cargo watch -x test`,
`dotnet watch test`, `pytest-watch`, `jest --watch`), or a documented equivalent if the stack has
no built-in one. This loop — edit, save, see pass/fail immediately — is the mode's whole teaching
mechanism; don't ship an exercise set that requires a manual rebuild-and-rerun cycle per attempt
when a watch mode exists for the stack.

## What this mode explicitly skips

Per `SKILL.md`'s shared skeleton: no per-exercise Abstract, Prerequisites section, Files Touched
note, inline scope callouts, or Further Reading. A top-level README for the whole set can carry the
things that would otherwise repeat per-exercise: the watch-loop command, the hint convention, the
overall topic ordering, and one paragraph of framing for the set as a whole.

## Self-check additions

- [ ] Every exercise fails for exactly the reason the top-of-file comment says it should — not an
      unrelated typo or unstated missing import
- [ ] Every exercise has a hint reachable outside the file itself, and no answer visible without
      deliberately asking for it
- [ ] The completion signal (test pass, compiles clean, or an explicit marker) is unambiguous and
      stated once at the exercise-set level
- [ ] Exercises within a topic are ordered easiest-first; topics are ordered in real dependency
      order for the target language
- [ ] No exercise silently requires knowledge from a later, not-yet-reached topic
- [ ] The set's README states the watch/rerun command for this stack
