# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build (all projects)
dotnet build

# Run the application
dotnet run --project src/Fussballturnierverwaltung.Console

# Run all tests
dotnet test

# Run a single test class
dotnet test --filter "FullyQualifiedName~KoRundeGeneratorTests"

# Run a single test method
dotnet test --filter "FullyQualifiedName~KoRundeGeneratorTests.AnzahlSpiele_IstHalbTeamAnzahl"
```

## Database

SQL Server is required. The connection string defaults to `Server=.;Database=Fussballturnierverwaltung;Trusted_Connection=True;TrustServerCertificate=True;` (see `appsettings.json`).

```bash
# Apply migrations (creates DB if not exists)
dotnet ef database update \
  --project src/Fussballturnierverwaltung.Infrastructure \
  --startup-project src/Fussballturnierverwaltung.Console

# Add a new migration after model changes
dotnet ef migrations add <Name> \
  --project src/Fussballturnierverwaltung.Infrastructure \
  --startup-project src/Fussballturnierverwaltung.Console
```

Seed data (Verbände, Länder, Mannschaften, Regelwerke) is applied automatically on first startup via `SeedDatenService.InitialisierenAsync()`.

## Architecture

**Clean Architecture** with four projects:

| Project | Namespace | Role |
|---|---|---|
| `Domain` | `Fussballturnierverwaltung.Domain` | Entities, enums — no external deps |
| `Application` | `Fussballturnierverwaltung.Application` | Services, validators, repository interfaces |
| `Infrastructure` | `Fussballturnierverwaltung.Infrastructure` | EF Core, repositories, migrations, seed |
| `Console` | `Fussballturnierverwaltung.Konsole` | Spectre.Console menus, DI root |

Note: The Console project is named `*.Console` on disk but uses the namespace `*.Konsole` to avoid collision with `System.Console` (AD-005).

**Dependency direction:** `Domain ← Application ← Infrastructure ← Console`

## Key Patterns

**Adding a new feature** typically requires touching all four layers:
1. **Domain**: entity in `Entitaeten/`, enum in `Enums/`
2. **Application**: repository interface + service class; validator if user input is involved
3. **Infrastructure**: EF Core configuration in `Persistenz/Konfigurationen/`, repository implementation, migration
4. **Console**: menu class registered in `Program.cs`, wired into an existing `*Menue` or `HauptMenue`

**Repository pattern**: All repository interfaces extend `IRepository<T>` (`Application/Abstraktionen/IRepository.cs`). Concrete implementations extend `GenericRepository<T>`. Domain-specific query methods are added to the interface and implemented in Infrastructure.

**DI registration** lives entirely in `Program.cs` (Console) and `InfrastrukturErweiterungen.cs` (Infrastructure). All services are `Scoped`.

**Validation**: Validators live in `Application/Validierung/` and return `ValidationErgebnis`. Services call `ValidationException.WerfenWennUngueltig(validator.Validieren(...))`. The Console layer catches `Exception` and shows it via `KonsolenHilfsmittel.FehlerAnzeigen`.

**Spielplan generators** (`ISpielplanGenerator`) use a factory pattern (`SpielplanGeneratorFactory`) keyed on `Rundenart`. Register new generators in `Program.cs` as `ISpielplanGenerator`.

## Naming Conventions

All domain types, properties, methods, and enums are in **German**. English is used only where established in German IT/football context (Rating, Repository, Service, DbContext, Seed, Import, Export).

## Important Domain Concepts

**`TurnierRunde.AuswaertstorRegelAktiv`** is a Boolean flag, not a `VerfahrenArt` entry. The away-goals rule is evaluated *before* `EntscheidungsVerfahren` when checking aggregate scores (AD-001).

**`Mannschaft.GesamtRating`** is a computed C# property `(O+M+D)/3m`, not stored in the database (AD-002).

**`SimulationsEngine`** is stateless (pure static methods). It uses the Knuth algorithm for Poisson draws. Pass a seeded `Random` instance for reproducible results. The seed is recorded in `SimulationsLauf.ZufallsSeed`.

**`TurnierBaumKnoten`** has circular self-references (Eltern/LinkerKind/RechterKind) and a circular reference back to `TurnierBaum.WurzelKnotenId`. EF Core is configured with `OnDelete(DeleteBehavior.NoAction)` throughout. When deleting a `TurnierBaum`, the repository must null out all references first, then delete nodes, then delete the tree (three SaveChanges calls — see `TurnierBaumRepository.LoeschenMitKnotenAsync`).

**`AuslosungRepository`** similarly manages a two-phase save: insert all `TurnierBaumKnoten` without links first, then update with parent/child IDs.

## Testing

Tests use xUnit + FluentAssertions. Integration tests use `Microsoft.EntityFrameworkCore.InMemory`. Test files mirror the source namespace structure under `tests/Fussballturnierverwaltung.Tests/`. No mocking framework is used — generators and validators are instantiated directly.
