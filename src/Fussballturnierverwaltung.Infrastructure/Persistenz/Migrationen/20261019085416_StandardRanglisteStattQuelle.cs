using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class StandardRanglisteStattQuelle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TurnierRunden_RanglistenQuellen_StandardRanglistenQuelleId",
                table: "TurnierRunden");

            migrationBuilder.RenameColumn(
                name: "StandardRanglistenQuelleId",
                table: "TurnierRunden",
                newName: "StandardRanglisteId");

            migrationBuilder.RenameIndex(
                name: "IX_TurnierRunden_StandardRanglistenQuelleId",
                table: "TurnierRunden",
                newName: "IX_TurnierRunden_StandardRanglisteId");

            // Issue #393: Die Runde verwies auf eine Ranglisten-Quelle und nahm deren neueste Rangliste — jetzt verweist
            // sie direkt auf genau diese Rangliste. Ohne Rangliste aus der Quelle entfällt der Verweis (er bewirkte nichts).
            migrationBuilder.Sql(@"
UPDATE r SET StandardRanglisteId =
    (SELECT TOP 1 g.Id FROM Ranglisten g
     WHERE g.RanglistenQuelleId = r.StandardRanglisteId
     ORDER BY g.Datum DESC, g.Id DESC)
FROM TurnierRunden r
WHERE r.StandardRanglisteId IS NOT NULL;");

            migrationBuilder.AddForeignKey(
                name: "FK_TurnierRunden_Ranglisten_StandardRanglisteId",
                table: "TurnierRunden",
                column: "StandardRanglisteId",
                principalTable: "Ranglisten",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TurnierRunden_Ranglisten_StandardRanglisteId",
                table: "TurnierRunden");

            migrationBuilder.RenameColumn(
                name: "StandardRanglisteId",
                table: "TurnierRunden",
                newName: "StandardRanglistenQuelleId");

            migrationBuilder.RenameIndex(
                name: "IX_TurnierRunden_StandardRanglisteId",
                table: "TurnierRunden",
                newName: "IX_TurnierRunden_StandardRanglistenQuelleId");

            // Rückweg: die Quelle der Rangliste; von Hand gepflegte Ranglisten ohne Quelle verlieren den Verweis.
            migrationBuilder.Sql(@"
UPDATE r SET StandardRanglistenQuelleId =
    (SELECT g.RanglistenQuelleId FROM Ranglisten g WHERE g.Id = r.StandardRanglistenQuelleId)
FROM TurnierRunden r
WHERE r.StandardRanglistenQuelleId IS NOT NULL;");

            migrationBuilder.AddForeignKey(
                name: "FK_TurnierRunden_RanglistenQuellen_StandardRanglistenQuelleId",
                table: "TurnierRunden",
                column: "StandardRanglistenQuelleId",
                principalTable: "RanglistenQuellen",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
