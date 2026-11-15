using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Beschreibung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Regelart = table.Column<int>(type: "integer", nullable: false),
                    Sortierrichtung = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankingRegeln", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RankingRegelSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Beschreibung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DirektenVergleichWiederholen = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankingRegelSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RankingRegelSetEintraege",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RankingRegelSetId = table.Column<int>(type: "integer", nullable: false),
                    RankingRegelId = table.Column<int>(type: "integer", nullable: false),
                    Reihenfolge = table.Column<int>(type: "integer", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Beschreibung = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    GueltigVon = table.Column<DateOnly>(type: "date", nullable: true),
                    GueltigBis = table.Column<DateOnly>(type: "date", nullable: true),
                    RankingRegelSetId = table.Column<int>(type: "integer", nullable: true)
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
                name: "Entscheidungsverfahren",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RegelwerkId = table.Column<int>(type: "integer", nullable: false),
                    Reihenfolge = table.Column<int>(type: "integer", nullable: false),
                    VerfahrenArt = table.Column<int>(type: "integer", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RegelwerkId = table.Column<int>(type: "integer", nullable: false),
                    PunkteFuerSieg = table.Column<int>(type: "integer", nullable: false),
                    PunkteFuerUnentschieden = table.Column<int>(type: "integer", nullable: false),
                    PunkteFuerNiederlage = table.Column<int>(type: "integer", nullable: false)
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
                name: "Auslosungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TurnierInstanzId = table.Column<int>(type: "integer", nullable: false),
                    TurnierRundeId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Datum = table.Column<DateOnly>(type: "date", nullable: true),
                    Auslosungsart = table.Column<int>(type: "integer", nullable: false),
                    Gruppenposition = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IstAutomatisch = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auslosungen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuslosungsRegeln",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuslosungId = table.Column<int>(type: "integer", nullable: false),
                    Regelart = table.Column<int>(type: "integer", nullable: false),
                    Beschreibung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IstAktiv = table.Column<bool>(type: "boolean", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuslosungId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Reihenfolge = table.Column<int>(type: "integer", nullable: false)
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
                name: "AuslosungsErgebnisse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuslosungId = table.Column<int>(type: "integer", nullable: false),
                    PaarungsNummer = table.Column<int>(type: "integer", nullable: false),
                    HeimMannschaftId = table.Column<int>(type: "integer", nullable: true),
                    AuswaertsMannschaftId = table.Column<int>(type: "integer", nullable: true),
                    HeimPlatzhalterName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AuswaertsPlatzhalterName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ZugeordneterTurnierBaumKnotenId = table.Column<int>(type: "integer", nullable: true),
                    IstFreilos = table.Column<bool>(type: "boolean", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "Laender",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kurzname = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FifaCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Kontinent = table.Column<int>(type: "integer", nullable: false),
                    VerbandId = table.Column<int>(type: "integer", nullable: false),
                    Flagge = table.Column<byte[]>(type: "bytea", nullable: true),
                    RanglisteId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Laender", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mannschaften",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kurzname = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Mannschaftsart = table.Column<int>(type: "integer", nullable: false),
                    LandId = table.Column<int>(type: "integer", nullable: false),
                    OffensivRating = table.Column<int>(type: "integer", nullable: false),
                    MittelfeldRating = table.Column<int>(type: "integer", nullable: false),
                    DefensivRating = table.Column<int>(type: "integer", nullable: false),
                    Heimstaerke = table.Column<int>(type: "integer", nullable: false),
                    Auswaertsstaerke = table.Column<int>(type: "integer", nullable: false),
                    Wappen = table.Column<byte[]>(type: "bytea", nullable: true)
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
                name: "Ranglisten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Datum = table.Column<DateOnly>(type: "date", nullable: false),
                    Quelle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProfigrenzeSpielklasse = table.Column<int>(type: "integer", nullable: true),
                    PokalsiegerId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ranglisten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ranglisten_Mannschaften_PokalsiegerId",
                        column: x => x.PokalsiegerId,
                        principalTable: "Mannschaften",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RanglistenAbschnitte",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RanglisteId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Spielklasse = table.Column<int>(type: "integer", nullable: false),
                    BisPlatz = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RanglistenAbschnitte", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RanglistenAbschnitte_Ranglisten_RanglisteId",
                        column: x => x.RanglisteId,
                        principalTable: "Ranglisten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RanglistenEintraege",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RanglisteId = table.Column<int>(type: "integer", nullable: false),
                    MannschaftId = table.Column<int>(type: "integer", nullable: false),
                    Rang = table.Column<int>(type: "integer", nullable: false),
                    Punkte = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RanglistenEintraege", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RanglistenEintraege_Mannschaften_MannschaftId",
                        column: x => x.MannschaftId,
                        principalTable: "Mannschaften",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RanglistenEintraege_Ranglisten_RanglisteId",
                        column: x => x.RanglisteId,
                        principalTable: "Ranglisten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Verbaende",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kurzname = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Logo = table.Column<byte[]>(type: "bytea", nullable: true),
                    RanglisteId = table.Column<int>(type: "integer", nullable: true),
                    VereinsRanglisteId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Verbaende", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Verbaende_Ranglisten_RanglisteId",
                        column: x => x.RanglisteId,
                        principalTable: "Ranglisten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Verbaende_Ranglisten_VereinsRanglisteId",
                        column: x => x.VereinsRanglisteId,
                        principalTable: "Ranglisten",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TurnierInstanzen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Beschreibung = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Variante = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SaisonOderJahr = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Kategorie = table.Column<int>(type: "integer", nullable: false),
                    ZielMannschaftsanzahl = table.Column<int>(type: "integer", nullable: true),
                    Logo = table.Column<byte[]>(type: "bytea", nullable: true),
                    Startdatum = table.Column<DateOnly>(type: "date", nullable: true),
                    Enddatum = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StandardRegelwerkId = table.Column<int>(type: "integer", nullable: true),
                    VerbandId = table.Column<int>(type: "integer", nullable: true),
                    LandId = table.Column<int>(type: "integer", nullable: true),
                    MannschaftsartFilter = table.Column<int>(type: "integer", nullable: false),
                    TitelverteidigerId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnierInstanzen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TurnierInstanzen_Laender_LandId",
                        column: x => x.LandId,
                        principalTable: "Laender",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TurnierInstanzen_Mannschaften_TitelverteidigerId",
                        column: x => x.TitelverteidigerId,
                        principalTable: "Mannschaften",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TurnierInstanzen_Regelwerke_StandardRegelwerkId",
                        column: x => x.StandardRegelwerkId,
                        principalTable: "Regelwerke",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TurnierInstanzen_Verbaende_VerbandId",
                        column: x => x.VerbandId,
                        principalTable: "Verbaende",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LandesQuoten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TurnierInstanzId = table.Column<int>(type: "integer", nullable: false),
                    LandId = table.Column<int>(type: "integer", nullable: false),
                    Anzahl = table.Column<int>(type: "integer", nullable: false),
                    Spielklasse = table.Column<int>(type: "integer", nullable: true),
                    AbPlatz = table.Column<int>(type: "integer", nullable: true),
                    Pokalsieger = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandesQuoten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandesQuoten_Laender_LandId",
                        column: x => x.LandId,
                        principalTable: "Laender",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandesQuoten_TurnierInstanzen_TurnierInstanzId",
                        column: x => x.TurnierInstanzId,
                        principalTable: "TurnierInstanzen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SimulationsLaeufe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TurnierInstanzId = table.Column<int>(type: "integer", nullable: false),
                    StartZeitpunkt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndZeitpunkt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ZufallsSeed = table.Column<int>(type: "integer", nullable: false),
                    Beschreibung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
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
                name: "TurnierGastgeber",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TurnierInstanzId = table.Column<int>(type: "integer", nullable: false),
                    MannschaftId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnierGastgeber", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TurnierGastgeber_Mannschaften_MannschaftId",
                        column: x => x.MannschaftId,
                        principalTable: "Mannschaften",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TurnierGastgeber_TurnierInstanzen_TurnierInstanzId",
                        column: x => x.TurnierInstanzId,
                        principalTable: "TurnierInstanzen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TurnierRunden",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TurnierInstanzId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reihenfolge = table.Column<int>(type: "integer", nullable: false),
                    Rundenart = table.Column<int>(type: "integer", nullable: false),
                    EingangsMannschaftsanzahl = table.Column<int>(type: "integer", nullable: false),
                    AusgangsMannschaftsanzahl = table.Column<int>(type: "integer", nullable: false),
                    GruppenAnzahl = table.Column<int>(type: "integer", nullable: true),
                    RegelwerkId = table.Column<int>(type: "integer", nullable: true),
                    MitRueckspiel = table.Column<bool>(type: "boolean", nullable: false),
                    AuswaertstorRegelAktiv = table.Column<bool>(type: "boolean", nullable: false),
                    NeutralerSpielort = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Ueberraschungsfaktor = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    KonfigurationJson = table.Column<string>(type: "text", nullable: true),
                    EingangUeberUebergangsregeln = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PaarungFestVorgegeben = table.Column<bool>(type: "boolean", nullable: false),
                    VorgegebeneAuslosungsart = table.Column<int>(type: "integer", nullable: true),
                    VorgegebeneAuslosungsregeln = table.Column<int[]>(type: "integer[]", nullable: false),
                    VorgegebeneGruppenposition = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    GastgeberGruppen = table.Column<List<int>>(type: "integer[]", nullable: false),
                    TitelverteidigerGruppe = table.Column<int>(type: "integer", nullable: true),
                    StandardRanglisteId = table.Column<int>(type: "integer", nullable: true),
                    TitelverteidigerInTopf1 = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    GegnerJeTopf = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    KombinationstabelleJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnierRunden", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TurnierRunden_Ranglisten_StandardRanglisteId",
                        column: x => x.StandardRanglisteId,
                        principalTable: "Ranglisten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                name: "VerbandsQuoten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TurnierInstanzId = table.Column<int>(type: "integer", nullable: false),
                    VerbandId = table.Column<int>(type: "integer", nullable: false),
                    Anzahl = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerbandsQuoten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VerbandsQuoten_TurnierInstanzen_TurnierInstanzId",
                        column: x => x.TurnierInstanzId,
                        principalTable: "TurnierInstanzen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VerbandsQuoten_Verbaende_VerbandId",
                        column: x => x.VerbandId,
                        principalTable: "Verbaende",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Spiele",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TurnierInstanzId = table.Column<int>(type: "integer", nullable: false),
                    TurnierRundeId = table.Column<int>(type: "integer", nullable: false),
                    Spielnummer = table.Column<int>(type: "integer", nullable: false),
                    Spieltag = table.Column<int>(type: "integer", nullable: true),
                    GruppenNummer = table.Column<int>(type: "integer", nullable: true),
                    Datum = table.Column<DateOnly>(type: "date", nullable: true),
                    HeimMannschaftId = table.Column<int>(type: "integer", nullable: true),
                    HeimPlatzhalterName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AuswaertsMannschaftId = table.Column<int>(type: "integer", nullable: true),
                    AuswaertsPlatzhalterName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IstRueckspiel = table.Column<bool>(type: "boolean", nullable: false),
                    HinspielId = table.Column<int>(type: "integer", nullable: true),
                    HeimQuellSpielId = table.Column<int>(type: "integer", nullable: true),
                    HeimQuellErgebnis = table.Column<int>(type: "integer", nullable: true),
                    AuswaertsQuellSpielId = table.Column<int>(type: "integer", nullable: true),
                    AuswaertsQuellErgebnis = table.Column<int>(type: "integer", nullable: true)
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
                        name: "FK_Spiele_Spiele_AuswaertsQuellSpielId",
                        column: x => x.AuswaertsQuellSpielId,
                        principalTable: "Spiele",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Spiele_Spiele_HeimQuellSpielId",
                        column: x => x.HeimQuellSpielId,
                        principalTable: "Spiele",
                        principalColumn: "Id");
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
                name: "TabellenBereiche",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TurnierRundeId = table.Column<int>(type: "integer", nullable: false),
                    PlatzVon = table.Column<int>(type: "integer", nullable: false),
                    PlatzBis = table.Column<int>(type: "integer", nullable: false),
                    Art = table.Column<int>(type: "integer", nullable: false),
                    Bezeichnung = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TabellenBereiche", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TabellenBereiche_TurnierRunden_TurnierRundeId",
                        column: x => x.TurnierRundeId,
                        principalTable: "TurnierRunden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Uebergangsregeln",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuellRundeId = table.Column<int>(type: "integer", nullable: false),
                    ZielRundeId = table.Column<int>(type: "integer", nullable: false),
                    GruppeIndex = table.Column<int>(type: "integer", nullable: true),
                    PlatzierungVon = table.Column<int>(type: "integer", nullable: false),
                    PlatzierungBis = table.Column<int>(type: "integer", nullable: false),
                    AnzahlBeste = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Uebergangsregeln", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Uebergangsregeln_TurnierRunden_QuellRundeId",
                        column: x => x.QuellRundeId,
                        principalTable: "TurnierRunden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Uebergangsregeln_TurnierRunden_ZielRundeId",
                        column: x => x.ZielRundeId,
                        principalTable: "TurnierRunden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpielEreignisse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SpielId = table.Column<int>(type: "integer", nullable: false),
                    Minute = table.Column<int>(type: "integer", nullable: false),
                    EreignisArt = table.Column<int>(type: "integer", nullable: false),
                    MannschaftId = table.Column<int>(type: "integer", nullable: true),
                    Beschreibung = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SpielId = table.Column<int>(type: "integer", nullable: false),
                    HeimToreRegulaer = table.Column<int>(type: "integer", nullable: false),
                    AuswaertsToreRegulaer = table.Column<int>(type: "integer", nullable: false),
                    HeimToreVerlaengerung = table.Column<int>(type: "integer", nullable: true),
                    AuswaertsToreVerlaengerung = table.Column<int>(type: "integer", nullable: true),
                    HeimElfmetertore = table.Column<int>(type: "integer", nullable: true),
                    AuswaertsElfmetertore = table.Column<int>(type: "integer", nullable: true),
                    HalbzeitHeimTore = table.Column<int>(type: "integer", nullable: true),
                    HalbzeitAuswaertsTore = table.Column<int>(type: "integer", nullable: true),
                    SiegerMannschaftId = table.Column<int>(type: "integer", nullable: true),
                    EntscheidungArt = table.Column<int>(type: "integer", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LostopfId = table.Column<int>(type: "integer", nullable: false),
                    MannschaftId = table.Column<int>(type: "integer", nullable: true),
                    PlatzhalterName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UebergangsregelId = table.Column<int>(type: "integer", nullable: true),
                    Platzierung = table.Column<int>(type: "integer", nullable: true)
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
                    table.ForeignKey(
                        name: "FK_LostopfEintraege_Uebergangsregeln_UebergangsregelId",
                        column: x => x.UebergangsregelId,
                        principalTable: "Uebergangsregeln",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TurnierBaeume",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TurnierInstanzId = table.Column<int>(type: "integer", nullable: false),
                    WurzelKnotenId = table.Column<int>(type: "integer", nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TurnierBaumId = table.Column<int>(type: "integer", nullable: false),
                    ElternKnotenId = table.Column<int>(type: "integer", nullable: true),
                    LinkerKindKnotenId = table.Column<int>(type: "integer", nullable: true),
                    RechterKindKnotenId = table.Column<int>(type: "integer", nullable: true),
                    SpielId = table.Column<int>(type: "integer", nullable: true),
                    Runde = table.Column<int>(type: "integer", nullable: false)
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
                name: "IX_Laender_RanglisteId",
                table: "Laender",
                column: "RanglisteId");

            migrationBuilder.CreateIndex(
                name: "IX_Laender_VerbandId",
                table: "Laender",
                column: "VerbandId");

            migrationBuilder.CreateIndex(
                name: "IX_LandesQuoten_LandId",
                table: "LandesQuoten",
                column: "LandId");

            migrationBuilder.CreateIndex(
                name: "IX_LandesQuoten_TurnierInstanzId",
                table: "LandesQuoten",
                column: "TurnierInstanzId");

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
                name: "IX_LostopfEintraege_UebergangsregelId",
                table: "LostopfEintraege",
                column: "UebergangsregelId");

            migrationBuilder.CreateIndex(
                name: "IX_Mannschaften_LandId",
                table: "Mannschaften",
                column: "LandId");

            migrationBuilder.CreateIndex(
                name: "IX_PunkteRegeln_RegelwerkId",
                table: "PunkteRegeln",
                column: "RegelwerkId");

            migrationBuilder.CreateIndex(
                name: "IX_Ranglisten_PokalsiegerId",
                table: "Ranglisten",
                column: "PokalsiegerId");

            migrationBuilder.CreateIndex(
                name: "IX_RanglistenAbschnitte_RanglisteId_BisPlatz",
                table: "RanglistenAbschnitte",
                columns: new[] { "RanglisteId", "BisPlatz" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RanglistenEintraege_MannschaftId",
                table: "RanglistenEintraege",
                column: "MannschaftId");

            migrationBuilder.CreateIndex(
                name: "IX_RanglistenEintraege_RanglisteId_MannschaftId",
                table: "RanglistenEintraege",
                columns: new[] { "RanglisteId", "MannschaftId" },
                unique: true);

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
                name: "IX_Spiele_AuswaertsQuellSpielId",
                table: "Spiele",
                column: "AuswaertsQuellSpielId");

            migrationBuilder.CreateIndex(
                name: "IX_Spiele_HeimMannschaftId",
                table: "Spiele",
                column: "HeimMannschaftId");

            migrationBuilder.CreateIndex(
                name: "IX_Spiele_HeimQuellSpielId",
                table: "Spiele",
                column: "HeimQuellSpielId");

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
                name: "IX_TabellenBereiche_TurnierRundeId",
                table: "TabellenBereiche",
                column: "TurnierRundeId");

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
                name: "IX_TurnierGastgeber_MannschaftId",
                table: "TurnierGastgeber",
                column: "MannschaftId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnierGastgeber_TurnierInstanzId_MannschaftId",
                table: "TurnierGastgeber",
                columns: new[] { "TurnierInstanzId", "MannschaftId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TurnierInstanzen_LandId",
                table: "TurnierInstanzen",
                column: "LandId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnierInstanzen_StandardRegelwerkId",
                table: "TurnierInstanzen",
                column: "StandardRegelwerkId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnierInstanzen_TitelverteidigerId",
                table: "TurnierInstanzen",
                column: "TitelverteidigerId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnierInstanzen_VerbandId",
                table: "TurnierInstanzen",
                column: "VerbandId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnierRunden_RegelwerkId",
                table: "TurnierRunden",
                column: "RegelwerkId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnierRunden_StandardRanglisteId",
                table: "TurnierRunden",
                column: "StandardRanglisteId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnierRunden_TurnierInstanzId_Reihenfolge",
                table: "TurnierRunden",
                columns: new[] { "TurnierInstanzId", "Reihenfolge" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Uebergangsregeln_QuellRundeId",
                table: "Uebergangsregeln",
                column: "QuellRundeId");

            migrationBuilder.CreateIndex(
                name: "IX_Uebergangsregeln_ZielRundeId",
                table: "Uebergangsregeln",
                column: "ZielRundeId");

            migrationBuilder.CreateIndex(
                name: "IX_Verbaende_Kurzname",
                table: "Verbaende",
                column: "Kurzname",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Verbaende_RanglisteId",
                table: "Verbaende",
                column: "RanglisteId");

            migrationBuilder.CreateIndex(
                name: "IX_Verbaende_VereinsRanglisteId",
                table: "Verbaende",
                column: "VereinsRanglisteId");

            migrationBuilder.CreateIndex(
                name: "IX_VerbandsQuoten_TurnierInstanzId",
                table: "VerbandsQuoten",
                column: "TurnierInstanzId");

            migrationBuilder.CreateIndex(
                name: "IX_VerbandsQuoten_VerbandId",
                table: "VerbandsQuoten",
                column: "VerbandId");

            migrationBuilder.AddForeignKey(
                name: "FK_Auslosungen_TurnierInstanzen_TurnierInstanzId",
                table: "Auslosungen",
                column: "TurnierInstanzId",
                principalTable: "TurnierInstanzen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Auslosungen_TurnierRunden_TurnierRundeId",
                table: "Auslosungen",
                column: "TurnierRundeId",
                principalTable: "TurnierRunden",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AuslosungsErgebnisse_Mannschaften_AuswaertsMannschaftId",
                table: "AuslosungsErgebnisse",
                column: "AuswaertsMannschaftId",
                principalTable: "Mannschaften",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AuslosungsErgebnisse_Mannschaften_HeimMannschaftId",
                table: "AuslosungsErgebnisse",
                column: "HeimMannschaftId",
                principalTable: "Mannschaften",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AuslosungsErgebnisse_TurnierBaumKnoten_ZugeordneterTurnierB~",
                table: "AuslosungsErgebnisse",
                column: "ZugeordneterTurnierBaumKnotenId",
                principalTable: "TurnierBaumKnoten",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Laender_Ranglisten_RanglisteId",
                table: "Laender",
                column: "RanglisteId",
                principalTable: "Ranglisten",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Laender_Verbaende_VerbandId",
                table: "Laender",
                column: "VerbandId",
                principalTable: "Verbaende",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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
                name: "FK_Ranglisten_Mannschaften_PokalsiegerId",
                table: "Ranglisten");

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
                name: "LandesQuoten");

            migrationBuilder.DropTable(
                name: "LostopfEintraege");

            migrationBuilder.DropTable(
                name: "PunkteRegeln");

            migrationBuilder.DropTable(
                name: "RanglistenAbschnitte");

            migrationBuilder.DropTable(
                name: "RanglistenEintraege");

            migrationBuilder.DropTable(
                name: "RankingRegelSetEintraege");

            migrationBuilder.DropTable(
                name: "SimulationsLaeufe");

            migrationBuilder.DropTable(
                name: "SpielEreignisse");

            migrationBuilder.DropTable(
                name: "SpielErgebnisse");

            migrationBuilder.DropTable(
                name: "TabellenBereiche");

            migrationBuilder.DropTable(
                name: "TurnierGastgeber");

            migrationBuilder.DropTable(
                name: "VerbandsQuoten");

            migrationBuilder.DropTable(
                name: "Lostoepfe");

            migrationBuilder.DropTable(
                name: "Uebergangsregeln");

            migrationBuilder.DropTable(
                name: "RankingRegeln");

            migrationBuilder.DropTable(
                name: "Auslosungen");

            migrationBuilder.DropTable(
                name: "TurnierInstanzen");

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
                name: "Ranglisten");

            migrationBuilder.DropTable(
                name: "TurnierBaumKnoten");

            migrationBuilder.DropTable(
                name: "Spiele");

            migrationBuilder.DropTable(
                name: "TurnierBaeume");
        }
    }
}
