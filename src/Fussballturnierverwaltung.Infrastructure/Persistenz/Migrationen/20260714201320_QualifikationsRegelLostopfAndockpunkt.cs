using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class QualifikationsRegelLostopfAndockpunkt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Platzierung",
                table: "LostopfEintraege",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QualifikationsRegelId",
                table: "LostopfEintraege",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LostopfEintraege_QualifikationsRegelId",
                table: "LostopfEintraege",
                column: "QualifikationsRegelId");

            migrationBuilder.AddForeignKey(
                name: "FK_LostopfEintraege_QualifikationsRegeln_QualifikationsRegelId",
                table: "LostopfEintraege",
                column: "QualifikationsRegelId",
                principalTable: "QualifikationsRegeln",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LostopfEintraege_QualifikationsRegeln_QualifikationsRegelId",
                table: "LostopfEintraege");

            migrationBuilder.DropIndex(
                name: "IX_LostopfEintraege_QualifikationsRegelId",
                table: "LostopfEintraege");

            migrationBuilder.DropColumn(
                name: "Platzierung",
                table: "LostopfEintraege");

            migrationBuilder.DropColumn(
                name: "QualifikationsRegelId",
                table: "LostopfEintraege");
        }
    }
}
