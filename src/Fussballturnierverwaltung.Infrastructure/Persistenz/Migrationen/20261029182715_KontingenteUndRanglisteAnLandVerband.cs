using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class KontingenteUndRanglisteAnLandVerband : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RanglisteId",
                table: "Verbaende",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AbPlatz",
                table: "LandesQuoten",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Spielklasse",
                table: "LandesQuoten",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RanglisteId",
                table: "Laender",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Verbaende_RanglisteId",
                table: "Verbaende",
                column: "RanglisteId");

            migrationBuilder.CreateIndex(
                name: "IX_Laender_RanglisteId",
                table: "Laender",
                column: "RanglisteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Laender_Ranglisten_RanglisteId",
                table: "Laender",
                column: "RanglisteId",
                principalTable: "Ranglisten",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Verbaende_Ranglisten_RanglisteId",
                table: "Verbaende",
                column: "RanglisteId",
                principalTable: "Ranglisten",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Laender_Ranglisten_RanglisteId",
                table: "Laender");

            migrationBuilder.DropForeignKey(
                name: "FK_Verbaende_Ranglisten_RanglisteId",
                table: "Verbaende");

            migrationBuilder.DropIndex(
                name: "IX_Verbaende_RanglisteId",
                table: "Verbaende");

            migrationBuilder.DropIndex(
                name: "IX_Laender_RanglisteId",
                table: "Laender");

            migrationBuilder.DropColumn(
                name: "RanglisteId",
                table: "Verbaende");

            migrationBuilder.DropColumn(
                name: "AbPlatz",
                table: "LandesQuoten");

            migrationBuilder.DropColumn(
                name: "Spielklasse",
                table: "LandesQuoten");

            migrationBuilder.DropColumn(
                name: "RanglisteId",
                table: "Laender");
        }
    }
}
