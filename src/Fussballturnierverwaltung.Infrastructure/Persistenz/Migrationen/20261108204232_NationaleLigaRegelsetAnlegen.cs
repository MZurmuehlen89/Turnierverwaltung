using Fussballturnierverwaltung.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <summary>#628: Regelset „Nationale Liga (direkter Vergleich)“ für bestehende Datenbanken, siehe
    /// <see cref="NationaleLigaRegelsetNachruestung"/>.</summary>
    public partial class NationaleLigaRegelsetAnlegen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(NationaleLigaRegelsetNachruestung.Sql());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Regelset und Zuordnung bleiben; sie schaden ohne die Migration nicht.
        }
    }
}
