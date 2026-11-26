# Claude-Code-Hooks (.claude/hooks)

Workflow rules that used to live only in memory are enforced by hooks in `.claude/settings.json` (scripts in
`.claude/hooks/`, PowerShell 7, shared helpers in `Hook-Gemeinsam.ps1`). They apply to the `Bash` and `PowerShell` tools.
`Vorfilter.sh` starts the PowerShell scripts only when the command text matches (a pwsh start costs ~0.7 s and the hooks
run on every shell call; with the filter an ordinary call costs ~0.25 s). It uses bash built-ins only, so it cannot
silently pass everything when Git Bash's tools are missing from `PATH`.

| Hook | When | What |
|---|---|---|
| `Commit-Pruefung.ps1` | before `git commit` | Only if code under `src/`/`tests/` (or build config) changed: build the solution; if Domain or the EF configuration changed, `dotnet ef migrations has-pending-model-changes`; if code outside WinUi/GUI tests changed, run the unit tests. Red → commit blocked. |
| `Push-Pruefung.ps1` | before `git push` | blocks while the working tree has uncommitted changes |
| `Migration-Anwenden-Hinweis.ps1` | after `dotnet ef migrations add` | reminds to run `dotnet ef database update` right away |

- The commit check looks at the working tree, not only the index (`git add -A && git commit` stages after the hook ran),
  of the repository the command targets (`git -C <path>`, a preceding `cd`, otherwise the session directory) — so a
  commit in another worktree is checked there. Only this project and its `git worktree`s are checked: hooks run before
  the command is approved, and building a foreign repository would execute its MSBuild code. Build restores only when project/build files changed (64 s vs. 26 s).
- Commands are recognised from the text (`git` at the start of a command, also with options like `-c`/`--no-pager`);
  a test command that merely *contains* `; git push` as a string is blocked too — put such tests in a script file.
- Hooks see the whole command before any part runs: `git commit … && git push` is blocked (the tree is still dirty
  when the push check runs). Commit and push in separate calls.
- It takes about 35–40 s for a WinUi-only change and 1.5–2 min with unit tests — **don't build and test by hand before
  committing**, the hook does it. GUI tests are not part of it (CI, see memory).
- A blocked call returns the findings to Claude (exit code 2); fix them, don't work around the hook.

