using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class AuslosungsfilterUndPlatzhalter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GruppenAnzahl",
                table: "TurnierRunden",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MannschaftsartFilter",
                table: "TurnierInstanzen",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VerbandId",
                table: "TurnierInstanzen",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MannschaftId",
                table: "LostopfEintraege",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "PlatzhalterName",
                table: "LostopfEintraege",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuswaertsPlatzhalterName",
                table: "AuslosungsErgebnisse",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeimPlatzhalterName",
                table: "AuslosungsErgebnisse",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TurnierInstanzen_VerbandId",
                table: "TurnierInstanzen",
                column: "VerbandId");

            migrationBuilder.AddForeignKey(
                name: "FK_TurnierInstanzen_Verbaende_VerbandId",
                table: "TurnierInstanzen",
                column: "VerbandId",
                principalTable: "Verbaende",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TurnierInstanzen_Verbaende_VerbandId",
                table: "TurnierInstanzen");

            migrationBuilder.DropIndex(
                name: "IX_TurnierInstanzen_VerbandId",
                table: "TurnierInstanzen");

            migrationBuilder.DropColumn(
                name: "GruppenAnzahl",
                table: "TurnierRunden");

            migrationBuilder.DropColumn(
                name: "MannschaftsartFilter",
                table: "TurnierInstanzen");

            migrationBuilder.DropColumn(
                name: "VerbandId",
                table: "TurnierInstanzen");

            migrationBuilder.DropColumn(
                name: "PlatzhalterName",
                table: "LostopfEintraege");

            migrationBuilder.DropColumn(
                name: "AuswaertsPlatzhalterName",
                table: "AuslosungsErgebnisse");

            migrationBuilder.DropColumn(
                name: "HeimPlatzhalterName",
                table: "AuslosungsErgebnisse");

            migrationBuilder.AlterColumn<int>(
                name: "MannschaftId",
                table: "LostopfEintraege",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
