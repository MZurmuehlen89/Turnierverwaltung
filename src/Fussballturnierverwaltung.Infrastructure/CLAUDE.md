# CLAUDE.md — Infrastructure

Gilt zusätzlich zur Root-`CLAUDE.md` bei Arbeit an diesem Projekt.

## Database

SQL Server is required. The connection string defaults to `Server=.;Database=Fussballturnierverwaltung;Trusted_Connection=True;TrustServerCertificate=True;` (see `appsettings.json` in each startup project).

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

On every startup the WinUi app calls `DatenbankWartungService.PruefenUndErgaenzenAsync()` (`Wartung/`): `MigrateAsync()` (creates the DB / applies pending migrations), then `SeedDatenService.InitialisierenAsync()` (non-destructive Nachrüsten of missing Stammdaten and Vorlagen — on an empty DB that is the whole seed; only the Verbände are created up front). The same flow is available manually on the "Datenbankwartung" page (#370). EF Core 10 makes `MigrateAsync` throw on model changes without a migration — always add the migration before starting the app.
