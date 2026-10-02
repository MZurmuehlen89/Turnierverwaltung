using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class NeutralerSpielortUndGastgeber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NeutralerSpielort",
                table: "TurnierRunden",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "TurnierGastgeber",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TurnierInstanzId = table.Column<int>(type: "int", nullable: false),
                    MannschaftId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnierGastgeber", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TurnierGastgeber_Mannschaften_MannschaftId",
                        column: x => x.MannschaftId,
                        principalTable: "Mannschaften",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TurnierGastgeber_TurnierInstanzen_TurnierInstanzId",
                        column: x => x.TurnierInstanzId,
                        principalTable: "TurnierInstanzen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TurnierGastgeber_MannschaftId",
                table: "TurnierGastgeber",
                column: "MannschaftId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnierGastgeber_TurnierInstanzId_MannschaftId",
                table: "TurnierGastgeber",
                columns: new[] { "TurnierInstanzId", "MannschaftId" },
                unique: true);

            // Issue #352: bestehende Seed-Vorlagen (Status 5 = Vorlage) einmalig vorbelegen — gleiche Regel wie
            // TurnierVorlagenSeedDaten.IstNeutralerSpielort. Hier statt im Seed-Nachrüsten, weil ein bool-Feld dort
            // nicht von einer späteren bewussten Abwahl in der UI zu unterscheiden wäre.
            // Rundenart: 7 = Finale, 8 = SpielUmPlatzDrei.
            migrationBuilder.Sql("""
                UPDATE r SET r.NeutralerSpielort = 1
                FROM TurnierRunden r
                JOIN TurnierInstanzen i ON i.Id = r.TurnierInstanzId
                WHERE i.Status = 5 AND (
                    i.Name IN (N'FIFA WM', N'FIFA Klub-WM', N'UEFA EM', N'Copa América', N'Afrika-Cup',
                               N'AFC Asian Cup', N'CONCACAF Gold Cup')
                    OR (i.Name = N'UEFA Nations League Liga A' AND (r.Rundenart IN (7, 8) OR r.Name = N'Halbfinale'))
                    OR (i.Name = N'FA Cup' AND (r.Rundenart = 7 OR r.Name = N'Halbfinale'))
                    OR (i.Name IN (N'UEFA Champions League', N'UEFA Europa League', N'UEFA Conference League',
                                   N'DFB-Pokal') AND r.Rundenart = 7));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TurnierGastgeber");

            migrationBuilder.DropColumn(
                name: "NeutralerSpielort",
                table: "TurnierRunden");
        }
    }
}
