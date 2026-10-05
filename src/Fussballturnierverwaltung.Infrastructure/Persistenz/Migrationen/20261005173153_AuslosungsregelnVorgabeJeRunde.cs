using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class AuslosungsregelnVorgabeJeRunde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VorgegebeneAuslosungsregeln",
                table: "TurnierRunden",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            // Issue #356: bestehende Datenbanken einmalig mit den Seed-Vorgaben nachrüsten (Zuordnung wie
            // TurnierVorlagenSeedDaten.VorgegebeneAuslosungsregeln). Status 5 = Vorlage; Rundenart 1 = Gruppenphase,
            // 2 = TopfLiga; Regeln als JSON-Array der Enum-Werte: 0 = KeinGleichesLand, 4 = HeimrechtNachLos,
            // 5 = HeimrechtNachSetzliste, 8 = Konfoederationsverteilung; Auslosungsart 4 = GruppensiegerGegenGruppenzweite.
            // Bewusst hier statt im SeedDatenService: dort liefe die Nachrüstung bei jedem Start und würde eine
            // später bewusst abgewählte Vorgabe immer wieder überschreiben.
            migrationBuilder.Sql(@"
UPDATE r SET r.VorgegebeneAuslosungsregeln = N'[8]'
FROM TurnierRunden r JOIN TurnierInstanzen i ON i.Id = r.TurnierInstanzId
WHERE i.Status = 5 AND i.Name = N'FIFA WM' AND i.ZielMannschaftsanzahl >= 32
  AND r.Rundenart = 1 AND r.Reihenfolge = 1;

UPDATE r SET r.VorgegebeneAuslosungsregeln = N'[8,0]'
FROM TurnierRunden r JOIN TurnierInstanzen i ON i.Id = r.TurnierInstanzId
WHERE i.Status = 5 AND i.Name = N'FIFA Klub-WM' AND r.Rundenart = 1 AND r.Reihenfolge = 1;

UPDATE r SET r.VorgegebeneAuslosungsregeln = N'[0]'
FROM TurnierRunden r JOIN TurnierInstanzen i ON i.Id = r.TurnierInstanzId
WHERE i.Status = 5 AND i.Name = N'UEFA Champions League' AND i.ZielMannschaftsanzahl = 32
  AND r.Rundenart = 1 AND r.Reihenfolge = 1;

UPDATE r SET r.VorgegebeneAuslosungsregeln = N'[0,5]',
             r.VorgegebeneAuslosungsart = COALESCE(r.VorgegebeneAuslosungsart, 4)
FROM TurnierRunden r JOIN TurnierInstanzen i ON i.Id = r.TurnierInstanzId
WHERE i.Status = 5 AND i.Name = N'UEFA Champions League' AND i.ZielMannschaftsanzahl = 32
  AND r.Name = N'Achtelfinale';

UPDATE r SET r.VorgegebeneAuslosungsregeln = N'[0]'
FROM TurnierRunden r JOIN TurnierInstanzen i ON i.Id = r.TurnierInstanzId
WHERE i.Status = 5 AND i.ZielMannschaftsanzahl = 36 AND r.Rundenart = 2
  AND i.Name IN (N'UEFA Champions League', N'UEFA Europa League', N'UEFA Conference League');

UPDATE r SET r.VorgegebeneAuslosungsregeln = N'[5]'
FROM TurnierRunden r JOIN TurnierInstanzen i ON i.Id = r.TurnierInstanzId
WHERE i.Status = 5 AND i.ZielMannschaftsanzahl = 36 AND r.Name IN (N'Playoff-Runde', N'Achtelfinale')
  AND i.Name IN (N'UEFA Champions League', N'UEFA Europa League', N'UEFA Conference League');

UPDATE r SET r.VorgegebeneAuslosungsregeln = N'[5]'
FROM TurnierRunden r JOIN TurnierInstanzen i ON i.Id = r.TurnierInstanzId
WHERE i.Status = 5 AND i.Name = N'UEFA Nations League Liga A' AND r.Name = N'Viertelfinale';

UPDATE r SET r.VorgegebeneAuslosungsregeln = N'[4]'
FROM TurnierRunden r JOIN TurnierInstanzen i ON i.Id = r.TurnierInstanzId
WHERE i.Status = 5 AND i.Name IN (N'DFB-Pokal', N'FA Cup') AND r.NeutralerSpielort = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VorgegebeneAuslosungsregeln",
                table: "TurnierRunden");
        }
    }
}
