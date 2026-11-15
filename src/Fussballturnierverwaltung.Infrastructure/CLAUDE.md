# CLAUDE.md — Infrastructure

Gilt zusätzlich zur Root-`CLAUDE.md` bei Arbeit an diesem Projekt.

## Database

PostgreSQL (15+, Npgsql) since #49 – SQL Server is gone, its 55 migrations live only in git history.

- **Setup:** `pwsh build/PostgreSQL-Einrichten.ps1` once per machine: login `turnierverwaltung` (no superuser, CREATEDB
  for the GUI-test databases), database `turnierverwaltung` (ICU de-DE), password into `%APPDATA%\postgresql\pgpass.conf`.
  psql: `"C:\Program Files\PostgreSQL\18\bin\psql.exe" -h localhost -U turnierverwaltung -w -d turnierverwaltung`.
- **Connection** (`Persistenz/Verbindungseinstellung`): `ConnectionStrings:Standard` from configuration wins (only set
  by the GUI tests, via environment variable); otherwise the block `Datenbank` in `einstellungen.json` (defaults
  localhost:5432, database and user `turnierverwaltung`, no password → pgpass.conf). A password entered in the app is
  stored DPAPI-encrypted (`Kennwortschutz`, Windows only). `appsettings.json` has no connection string any more.
- **Collation:** every text column uses the ICU collation `deutsch` (de-DE, deterministic) defined in the model
  (`TurnierverwaltungDbContext.ConfigureConventions`), so a database created by the app sorts like one from the setup
  script. Deterministic means `==` and unique indexes distinguish upper/lower case – normalise keys on save
  (`Verband.Kurzname`, `Land.FifaCode` are upper-cased).
- **DateTime:** `timestamptz`, only `DateTimeKind.Utc` (the app writes `UtcNow`). Npgsql throws on Local/Unspecified.
- **Raw SQL** (tests, tools): identifiers are mixed case → always in double quotes (`SELECT "Name" FROM "Laender"`).
- **Moving an existing SQL Server database:** `dotnet run --project tools/DatenUmzug -- [--ersetzen]` (COPY with all
  IDs, connects as superuser `postgres` for disabled FK triggers, migrates as the database owner).

```bash
# Apply migrations (creates DB if not exists)
dotnet ef database update \
  --project src/Fussballturnierverwaltung.Infrastructure \
  --startup-project src/Fussballturnierverwaltung.WinUi

# Add a new migration after model changes
dotnet ef migrations add <Name> \
  --project src/Fussballturnierverwaltung.Infrastructure \
  --startup-project src/Fussballturnierverwaltung.WinUi
```

**Pitfall `--no-build` (#586):** the solution build writes WinUi to `bin\x64\Debug`, while `dotnet ef … --startup-project src/Fussballturnierverwaltung.WinUi --no-build` reads `bin\Debug` and silently works against a stale model. With `--no-build`, use `--startup-project src/Fussballturnierverwaltung.Infrastructure` (design-time factory `TurnierverwaltungDbContextFactory`); without `--no-build` the commands above are fine.

On every startup the WinUi app calls `DatenbankWartungService.PruefenUndErgaenzenAsync()` (`Wartung/`): `MigrateAsync()` (creates the DB / applies pending migrations), then `SeedDatenService.InitialisierenAsync()` (non-destructive Nachrüsten of missing Stammdaten and Vorlagen — on an empty DB that is the whole seed; only the Verbände are created up front). The same flow is available manually on the "Datenbankwartung" page (#370). EF Core 10 makes `MigrateAsync` throw on model changes without a migration — always add the migration before starting the app.

## Zirkuläre Referenzen im Turnierbaum

**`TurnierBaumKnoten`** has circular self-references (Eltern/LinkerKind/RechterKind) and a circular reference back to `TurnierBaum.WurzelKnotenId`. EF Core is configured with `OnDelete(DeleteBehavior.NoAction)` throughout. When deleting a `TurnierBaum`, the repository must null out all references first, then delete nodes, then delete the tree (three SaveChanges calls — see `TurnierBaumRepository.LoeschenMitKnotenAsync`).

**`AuslosungRepository`** similarly manages a two-phase save: insert all `TurnierBaumKnoten` without links first, then update with parent/child IDs.

## Seed-Bildmaterial (#633)

Only the flag-icons flags (MIT, `THIRD-PARTY-NOTICES.md`) are in the repo (`Seed/Bildmaterial/Flaggen/`). Crests,
national-team images, tournament and confederation logos and the Chinese Taipei Olympic flag are trademarks or
copyrighted: they live in the private repo `Turnierverwaltung-Bildmaterial`, cloned locally into the git-ignored
`Seed/Bildmaterial/Privat/`. The build embeds both; `SeedBildQuelle.Eingebettet` looks in the public folder first,
then in `Privat`. Never commit such images to this repo — new ones go into the private repo. CI and fresh clones have
no `Privat` folder, so tests must not depend on it: build the seed via `SeedTestDienst` (tests), whose image source
returns an image for every request (`KeineBilder` for the case without images).
