# Mode: Staged Challenge

Codecrafters' "Build Your Own X" shape: the reader implements a real thing — a protocol, a file
format, a piece of a language runtime — against the actual external spec, in whatever language they
choose, advancing through numbered Stages that each add one capability. This mode inverts most of
Written Lessons' instincts: minimal narrative, the spec is the authority (not the tutorial's prose),
and the checkpoint is a machine-verifiable pass/fail, never a peekable solution.

## Grounding: the spec is the source of truth

Grounding for this mode is almost always **from-scratch against a spec** — there usually isn't a
pre-existing codebase to read; there's a real external document (an RFC, a protocol reference, a
file format spec, an existing tool's documented behavior) the reader's implementation must satisfy.
Apply `SKILL.md` Step 1's grounding rules with "spec" wherever it says "codebase": trace one
conformance path through the actual spec before writing a stage, pull real example inputs/outputs
from it, don't invent behavior the spec doesn't define.

**Point at the primary source; don't re-teach it.** Link the actual spec/RFC/format doc per stage,
and summarize only enough to orient the reader (what this stage's slice of the spec covers, why
it's the next logical increment) — not a full restatement in the tutorial's own words. The spec is
authoritative; the tutorial's job is sequencing it into a buildable path, not replacing it. If the
tutorial's summary and the spec ever disagree, that's a bug in the tutorial.

## Stage design

- Each **Stage** = one incremental capability, independently testable, that moves the
  implementation strictly closer to full spec conformance. A reader who stops after Stage N has a
  program that correctly does everything through Stage N — never a program that's only correct once
  every stage is finished.
- Stages accumulate. Stage 3 assumes Stages 1–2 are done and passing; it never asks the reader to
  redo earlier work differently.
- Number stages sequentially (`Stage 1`, `Stage 2`, …), not in tens — this mode doesn't need
  Written Lessons' insertion gaps, because a new stage can always be appended at the end or split
  as `Stage 3a`/`Stage 3b` if a stage turns out to bundle two concepts.
- Keep each stage small enough to finish in one sitting. If a stage needs its own sub-checkpoint
  partway through, it's two stages, not one.
- A reader's language/tooling choice is their own — the tutorial specifies the *behavior* a stage
  must satisfy, never a specific implementation, unless the whole challenge is pinned to one
  language on purpose.

## Checkpoint: automated verification, not a peekable solution

This is the one Mode that replaces `SKILL.md`'s default DIY-with-hidden-solution checkpoint
entirely. There is no collapsed "Solution" callout in this mode — the checkpoint *is* the automated
check, and showing an answer would defeat the challenge.

For every stage, define (or point to) a pass/fail check the reader runs against their own program:
a test script, a small conformance-test suite, or a CLI tester that talks to the reader's running
implementation the way a real client would (send it real protocol/format-shaped input, assert on
real protocol/format-shaped output). Specify:

- **Exact invocation** — the command the reader runs.
- **Pass signal** — what success output looks like, verbatim if possible.
- **Fail signal** — one or two of the most likely failure modes at this stage and what they mean,
  not an exhaustive debugging guide.

If no test harness exists yet for this challenge, design one as part of writing the stage — don't
ship a stage with only a manual "try it and see" check when the stage's behavior is objectively
checkable.

## Narrative: minimal, oriented, not exhaustive

Register still applies (see `SKILL.md`), but even at Beginner this mode stays leaner than Written
Lessons — trust the spec to teach the domain; the tutorial's prose job is orientation and gotchas,
not full explanation:

- One short paragraph per stage: what capability this stage adds and why it's next.
- Link to the exact spec section this stage implements.
- One or two callouts for genuine gotchas readers hit at this exact stage (an edge case the spec
  is easy to misread, a common off-by-one) — not a full walkthrough of the reference solution.
- No worked full-code example the reader could copy verbatim. Showing the finished implementation
  defeats a challenge whose entire point is the reader producing it. Small illustrative snippets
  (a struct shape, a parse-loop skeleton) are fine when the stage would otherwise be ambiguous;
  a complete solution is not.

## Progression discipline

- One commit per passing stage, on the reader's own repo — call this out explicitly as the
  expected workflow, so progress is checkpointed and revertible.
- A stage isn't "done" until its automated check passes, not until the reader feels finished.
- Don't let a later stage silently redefine what an earlier stage's check considered passing —
  if Stage 4 tightens Stage 2's behavior, say so explicitly and explain why the spec requires it.

## Self-check additions

- [ ] Every stage's behavior is traceable to a specific section of the real spec, linked
- [ ] Every stage has an automated pass/fail check with an exact invocation and pass signal — no
      stage relies on "run it and eyeball the output" alone when spec-checkable behavior exists
- [ ] No stage's narrative includes a complete, copy-pasteable reference solution
- [ ] Stages accumulate correctly — Stage N's check still passes for someone who only just finished
      Stage N, without requiring later-stage work
- [ ] Each stage is scoped to one sitting; a stage needing its own mid-point checkpoint is split
