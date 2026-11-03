using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class PokalsiegerAnRanglisteUndKontingent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PokalsiegerId",
                table: "Ranglisten",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Pokalsieger",
                table: "LandesQuoten",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Ranglisten_PokalsiegerId",
                table: "Ranglisten",
                column: "PokalsiegerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ranglisten_Mannschaften_PokalsiegerId",
                table: "Ranglisten",
                column: "PokalsiegerId",
                principalTable: "Mannschaften",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ranglisten_Mannschaften_PokalsiegerId",
                table: "Ranglisten");

            migrationBuilder.DropIndex(
                name: "IX_Ranglisten_PokalsiegerId",
                table: "Ranglisten");

            migrationBuilder.DropColumn(
                name: "PokalsiegerId",
                table: "Ranglisten");

            migrationBuilder.DropColumn(
                name: "Pokalsieger",
                table: "LandesQuoten");
        }
    }
}
