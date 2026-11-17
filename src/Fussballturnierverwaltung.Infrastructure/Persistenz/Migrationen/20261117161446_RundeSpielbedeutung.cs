using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <summary>#668: Spielbedeutung je Runde. Bestehende Runden (Turniere wie Vorlagen) werden nach der Rundenart
    /// vorbelegt – mit derselben Regel wie Formular und Vorlagen (<see cref="SpielbedeutungVorbelegung"/>).</summary>
    public partial class RundeSpielbedeutung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Spielbedeutung",
                table: "TurnierRunden",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            foreach (var rundenart in Enum.GetValues<Rundenart>())
            {
                var bedeutung = SpielbedeutungVorbelegung.Fuer(rundenart);
                if (bedeutung != Spielbedeutung.Normal)
                    migrationBuilder.Sql(
                        $"""UPDATE "TurnierRunden" SET "Spielbedeutung" = {(int)bedeutung} WHERE "Rundenart" = {(int)rundenart};""");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Spielbedeutung",
                table: "TurnierRunden");
        }
    }
}
