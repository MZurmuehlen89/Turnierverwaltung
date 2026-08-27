using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class BildmaterialFuerStammdatenHinzufuegen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Logo",
                table: "Verbaende",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Logo",
                table: "TurnierVorlagen",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Wappen",
                table: "Mannschaften",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Flagge",
                table: "Laender",
                type: "varbinary(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Logo",
                table: "Verbaende");

            migrationBuilder.DropColumn(
                name: "Logo",
                table: "TurnierVorlagen");

            migrationBuilder.DropColumn(
                name: "Wappen",
                table: "Mannschaften");

            migrationBuilder.DropColumn(
                name: "Flagge",
                table: "Laender");
        }
    }
}
