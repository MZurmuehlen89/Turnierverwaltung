using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <summary>#628: Regelsets können den direkten Vergleich für die nach der Mini-Tabelle noch gleichen Teams neu
    /// rechnen (UEFA). Bestehende Datenbanken bekommen den Seed-Stand einmalig: an für „UEFA Standard“. Neue Datenbanken
    /// haben hier noch keine Regelsets, dort setzt der Seed das Feld.</summary>
    public partial class RankingRegelSetDirektenVergleichWiederholen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DirektenVergleichWiederholen",
                table: "RankingRegelSets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE RankingRegelSets SET DirektenVergleichWiederholen = 1 WHERE Name = N'UEFA Standard'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DirektenVergleichWiederholen",
                table: "RankingRegelSets");
        }
    }
}
