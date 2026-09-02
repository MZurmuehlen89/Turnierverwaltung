# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build (all projects)
dotnet build

# Run the Console application
dotnet run --project src/Fussballturnierverwaltung.Console

# Run the WPF application
dotnet run --project src/Fussballturnierverwaltung.Wpf

# Run all tests
dotnet test

# Run a single test class
dotnet test --filter "FullyQualifiedName~KoRundeGeneratorTests"

# Run a single test method
dotnet test --filter "FullyQualifiedName~KoRundeGeneratorTests.AnzahlSpiele_IstHalbTeamAnzahl"
```

## Database

SQL Server is required. The connection string defaults to `Server=.;Database=Fussballturnierverwaltung;Trusted_Connection=True;TrustServerCertificate=True;` (see `appsettings.json` in each startup project).

```bash
# Apply migrations (creates DB if not exists) — use either startup project
dotnet ef database update \
  --project src/Fussballturnierverwaltung.Infrastructure \
  --startup-project src/Fussballturnierverwaltung.Console

# Add a new migration after model changes
dotnet ef migrations add <Name> \
  --project src/Fussballturnierverwaltung.Infrastructure \
  --startup-project src/Fussballturnierverwaltung.Console
```

Seed data (Verbände, Länder, Mannschaften, Regelwerke, Turniervorlagen) is applied automatically on first startup via `SeedDatenService.InitialisierenAsync()` — runs in both Console and WPF startup paths.

## Architecture

**Clean Architecture** with five core projects, plus two non-productive UI-technology spike projects:

| Project | Namespace | Role |
|---|---|---|
| `Domain` | `Fussballturnierverwaltung.Domain` | Entities, enums — no external deps |
| `Application` | `Fussballturnierverwaltung.Application` | Services, validators, repository interfaces, generators |
| `Infrastructure` | `Fussballturnierverwaltung.Infrastructure` | EF Core, repositories, migrations, seed |
| `Console` | `Fussballturnierverwaltung.Konsole` | Spectre.Console menus, DI root |
| `Wpf` | `Fussballturnierverwaltung.Wpf` | WPF GUI with Fluent.Ribbon, MVVM (CommunityToolkit.Mvvm) |
| `Avalonia` *(spike, not productive)* | `Fussballturnierverwaltung.AvaloniaSpike` | Avalonia 12 rebuild of a DataGrid-heavy view (Issue #278), feeds the WPF/WinUI3/Avalonia decision in #272 |
| `WinUi` *(spike, not productive)* | `Fussballturnierverwaltung.WinUi` | WinUI 3 / WindowsAppSDK rebuild of the same view, ListView-based instead of DataGrid (Issue #278) |

Note: The Console project is named `*.Console` on disk but uses the namespace `*.Konsole` to avoid collision with `System.Console` (AD-005). The Avalonia spike project has the same collision, but with its own `Fussballturnierverwaltung.Application` project instead — any `Fussballturnierverwaltung.*`-namespaced project whose code references the `Application` type from a UI framework (`Avalonia.Application`, `Microsoft.UI.Xaml.Application`, …) unqualified hits this, since C# resolves the sibling namespace `Fussballturnierverwaltung.Application` first; the Avalonia project is namespaced `*.AvaloniaSpike` to sidestep it, the WinUI project instead qualifies the base class as `global::Microsoft.UI.Xaml.Application`.

**Dependency direction:** `Domain ← Application ← Infrastructure ← Console / Wpf / Avalonia / WinUi`

`Console`, `Wpf`, `Avalonia`, and `WinUi` are independent startup projects that reference `Infrastructure` and share all Application-layer services. `Avalonia` and `WinUi` are Issue #278 machine-feasibility spikes only — not wired into any release process, not part of "add a new feature" below.

## Key Patterns

**Adding a new feature** typically requires touching all four core layers:
1. **Domain**: entity in `Entitaeten/`, enum in `Enums/`
2. **Application**: repository interface + service class; validator if user input is involved
3. **Infrastructure**: EF Core configuration in `Persistenz/Konfigurationen/`, repository implementation, migration
4. **Console**: menu class registered in `Program.cs`, wired into `StammdatenMenue`, `BewegungsdatenMenue`, or `HauptMenue`
5. **WPF** (if applicable): ViewModel + View pair, registered in `App.xaml.cs`, wired into `MainViewModel.NavigationsGruppen`, **and** given a `DataTemplate` in `MainWindow.xaml` (`Window.Resources`) mapping the ViewModel type to the View — omitting this compiles fine and navigates without error, but the view silently renders as the ViewModel's `ToString()` instead of the actual UI

**Repository pattern**: All repository interfaces extend `IRepository<T>` (`Application/Abstraktionen/IRepository.cs`). Concrete implementations extend `GenericRepository<T>`. Domain-specific query methods are added to the interface and implemented in Infrastructure.

**DI registration** for the Console lives in `Program.cs`. For the WPF it lives in `App.xaml.cs` (startup) and `InfrastrukturErweiterungen.cs` (Infrastructure). All services are `Scoped` except `EinstellungenService` which is `Singleton` (reads/writes a JSON file, holds no DbContext).

**Validation**: Validators live in `Application/Validierung/` and return `ValidationErgebnis`. Services call `ValidationException.WerfenWennUngueltig(validator.Validieren(...))`. The Console layer catches `Exception` and shows it via `KonsolenHilfsmittel.FehlerAnzeigen`.

**Spielplan generators** (`ISpielplanGenerator`) use a factory pattern (`SpielplanGeneratorFactory`) keyed on `Rundenart`. Four generators are implemented: `RoundRobinGenerator`, `KoRundeGenerator`, `TopfLigaGenerator`, `SchweizerSystemGenerator`. Register new generators in both `Program.cs` (Console) and `App.xaml.cs` (WPF) as `ISpielplanGenerator`.

**User settings** (`EinstellungenService`) persist to `%APPDATA%\Turnierverwaltung\einstellungen.json` as JSON. Registered as Singleton in both startup projects.

## WPF Patterns

**MVVM** uses CommunityToolkit.Mvvm. All ViewModels extend `ViewModelBase` (which extends `ObservableObject`). Source generators (`[ObservableProperty]`, `[RelayCommand]`) are used throughout.

**`ViewModelBase.AktivierenAsync()`** is called by `NavigationService` after each navigation. Override it to load data on arrival. Default implementation is a no-op.

**`NavigationService`** creates a fresh DI scope per navigation, resolving the requested ViewModel from it. This ensures each view gets its own `DbContext`. ViewModels are registered as `Transient`; `NavigationService` and `MainViewModel` are `Singleton`.

**`IRibbonAware`** interface: ViewModels that expose Fluent.Ribbon actions (Neu, Speichern, Abbrechen, Löschen) implement this interface. `MainViewModel` forwards the active ViewModel's ribbon commands to the ribbon bar. When no `IRibbonAware` ViewModel is active, the ribbon tab is hidden.

**`TurnierBereichViewModel`** composes `SpielplanViewModel` and `TurnierBaumViewModel` as tabs. It delegates `IRibbonAware` to whichever tab is currently active (`AktiverTabIndex`).

**Navigation groups** in `MainViewModel`:
- *Stammdaten*: Mannschaften, Länder & Verbände, Regelwerke, Turniervorlagen, Ranglisten
- *Turniere*: Turnierkopf, Turnierdefinition (Runden, Aufstiegsregeln, Verband-Quoten), Turnierdurchführung (Auslosung, Spielplan & Baum, Simulation), Einzelspiel-Simulation
- *Import / Export*: Import / Export

## WinUi Patterns

The WinUI 3 project (`Fussballturnierverwaltung.WinUi`) is the target of the ongoing WPF-to-WinUI3 migration
(#279). Its own `Navigation`/`Ribbon` types are deliberate independent copies of the WPF originals (both
startup projects must stay independent) — same names, same shape, but no shared assembly.

**`ViewModelTemplateSelector`** (`src/Fussballturnierverwaltung.WinUi/ViewModelTemplateSelector.cs`) is the
WinUI3 equivalent of the WPF `MainWindow.xaml` implicit `DataType`-`DataTemplate`s: `x:Bind` is strongly typed
and cannot map `MainViewModel.AktuellesViewModel` (`ViewModelBase`) to the concrete View at compile time, and
WinUI 3 does not reliably resolve implicit `DataTemplate`s for a bare `ContentControl` the way WPF does. Adding
a new Fach-View therefore means: add a `case` to `SelectTemplateCore` + a `DataTemplate` property on the
selector, declare the `DataTemplate` as a resource in `MainWindow.xaml`, and wire the property via
`{StaticResource ...}` on `ViewModelTemplateSelector` in `MainWindow.xaml` — omitting this compiles fine and
navigates without error, but the content area silently stays blank (analogous silent-failure risk to the WPF
`DataTemplate`-omission pitfall in "Adding a new feature" above).

**`IDialogService`** replaces WPF's `MessageBox.Show` (not available in WinUI 3). It holds the `XamlRoot` set
by `MainWindow` at startup and shows a `ContentDialog` — `NavigationService.DarfWegnavigierenAsync` depends on
it instead of calling a static dialog API directly.

**`[ObservableProperty]` stays on `_camelCase` private fields, not partial properties**, even though
CommunityToolkit.Mvvm 8.4 recommends partial properties for WinRT/AOT scenarios (warning `MVVMTK0045`): in this
project's WinUI3 setup, a `partial` property with `[ObservableProperty]` fails to compile (`CS9248`, generator
does not produce the implementation part) — root cause not identified, and not worth chasing since
`PublishTrimmed`/AOT is not used here. The `MVVMTK0045` warning is expected and intentionally left unresolved.

**CommandBar has no `ItemsSource` binding** for `PrimaryCommands`/`SecondaryCommands` (unlike WPF's
`fluent:RibbonGroupBox`, which binds children declaratively). `IRibbonAware.RibbonZusatzGruppen` is therefore
synced into `CommandBar.PrimaryCommands` imperatively in `MainWindow.xaml.cs`
(`RibbonZusatzGruppenAktualisieren`, triggered by `MainViewModel.PropertyChanged`) rather than via XAML binding.

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

Enforced machine-readably by the repo-root `.editorconfig` (naming rules + indentation/brace/using-directive settings, all at `suggestion` severity — visible as IDE hints, does not fail the build). Summary:

- **Indentation**: 4 spaces, no tabs — in both C# and XAML (2 spaces for `.csproj`/`.json`).
- **Braces**: Allman style (opening brace on its own line) for type/method/multi-statement blocks. A single-statement `if`/`foreach` body may skip braces and stay on one line (e.g. `if (result != 0) break;`).
- **Namespaces**: file-scoped (`namespace X.Y;`), never block-scoped.
- **`using` directives**: one flat block in plain alphabetical order — `System.*` is *not* grouped separately or sorted first.
- **Section-divider comments**: for logically distinct regions inside a larger file, use `// ── Section name ────────────────────────`.
- **XAML**: wrapped attributes align to the column of the first attribute on the opening tag; self-closing tags get a space before `/>`.

Binding for new code only — no big-bang reformat of the existing codebase. When a file is opened for an issue anyway, bring the touched parts in line with these conventions as part of that change (opportunistic cleanup, not a separate pass).



Binding for new code only — no retroactive reformatting of existing code.

## Important Domain Concepts

**`TurnierRunde.AuswaertstorRegelAktiv`** is a Boolean flag active only when `MitRueckspiel = true`. The away-goals rule is evaluated *before* `EntscheidungsVerfahren` when checking aggregate scores (AD-001).

**`TurnierRunde.RegelwerkId`** stores a per-round Regelwerk. Runden override the tournament-level default Regelwerk set on `TurnierInstanz` (AD-026).

**`Mannschaft.GesamtRating`** is a computed C# property `(O+M+D)/3m`, not stored in the database (AD-002).

**`SimulationsEngine`** is stateless (pure static methods). It uses the Knuth algorithm for Poisson draws. Pass a seeded `Random` instance for reproducible results. The seed is recorded in `SimulationsLauf.ZufallsSeed`.

**`TurnierBaumKnoten`** has circular self-references (Eltern/LinkerKind/RechterKind) and a circular reference back to `TurnierBaum.WurzelKnotenId`. EF Core is configured with `OnDelete(DeleteBehavior.NoAction)` throughout. When deleting a `TurnierBaum`, the repository must null out all references first, then delete nodes, then delete the tree (three SaveChanges calls — see `TurnierBaumRepository.LoeschenMitKnotenAsync`).

**`AuslosungRepository`** similarly manages a two-phase save: insert all `TurnierBaumKnoten` without links first, then update with parent/child IDs.

## Testing

Tests use xUnit + FluentAssertions. Integration tests use `Microsoft.EntityFrameworkCore.InMemory`. Test files mirror the source namespace structure under `tests/Fussballturnierverwaltung.Tests/`. No mocking framework is used — generators and validators are instantiated directly.

Coverage areas: `KoRundeGeneratorTests`, `RoundRobinGeneratorTests`, `TopfLigaGeneratorTests`, `SchweizerSystemGeneratorTests`, `SimulationsEngineTests`, `TabellenServiceTests`, `SpielErgebnisServiceTests`, `TurnierValidatorTests`, `RegelwerkValidatorTests`, `MannschaftValidatorTests`, `AuslosungsValidatorTests`, `AuslosungAlgorithmusTests`.
