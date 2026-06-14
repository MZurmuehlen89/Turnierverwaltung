using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class MitRueckspielFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MitRueckspiel",
                table: "VorlagenRunden",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MitRueckspiel",
                table: "TurnierRunden",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Datenmigration: alte Rundenart=4 (KoRundeMitHinUndRueckspiel) → Rundenart=3 + MitRueckspiel=true
            migrationBuilder.Sql(
                "UPDATE TurnierRunden  SET Rundenart = 3, MitRueckspiel = 1 WHERE Rundenart = 4;");
            migrationBuilder.Sql(
                "UPDATE VorlagenRunden SET Rundenart = 3, MitRueckspiel = 1 WHERE Rundenart = 4;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MitRueckspiel",
                table: "VorlagenRunden");

            migrationBuilder.DropColumn(
                name: "MitRueckspiel",
                table: "TurnierRunden");
        }
    }
}
