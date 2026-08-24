using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class LandFilterZuTurnierInstanzHinzufuegen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LandId",
                table: "TurnierInstanzen",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TurnierInstanzen_LandId",
                table: "TurnierInstanzen",
                column: "LandId");

            migrationBuilder.AddForeignKey(
                name: "FK_TurnierInstanzen_Laender_LandId",
                table: "TurnierInstanzen",
                column: "LandId",
                principalTable: "Laender",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TurnierInstanzen_Laender_LandId",
                table: "TurnierInstanzen");

            migrationBuilder.DropIndex(
                name: "IX_TurnierInstanzen_LandId",
                table: "TurnierInstanzen");

            migrationBuilder.DropColumn(
                name: "LandId",
                table: "TurnierInstanzen");
        }
    }
}
