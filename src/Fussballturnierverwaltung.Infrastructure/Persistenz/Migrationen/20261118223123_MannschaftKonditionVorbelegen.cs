using Fussballturnierverwaltung.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <summary>#677: Kondition der vorhandenen Mannschaften einmalig vorbelegen – die Stufenliste aus
    /// <see cref="KonditionNachruestung"/>, alle anderen bleiben „normal“. Kein Down: die Spalte selbst entfernt die
    /// vorige Migration.</summary>
    public partial class MannschaftKonditionVorbelegen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(KonditionNachruestung.Sql());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nichts zurückzunehmen: die vorigen Werte waren der Standard (normal) und gehen mit der vorigen
            // Migration samt Spalte.
        }
    }
}
