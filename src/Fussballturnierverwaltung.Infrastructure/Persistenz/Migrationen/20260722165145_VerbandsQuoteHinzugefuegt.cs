using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class VerbandsQuoteHinzugefuegt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VerbandsQuoten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TurnierInstanzId = table.Column<int>(type: "int", nullable: false),
                    VerbandId = table.Column<int>(type: "int", nullable: false),
                    MaxAnzahl = table.Column<int>(type: "int", nullable: false),
                    MinAnzahl = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerbandsQuoten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VerbandsQuoten_TurnierInstanzen_TurnierInstanzId",
                        column: x => x.TurnierInstanzId,
                        principalTable: "TurnierInstanzen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VerbandsQuoten_Verbaende_VerbandId",
                        column: x => x.VerbandId,
                        principalTable: "Verbaende",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VerbandsQuoten_TurnierInstanzId",
                table: "VerbandsQuoten",
                column: "TurnierInstanzId");

            migrationBuilder.CreateIndex(
                name: "IX_VerbandsQuoten_VerbandId",
                table: "VerbandsQuoten",
                column: "VerbandId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VerbandsQuoten");
        }
    }
}
