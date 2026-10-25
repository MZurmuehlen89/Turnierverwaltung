using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class AuslosungsartGruppenphaseNachToepfen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue #471: bestehende Vorlagen einmalig nachrüsten (Regel wie TurnierVorlagenSeedDaten.AuslosungsartSetzen):
            // Gruppenphase in Runde 1 außer dem Mini-Turnier → „Nach Töpfen“. Status 5 = Vorlage, Rundenart 1 =
            // Gruppenphase, Auslosungsart 2 = NachToepfen. Nur wo noch nichts vorgegeben ist; Turniere bleiben
            // unverändert. Hier statt im SeedDatenService, damit eine später bewusst entfernte Vorgabe nicht bei
            // jedem Start wieder gesetzt wird.
            migrationBuilder.Sql(@"
UPDATE r SET r.VorgegebeneAuslosungsart = 2
FROM TurnierRunden r
JOIN TurnierInstanzen i ON i.Id = r.TurnierInstanzId
WHERE i.Status = 5 AND i.Name <> N'Mini-Turnier'
  AND r.Rundenart = 1 AND r.Reihenfolge = 1
  AND r.VorgegebeneAuslosungsart IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Bewusst leer: ob eine Vorgabe aus dieser Migration stammt oder später von Hand gesetzt wurde, lässt
            // sich nicht mehr unterscheiden.
        }
    }
}
