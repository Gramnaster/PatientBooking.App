---
alwaysApply: true
description: >
  Defines the priority order for resolving trade-offs when writing or
  refactoring code — what wins when two concerns pull in different directions.
---

# Priority Order

When a change could be made multiple ways and the "right" approach isn't obvious, resolve it using this order. A higher item wins when it conflicts with a lower one.

1. **Correctness** — the code does what it's supposed to do, including edge cases.
2. **Security & Safety** — no injection, no leaked secrets, no unsafe input handling.
3. **Simplicity** — the most straightforward implementation that still satisfies 1 and 2. Fewer moving parts over clever abstractions.
4. **Readability & Maintainability** — a future reader (including future-you) understands it without archaeology.
5. **Testability** — the code is structured so behavior can be verified (dependency injection, small units, no hidden statics).
6. **Performance** — optimize only where profiling/measurement shows it's needed. Don't guess.

## How to apply

- **DON'T** reach for a performance optimization (caching, compiled queries, `ValueTask`, manual pooling) unless correctness, security, and simplicity are already settled and a measurement justifies it. `performance.md` covers the *how*; this ordering governs *when* those rules kick in.
- **DON'T** sacrifice simplicity for a "smarter-looking" abstraction that doesn't actually change behavior — that's optimizing for a lower-priority concern at the expense of a higher one.
- **DO** default to the plainest working solution, then move down the list only if a concrete requirement (a security rule, a real maintainability pain point, a measured perf problem) forces it.
- When two rule files disagree in a specific case, this ordering is the tiebreaker — e.g. a security requirement (`security.md`) always wins over a performance shortcut (`performance.md`).
