using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class WiederholungsspielImErgebnis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AuswaertsToreWiederholung",
                table: "SpielErgebnisse",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AuswaertsToreWiederholungVerlaengerung",
                table: "SpielErgebnisse",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HeimToreWiederholung",
                table: "SpielErgebnisse",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HeimToreWiederholungVerlaengerung",
                table: "SpielErgebnisse",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ImWiederholungsspiel",
                table: "SpielEreignisse",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuswaertsToreWiederholung",
                table: "SpielErgebnisse");

            migrationBuilder.DropColumn(
                name: "AuswaertsToreWiederholungVerlaengerung",
                table: "SpielErgebnisse");

            migrationBuilder.DropColumn(
                name: "HeimToreWiederholung",
                table: "SpielErgebnisse");

            migrationBuilder.DropColumn(
                name: "HeimToreWiederholungVerlaengerung",
                table: "SpielErgebnisse");

            migrationBuilder.DropColumn(
                name: "ImWiederholungsspiel",
                table: "SpielEreignisse");
        }
    }
}
