# Mode: Structured Learning Path

Microsoft Learn's shape: a **Path** is an ordered list of **Modules**; each Module states explicit
learning objectives up front, teaches through a few short Units, and closes with a Knowledge Check.
This mode suits conceptual material that isn't naturally one continuous running example — a survey
of a language's concurrency primitives, a tour of a framework's configuration system, exam-adjacent
material mapped to a certification's objective domains.

## Module anatomy

A Module replaces Written Lessons' single lesson file with a small set of Units sharing one
frontmatter block and one Knowledge Check:

1. **Introduction unit** — states the **Learning Objectives** as an action-verb bulleted list:
   *"By the end of this module, you'll be able to: …"* Three to five objectives, each independently
   verifiable by the Knowledge Check below. State Prerequisites here as links to earlier modules in
   the same Path.
2. **Content units** — one per objective, or grouped where two objectives are tightly coupled.
   Follows `SKILL.md`'s shared Content/Examples shape, but snippets may be standalone — this mode
   doesn't require Written Lessons' single running example across the whole module. Register still
   governs "why"-annotation density (see `SKILL.md`).
3. *(Optional)* **Sandbox/try-it unit** — a hands-on exercise distinct from the Knowledge Check,
   for material that benefits from doing rather than just reading. Skip it when the content is
   genuinely declarative (API surface, configuration options) and doing wouldn't add anything a
   knowledge check doesn't already test.
4. **Knowledge Check unit** — see below.
5. **Summary unit** — recaps which objectives were covered, restated against the Introduction's
   list, and links to the next Module in the Path.

## Learning objectives: the contract for everything after

Every objective must be independently:

- **Teachable** — covered by at least one Content unit.
- **Testable** — covered by at least one Knowledge Check question.
- **Action-verb phrased** — "explain," "configure," "identify," "implement," not "understand" or
  "know about" (both are unfalsifiable — a Knowledge Check can't verify "understanding").

If a Content unit teaches something no objective mentions, either add the objective or cut the
content — an untracked objective is scope creep the reader didn't sign up to be tested on.

## Knowledge Check: design rules

Three to five questions per module, closing the module (not scattered through Content units).
Multiple-choice is the default format; short-answer is acceptable where multiple-choice would make
the answer too guessable.

- **One question per objective, minimum** — a module with five objectives needs at least five
  questions, not three covering only the easiest ones.
- **Test the objective, not trivia.** A question about a Content unit's example variable name is
  trivia; a question requiring the reader to apply the actual concept to a new, unseen case is
  testing the objective.
- **Plausible distractors, not joke answers.** Every wrong option should be something a reader who
  half-understood the material could genuinely pick — a joke or obviously-wrong distractor makes
  the question free, not diagnostic.
- **Answer key is collapsed, same discipline as Written Lessons' DIY solutions** — reachable, not
  visible by default, so the reader commits to an answer before checking it.
- Immediate per-question feedback (why the right answer is right, and ideally why the most tempting
  distractor is wrong) beats a bare answer key when the output format supports it.

## Path chaining

- A Path is an ordered list of Modules; each Module's stated Prerequisites point only at earlier
  Modules in the same Path (or explicitly-named external prerequisite knowledge, not another
  Module).
- If the user says this Path maps to a real certification, mirror that certification's objective
  domains in the Path's module grouping and say so explicitly in the Path's own introduction — but
  don't invent exam-objective numbers or claim official affiliation that wasn't stated.
- Keep a Module's Summary unit scoped to that Module. It may link forward to the next Module, but
  doesn't inline that Module's content.

## Self-check additions

- [ ] Every stated Learning Objective is both taught by a Content unit and tested by at least one
      Knowledge Check question
- [ ] No Content unit teaches material absent from every objective
- [ ] Every objective uses a testable action verb, not "understand" / "know about"
- [ ] Knowledge Check has at least one question per objective, with plausible (not joke) distractors
- [ ] Answer key/feedback is collapsed or otherwise not visible before the reader commits to an
      answer
- [ ] Module Prerequisites point only at earlier Modules in the same Path or explicitly-named
      outside knowledge
- [ ] The Summary unit's recap matches the Introduction's objective list one-to-one
