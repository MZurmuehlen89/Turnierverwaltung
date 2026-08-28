using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class SpielQuellverknuepfungHinzugefuegt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AuswaertsQuellErgebnis",
                table: "Spiele",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AuswaertsQuellSpielId",
                table: "Spiele",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HeimQuellErgebnis",
                table: "Spiele",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HeimQuellSpielId",
                table: "Spiele",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Spiele_AuswaertsQuellSpielId",
                table: "Spiele",
                column: "AuswaertsQuellSpielId");

            migrationBuilder.CreateIndex(
                name: "IX_Spiele_HeimQuellSpielId",
                table: "Spiele",
                column: "HeimQuellSpielId");

            migrationBuilder.AddForeignKey(
                name: "FK_Spiele_Spiele_AuswaertsQuellSpielId",
                table: "Spiele",
                column: "AuswaertsQuellSpielId",
                principalTable: "Spiele",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Spiele_Spiele_HeimQuellSpielId",
                table: "Spiele",
                column: "HeimQuellSpielId",
                principalTable: "Spiele",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Spiele_Spiele_AuswaertsQuellSpielId",
                table: "Spiele");

            migrationBuilder.DropForeignKey(
                name: "FK_Spiele_Spiele_HeimQuellSpielId",
                table: "Spiele");

            migrationBuilder.DropIndex(
                name: "IX_Spiele_AuswaertsQuellSpielId",
                table: "Spiele");

            migrationBuilder.DropIndex(
                name: "IX_Spiele_HeimQuellSpielId",
                table: "Spiele");

            migrationBuilder.DropColumn(
                name: "AuswaertsQuellErgebnis",
                table: "Spiele");

            migrationBuilder.DropColumn(
                name: "AuswaertsQuellSpielId",
                table: "Spiele");

            migrationBuilder.DropColumn(
                name: "HeimQuellErgebnis",
                table: "Spiele");

            migrationBuilder.DropColumn(
                name: "HeimQuellSpielId",
                table: "Spiele");
        }
    }
}
