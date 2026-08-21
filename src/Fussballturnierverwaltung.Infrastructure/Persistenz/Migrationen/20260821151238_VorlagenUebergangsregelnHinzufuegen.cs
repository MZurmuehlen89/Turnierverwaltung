using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class VorlagenUebergangsregelnHinzufuegen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VorlagenUebergangsregeln",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuellVorlagenRundeId = table.Column<int>(type: "int", nullable: false),
                    ZielVorlagenRundeId = table.Column<int>(type: "int", nullable: false),
                    GruppeIndex = table.Column<int>(type: "int", nullable: true),
                    PlatzierungVon = table.Column<int>(type: "int", nullable: false),
                    PlatzierungBis = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VorlagenUebergangsregeln", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VorlagenUebergangsregeln_VorlagenRunden_QuellVorlagenRundeId",
                        column: x => x.QuellVorlagenRundeId,
                        principalTable: "VorlagenRunden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VorlagenUebergangsregeln_VorlagenRunden_ZielVorlagenRundeId",
                        column: x => x.ZielVorlagenRundeId,
                        principalTable: "VorlagenRunden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VorlagenUebergangsregeln_QuellVorlagenRundeId",
                table: "VorlagenUebergangsregeln",
                column: "QuellVorlagenRundeId");

            migrationBuilder.CreateIndex(
                name: "IX_VorlagenUebergangsregeln_ZielVorlagenRundeId",
                table: "VorlagenUebergangsregeln",
                column: "ZielVorlagenRundeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VorlagenUebergangsregeln");
        }
    }
}
