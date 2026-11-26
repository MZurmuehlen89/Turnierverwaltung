---
name: dependabot-prs
description: Dependabot-PRs prüfen und mergen (Minor/Patch-Gruppen selbst, Major nur vorlegen), WinUi-csproj-Prüfung. Nutzen, wenn offene Dependabot-PRs bearbeitet werden sollen oder ein Issue startet.
---

`.github/dependabot.yml` checks NuGet (directories `/`, `/src/*`, `/tests/*` — via the `.slnx` alone Dependabot found
only one project; `tools/` is not covered) and GitHub Actions every Monday 06:00. Minor/patch updates come grouped (`dotnet` = EF Core + `Microsoft.Extensions` + Npgsql + ProtectedData,
which depend on each other and move together within a major version — `Microsoft.Extensions` may run one major
ahead of EF Core, since 11.0 targets net10.0 too (#640/#641); when EF Core's next major arrives, take its packages
(Design, Relational, InMemory, and Npgsql's provider of the same major) in one step and drop the `ignore` for
ProtectedData majors in `dependabot.yml` (ProtectedData follows the EF Core line like Pkcs did before #49, user decision
11.11.2026); `windows-sdk`, `analyzer`, `testpakete`, `uebrige`), major updates one PR each. Dependabot alerts and security
updates are switched on in the repo settings — security fix PRs arrive any time. Every PR runs the unit and GUI tests (#525, #636).

**WinUi project:** in the old repository Dependabot skipped it (27.10.2026); since the move to the new repository
(#634) it is updated like the others (10.11.2026: #637 raised WinUi together with the `dotnet` group, #638 is the
WinUi-only `windows-sdk` group). Still check every PR that raises EF Core/`Microsoft.Extensions` (or another package
WinUi uses too) for `Fussballturnierverwaltung.WinUi.csproj` in its file list; if it is missing, raise the same
packages there in the same step — the build does not catch the mismatch, NuGet silently resolves the higher
transitive version.

How PRs are handled (user decision 27.10.2026):
- **Minor/patch groups:** Claude checks open Dependabot PRs (`gh pr list --author app/dependabot`) when asked or
  when starting an issue, and merges them itself once unit and GUI tests are green (both run on every PR since #636;
  the GUI run matters most for groups that reach the UI: `windows-sdk`, `testpakete` with FlaUI, `uebrige` with
  CommunityToolkit/Serilog). GitHub Actions groups: merge when green.
- **Major updates:** Claude reads the release notes/breaking changes, builds locally, runs unit and GUI tests (EF Core:
  additionally migrations and the real database), and presents the result — **the user decides** on the merge.
- After merging, pull `main` locally before continuing work.

