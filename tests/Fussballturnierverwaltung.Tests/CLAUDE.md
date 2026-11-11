# CLAUDE.md — Tests

Gilt zusätzlich zur Root-`CLAUDE.md` bei Arbeit an diesem Projekt.

## Testing

No mocking framework is used — generators and validators are instantiated directly. Integration tests use the EF Core InMemory provider.

**Runner:** xunit.v3 on the Microsoft Testing Platform (MTP v2), fixed repo-wide in `global.json` (#497). Every test project in this repo must use MTP too — `dotnet test` cannot mix runners. MTP syntax differs from VSTest: the project is passed with `--project`, filters use `--filter-class`/`--filter-method` (xunit options) instead of `--filter`, coverage is `--coverage --coverage-output-format cobertura` (`Microsoft.Testing.Extensions.CodeCoverage`; coverlet does not run under MTP). Results land in `TestResults\` of the current directory (git-ignored).

Die Tests der Spielplan-Dienste bauen ihre Dienste über `Spielplan/SpielplanDienste` (ein Objektgraph wie in der App) und ihre Turniere über `Spielplan/SpielplanTestdaten` auf.

**Architecture tests** (`Architektur/`, #540, NetArchTest.eNhancedEdition): `SchichtabhaengigkeitenTests` enforce the
dependency direction of the root `CLAUDE.md` (Domain without other layers, EF Core or `Microsoft.Extensions`;
Application without Infrastructure and EF Core; nothing on WinUi, which the test project cannot load).
`KonventionenTests` enforce placement and naming: repository interfaces in Application, classes in
`Infrastructure.Repositories`; EF configurations end on `Konfiguration` under `Persistenz.Konfigurationen`; entities
(`BasisEntitaet`) under `Domain.Entitaeten`, Domain enums in `Domain.Enums`; `…Validator` in `Application.Validierung`;
interfaces start with `I`. A new rule goes through `Schichten.SollteErfuelltSein()` (fails on an empty selection, so a
rename cannot silently disable it) and uses `Genau()`/`Darunter()` for namespaces — `ResideInNamespace` leaves open
whether sub-namespaces count. A rule is only added when the codebase already follows it throughout; exceptions are
named in the test, not defined away. A red test means: fix the code, not the rule — unless the rule itself changes.


**Mutation testing** (#541, Stryker.NET 5 as local tool in `.config/dotnet-tools.json`, `--test-runner mtp`):
`pwsh build/Mutation-Ausfuehren.ps1` (`-Seit main` = only files changed since `main`, `-Mutieren "<glob>"` = own file
selection, `-Nebenlaeufigkeit <n>`, `-Oeffnen`). Scope in `stryker-config.json` next to this file: Auslosungen,
Spielplan, Tabelle, Simulation in Application, without `*Service` classes. Report:
`artifacts/stryker/<timestamp>/reports/mutation-report.html` (+ `.json`).
- Duration: ~2 min coverage capture of all tests per run, then ~3 s per mutant at default concurrency (half the
  logical cores; on the 6-core dev laptop more parallelism gains only ~15–25 %). The full scope (~2800 mutants)
  takes hours and loads the CPU — use `-Seit` for everyday work. Not run in CI (a full run takes hours).
- Limit: conditions with pattern variables (`x is { } wert && …`) cannot be mutated without CS0165 (unassigned
  variable); Stryker then drops *all* mutants of that method ("Safe Mode", status CompileError). Those methods stay
  unchecked — code is not rewritten for this.
- `Timeout` counts as detected (typically an endless loop after a statement mutation).
- `ignore-methods` in `stryker-config.json` skips mutants inside log calls (`LogInformation` …, `Vorgang`) and
  exception constructors (message texts) — noise, not test gaps. Other user-visible texts (event texts, rule names)
  stay in and are judged case by case.
- Second full run (08.11.2026, 2 h 10 min): score 81.2 %, 413 Survived / 109 NoCoverage, 0 Dubious. Follow-up
  issues per area: #615 Simulation, #616 Spielplan, #617 Tabelle, #618 Auslosungen.
- **Verify results — the MTP runner is still "preview"** (Stryker logs a warning). Stryker issue #3871: a test that
  exceeds the timeout during per-test coverage capture is marked `Dubious` and attached to almost every mutant; a
  surviving mutant then runs into the timeout and counts as detected. The first full run (01.11.2026) reported
  0 Survived / 665 Timeout / 100 % this way — a mutant reported as Timeout survived when built by hand.
  Warning signs: 0 Survived, many Timeouts, `Dubious` in the console output. Counter-check one Timeout mutant by hand.
- Countermeasures: `additional-timeout` 120 s in `stryker-config.json`, and tests that become too slow under
  Stryker's instrumentation skip themselves via `Mutationslauf.Aktiv` (the script sets
  `TURNIERVERWALTUNG_MUTATIONSLAUF=1`; `[Fact(Skip = …, SkipWhen = nameof(Mutationslauf.Aktiv), SkipType =
  typeof(Mutationslauf))]`). Currently: `TopfLiga_TopfZuordnung_KeinGleichesLand_HoechstensZweiGegnerJeLand`
  (5 s normally, > 120 s under Stryker). Stryker's own `test-case-filter` is not passed on to MTP in 5.0.0.

## GUI tests (#494, #521)

Three more projects live under `tests/`:

| Project | Role |
|---|---|
| `GuiTest.Framework` | general FlaUI/UIA3 framework, **English API**, must not reference anything of the Turnierverwaltung (to be extracted as a package, #524) |
| `GuiTest.Framework.Tests` | unit tests of its stateless parts; runs in the normal `dotnet test` |
| `Fussballturnierverwaltung.GuiTests` | page objects (`Seiten/`) and scenarios against the real app, German |

**Running:** `pwsh build/GuiTests-Ausfuehren.ps1` (add `-OhneBau` to skip rebuilding the app, further args go to the
xunit runner, e.g. `-class …`). The script builds an **unpackaged, self-contained test variant** of the app into
`artifacts/guitest` (the MSIX/F5 build cannot be started with environment variables) and starts the tests with
`dotnet run`. The tests take over mouse and keyboard — do not run them while the user is working without asking.

- `Fussballturnierverwaltung.GuiTests` is deliberately **not** run by `dotnet test` or the Test Explorer:
  `IsTestingPlatformApplication=false` (under MTP `IsTestProject=false` alone is not enough), so it runs on xunit's
  in-process runner.
- Each run gets its own database `Fussballturnierverwaltung_GuiTest_<timestamp>` (assembly fixture `TestDatenbank`;
  the app creates it with migrations and seed on first start, ~9 s, and the fixture drops it at the end), its own
  data path (#518) and the fixed random seed 4711 (#519). The user's database, settings and logs are never touched.
- The app restarts per test class (class fixture `TurnierverwaltungApp`); tests do not run in parallel. A test that
  aborts with half-done state the app asks about later (an unfinished Vorbelegung → „Vorbelegung verwerfen?“ on the
  next save) leaves a modal dialog that breaks every later test of the same class, and UIA invokes past the dialog
  can open a second one, which crashes the app. Scenarios that can leave such state get a class of their own (#531).
- Drag & drop cannot be driven reliably over UIA. Use the app's alternatives with real mouse clicks instead
  (`DoubleClick`/`RightClick` — the Vorbelegung places a team by double-click and removes it via a right-click menu, #531).
- On failure, screenshot and log tail land in `artifacts/guitest/laeufe/<run>/fehlschlaege/`.
- "Page loaded" = expected page title and `Hauptfenster.Befehlsleiste` ItemStatus `Bereit` stable for 300 ms — never
  `Sleep`. AutomationId convention: see `src/Fussballturnierverwaltung.WinUi/CLAUDE.md`.

**Where to run** (user decision 27.10.2026): GUI test runs go to **GitHub Actions by default** (see CI below) — they
do not block the user's machine and need no prior announcement. Local runs (`build/GuiTests-Ausfuehren.ps1`) only
when the user asks for one; those take over mouse and keyboard and must be announced first.

**How often to run** (user decision 25.10.2026, adapted 11.11.2026 in #636): every push to `ci/**` or `main` runs
the full GUI suite automatically. A new or changed scenario gets **one targeted run** (`-class`) on top, so it runs
**2×** in total, plus a counter-check (the test fails without the change). A single red run is investigated, not
simply re-run.

## CI (GitHub Actions, #525)

| Workflow | Runner | Trigger | Runs |
|---|---|---|---|
| `.github/workflows/unit-tests.yml` | `ubuntu-latest` | push to `main`/`ci/**`, pull request, manual; not for `*.md`-only changes | `Fussballturnierverwaltung.Tests` (incl. architecture tests) |
| `.github/workflows/gui-tests.yml` | `windows-2025` | same as the unit tests; manual with a filter (`gh workflow run gui-tests.yml --ref <branch>`) | `GuiTest.Framework.Tests` + GUI tests via `build/GuiTests-Ausfuehren.ps1` against LocalDB |

- Since the repo went public (11.11.2026, #636) Actions minutes are free, Windows included — both workflows run on
  every push and PR. The unit tests stay on Linux: faster feedback, and it catches Windows assumptions (next point).
- Pull requests from forks run with a read-only token and no secrets (the repo has none); every run of an outside
  contributor needs the owner's approval (repo setting, #636).
- Because unit tests run on **Linux**, test code must not assume Windows: no fixed paths like `C:\…` (build them from
  `Path.GetTempPath()`/`Path.Combine`), mind case-sensitive file names, use `/` in MSBuild paths. The job runs with
  `LANG=de_DE.UTF-8` and `TZ=Europe/Berlin` like the development machine.
- Both jobs write TRX (artifact) and a Markdown summary (job summary) via xunit's own reporters
  (`--report-xunit-trx/-markdown` under MTP, `-result-trx/-result-markdown` for the GUI tests' in-process runner).
- Running a GUI scenario in CI: push the work to a branch `ci/<issue>` (unit tests and the full GUI run start there
  automatically, `main` stays untouched until everything is green); a targeted run on top:
  `gh workflow run gui-tests.yml --ref ci/<issue> -f xunit_argumente="-class Fussballturnierverwaltung.GuiTests.<Klasse>"`.
  Follow a run with `gh run watch <id> --exit-status`, download evidence with `gh run download <id> -n gui-tests-belege`.
  `concurrency` groups by ref and event: a new push cancels the previous push run, a manual run cancels only an
  earlier manual run on the same ref — start the second of two manual runs only after the first has finished.
- GUI job: `TURNIERVERWALTUNG_GUITEST_SERVER=(localdb)\MSSQLLocalDB`; on failure `artifacts/guitest/laeufe/` is
  uploaded as artifact `gui-tests-belege` (screenshots, log tails, app log). The job logs the screen resolution and
  tries to raise it to 1920×1080.

### No self-hosted runner (#609 dropped in #636)

A self-hosted GUI runner in a VirtualBox VM (#609) was meant to save Actions minutes while the repo was private. It
never ran (VirtualBox over the Windows Hypervisor Platform froze in Windows setup) and became pointless once the repo
went public (free minutes). **Do not register a self-hosted runner for this public repo:** a pull request from a fork
can change a workflow to `runs-on: self-hosted` and run its own code on that machine. The build scripts stay in
`build/GuiRunner-VM*` for reference; the full documentation is in the closing comment of #609.
