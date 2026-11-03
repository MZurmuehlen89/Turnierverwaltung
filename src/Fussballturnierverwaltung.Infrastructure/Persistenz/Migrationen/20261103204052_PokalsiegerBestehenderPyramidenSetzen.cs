using Fussballturnierverwaltung.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <summary>#583: setzt einmalig den Pokalsieger an den Ligapyramiden bestehender Datenbanken (Stand der Seed-Daten
    /// beim Ausführen). Neue Datenbanken haben hier noch keine Ranglisten; dort setzt ihn die Seed-Nachrüstung beim
    /// Anlegen der Pyramide. Einmalig, damit ein im Editor entfernter Pokalsieger entfernt bleibt.</summary>
    public partial class PokalsiegerBestehenderPyramidenSetzen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PokalsiegerNachruestung.Sql());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Pokalsieger bleiben; die Spalte entfernt die vorige Migration.
        }
    }
}
