# Source: dotnet-claude-kit (content-only import)

- Upstream: https://github.com/codewithmukesh/dotnet-claude-kit
- Commit: `cbad34af27faf94b52a86694925b91ba49f3d10c` (main, 2026-07-06)
- License: MIT — see `dotnet-claude-kit-LICENSE`

## What was imported
- `skills/` — all 45 `SKILL.md` teaching modules (as-is, reference material)
- `rules/` — the 10 always-active coding convention docs (originally `.claude/rules/` upstream)
- `knowledge/` — antipatterns, breaking changes, package recommendations, architecture decision records

## What was deliberately left out
Full plugin install (`agents/`, `hooks/` shell scripts that auto-execute on bash/edit/commit,
the `CWM.RoslynNavigator` MCP server, `.claude-plugin/` marketplace manifest) — these require
`/plugin install` + a global `dotnet tool install`, and were skipped in favor of a plain,
git-trackable content copy. Re-run the extraction from the commit above, or use the official
plugin route (`/plugin marketplace add codewithmukesh/dotnet-claude-kit`), to get those too.

To reuse in another project: copy this whole `.claude/` folder over.
