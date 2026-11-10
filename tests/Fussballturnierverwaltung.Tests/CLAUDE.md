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
  takes hours and loads the CPU — use `-Seit` for everyday work. Not run in CI (budget, #525).
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

**How often to run** (user decision 25.10.2026, applies to CI runs as well):

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
| `.github/workflows/gui-tests.yml` | own runner in a VirtualBox VM (default), `windows-2025` as fallback | manual only (`gh workflow run gui-tests.yml --ref <branch>`) | `GuiTest.Framework.Tests` + GUI tests via `build/GuiTests-Ausfuehren.ps1` against LocalDB |

- The repo is private and Windows minutes count double, hence Linux for every push and Windows only on demand
  (budget in #525).
- Because unit tests run on **Linux**, test code must not assume Windows: no fixed paths like `C:\…` (build them from
  `Path.GetTempPath()`/`Path.Combine`), mind case-sensitive file names, use `/` in MSBuild paths. The job runs with
  `LANG=de_DE.UTF-8` and `TZ=Europe/Berlin` like the development machine.
- Both jobs write TRX (artifact) and a Markdown summary (job summary) via xunit's own reporters
  (`--report-xunit-trx/-markdown` under MTP, `-result-trx/-result-markdown` for the GUI tests' in-process runner).
- Running a GUI scenario in CI: push the work to a branch `ci/<issue>` (the unit tests run there automatically,
  `main` stays untouched until everything is green), then
  `gh workflow run gui-tests.yml --ref ci/<issue> -f xunit_argumente="-class Fussballturnierverwaltung.GuiTests.<Klasse>"`
  (empty = full run), follow it with `gh run watch <id> --exit-status`, download evidence with
  `gh run download <id> -n gui-tests-belege`. Runs on the same ref cancel each other (`concurrency`) — start the
  second of two runs only after the first has finished.
- GUI job: `TURNIERVERWALTUNG_GUITEST_SERVER=(localdb)\MSSQLLocalDB`; on failure `artifacts/guitest/laeufe/` is
  uploaded as artifact `gui-tests-belege` (screenshots, log tails, app log). The job logs the screen resolution and
  tries to raise it to 1920×1080.

### Own GUI runner (VirtualBox VM, #609)

The Actions budget (2,000 min/month, Windows counts double) did not cover the GUI runs, so they run on a
**self-hosted runner in a VirtualBox VM** on the development machine (no Actions minutes). Not on the desktop itself
(FlaUI takes over mouse and keyboard), not in a container (Windows containers have no interactive desktop).

- **Why VirtualBox, not Hyper-V:** Hyper-V starts every VM under a virtual account of `NT VIRTUAL MACHINE\Virtual
  Machines`, which needs the right "Log on as a service"; a group policy sets that right without the group
  (`New-VM` → `0x80070569`), not changeable locally. VirtualBox runs the VM under the user's own account. Memory
  integrity (HVCI) keeps the Windows hypervisor running, so VirtualBox needs the Windows feature **Windows Hypervisor
  Platform** (slower than native VT-x, enough for the GUI tests).
- **Choosing the runner:** input `runner` of `gui-tests.yml` — `eigener` (default; job labels
  `self-hosted, windows, gui`) or `windows-2025` (GitHub runner, costs minutes):
  `gh workflow run gui-tests.yml --ref <branch> -f runner=windows-2025`. If the development machine is off, a job for
  the own runner waits in the queue (GitHub cancels it after 24 h).
- **Building the VM:** `pwsh build/GuiRunner-VM-Aufbauen.ps1` in an **elevated** PowerShell 7 (Claude cannot start
  it — the user does; Claude reads the logs afterwards). Needs VirtualBox 7.1+, Windows Hypervisor Platform, `gh`
  logged in with admin rights on the repo, the ISO "Windows Server 2025 Evaluation" (default: `*SERVER_EVAL*.iso` in
  Downloads, otherwise `-IsoPfad`). Runs unattended in about 45–75 min:
  1. creates a VHD with diskpart and applies the Datacenter Desktop Experience image straight onto it with DISM (no
     ISO boot — an EFI VM would wait for "Press any key"); puts the answer file `build/GuiRunner-VM/unattend.xml`,
     `build/GuiRunner-VM/Gast-Einrichten.ps1` (copied **with BOM** — Windows PowerShell 5.1 reads BOM-less files as
     ANSI, "–" then turns into a typographic quote that ends a string) and `C:\Einrichtung\parameter.json`
     (password, runner token — valid 1 h, so fetched only after DISM) into the image;
  2. creates the VM with `VBoxManage` and starts it headless; Windows sets itself up, logs on automatically and runs
     the guest script via `FirstLogonCommands`: runner registration first, then .NET 10 SDK, LocalDB, Git,
     PowerShell 7, permanent autologon, no lock screen/sleep/automatic updates; deletes `C:\Einrichtung` and the
     answer file (plain-text password), restarts;
  3. waits until the runner is online, shuts the VM down, takes the snapshot `Grundzustand`, registers the autostart
     task and starts the VM through it.
- **Seeing what happens inside the VM:** there is no PowerShell Direct and no guest additions; the guest script
  writes every step to **COM1**, VirtualBox writes the serial port to `protokolle\gast-*.log` on the host, and the
  build script echoes it. A line `Gast: FEHLER: …` aborts the build. Full transcript inside the VM:
  `C:\GuiRunner-Einrichtung.log`. The serial port is switched off before the snapshot.
- **Location:** `%USERPROFILE%\VirtualBox VMs\Turnierverwaltung-GuiRunner` — VHD, VM files,
  `protokolle\aufbau-*.log`/`gast-*.log` (one pair per build), `zugang.xml` (random Administrator password, DPAPI,
  readable only by the user's account: `(Import-Clixml …\zugang.xml).GetNetworkCredential().Password`).
  VM: EFI, 4 cores, 8 GB, NAT, fixed 1920×1080 (extra data `VBoxInternal2/EfiGraphicsResolution`; without guest
  additions Windows keeps the EFI resolution).
- **Autostart:** scheduled task "Turnierverwaltung GUI-Runner starten" (at the user's logon, not elevated — like the
  VirtualBox Manager, otherwise two `VBoxSVC` would run) restores `Grundzustand` and starts the VM headless. Every
  start therefore begins from a clean state, and VirtualBox killing the VM at logoff does no harm. The runner updates
  itself after each restore if GitHub has released a newer version.
- **Runner runs interactively**, not as a service (services have no desktop): a scheduled task inside the VM starts
  `run.cmd` at the Administrator's autologon in the console session.
- **Pitfall – looking into the VM:** VirtualBox Manager → "Show" opens the display of the headless VM; close it with
  "continue running in the background". Do not connect via RDP: it takes over the console session, which stays
  locked afterwards and UI Automation fails. If that happened: restart the VM (or log off and on at the host).
- **Rebuild** (evaluation expires after 180 days, or the VM is broken): `pwsh build/GuiRunner-VM-Aufbauen.ps1 -Neuaufbau`
  — removes the VM with its disk and snapshots, deregisters the runner at GitHub, builds anew.
- Stage 2 (reset to the snapshot before every job, `--ephemeral` runner) is a separate follow-up issue.
