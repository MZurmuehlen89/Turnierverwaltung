using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class LandesQuoteHinzugefuegt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LandesQuoten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TurnierInstanzId = table.Column<int>(type: "int", nullable: false),
                    LandId = table.Column<int>(type: "int", nullable: false),
                    Anzahl = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandesQuoten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandesQuoten_Laender_LandId",
                        column: x => x.LandId,
                        principalTable: "Laender",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandesQuoten_TurnierInstanzen_TurnierInstanzId",
                        column: x => x.TurnierInstanzId,
                        principalTable: "TurnierInstanzen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LandesQuoten_LandId",
                table: "LandesQuoten",
                column: "LandId");

            migrationBuilder.CreateIndex(
                name: "IX_LandesQuoten_TurnierInstanzId",
                table: "LandesQuoten",
                column: "TurnierInstanzId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LandesQuoten");
        }
    }
}
