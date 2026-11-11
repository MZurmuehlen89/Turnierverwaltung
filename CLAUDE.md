# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Database

See `src/Fussballturnierverwaltung.Infrastructure/CLAUDE.md` (connection string, EF-Migrationsbefehle, Seed) — loaded automatically once a file in that project is touched. If you need the migration commands without having touched Infrastructure yet (e.g. right after a Domain entity change), read that file directly.

## Architecture

**Clean Architecture**, **dependency direction:** `Domain ← Application ← Infrastructure ← WinUi`

Domain has no external dependencies. `WinUi` is the sole startup project, referencing `Infrastructure` and all Application-layer services.

Note: Console, WPF, and an Avalonia feasibility spike previously existed alongside WinUi (see Issue #326 for their archived architecture/status before removal). WPF was the original desktop GUI but was retired because native WPF controls (`DataGrid`, `TextBox`, `ComboBox`) bind directly to `SystemColors` keys instead of inheritable styles, making a clean Dark Mode structurally unworkable — this drove the WinUI3 rebuild (#279ff.). Console was the original UI before any graphical one existed. Avalonia was a side-by-side feasibility spike (#278) evaluated against WinUI3 and not pursued further.

## Key Patterns

**Adding a new feature** typically requires touching all four layers:
1. **Domain**: entity in `Entitaeten/`, enum in `Enums/`
2. **Application**: repository interface + service class; validator if user input is involved
3. **Infrastructure**: EF Core configuration in `Persistenz/Konfigurationen/`, repository implementation, migration
4. **WinUi**: ViewModel + View pair, registered in DI, wired into navigation — see `src/Fussballturnierverwaltung.WinUi/CLAUDE.md` for the exact pattern and its pitfalls

**DI registration** lives in the WinUi startup (see `src/Fussballturnierverwaltung.WinUi/CLAUDE.md`) and `InfrastrukturErweiterungen.cs` (Infrastructure). All services are `Scoped` except `EinstellungenService` which is `Singleton` (reads/writes a JSON file, holds no DbContext).

**Data folder**: settings (`einstellungen.json`) and logs (`logs\`) live under `%APPDATA%\Turnierverwaltung`. The environment variable `TURNIERVERWALTUNG_DATENPFAD` redirects that folder (meant for automated GUI tests, #518/#494). Always get these paths from `Datenpfade` (Application/Einstellungen), never build them from `SpecialFolder.ApplicationData` yourself.

## WinUi Patterns

See `src/Fussballturnierverwaltung.WinUi/CLAUDE.md` (ViewModelTemplateSelector/IDialogService/ObservableProperty-Fallstrick/CommandBar/Theme-Fallstricke) — loaded automatically once a file in that project is touched.

## Naming Conventions

All domain types, properties, methods, and enums are in **German**. English is used only where established in German IT/football context (Rating, Repository, Service, DbContext, Seed, Import, Export, ViewModel, View).

| Pattern | Usage |
|---|---|
| `_camelCase` | Private/static fields |
| `camelCase` | Local variables |
| `ALL_UPPER` | Constants |
| `PascalCase` | Everything else (properties, methods, types, public members) |

`_camelCase` for private/static fields is mandatory for source-generated `[ObservableProperty]` fields (CommunityToolkit.Mvvm generates the PascalCase property from the `_camelCase` field name). Binding for new code only — no retroactive rename of existing code (see Issue #143).

## Formatting Conventions

Enforced machine-readably by the repo-root `.editorconfig` (naming rules + indentation/brace/using-directive settings, all at `suggestion` severity — visible as IDE hints, does not fail the build) — follow it. Not covered there:

- **Section-divider comments**: for logically distinct regions inside a larger file, use `// ── Section name ────────────────────────`.
- **XAML**: wrapped attributes align to the column of the first attribute on the opening tag; self-closing tags get a space before `/>`.

Binding for new code only — no big-bang reformat of the existing codebase. When a file is opened for an issue anyway, bring the touched parts in line with these conventions as part of that change (opportunistic cleanup, not a separate pass).

## Clean Code

New code and every refactoring follow this fixed selection from Clean Code Development
(clean-code-developer.de, grade in brackets):

| Principle | What it means here |
|---|---|
| **SRP / SoC** (orange) | One reason to change per class. A ViewModel/service that combines several independent responsibilities gets split (child ViewModels, builders, helper classes). |
| **IOSP + SLA** (red/orange) | A method either coordinates other methods (integration) or contains logic (operation) — not both, and it stays on one level of abstraction. |
| **DRY** (red) | The same logic exists once. |
| **KISS / YAGNI** (red/blue) | No interfaces, base classes or options in reserve — only what the current issue needs. |
| **FCoI** (red) | Composition over inheritance: child ViewModels are composed, no new inheritance hierarchies. |
| **Information Hiding / Tell, don't ask** (yellow/green) | Child ViewModels expose state read-only; the parent calls methods instead of writing into their fields. |
| **Boy Scout Rule** (red) | Touched code is brought in line with these principles as part of the change (same rule as for formatting). |
| **Automated unit tests** (yellow) | Logic that becomes pure/stateless through a split gets tests — as far as the test project can reach it (it does not reference `WinUi`). |

**Thresholds** (project convention, not part of CCD) at which a split must be considered — and the reason stated
if it is not done: method > ~40 lines, class > ~400 lines, more than ~5 constructor dependencies. These are
guidelines, not limits: what decides is whether the class is one coherent responsibility. A class above the
threshold that belongs together stays together; a split that only serves the line count is not made.

**Deliberate deviations:**

- **Comments stay** where they record the *why* that the code cannot show (WinUI pitfalls, issue references such
  as `#406`). Clean Code's "as few comments as possible" applies only to comments that restate the code.
- **Law of Demeter does not apply to XAML binding paths** (`Ziehung.AnimationLaeuft`): pass-through properties on
  the parent ViewModel would recreate exactly the bulk a split removes.
- **No big bang**: existing code is only restructured when an issue opens the file anyway, or through dedicated
  refactoring issues (label `thema: code-qualitaet`).

**Refactoring procedure:** two commits per cut — first move the code unchanged, then clean up inside the new
class — so a behaviour change can be traced to one of the two. No behaviour change in either.

## Static Analysis

`SonarAnalyzer.CSharp` and `Roslynator.Analyzers` run on every build of the projects under `src/` and `tests/`
(#498) — never as errors, the build does not fail on findings.

| File | Purpose |
|---|---|
| `Directory.Build.props` | adds both packages, writes one SARIF file per project to `analyse/` (git-ignored) |
| `.globalconfig` | severities: which rules are on, off or downgraded to `suggestion`, each with its reason |
| `build/SonarLint.xml` | Sonar thresholds (rule parameters cannot be set in `.globalconfig`) and the exclusion of EF migrations |
| `.editorconfig` (last section) | path-specific deviations for `tests/` and the migrations |

**Warnings are the Clean Code thresholds above**: S3776 (cognitive complexity > 15), S138 (method > 40 lines),
S107 (more than 5 parameters). When an issue touches a flagged method, consider the split as described under
Clean Code — the warning is a prompt, not a mandate. Everything else that stays on is either a likely bug or
cheap to fix; fix new findings instead of suppressing them. A deliberate exception gets a scoped
`#pragma warning disable/restore` with the reason on the same line (example: S2234 where home and away are
swapped on purpose for the second leg).

**Evaluation**: after a build, `pwsh build/Analyse-Auswerten.ps1` counts warnings per rule; `-MitHinweisen`
includes `suggestion` findings, `-Regel S3776,S138` lists the locations.

**Pitfalls** (both handled in the config, keep them in mind when changing it):
- Sonar ignores `generated_code = true` from `.editorconfig` — exclusions go into `sonar.exclusions` in `build/SonarLint.xml`.
- The WinUi XAML pre-compile pass (`XamlPreCompile`) runs the compiler without analyzers but with the same
  `ErrorLog`; `Directory.Build.props` blanks the `ErrorLog` around that target, otherwise an incremental build
  overwrites the WinUi SARIF file with an empty analysis.

## Claude Code Hooks (#586)

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

## Dependency Updates (Dependabot, #542)

`.github/dependabot.yml` checks NuGet (directories `/`, `/src/*`, `/tests/*` — via the `.slnx` alone Dependabot found
only one project; `tools/` is not covered) and GitHub Actions every Monday 06:00. Minor/patch updates come grouped (`dotnet` = EF Core + `Microsoft.Extensions` + Pkcs,
which depend on each other and move together within a major version — `Microsoft.Extensions` may run one major
ahead of EF Core, since 11.0 targets net10.0 too (#640/#641); when EF Core's next major arrives, take its packages
(Design, SqlServer, InMemory) in one step; `windows-sdk`, `analyzer`, `testpakete`, `uebrige`), major updates one PR each. Dependabot alerts and security
updates are switched on in the repo settings — security fix PRs arrive any time. Every PR runs the unit tests (#525).

**WinUi project:** in the old repository Dependabot skipped it (27.10.2026); since the move to the new repository
(#634) it is updated like the others (10.11.2026: #637 raised WinUi together with the `dotnet` group, #638 is the
WinUi-only `windows-sdk` group). Still check every PR that raises EF Core/`Microsoft.Extensions` (or another package
WinUi uses too) for `Fussballturnierverwaltung.WinUi.csproj` in its file list; if it is missing, raise the same
packages there in the same step — the build does not catch the mismatch, NuGet silently resolves the higher
transitive version.

How PRs are handled (user decision 27.10.2026):
- **Minor/patch groups:** Claude checks open Dependabot PRs (`gh pr list --author app/dependabot`) when asked or
  when starting an issue, and merges them itself once the unit tests are green. Groups that reach the UI
  (`windows-sdk`, `testpakete` with FlaUI, `uebrige` with CommunityToolkit/Serilog) first get a GUI run in CI on the
  PR branch (`gh workflow run gui-tests.yml --ref <pr-branch>`). GitHub Actions groups: merge when green.
- **Major updates:** Claude reads the release notes/breaking changes, builds locally, runs unit and GUI tests (EF Core:
  additionally migrations and the real database), and presents the result — **the user decides** on the merge.
- After merging, pull `main` locally before continuing work.

## Important Domain Concepts

**`TurnierRunde.AuswaertstorRegelAktiv`** is a Boolean flag active only when `MitRueckspiel = true`. The away-goals rule is evaluated *before* `EntscheidungsVerfahren` when checking aggregate scores (AD-001).

**`TurnierRunde.RegelwerkId`** stores a per-round Regelwerk. Runden override the tournament-level default Regelwerk set on `TurnierInstanz` (AD-026).

**`Mannschaft.GesamtRating`** is a computed C# property `(O+M+D)/3m`, not stored in the database (AD-002).

**`SimulationsEngine`** is stateless (pure static methods). It uses the Knuth algorithm for Poisson draws. Pass a seeded `Random` instance for reproducible results. The seed is recorded in `SimulationsLauf.ZufallsSeed`.

**`Zufallsquelle`** (Application/Zufall, singleton) is the only source of randomness for draws, fixtures and simulation (#519): get a `Random` per operation via `Erzeugen()`, never use `Random.Shared` or `new Random()` directly (exception: `GruppenZiehung`'s deliberately fixed `new Random(0)`). The configuration value `Zufall:Seed` (environment variable `Zufall__Seed`) fixes the seed for GUI tests — every operation then starts with the same sequence; the app logs a warning at startup when it is set.

**`TurnierBaumKnoten`** has circular self-references — deleting/saving trees needs a multi-phase save; see `src/Fussballturnierverwaltung.Infrastructure/CLAUDE.md`.

## Testing

See `tests/Fussballturnierverwaltung.Tests/CLAUDE.md` — loaded automatically once a file in that project is touched.
