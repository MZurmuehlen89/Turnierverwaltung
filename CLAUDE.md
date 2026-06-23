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

**Clean Architecture** with five projects:

| Project | Namespace | Role |
|---|---|---|
| `Domain` | `Fussballturnierverwaltung.Domain` | Entities, enums — no external deps |
| `Application` | `Fussballturnierverwaltung.Application` | Services, validators, repository interfaces, generators |
| `Infrastructure` | `Fussballturnierverwaltung.Infrastructure` | EF Core, repositories, migrations, seed |
| `Console` | `Fussballturnierverwaltung.Konsole` | Spectre.Console menus, DI root |
| `Wpf` | `Fussballturnierverwaltung.Wpf` | WPF GUI with Fluent.Ribbon, MVVM (CommunityToolkit.Mvvm) |

Note: The Console project is named `*.Console` on disk but uses the namespace `*.Konsole` to avoid collision with `System.Console` (AD-005).

**Dependency direction:** `Domain ← Application ← Infrastructure ← Console / Wpf`

Both `Console` and `Wpf` are independent startup projects that reference `Infrastructure` and share all Application-layer services.

## Key Patterns

**Adding a new feature** typically requires touching all four core layers:
1. **Domain**: entity in `Entitaeten/`, enum in `Enums/`
2. **Application**: repository interface + service class; validator if user input is involved
3. **Infrastructure**: EF Core configuration in `Persistenz/Konfigurationen/`, repository implementation, migration
4. **Console**: menu class registered in `Program.cs`, wired into `StammdatenMenue`, `BewegungsdatenMenue`, or `HauptMenue`
5. **WPF** (if applicable): ViewModel + View pair, registered in `App.xaml.cs`, wired into `MainViewModel.NavigationsGruppen`

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
- *Stammdaten*: Mannschaften, Länder & Verbände, Regelwerke, Turniervorlagen
- *Bewegungsdaten*: Turniere, Runden, Auslosung, Spielplan & Baum, Simulation
- *Import / Export*: Import / Export

## Naming Conventions

All domain types, properties, methods, and enums are in **German**. English is used only where established in German IT/football context (Rating, Repository, Service, DbContext, Seed, Import, Export, ViewModel, View).

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
