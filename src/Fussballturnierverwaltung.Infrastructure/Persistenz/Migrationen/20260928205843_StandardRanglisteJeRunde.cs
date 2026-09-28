using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class StandardRanglisteJeRunde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StandardRanglistenQuelleId",
                table: "TurnierRunden",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TitelverteidigerInTopf1",
                table: "TurnierRunden",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TitelverteidigerId",
                table: "TurnierInstanzen",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RanglistenQuelleId",
                table: "Ranglisten",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TurnierRunden_StandardRanglistenQuelleId",
                table: "TurnierRunden",
                column: "StandardRanglistenQuelleId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnierInstanzen_TitelverteidigerId",
                table: "TurnierInstanzen",
                column: "TitelverteidigerId");

            migrationBuilder.CreateIndex(
                name: "IX_Ranglisten_RanglistenQuelleId",
                table: "Ranglisten",
                column: "RanglistenQuelleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ranglisten_RanglistenQuellen_RanglistenQuelleId",
                table: "Ranglisten",
                column: "RanglistenQuelleId",
                principalTable: "RanglistenQuellen",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TurnierInstanzen_Mannschaften_TitelverteidigerId",
                table: "TurnierInstanzen",
                column: "TitelverteidigerId",
                principalTable: "Mannschaften",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TurnierRunden_RanglistenQuellen_StandardRanglistenQuelleId",
                table: "TurnierRunden",
                column: "StandardRanglistenQuelleId",
                principalTable: "RanglistenQuellen",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Issue #349: bestehende Datenbanken einmalig nachrüsten — Quelle "UEFA-Klub-Koeffizient (Kassies)"
            // mit denselben Werten wie RanglistenQuellenSeedDaten anlegen (der Seed erkennt sie danach am Namen und
            // legt sie nicht doppelt an) und in der Ligaphase der drei UEFA-Vorlagen ab 2024/25 als Standard-
            // Rangliste eintragen; Titelverteidiger in Topf 1 nur in der Champions League. Bewusst hier statt im
            // SeedDatenService, damit eine später bewusst entfernte Vorgabe nicht bei jedem Start zurückkehrt
            // (gleiches Vorgehen wie AuslosungsartVorgabeJeRunde, #347). Status 5 = Vorlage.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM RanglistenQuellen WHERE Name = N'UEFA-Klub-Koeffizient (Kassies)')
    INSERT INTO RanglistenQuellen (Name, Url, TabellenIndex, TabellenSelektor, MannschaftSpalte, PunkteSpalte, KopfzeileUeberspringen)
    VALUES (N'UEFA-Klub-Koeffizient (Kassies)', N'https://kassiesa.net/uefa/data/method5/trank2026.html', 0, NULL, 2, 9, 1);

UPDATE r SET
    r.StandardRanglistenQuelleId = (SELECT MIN(q.Id) FROM RanglistenQuellen q WHERE q.Name = N'UEFA-Klub-Koeffizient (Kassies)'),
    r.TitelverteidigerInTopf1 = CASE WHEN i.Name = N'UEFA Champions League (36 Teams, ab 2024/25)' THEN 1 ELSE 0 END
FROM TurnierRunden r
JOIN TurnierInstanzen i ON i.Id = r.TurnierInstanzId
WHERE i.Status = 5
  AND r.Reihenfolge = 1 AND r.Name = N'Ligaphase'
  AND i.Name IN (N'UEFA Champions League (36 Teams, ab 2024/25)',
                 N'UEFA Europa League (36 Teams, ab 2024/25)',
                 N'UEFA Conference League (36 Teams, ab 2024/25)')
  AND r.StandardRanglistenQuelleId IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ranglisten_RanglistenQuellen_RanglistenQuelleId",
                table: "Ranglisten");

            migrationBuilder.DropForeignKey(
                name: "FK_TurnierInstanzen_Mannschaften_TitelverteidigerId",
                table: "TurnierInstanzen");

            migrationBuilder.DropForeignKey(
                name: "FK_TurnierRunden_RanglistenQuellen_StandardRanglistenQuelleId",
                table: "TurnierRunden");

            migrationBuilder.DropIndex(
                name: "IX_TurnierRunden_StandardRanglistenQuelleId",
                table: "TurnierRunden");

            migrationBuilder.DropIndex(
                name: "IX_TurnierInstanzen_TitelverteidigerId",
                table: "TurnierInstanzen");

            migrationBuilder.DropIndex(
                name: "IX_Ranglisten_RanglistenQuelleId",
                table: "Ranglisten");

            migrationBuilder.DropColumn(
                name: "StandardRanglistenQuelleId",
                table: "TurnierRunden");

            migrationBuilder.DropColumn(
                name: "TitelverteidigerInTopf1",
                table: "TurnierRunden");

            migrationBuilder.DropColumn(
                name: "TitelverteidigerId",
                table: "TurnierInstanzen");

            migrationBuilder.DropColumn(
                name: "RanglistenQuelleId",
                table: "Ranglisten");
        }
    }
}
