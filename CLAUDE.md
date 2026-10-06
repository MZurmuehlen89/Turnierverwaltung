# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build (all projects)
dotnet build

# Run the WinUi application
dotnet run --project src/Fussballturnierverwaltung.WinUi

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

**Clean Architecture** with four projects:

| Project | Namespace | Role |
|---|---|---|
| `Domain` | `Fussballturnierverwaltung.Domain` | Entities, enums — no external deps |
| `Application` | `Fussballturnierverwaltung.Application` | Services, validators, repository interfaces, generators |
| `Infrastructure` | `Fussballturnierverwaltung.Infrastructure` | EF Core, repositories, migrations, seed |
| `WinUi` | `Fussballturnierverwaltung.WinUi` | WinUI 3 / WindowsAppSDK GUI, MVVM (CommunityToolkit.Mvvm), DI root |

**Dependency direction:** `Domain ← Application ← Infrastructure ← WinUi`

Domain has no external dependencies. `WinUi` is the sole startup project, referencing `Infrastructure` and all Application-layer services.

Note: Console, WPF, and an Avalonia feasibility spike previously existed alongside WinUi (see Issue #326 for their archived architecture/status before removal). WPF was the original desktop GUI but was retired because native WPF controls (`DataGrid`, `TextBox`, `ComboBox`) bind directly to `SystemColors` keys instead of inheritable styles, making a clean Dark Mode structurally unworkable — this drove the WinUI3 rebuild (#279ff.). Console was the original UI before any graphical one existed. Avalonia was a side-by-side feasibility spike (#278) evaluated against WinUI3 and not pursued further.

## Key Patterns

**Adding a new feature** typically requires touching all four layers:
1. **Domain**: entity in `Entitaeten/`, enum in `Enums/`
2. **Application**: repository interface + service class; validator if user input is involved
3. **Infrastructure**: EF Core configuration in `Persistenz/Konfigurationen/`, repository implementation, migration
4. **WinUi**: ViewModel + View pair, registered in DI, wired into navigation — see `src/Fussballturnierverwaltung.WinUi/CLAUDE.md` for the exact pattern and its pitfalls

**Repository pattern**: All repository interfaces extend `IRepository<T>` (`Application/Abstraktionen/IRepository.cs`). Concrete implementations extend `GenericRepository<T>`. Domain-specific query methods are added to the interface and implemented in Infrastructure.

**DI registration** lives in the WinUi startup (see `src/Fussballturnierverwaltung.WinUi/CLAUDE.md`) and `InfrastrukturErweiterungen.cs` (Infrastructure). All services are `Scoped` except `EinstellungenService` which is `Singleton` (reads/writes a JSON file, holds no DbContext).

**Validation**: Validators live in `Application/Validierung/` and return `ValidationErgebnis`. Services call `ValidationException.WerfenWennUngueltig(validator.Validieren(...))`.

**Spielplan generators** (`ISpielplanGenerator`) use a factory pattern (`SpielplanGeneratorFactory`) keyed on `Rundenart`. Four generators are implemented: `RoundRobinGenerator`, `KoRundeGenerator`, `TopfLigaGenerator`, `SchweizerSystemGenerator`. Register new generators as `ISpielplanGenerator` in the WinUi DI setup.

**User settings** (`EinstellungenService`) persist to `%APPDATA%\Turnierverwaltung\einstellungen.json` as JSON. Registered as Singleton.

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
if it is not done: method > ~40 lines, class > ~400 lines, more than ~5 constructor dependencies.

**Deliberate deviations:**

- **Comments stay** where they record the *why* that the code cannot show (WinUI pitfalls, issue references such
  as `#406`). Clean Code's "as few comments as possible" applies only to comments that restate the code.
- **Law of Demeter does not apply to XAML binding paths** (`Ziehung.AnimationLaeuft`): pass-through properties on
  the parent ViewModel would recreate exactly the bulk a split removes.
- **No big bang**: existing code is only restructured when an issue opens the file anyway, or through dedicated
  refactoring issues (label `thema: code-qualitaet`).

**Refactoring procedure:** two commits per cut — first move the code unchanged, then clean up inside the new
class — so a behaviour change can be traced to one of the two. No behaviour change in either.

## Important Domain Concepts

**`TurnierRunde.AuswaertstorRegelAktiv`** is a Boolean flag active only when `MitRueckspiel = true`. The away-goals rule is evaluated *before* `EntscheidungsVerfahren` when checking aggregate scores (AD-001).

**`TurnierRunde.RegelwerkId`** stores a per-round Regelwerk. Runden override the tournament-level default Regelwerk set on `TurnierInstanz` (AD-026).

**`Mannschaft.GesamtRating`** is a computed C# property `(O+M+D)/3m`, not stored in the database (AD-002).

**`SimulationsEngine`** is stateless (pure static methods). It uses the Knuth algorithm for Poisson draws. Pass a seeded `Random` instance for reproducible results. The seed is recorded in `SimulationsLauf.ZufallsSeed`.

**`TurnierBaumKnoten`** has circular self-references (Eltern/LinkerKind/RechterKind) and a circular reference back to `TurnierBaum.WurzelKnotenId`. EF Core is configured with `OnDelete(DeleteBehavior.NoAction)` throughout. When deleting a `TurnierBaum`, the repository must null out all references first, then delete nodes, then delete the tree (three SaveChanges calls — see `TurnierBaumRepository.LoeschenMitKnotenAsync`).

**`AuslosungRepository`** similarly manages a two-phase save: insert all `TurnierBaumKnoten` without links first, then update with parent/child IDs.

## Testing

See `tests/Fussballturnierverwaltung.Tests/CLAUDE.md` (Framework/Coverage-Liste) — loaded automatically once a file in that project is touched. Test-Kommandos (Build/Filter) stehen oben unter "Build & Run".
