using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <summary>#572: setzt einmalig die Ranglisten-Verweise bestehender Datenbanken – Deutschland auf seine Ligapyramide,
    /// jeder Verband auf die neueste importierte FIFA-Weltrangliste. Neue Datenbanken haben hier noch keine Ranglisten;
    /// dort setzt die Seed-Nachrüstung der Pyramide den Verweis Deutschlands, den Verbänden weist ihn der Nutzer zu.</summary>
    public partial class KontingenteVerweiseSetzen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE l SET RanglisteId = r.Id
                FROM Laender l
                JOIN Ranglisten r ON r.Name = N'Ligapyramide Deutschland 2025/26'
                WHERE l.FifaCode = 'GER' AND l.RanglisteId IS NULL;

                UPDATE Verbaende SET RanglisteId =
                    (SELECT TOP 1 r.Id FROM Ranglisten r
                     JOIN RanglistenQuellen q ON q.Id = r.RanglistenQuelleId
                     WHERE q.Name = N'FIFA-Weltrangliste (Herren)'
                     ORDER BY r.Datum DESC, r.Id DESC)
                WHERE RanglisteId IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Verweise bleiben; die Spalten entfernt die vorige Migration.
        }
    }
}
