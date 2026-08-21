using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class HalbzeitstandHinzufuegen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HalbzeitAuswaertsTore",
                table: "SpielErgebnisse",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HalbzeitHeimTore",
                table: "SpielErgebnisse",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HalbzeitAuswaertsTore",
                table: "SpielErgebnisse");

            migrationBuilder.DropColumn(
                name: "HalbzeitHeimTore",
                table: "SpielErgebnisse");
        }
    }
}
