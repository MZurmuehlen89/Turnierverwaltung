using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class VorlagenRundenHinzugefuegt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VorlagenRunden",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TurnierVorlageId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reihenfolge = table.Column<int>(type: "int", nullable: false),
                    Rundenart = table.Column<int>(type: "int", nullable: false),
                    EingangsMannschaftsanzahl = table.Column<int>(type: "int", nullable: false),
                    AusgangsMannschaftsanzahl = table.Column<int>(type: "int", nullable: false),
                    AuswaertstorRegelAktiv = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VorlagenRunden", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VorlagenRunden_TurnierVorlagen_TurnierVorlageId",
                        column: x => x.TurnierVorlageId,
                        principalTable: "TurnierVorlagen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VorlagenRunden_TurnierVorlageId_Reihenfolge",
                table: "VorlagenRunden",
                columns: new[] { "TurnierVorlageId", "Reihenfolge" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VorlagenRunden");
        }
    }
}
