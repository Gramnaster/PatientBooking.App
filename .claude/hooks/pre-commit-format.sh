#!/usr/bin/env bash
# Pre-commit hook: verify code formatting
# Runs dotnet format in verify mode — fails if any files need formatting.
# Whitespace fixer only, scoped to staged .cs files. `dotnet format` has no
# incremental mode: `style`/`analyzers` each force a full MSBuildWorkspace
# semantic compile with every analyzer package loaded (Sonar, Roslynator,
# Meziantou, AsyncFixer, IDisposableAnalyzers, SharpSource, ErrorProne.NET),
# ~20s regardless of --include scope, because --include only filters which
# violations get reported, not what gets analyzed. `whitespace` only needs
# syntax trees, so it's the only fixer cheap enough for a commit-time gate
# (~8s). Style/analyzer diagnostics aren't lost — they're active analyzers
# on every `dotnet build` and surface there as warnings.

set -euo pipefail

STAGED_CS_FILES=$(git diff --cached --name-only --diff-filter=ACM -- '*.cs')

if [[ -z "$STAGED_CS_FILES" ]]; then
    exit 0
fi

echo "Checking code formatting..."

if dotnet format whitespace --verify-no-changes --verbosity quiet --include $STAGED_CS_FILES 2>/dev/null; then
    echo "Format check passed."
else
    echo "Format check failed. Run 'dotnet format whitespace' to fix formatting issues."
    exit 1
fi
