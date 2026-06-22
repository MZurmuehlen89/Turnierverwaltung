using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class StandardRegelwerkAufInstanzebene : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StandardRegelwerkId",
                table: "TurnierInstanzen",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TurnierInstanzen_StandardRegelwerkId",
                table: "TurnierInstanzen",
                column: "StandardRegelwerkId");

            migrationBuilder.AddForeignKey(
                name: "FK_TurnierInstanzen_Regelwerke_StandardRegelwerkId",
                table: "TurnierInstanzen",
                column: "StandardRegelwerkId",
                principalTable: "Regelwerke",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TurnierInstanzen_Regelwerke_StandardRegelwerkId",
                table: "TurnierInstanzen");

            migrationBuilder.DropIndex(
                name: "IX_TurnierInstanzen_StandardRegelwerkId",
                table: "TurnierInstanzen");

            migrationBuilder.DropColumn(
                name: "StandardRegelwerkId",
                table: "TurnierInstanzen");
        }
    }
}
