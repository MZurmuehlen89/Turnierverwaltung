using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Gruppenposition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VorgegebeneGruppenposition",
                table: "TurnierRunden",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Gruppenposition",
                table: "Auslosungen",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Issue #475: bestehende Vorlagen einmalig nachrüsten (Zuordnung wie
            // TurnierVorlagenSeedDaten.GruppenpositionJeVorlage). Status 5 = Vorlage, Rundenart 1 = Gruppenphase;
            // 1 = Topf1Fest, 2 = AlleGelost. Turniere und vorhandene Auslosungen behalten 0 = Nach Topf, das
            // bisherige Verhalten. Hier statt im SeedDatenService, damit eine später bewusst geänderte Vorgabe
            // nicht bei jedem Start überschrieben wird.
            migrationBuilder.Sql(@"
UPDATE r SET r.VorgegebeneGruppenposition = v.Position
FROM TurnierRunden r
JOIN TurnierInstanzen i ON i.Id = r.TurnierInstanzId
JOIN (VALUES
    (N'FIFA WM',                    N'48 Teams · ab 2026',                           1),
    (N'FIFA WM',                    N'32 Teams · 1998–2022',                         1),
    (N'UEFA EM',                    N'16 Teams · 1996–2012',                         1),
    (N'Copa América',               N'12 Teams · 1993–2019',                         1),
    (N'CONCACAF Gold Cup',          N'16 Teams · ab 2021',                           1),
    (N'FIFA Klub-WM',               N'32 Teams · ab 2025',                           1),
    (N'UEFA EM',                    N'24 Teams · 2016+',                             2),
    (N'UEFA Champions League',      N'32 Teams · mit Auswärtstorregel · 2003–2021',  2),
    (N'UEFA Champions League',      N'32 Teams · ohne Auswärtstorregel · 2021–2024', 2),
    (N'UEFA Nations League Liga A', N'16 Teams · ab 2024/25',                        2)
) AS v(Name, Variante, Position) ON v.Name = i.Name AND v.Variante = i.Variante
WHERE i.Status = 5 AND r.Rundenart = 1 AND r.Reihenfolge = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VorgegebeneGruppenposition",
                table: "TurnierRunden");

            migrationBuilder.DropColumn(
                name: "Gruppenposition",
                table: "Auslosungen");
        }
    }
}
