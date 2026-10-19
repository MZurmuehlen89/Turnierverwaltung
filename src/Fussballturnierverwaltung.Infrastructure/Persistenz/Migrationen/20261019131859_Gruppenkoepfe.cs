using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Gruppenkoepfe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GastgeberGruppen",
                table: "TurnierRunden",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "TitelverteidigerGruppe",
                table: "TurnierRunden",
                type: "int",
                nullable: true);

            // Issue #487: bestehende Vorlagen einmalig nachrüsten (Zuordnung wie
            // TurnierVorlagenSeedDaten.GruppenkoepfeJeVorlage). Status 5 = Vorlage, Rundenart 1 = Gruppenphase;
            // Gruppen ab 1 (A = 1). Turniere bleiben unverändert. Hier statt im SeedDatenService, damit eine später
            // bewusst geänderte Vorgabe nicht bei jedem Start überschrieben wird.
            migrationBuilder.Sql(@"
UPDATE r SET r.GastgeberGruppen = v.Gastgeber, r.TitelverteidigerGruppe = v.Titelverteidiger
FROM TurnierRunden r
JOIN TurnierInstanzen i ON i.Id = r.TurnierInstanzId
JOIN (VALUES
    (N'FIFA WM',           N'48 Teams · ab 2026',   N'[1,2,4]', NULL),
    (N'FIFA WM',           N'32 Teams · 1998–2022', N'[1]',     NULL),
    (N'FIFA WM',           N'24 Teams · 1986–1994', N'[1]',     3),
    (N'FIFA WM',           N'24 Teams · 1982',      N'[5]',     3),
    (N'UEFA EM',           N'24 Teams · 2016+',     N'[1]',     NULL),
    (N'UEFA EM',           N'16 Teams · 1996–2012', N'[1,4]',   NULL),
    (N'UEFA EM',           N'8 Teams · 1984–1992',  N'[1]',     2),
    (N'UEFA EM',           N'8 Teams · 1980',       N'[2]',     1),
    (N'Copa América',      N'16 Teams · 2016/2024', N'[3]',     1),
    (N'Copa América',      N'12 Teams · 1993–2019', N'[1]',     NULL),
    (N'CONCACAF Gold Cup', N'16 Teams · ab 2021',   N'[]',      1),
    (N'Afrika-Cup',        N'24 Teams · ab 2019',   N'[1]',     NULL),
    (N'AFC Asian Cup',     N'24 Teams · ab 2019',   N'[1]',     NULL)
) AS v(Name, Variante, Gastgeber, Titelverteidiger) ON v.Name = i.Name AND v.Variante = i.Variante
WHERE i.Status = 5 AND r.Rundenart = 1 AND r.Reihenfolge = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GastgeberGruppen",
                table: "TurnierRunden");

            migrationBuilder.DropColumn(
                name: "TitelverteidigerGruppe",
                table: "TurnierRunden");
        }
    }
}
