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

Seed data (Verbände, Länder, Mannschaften, Regelwerke, Turniervorlagen) is applied automatically on first startup via `SeedDatenService.InitialisierenAsync()` in the WinUi startup path.
