using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class TurnierVorlagenVereinheitlichen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TurnierInstanzen_TurnierVorlagen_TurnierVorlageId",
                table: "TurnierInstanzen");

            migrationBuilder.DropTable(
                name: "VorlagenPaarungen");

            migrationBuilder.DropTable(
                name: "VorlagenUebergangsregeln");

            migrationBuilder.DropTable(
                name: "VorlagenRunden");

            migrationBuilder.DropTable(
                name: "TurnierVorlagen");

            migrationBuilder.DropIndex(
                name: "IX_TurnierInstanzen_TurnierVorlageId",
                table: "TurnierInstanzen");

            // Kein Rename: TurnierVorlageId (Fremdschlüssel, entfernte Selbstreferenz) und
            // ZielMannschaftsanzahl (neues, fachlich unabhängiges Feld) sind trotz identischem CLR-Typ
            // (int?) an derselben Spaltenposition zwei verschiedene Dinge — das automatische Scaffolding
            // schlug hier fälschlich einen Rename vor, händisch auf Drop+Add korrigiert.
            migrationBuilder.DropColumn(
                name: "TurnierVorlageId",
                table: "TurnierInstanzen");

            migrationBuilder.AlterColumn<int>(
                name: "RegelwerkId",
                table: "TurnierRunden",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "Beschreibung",
                table: "TurnierInstanzen",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Kategorie",
                table: "TurnierInstanzen",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "Logo",
                table: "TurnierInstanzen",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ZielMannschaftsanzahl",
                table: "TurnierInstanzen",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Beschreibung",
                table: "TurnierInstanzen");

            migrationBuilder.DropColumn(
                name: "Kategorie",
                table: "TurnierInstanzen");

            migrationBuilder.DropColumn(
                name: "Logo",
                table: "TurnierInstanzen");

            migrationBuilder.DropColumn(
                name: "ZielMannschaftsanzahl",
                table: "TurnierInstanzen");

            migrationBuilder.AddColumn<int>(
                name: "TurnierVorlageId",
                table: "TurnierInstanzen",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "RegelwerkId",
                table: "TurnierRunden",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "TurnierVorlagen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Beschreibung = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ErforderlicheMannschaftsanzahl = table.Column<int>(type: "int", nullable: false),
                    GueltigBis = table.Column<DateOnly>(type: "date", nullable: true),
                    GueltigVon = table.Column<DateOnly>(type: "date", nullable: true),
                    IstHistorisch = table.Column<bool>(type: "bit", nullable: false),
                    Logo = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VorlagenKategorie = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnierVorlagen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VorlagenRunden",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegelwerkId = table.Column<int>(type: "int", nullable: true),
                    TurnierVorlageId = table.Column<int>(type: "int", nullable: false),
                    AusgangsMannschaftsanzahl = table.Column<int>(type: "int", nullable: false),
                    AuswaertstorRegelAktiv = table.Column<bool>(type: "bit", nullable: false),
                    EingangsMannschaftsanzahl = table.Column<int>(type: "int", nullable: false),
                    GruppenAnzahl = table.Column<int>(type: "int", nullable: true),
                    MitRueckspiel = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PaarungFestVorgegeben = table.Column<bool>(type: "bit", nullable: false),
                    Reihenfolge = table.Column<int>(type: "int", nullable: false),
                    Rundenart = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VorlagenRunden", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VorlagenRunden_Regelwerke_RegelwerkId",
                        column: x => x.RegelwerkId,
                        principalTable: "Regelwerke",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VorlagenRunden_TurnierVorlagen_TurnierVorlageId",
                        column: x => x.TurnierVorlageId,
                        principalTable: "TurnierVorlagen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VorlagenPaarungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VorlagenRundeId = table.Column<int>(type: "int", nullable: false),
                    PaarungsNummer = table.Column<int>(type: "int", nullable: false),
                    PlatzhalterBezeichnung = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SlotNr = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VorlagenPaarungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VorlagenPaarungen_VorlagenRunden_VorlagenRundeId",
                        column: x => x.VorlagenRundeId,
                        principalTable: "VorlagenRunden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VorlagenUebergangsregeln",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuellVorlagenRundeId = table.Column<int>(type: "int", nullable: false),
                    ZielVorlagenRundeId = table.Column<int>(type: "int", nullable: false),
                    AnzahlBeste = table.Column<int>(type: "int", nullable: true),
                    GruppeIndex = table.Column<int>(type: "int", nullable: true),
                    PlatzierungBis = table.Column<int>(type: "int", nullable: false),
                    PlatzierungVon = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VorlagenUebergangsregeln", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VorlagenUebergangsregeln_VorlagenRunden_QuellVorlagenRundeId",
                        column: x => x.QuellVorlagenRundeId,
                        principalTable: "VorlagenRunden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VorlagenUebergangsregeln_VorlagenRunden_ZielVorlagenRundeId",
                        column: x => x.ZielVorlagenRundeId,
                        principalTable: "VorlagenRunden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TurnierInstanzen_TurnierVorlageId",
                table: "TurnierInstanzen",
                column: "TurnierVorlageId");

            migrationBuilder.CreateIndex(
                name: "IX_VorlagenPaarungen_VorlagenRundeId_PaarungsNummer_SlotNr",
                table: "VorlagenPaarungen",
                columns: new[] { "VorlagenRundeId", "PaarungsNummer", "SlotNr" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VorlagenRunden_RegelwerkId",
                table: "VorlagenRunden",
                column: "RegelwerkId");

            migrationBuilder.CreateIndex(
                name: "IX_VorlagenRunden_TurnierVorlageId_Reihenfolge",
                table: "VorlagenRunden",
                columns: new[] { "TurnierVorlageId", "Reihenfolge" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VorlagenUebergangsregeln_QuellVorlagenRundeId",
                table: "VorlagenUebergangsregeln",
                column: "QuellVorlagenRundeId");

            migrationBuilder.CreateIndex(
                name: "IX_VorlagenUebergangsregeln_ZielVorlagenRundeId",
                table: "VorlagenUebergangsregeln",
                column: "ZielVorlagenRundeId");

            migrationBuilder.AddForeignKey(
                name: "FK_TurnierInstanzen_TurnierVorlagen_TurnierVorlageId",
                table: "TurnierInstanzen",
                column: "TurnierVorlageId",
                principalTable: "TurnierVorlagen",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
