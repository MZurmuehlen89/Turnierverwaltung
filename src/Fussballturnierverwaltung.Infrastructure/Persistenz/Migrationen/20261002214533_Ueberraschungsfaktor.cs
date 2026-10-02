using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Ueberraschungsfaktor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Ueberraschungsfaktor",
                table: "TurnierRunden",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Issue #360: bestehende Pokal-Vorlagen (Status 5 = Vorlage) einmalig vorbelegen — gleiche Regel wie
            // TurnierVorlagenSeedDaten.UeberraschungsfaktorSetzen (POKAL_UEBERRASCHUNGSFAKTOR). Hier statt im
            // Seed-Nachrüsten, damit eine spätere bewusste Änderung in der UI nicht überschrieben wird (wie #352).
            migrationBuilder.Sql("""
                UPDATE r SET r.Ueberraschungsfaktor = 20
                FROM TurnierRunden r
                JOIN TurnierInstanzen i ON i.Id = r.TurnierInstanzId
                WHERE i.Status = 5 AND i.Name IN (N'DFB-Pokal', N'FA Cup');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ueberraschungsfaktor",
                table: "TurnierRunden");
        }
    }
}
