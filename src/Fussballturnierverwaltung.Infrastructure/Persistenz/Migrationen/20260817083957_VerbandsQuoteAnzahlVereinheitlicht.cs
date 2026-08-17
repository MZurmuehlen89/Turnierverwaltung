using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class VerbandsQuoteAnzahlVereinheitlicht : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinAnzahl",
                table: "VerbandsQuoten");

            migrationBuilder.RenameColumn(
                name: "MaxAnzahl",
                table: "VerbandsQuoten",
                newName: "Anzahl");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Anzahl",
                table: "VerbandsQuoten",
                newName: "MaxAnzahl");

            migrationBuilder.AddColumn<int>(
                name: "MinAnzahl",
                table: "VerbandsQuoten",
                type: "int",
                nullable: true);
        }
    }
}
