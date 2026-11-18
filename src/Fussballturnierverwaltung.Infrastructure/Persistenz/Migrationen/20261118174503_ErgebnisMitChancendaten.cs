using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class ErgebnisMitChancendaten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MitChancendaten",
                table: "SpielErgebnisse",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Ausführlich simulierte Spiele seit #431 tragen an Toren und Großchancen eine Chancenart. Ein 0:0 ganz ohne
            // Großchance aus dieser Zeit ist nicht erkennbar und bleibt ohne Chancendaten, bis es neu simuliert wird.
            migrationBuilder.Sql("""
                UPDATE "SpielErgebnisse" SET "MitChancendaten" = TRUE
                WHERE "SpielId" IN (SELECT "SpielId" FROM "SpielEreignisse" WHERE "Chancenart" IS NOT NULL)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MitChancendaten",
                table: "SpielErgebnisse");
        }
    }
}
