using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class QualifikationsRegelHinzugefuegt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QualifikationsRegeln",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuellRundeId = table.Column<int>(type: "int", nullable: false),
                    ZielRundeId = table.Column<int>(type: "int", nullable: false),
                    GruppeIndex = table.Column<int>(type: "int", nullable: true),
                    PlatzierungVon = table.Column<int>(type: "int", nullable: false),
                    PlatzierungBis = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualifikationsRegeln", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualifikationsRegeln_TurnierRunden_QuellRundeId",
                        column: x => x.QuellRundeId,
                        principalTable: "TurnierRunden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QualifikationsRegeln_TurnierRunden_ZielRundeId",
                        column: x => x.ZielRundeId,
                        principalTable: "TurnierRunden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QualifikationsRegeln_QuellRundeId",
                table: "QualifikationsRegeln",
                column: "QuellRundeId");

            migrationBuilder.CreateIndex(
                name: "IX_QualifikationsRegeln_ZielRundeId",
                table: "QualifikationsRegeln",
                column: "ZielRundeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QualifikationsRegeln");
        }
    }
}
