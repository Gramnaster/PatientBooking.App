# Source: dotnet-claude-kit

- Upstream: https://github.com/codewithmukesh/dotnet-claude-kit
- Commit: `cd83d315986c27621da178dad73bd95d503c1540` (main, 2026-07-25)
- License: MIT — see `dotnet-claude-kit-LICENSE`

## What was imported
- `skills/` — all 47 `SKILL.md` teaching modules (as-is, reference material)
- `rules/` — the 10 always-active coding convention docs (originally `.claude/rules/` upstream)
- `knowledge/` — antipatterns, breaking changes, package recommendations, architecture decision records
- `agents/` — the 10 specialist agent definitions
- `hooks/` — the auto-executing Claude Code hooks (`pre-bash-guard.sh`, `post-edit-format.sh`,
  `post-scaffold-restore.sh`), the manual git pre-commit scripts (`pre-commit-format.sh`,
  `pre-commit-antipattern.sh`), and the utility scripts (`pre-build-validate.sh`,
  `post-test-analyze.sh`)
- `AGENTS.md` — the agent routing table, at project root (`rules/agents.md` points here)

Copied directly from the sibling `HotelListing.App` project's already-verified import
(2026-07-29) rather than re-fetched from GitHub — same commit, byte-identical content.

## Local transferable extensions

This copy now includes `knowledge/security-vulnerability-review-catalog.md`, a reusable .NET web
application review catalog derived from current OWASP/CWE/Microsoft authorities and a deliberately
vulnerable .NET teaching corpus. `rules/security.md`, `agents/security-auditor.md`, and
`skills/security-scan/SKILL.md` load it explicitly. The Codex discovery adapter under
`.agents/skills/security-scan/` points to the same canonical knowledge file rather than maintaining a
second copy. Preserve these files and references when copying the kit into another project.

## How hooks are wired here (not the plugin route)
This is a content-only copy, not a `/plugin install`, so `hooks/hooks.json` is not
auto-registered. Its `PreToolUse`/`PostToolUse` config was hand-copied into
`.claude/settings.json` with `${CLAUDE_PLUGIN_ROOT}` swapped for `$CLAUDE_PROJECT_DIR`
(the portable env var Claude Code sets for hook commands outside a plugin).
`.git/hooks/pre-commit` was installed locally (not tracked by git — per-clone, matches
upstream's "install manually" instructions in `hooks/README.md`) to run
`pre-commit-format.sh` && `pre-commit-antipattern.sh`.

## What was deliberately left out
The `CWM.RoslynNavigator` MCP server and `.claude-plugin/` marketplace manifest — the MCP
server is a global `dotnet tool install`, configured outside this repo. The marketplace
manifest only matters for the `/plugin install` distribution path, which this project
isn't using.

No root `CLAUDE.md` yet — run `/dotnet-init` to generate one from the actual project
structure (planned as a follow-up, not done as part of this import).

To reuse in another project: copy this whole `.claude/` folder, `AGENTS.md`, and
`.git/hooks/pre-commit` (the last one manually, since git doesn't track hook files) over.
