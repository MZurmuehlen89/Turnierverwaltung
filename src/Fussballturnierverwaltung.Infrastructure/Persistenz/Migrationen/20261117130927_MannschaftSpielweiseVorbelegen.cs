using Fussballturnierverwaltung.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <summary>#667: Spielweise der vorhandenen Mannschaften einmalig vorbelegen – Rating-Vorschlag, dann Handliste
    /// (<see cref="SpielweiseNachruestung"/>). Kein Down: die Spalten selbst entfernt die vorige Migration.</summary>
    public partial class MannschaftSpielweiseVorbelegen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SpielweiseNachruestung.Sql());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nichts zurückzunehmen: die vorigen Werte waren der Standard (ausgeglichen/normal) und gehen mit der
            // vorigen Migration samt Spalten.
        }
    }
}
