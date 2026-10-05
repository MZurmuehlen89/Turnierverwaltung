using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class FairplaySortierrichtungKorrigieren : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue #353: der Fairplay-Wert ist negativ (-1 je Gelbe, -3 je Rote Karte), der Seed legte die Regel
            // FairplayWertung (9) aber mit Aufsteigend (1) an - damit stand das Team mit mehr Karten besser.
            // Auf Absteigend (0) korrigieren.
            migrationBuilder.Sql("UPDATE RankingRegeln SET Sortierrichtung = 0 WHERE Regelart = 9 AND Sortierrichtung = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Der alte Wert war fachlich falsch - kein Zurückstellen.
        }
    }
}
