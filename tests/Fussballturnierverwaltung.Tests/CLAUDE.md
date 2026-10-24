# CLAUDE.md — Tests

Gilt zusätzlich zur Root-`CLAUDE.md` bei Arbeit an diesem Projekt.

## Testing

No mocking framework is used — generators and validators are instantiated directly. Integration tests use the EF Core InMemory provider.

**Runner:** xunit.v3 on the Microsoft Testing Platform (MTP v2), fixed repo-wide in `global.json` (#497). Every test project in this repo must use MTP too — `dotnet test` cannot mix runners. MTP syntax differs from VSTest: the project is passed with `--project`, filters use `--filter-class`/`--filter-method` (xunit options) instead of `--filter`, coverage is `--coverage --coverage-output-format cobertura` (`Microsoft.Testing.Extensions.CodeCoverage`; coverlet does not run under MTP). Results land in `TestResults\` of the current directory (git-ignored).

Die Tests der Spielplan-Dienste bauen ihre Dienste über `Spielplan/SpielplanDienste` (ein Objektgraph wie in der App) und ihre Turniere über `Spielplan/SpielplanTestdaten` auf.


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
