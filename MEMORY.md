# Project Memory

## Tooling

- Keep shared project instructions and knowledge routing in `CLAUDE.md`; `AGENTS.md` is a discovery pointer to it, not a duplicate policy document.

- For Codex lifecycle hooks on native Windows, use a `commandWindows` PowerShell override; the existing Bash command resolves to WSL and fails with `Bash/Service/CreateInstance/E_ACCESSDENIED` in this environment.
- `PreToolUse` with matcher `Bash` runs before normal console commands. Do not conclude hooks are disabled merely because an agent command runner fails before the hook process starts.
- After changing a project-local hook definition, open `/hooks` in a fresh Codex session and trust the new hash before testing it. Test guard behavior with a fixture payload; never issue the destructive command being guarded.
- In workspace-write mode, the repository `.codex` directory is protected. Use a full-access desktop Codex session for hook-script or hook-config edits.

## Tutorial Authoring

- For `Ultimate-ASPNET-Core-Web-API/RECREATE` runsheet work, treat `00-Recreate-v2.md` as a lesson-authoring contract and `PatientBooking.App` plus historical lessons as read-only evidence; never modify application source unless the user explicitly requests implementation.
