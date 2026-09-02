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

See `src/Fussballturnierverwaltung.Infrastructure/CLAUDE.md` (connection string, EF-Migrationsbefehle, Seed) — loaded automatically once a file in that project is touched. If you need the migration commands without having touched Infrastructure yet (e.g. right after a Domain entity change), read that file directly.

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

See `src/Fussballturnierverwaltung.Wpf/CLAUDE.md` (MVVM/ViewModelBase/NavigationService/IRibbonAware/Navigation groups) — loaded automatically once a file in that project is touched.

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

See `tests/Fussballturnierverwaltung.Tests/CLAUDE.md` (Framework/Coverage-Liste) — loaded automatically once a file in that project is touched. Test-Kommandos (Build/Filter) stehen oben unter "Build & Run".
