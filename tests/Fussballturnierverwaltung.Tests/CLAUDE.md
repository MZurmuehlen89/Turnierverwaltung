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
- The app restarts per test class (class fixture `TurnierverwaltungApp`); tests do not run in parallel.
- On failure, screenshot and log tail land in `artifacts/guitest/laeufe/<run>/fehlschlaege/`.
- "Page loaded" = expected page title and `Hauptfenster.Befehlsleiste` ItemStatus `Bereit` stable for 300 ms — never
  `Sleep`. AutomationId convention: see `src/Fussballturnierverwaltung.WinUi/CLAUDE.md`.

**How often to run** (user decision 25.10.2026 — every run blocks the user's mouse and keyboard):

| Change | New/changed scenario | Full run |
|---|---|---|
| Normal case: own view, own AutomationIds, test only reads or uses unique test data | **2×** + counter-check | no |
| Shared parts touched: framework/page objects, controls used in several places, main window/navigation/command bar, seed, startup path, DB schema | 1× | **1×** (counts as the scenario's second run) |
| New test creates data other scenarios could see (one database per run) | 1× | **1×** |

A single red run is investigated, not simply re-run.

## CI (GitHub Actions, #525)

| Workflow | Runner | Trigger | Runs |
|---|---|---|---|
| `.github/workflows/unit-tests.yml` | `ubuntu-latest` | push to `main`/`ci/**`, pull request, manual; not for `*.md`-only changes | `Fussballturnierverwaltung.Tests` (incl. architecture tests) |
| `.github/workflows/gui-tests.yml` | `windows-2025` | manual only (`gh workflow run gui-tests.yml --ref <branch>`) | `GuiTest.Framework.Tests` + GUI tests via `build/GuiTests-Ausfuehren.ps1` against LocalDB |

- The repo is private and Windows minutes count double, hence Linux for every push and Windows only on demand
  (budget in #525).
- Because unit tests run on **Linux**, test code must not assume Windows: no fixed paths like `C:\…` (build them from
  `Path.GetTempPath()`/`Path.Combine`), mind case-sensitive file names, use `/` in MSBuild paths. The job runs with
  `LANG=de_DE.UTF-8` and `TZ=Europe/Berlin` like the development machine.
- Both jobs write TRX (artifact) and a Markdown summary (job summary) via xunit's own reporters
  (`--report-xunit-trx/-markdown` under MTP, `-result-trx/-result-markdown` for the GUI tests' in-process runner).
- GUI job: `TURNIERVERWALTUNG_GUITEST_SERVER=(localdb)\MSSQLLocalDB`; on failure `artifacts/guitest/laeufe/` is
  uploaded as artifact `gui-tests-belege` (screenshots, log tails, app log). The job logs the screen resolution and
  tries to raise it to 1920×1080.
