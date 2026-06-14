using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class VorlagenRundeRegelwerk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RegelwerkId",
                table: "VorlagenRunden",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VorlagenRunden_RegelwerkId",
                table: "VorlagenRunden",
                column: "RegelwerkId");

            migrationBuilder.AddForeignKey(
                name: "FK_VorlagenRunden_Regelwerke_RegelwerkId",
                table: "VorlagenRunden",
                column: "RegelwerkId",
                principalTable: "Regelwerke",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VorlagenRunden_Regelwerke_RegelwerkId",
                table: "VorlagenRunden");

            migrationBuilder.DropIndex(
                name: "IX_VorlagenRunden_RegelwerkId",
                table: "VorlagenRunden");

            migrationBuilder.DropColumn(
                name: "RegelwerkId",
                table: "VorlagenRunden");
        }
    }
}
