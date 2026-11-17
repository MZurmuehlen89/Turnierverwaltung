using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <summary>#668: Spielbedeutung je Runde. Bestehende Runden (Turniere wie Vorlagen) werden nach der Rundenart
    /// vorbelegt wie in <c>SpielbedeutungVorbelegung</c>: Finale (7) hoch (1), Spiel um Platz 3 (8) gering (−1).</summary>
    public partial class RundeSpielbedeutung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Spielbedeutung",
                table: "TurnierRunden",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""UPDATE "TurnierRunden" SET "Spielbedeutung" = 1 WHERE "Rundenart" = 7;""");
            migrationBuilder.Sql("""UPDATE "TurnierRunden" SET "Spielbedeutung" = -1 WHERE "Rundenart" = 8;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Spielbedeutung",
                table: "TurnierRunden");
        }
    }
}
