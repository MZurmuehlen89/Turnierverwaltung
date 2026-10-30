using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <summary>#574: Vereinsrangliste am Verband. Bestehende Datenbanken: die UEFA verweist einmalig auf den neuesten
    /// importierten Klub-Koeffizienten. Neue Datenbanken haben ihn noch nicht; dort weist ihn der Nutzer zu, wie die
    /// Verbandsrangliste in KontingenteVerweiseSetzen (#572).</summary>
    public partial class VereinsranglisteAmVerband : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VereinsRanglisteId",
                table: "Verbaende",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Verbaende_VereinsRanglisteId",
                table: "Verbaende",
                column: "VereinsRanglisteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Verbaende_Ranglisten_VereinsRanglisteId",
                table: "Verbaende",
                column: "VereinsRanglisteId",
                principalTable: "Ranglisten",
                principalColumn: "Id");

            migrationBuilder.Sql("""
                UPDATE Verbaende SET VereinsRanglisteId =
                    (SELECT TOP 1 r.Id FROM Ranglisten r
                     JOIN RanglistenQuellen q ON q.Id = r.RanglistenQuelleId
                     WHERE q.Name = N'UEFA-Klub-Koeffizient (Kassies)'
                     ORDER BY r.Datum DESC, r.Id DESC)
                WHERE Kurzname = 'UEFA' AND VereinsRanglisteId IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Verbaende_Ranglisten_VereinsRanglisteId",
                table: "Verbaende");

            migrationBuilder.DropIndex(
                name: "IX_Verbaende_VereinsRanglisteId",
                table: "Verbaende");

            migrationBuilder.DropColumn(
                name: "VereinsRanglisteId",
                table: "Verbaende");
        }
    }
}
