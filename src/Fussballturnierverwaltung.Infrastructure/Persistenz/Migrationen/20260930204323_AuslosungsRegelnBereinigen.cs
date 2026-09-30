using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class AuslosungsRegelnBereinigen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue #331: die entfallenen Regeln GruppensiegerGegenGruppenzweite (2), GesetzteGegenUngesetzte (3)
            // und ManuelleEinschraenkung (7) hatten nie eine Wirkung — gespeicherte Zeilen entfernen.
            migrationBuilder.Sql("DELETE FROM AuslosungsRegeln WHERE Regelart IN (2, 3, 7);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Gelöschte Zeilen waren wirkungslos — kein Wiederherstellen nötig.
        }
    }
}
