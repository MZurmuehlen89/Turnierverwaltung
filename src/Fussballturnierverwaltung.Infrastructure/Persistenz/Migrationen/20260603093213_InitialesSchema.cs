using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class InitialesSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RankingRegeln",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Beschreibung = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Regelart = table.Column<int>(type: "int", nullable: false),
                    Sortierrichtung = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankingRegeln", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RankingRegelSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Beschreibung = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankingRegelSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TurnierVorlagen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Beschreibung = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ErforderlicheMannschaftsanzahl = table.Column<int>(type: "int", nullable: false),
                    VorlagenKategorie = table.Column<int>(type: "int", nullable: false),
                    IstHistorisch = table.Column<bool>(type: "bit", nullable: false),
                    GueltigVon = table.Column<DateOnly>(type: "date", nullable: true),
                    GueltigBis = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnierVorlagen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Verbaende",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kurzname = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Verbaende", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RankingRegelSetEintraege",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RankingRegelSetId = table.Column<int>(type: "int", nullable: false),
                    RankingRegelId = table.Column<int>(type: "int", nullable: false),
                    Reihenfolge = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankingRegelSetEintraege", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RankingRegelSetEintraege_RankingRegelSets_RankingRegelSetId",
                        column: x => x.RankingRegelSetId,
                        principalTable: "RankingRegelSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RankingRegelSetEintraege_RankingRegeln_RankingRegelId",
                        column: x => x.RankingRegelId,
                        principalTable: "RankingRegeln",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Regelwerke",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Beschreibung = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    GueltigVon = table.Column<DateOnly>(type: "date", nullable: true),
                    GueltigBis = table.Column<DateOnly>(type: "date", nullable: true),
                    RankingRegelSetId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regelwerke", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Regelwerke_RankingRegelSets_RankingRegelSetId",
                        column: x => x.RankingRegelSetId,
                        principalTable: "RankingRegelSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TurnierInstanzen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TurnierVorlageId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SaisonOderJahr = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Startdatum = table.Column<DateOnly>(type: "date", nullable: true),
                    Enddatum = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnierInstanzen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TurnierInstanzen_TurnierVorlagen_TurnierVorlageId",
                        column: x => x.TurnierVorlageId,
                        principalTable: "TurnierVorlagen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Laender",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kurzname = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FifaCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Kontinent = table.Column<int>(type: "int", nullable: false),
                    VerbandId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Laender", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Laender_Verbaende_VerbandId",
                        column: x => x.VerbandId,
                        principalTable: "Verbaende",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Entscheidungsverfahren",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegelwerkId = table.Column<int>(type: "int", nullable: false),
                    Reihenfolge = table.Column<int>(type: "int", nullable: false),
                    VerfahrenArt = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entscheidungsverfahren", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Entscheidungsverfahren_Regelwerke_RegelwerkId",
                        column: x => x.RegelwerkId,
                        principalTable: "Regelwerke",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PunkteRegeln",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegelwerkId = table.Column<int>(type: "int", nullable: false),
                    PunkteFuerSieg = table.Column<int>(type: "int", nullable: false),
                    PunkteFuerUnentschieden = table.Column<int>(type: "int", nullable: false),
                    PunkteFuerNiederlage = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PunkteRegeln", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PunkteRegeln_Regelwerke_RegelwerkId",
                        column: x => x.RegelwerkId,
                        principalTable: "Regelwerke",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SimulationsLaeufe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TurnierInstanzId = table.Column<int>(type: "int", nullable: false),
                    StartZeitpunkt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndZeitpunkt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ZufallsSeed = table.Column<int>(type: "int", nullable: false),
                    Beschreibung = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulationsLaeufe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SimulationsLaeufe_TurnierInstanzen_TurnierInstanzId",
                        column: x => x.TurnierInstanzId,
                        principalTable: "TurnierInstanzen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TurnierRunden",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TurnierInstanzId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reihenfolge = table.Column<int>(type: "int", nullable: false),
                    Rundenart = table.Column<int>(type: "int", nullable: false),
                    EingangsMannschaftsanzahl = table.Column<int>(type: "int", nullable: false),
                    AusgangsMannschaftsanzahl = table.Column<int>(type: "int", nullable: false),
                    RegelwerkId = table.Column<int>(type: "int", nullable: false),
                    AuswaertstorRegelAktiv = table.Column<bool>(type: "bit", nullable: false),
                    KonfigurationJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnierRunden", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TurnierRunden_Regelwerke_RegelwerkId",
                        column: x => x.RegelwerkId,
                        principalTable: "Regelwerke",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TurnierRunden_TurnierInstanzen_TurnierInstanzId",
                        column: x => x.TurnierInstanzId,
                        principalTable: "TurnierInstanzen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mannschaften",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kurzname = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Mannschaftsart = table.Column<int>(type: "int", nullable: false),
                    LandId = table.Column<int>(type: "int", nullable: false),
                    OffensivRating = table.Column<int>(type: "int", nullable: false),
                    MittelfeldRating = table.Column<int>(type: "int", nullable: false),
                    DefensivRating = table.Column<int>(type: "int", nullable: false),
                    Heimstaerke = table.Column<int>(type: "int", nullable: false),
                    Auswaertsstaerke = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mannschaften", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mannschaften_Laender_LandId",
                        column: x => x.LandId,
                        principalTable: "Laender",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Auslosungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TurnierInstanzId = table.Column<int>(type: "int", nullable: false),
                    TurnierRundeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Datum = table.Column<DateOnly>(type: "date", nullable: true),
                    Auslosungsart = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IstAutomatisch = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auslosungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Auslosungen_TurnierInstanzen_TurnierInstanzId",
                        column: x => x.TurnierInstanzId,
                        principalTable: "TurnierInstanzen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Auslosungen_TurnierRunden_TurnierRundeId",
                        column: x => x.TurnierRundeId,
                        principalTable: "TurnierRunden",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Spiele",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TurnierInstanzId = table.Column<int>(type: "int", nullable: false),
                    TurnierRundeId = table.Column<int>(type: "int", nullable: false),
                    Spielnummer = table.Column<int>(type: "int", nullable: false),
                    Spieltag = table.Column<int>(type: "int", nullable: true),
                    Datum = table.Column<DateOnly>(type: "date", nullable: true),
                    HeimMannschaftId = table.Column<int>(type: "int", nullable: false),
                    AuswaertsMannschaftId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SimulationsModus = table.Column<int>(type: "int", nullable: false),
                    IstRueckspiel = table.Column<bool>(type: "bit", nullable: false),
                    HinspielId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spiele", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Spiele_Mannschaften_AuswaertsMannschaftId",
                        column: x => x.AuswaertsMannschaftId,
                        principalTable: "Mannschaften",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Spiele_Mannschaften_HeimMannschaftId",
                        column: x => x.HeimMannschaftId,
                        principalTable: "Mannschaften",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Spiele_Spiele_HinspielId",
                        column: x => x.HinspielId,
                        principalTable: "Spiele",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Spiele_TurnierInstanzen_TurnierInstanzId",
                        column: x => x.TurnierInstanzId,
                        principalTable: "TurnierInstanzen",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Spiele_TurnierRunden_TurnierRundeId",
                        column: x => x.TurnierRundeId,
                        principalTable: "TurnierRunden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuslosungsRegeln",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuslosungId = table.Column<int>(type: "int", nullable: false),
                    Regelart = table.Column<int>(type: "int", nullable: false),
                    Beschreibung = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IstAktiv = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuslosungsRegeln", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuslosungsRegeln_Auslosungen_AuslosungId",
                        column: x => x.AuslosungId,
                        principalTable: "Auslosungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Lostoepfe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuslosungId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reihenfolge = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lostoepfe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lostoepfe_Auslosungen_AuslosungId",
                        column: x => x.AuslosungId,
                        principalTable: "Auslosungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpielEreignisse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SpielId = table.Column<int>(type: "int", nullable: false),
                    Minute = table.Column<int>(type: "int", nullable: false),
                    EreignisArt = table.Column<int>(type: "int", nullable: false),
                    MannschaftId = table.Column<int>(type: "int", nullable: true),
                    Beschreibung = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpielEreignisse", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpielEreignisse_Mannschaften_MannschaftId",
                        column: x => x.MannschaftId,
                        principalTable: "Mannschaften",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpielEreignisse_Spiele_SpielId",
                        column: x => x.SpielId,
                        principalTable: "Spiele",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpielErgebnisse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SpielId = table.Column<int>(type: "int", nullable: false),
                    HeimToreRegulaer = table.Column<int>(type: "int", nullable: false),
                    AuswaertsToreRegulaer = table.Column<int>(type: "int", nullable: false),
                    HeimToreVerlaengerung = table.Column<int>(type: "int", nullable: true),
                    AuswaertsToreVerlaengerung = table.Column<int>(type: "int", nullable: true),
                    HeimElfmetertore = table.Column<int>(type: "int", nullable: true),
                    AuswaertsElfmetertore = table.Column<int>(type: "int", nullable: true),
                    SiegerMannschaftId = table.Column<int>(type: "int", nullable: true),
                    EntscheidungArt = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpielErgebnisse", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpielErgebnisse_Mannschaften_SiegerMannschaftId",
                        column: x => x.SiegerMannschaftId,
                        principalTable: "Mannschaften",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpielErgebnisse_Spiele_SpielId",
                        column: x => x.SpielId,
                        principalTable: "Spiele",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LostopfEintraege",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LostopfId = table.Column<int>(type: "int", nullable: false),
                    MannschaftId = table.Column<int>(type: "int", nullable: false),
                    SetzPosition = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LostopfEintraege", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LostopfEintraege_Lostoepfe_LostopfId",
                        column: x => x.LostopfId,
                        principalTable: "Lostoepfe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LostopfEintraege_Mannschaften_MannschaftId",
                        column: x => x.MannschaftId,
                        principalTable: "Mannschaften",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuslosungsErgebnisse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuslosungId = table.Column<int>(type: "int", nullable: false),
                    PaarungsNummer = table.Column<int>(type: "int", nullable: false),
                    HeimMannschaftId = table.Column<int>(type: "int", nullable: true),
                    AuswaertsMannschaftId = table.Column<int>(type: "int", nullable: true),
                    ZugeordneterTurnierBaumKnotenId = table.Column<int>(type: "int", nullable: true),
                    IstFreilos = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuslosungsErgebnisse", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuslosungsErgebnisse_Auslosungen_AuslosungId",
                        column: x => x.AuslosungId,
                        principalTable: "Auslosungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuslosungsErgebnisse_Mannschaften_AuswaertsMannschaftId",
                        column: x => x.AuswaertsMannschaftId,
                        principalTable: "Mannschaften",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuslosungsErgebnisse_Mannschaften_HeimMannschaftId",
                        column: x => x.HeimMannschaftId,
                        principalTable: "Mannschaften",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TurnierBaeume",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TurnierInstanzId = table.Column<int>(type: "int", nullable: false),
                    WurzelKnotenId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnierBaeume", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TurnierBaeume_TurnierInstanzen_TurnierInstanzId",
                        column: x => x.TurnierInstanzId,
                        principalTable: "TurnierInstanzen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TurnierBaumKnoten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TurnierBaumId = table.Column<int>(type: "int", nullable: false),
                    ElternKnotenId = table.Column<int>(type: "int", nullable: true),
                    LinkerKindKnotenId = table.Column<int>(type: "int", nullable: true),
                    RechterKindKnotenId = table.Column<int>(type: "int", nullable: true),
                    SpielId = table.Column<int>(type: "int", nullable: true),
                    Runde = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnierBaumKnoten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TurnierBaumKnoten_Spiele_SpielId",
                        column: x => x.SpielId,
                        principalTable: "Spiele",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TurnierBaumKnoten_TurnierBaeume_TurnierBaumId",
                        column: x => x.TurnierBaumId,
                        principalTable: "TurnierBaeume",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TurnierBaumKnoten_TurnierBaumKnoten_ElternKnotenId",
                        column: x => x.ElternKnotenId,
                        principalTable: "TurnierBaumKnoten",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TurnierBaumKnoten_TurnierBaumKnoten_LinkerKindKnotenId",
                        column: x => x.LinkerKindKnotenId,
                        principalTable: "TurnierBaumKnoten",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TurnierBaumKnoten_TurnierBaumKnoten_RechterKindKnotenId",
                        column: x => x.RechterKindKnotenId,
                        principalTable: "TurnierBaumKnoten",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auslosungen_TurnierInstanzId",
                table: "Auslosungen",
                column: "TurnierInstanzId");

            migrationBuilder.CreateIndex(
                name: "IX_Auslosungen_TurnierRundeId",
                table: "Auslosungen",
                column: "TurnierRundeId");

            migrationBuilder.CreateIndex(
                name: "IX_AuslosungsErgebnisse_AuslosungId",
                table: "AuslosungsErgebnisse",
                column: "AuslosungId");

            migrationBuilder.CreateIndex(
                name: "IX_AuslosungsErgebnisse_AuswaertsMannschaftId",
                table: "AuslosungsErgebnisse",
                column: "AuswaertsMannschaftId");

            migrationBuilder.CreateIndex(
                name: "IX_AuslosungsErgebnisse_HeimMannschaftId",
                table: "AuslosungsErgebnisse",
                column: "HeimMannschaftId");

            migrationBuilder.CreateIndex(
                name: "IX_AuslosungsErgebnisse_ZugeordneterTurnierBaumKnotenId",
                table: "AuslosungsErgebnisse",
                column: "ZugeordneterTurnierBaumKnotenId");

            migrationBuilder.CreateIndex(
                name: "IX_AuslosungsRegeln_AuslosungId",
                table: "AuslosungsRegeln",
                column: "AuslosungId");

            migrationBuilder.CreateIndex(
                name: "IX_Entscheidungsverfahren_RegelwerkId",
                table: "Entscheidungsverfahren",
                column: "RegelwerkId");

            migrationBuilder.CreateIndex(
                name: "IX_Laender_FifaCode",
                table: "Laender",
                column: "FifaCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Laender_VerbandId",
                table: "Laender",
                column: "VerbandId");

            migrationBuilder.CreateIndex(
                name: "IX_Lostoepfe_AuslosungId_Reihenfolge",
                table: "Lostoepfe",
                columns: new[] { "AuslosungId", "Reihenfolge" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LostopfEintraege_LostopfId",
                table: "LostopfEintraege",
                column: "LostopfId");

            migrationBuilder.CreateIndex(
                name: "IX_LostopfEintraege_MannschaftId",
                table: "LostopfEintraege",
                column: "MannschaftId");

            migrationBuilder.CreateIndex(
                name: "IX_Mannschaften_LandId",
                table: "Mannschaften",
                column: "LandId");

            migrationBuilder.CreateIndex(
                name: "IX_PunkteRegeln_RegelwerkId",
                table: "PunkteRegeln",
                column: "RegelwerkId");

            migrationBuilder.CreateIndex(
                name: "IX_RankingRegelSetEintraege_RankingRegelId",
                table: "RankingRegelSetEintraege",
                column: "RankingRegelId");

            migrationBuilder.CreateIndex(
                name: "IX_RankingRegelSetEintraege_RankingRegelSetId_Reihenfolge",
                table: "RankingRegelSetEintraege",
                columns: new[] { "RankingRegelSetId", "Reihenfolge" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Regelwerke_RankingRegelSetId",
                table: "Regelwerke",
                column: "RankingRegelSetId");

            migrationBuilder.CreateIndex(
                name: "IX_SimulationsLaeufe_TurnierInstanzId",
                table: "SimulationsLaeufe",
                column: "TurnierInstanzId");

            migrationBuilder.CreateIndex(
                name: "IX_Spiele_AuswaertsMannschaftId",
                table: "Spiele",
                column: "AuswaertsMannschaftId");

            migrationBuilder.CreateIndex(
                name: "IX_Spiele_HeimMannschaftId",
                table: "Spiele",
                column: "HeimMannschaftId");

            migrationBuilder.CreateIndex(
                name: "IX_Spiele_HinspielId",
                table: "Spiele",
                column: "HinspielId");

            migrationBuilder.CreateIndex(
                name: "IX_Spiele_TurnierInstanzId",
                table: "Spiele",
                column: "TurnierInstanzId");

            migrationBuilder.CreateIndex(
                name: "IX_Spiele_TurnierRundeId",
                table: "Spiele",
                column: "TurnierRundeId");

            migrationBuilder.CreateIndex(
                name: "IX_SpielEreignisse_MannschaftId",
                table: "SpielEreignisse",
                column: "MannschaftId");

            migrationBuilder.CreateIndex(
                name: "IX_SpielEreignisse_SpielId",
                table: "SpielEreignisse",
                column: "SpielId");

            migrationBuilder.CreateIndex(
                name: "IX_SpielErgebnisse_SiegerMannschaftId",
                table: "SpielErgebnisse",
                column: "SiegerMannschaftId");

            migrationBuilder.CreateIndex(
                name: "IX_SpielErgebnisse_SpielId",
                table: "SpielErgebnisse",
                column: "SpielId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TurnierBaeume_TurnierInstanzId",
                table: "TurnierBaeume",
                column: "TurnierInstanzId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnierBaeume_WurzelKnotenId",
                table: "TurnierBaeume",
                column: "WurzelKnotenId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnierBaumKnoten_ElternKnotenId",
                table: "TurnierBaumKnoten",
                column: "ElternKnotenId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnierBaumKnoten_LinkerKindKnotenId",
                table: "TurnierBaumKnoten",
                column: "LinkerKindKnotenId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnierBaumKnoten_RechterKindKnotenId",
                table: "TurnierBaumKnoten",
                column: "RechterKindKnotenId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnierBaumKnoten_SpielId",
                table: "TurnierBaumKnoten",
                column: "SpielId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnierBaumKnoten_TurnierBaumId",
                table: "TurnierBaumKnoten",
                column: "TurnierBaumId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnierInstanzen_TurnierVorlageId",
                table: "TurnierInstanzen",
                column: "TurnierVorlageId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnierRunden_RegelwerkId",
                table: "TurnierRunden",
                column: "RegelwerkId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnierRunden_TurnierInstanzId_Reihenfolge",
                table: "TurnierRunden",
                columns: new[] { "TurnierInstanzId", "Reihenfolge" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Verbaende_Kurzname",
                table: "Verbaende",
                column: "Kurzname",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AuslosungsErgebnisse_TurnierBaumKnoten_ZugeordneterTurnierBaumKnotenId",
                table: "AuslosungsErgebnisse",
                column: "ZugeordneterTurnierBaumKnotenId",
                principalTable: "TurnierBaumKnoten",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TurnierBaeume_TurnierBaumKnoten_WurzelKnotenId",
                table: "TurnierBaeume",
                column: "WurzelKnotenId",
                principalTable: "TurnierBaumKnoten",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Spiele_TurnierInstanzen_TurnierInstanzId",
                table: "Spiele");

            migrationBuilder.DropForeignKey(
                name: "FK_TurnierBaeume_TurnierInstanzen_TurnierInstanzId",
                table: "TurnierBaeume");

            migrationBuilder.DropForeignKey(
                name: "FK_TurnierRunden_TurnierInstanzen_TurnierInstanzId",
                table: "TurnierRunden");

            migrationBuilder.DropForeignKey(
                name: "FK_Spiele_TurnierRunden_TurnierRundeId",
                table: "Spiele");

            migrationBuilder.DropForeignKey(
                name: "FK_Spiele_Mannschaften_AuswaertsMannschaftId",
                table: "Spiele");

            migrationBuilder.DropForeignKey(
                name: "FK_Spiele_Mannschaften_HeimMannschaftId",
                table: "Spiele");

            migrationBuilder.DropForeignKey(
                name: "FK_TurnierBaeume_TurnierBaumKnoten_WurzelKnotenId",
                table: "TurnierBaeume");

            migrationBuilder.DropTable(
                name: "AuslosungsErgebnisse");

            migrationBuilder.DropTable(
                name: "AuslosungsRegeln");

            migrationBuilder.DropTable(
                name: "Entscheidungsverfahren");

            migrationBuilder.DropTable(
                name: "LostopfEintraege");

            migrationBuilder.DropTable(
                name: "PunkteRegeln");

            migrationBuilder.DropTable(
                name: "RankingRegelSetEintraege");

            migrationBuilder.DropTable(
                name: "SimulationsLaeufe");

            migrationBuilder.DropTable(
                name: "SpielEreignisse");

            migrationBuilder.DropTable(
                name: "SpielErgebnisse");

            migrationBuilder.DropTable(
                name: "Lostoepfe");

            migrationBuilder.DropTable(
                name: "RankingRegeln");

            migrationBuilder.DropTable(
                name: "Auslosungen");

            migrationBuilder.DropTable(
                name: "TurnierInstanzen");

            migrationBuilder.DropTable(
                name: "TurnierVorlagen");

            migrationBuilder.DropTable(
                name: "TurnierRunden");

            migrationBuilder.DropTable(
                name: "Regelwerke");

            migrationBuilder.DropTable(
                name: "RankingRegelSets");

            migrationBuilder.DropTable(
                name: "Mannschaften");

            migrationBuilder.DropTable(
                name: "Laender");

            migrationBuilder.DropTable(
                name: "Verbaende");

            migrationBuilder.DropTable(
                name: "TurnierBaumKnoten");

            migrationBuilder.DropTable(
                name: "Spiele");

            migrationBuilder.DropTable(
                name: "TurnierBaeume");
        }
    }
}
