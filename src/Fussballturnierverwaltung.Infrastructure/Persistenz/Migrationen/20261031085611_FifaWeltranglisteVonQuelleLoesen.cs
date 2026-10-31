using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <summary>#579: löst die FIFA-Weltrangliste bestehender Datenbanken einmalig von ihrer Import-Quelle und leert ihre
    /// Einträge. Die Seed-Nachrüstung (FifaWeltranglisteNachruestung) füllt eine leere Liste mit dem Seed-Stand — so
    /// bleibt es bei der einen Rangliste, und Verweise von Verbänden und Runden bleiben erhalten. Ein Merkmal im Seed
    /// selbst ginge nicht: ein späterer Import verknüpft die Liste wieder, der Seed überschriebe ihn dann bei jedem Start.
    /// Die Quelle selbst bleibt für die Aktualisierung per Import bestehen.</summary>
    public partial class FifaWeltranglisteVonQuelleLoesen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE e FROM RanglistenEintraege e
                JOIN Ranglisten r ON r.Id = e.RanglisteId
                WHERE r.Name = N'FIFA-Weltrangliste (Herren)' AND r.RanglistenQuelleId IS NOT NULL;

                UPDATE Ranglisten SET RanglistenQuelleId = NULL
                WHERE Name = N'FIFA-Weltrangliste (Herren)' AND RanglistenQuelleId IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nicht umkehrbar: die importierten Einträge sind ersetzt.
        }
    }
}
