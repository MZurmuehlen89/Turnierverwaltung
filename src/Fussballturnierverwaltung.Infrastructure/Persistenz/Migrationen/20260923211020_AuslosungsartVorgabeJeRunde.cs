using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class AuslosungsartVorgabeJeRunde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VorgegebeneAuslosungsart",
                table: "TurnierRunden",
                type: "int",
                nullable: true);

            // Issue #347: bestehende Datenbanken einmalig mit den Seed-Vorgaben der drei UEFA-Vorlagen ab 2024/25
            // nachrüsten (Status 5 = Vorlage; Auslosungsart 0 = Zufall, 2 = NachToepfen, 3 = TopfZuordnung).
            // Bewusst hier statt im SeedDatenService: dort liefe die Nachrüstung bei jedem Start und würde eine
            // später bewusst auf "frei wählbar" zurückgesetzte Vorgabe immer wieder überschreiben.
            migrationBuilder.Sql(@"
UPDATE r SET r.VorgegebeneAuslosungsart = v.Art
FROM TurnierRunden r
JOIN TurnierInstanzen i ON i.Id = r.TurnierInstanzId
JOIN (VALUES (1, N'Ligaphase', 3), (2, N'Playoff-Runde', 2), (3, N'Achtelfinale', 2),
             (4, N'Viertelfinale', 0), (5, N'Halbfinale', 0), (6, N'Finale', 0)) v(Reihenfolge, Name, Art)
  ON v.Reihenfolge = r.Reihenfolge AND v.Name = r.Name
WHERE i.Status = 5
  AND i.Name IN (N'UEFA Champions League (36 Teams, ab 2024/25)',
                 N'UEFA Europa League (36 Teams, ab 2024/25)',
                 N'UEFA Conference League (36 Teams, ab 2024/25)')
  AND r.VorgegebeneAuslosungsart IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VorgegebeneAuslosungsart",
                table: "TurnierRunden");
        }
    }
}
