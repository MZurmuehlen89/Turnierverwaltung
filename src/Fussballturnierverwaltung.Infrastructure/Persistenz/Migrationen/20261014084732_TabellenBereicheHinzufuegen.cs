using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class TabellenBereicheHinzufuegen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TabellenBereiche",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TurnierRundeId = table.Column<int>(type: "int", nullable: false),
                    PlatzVon = table.Column<int>(type: "int", nullable: false),
                    PlatzBis = table.Column<int>(type: "int", nullable: false),
                    Art = table.Column<int>(type: "int", nullable: false),
                    Bezeichnung = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TabellenBereiche", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TabellenBereiche_TurnierRunden_TurnierRundeId",
                        column: x => x.TurnierRundeId,
                        principalTable: "TurnierRunden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TabellenBereiche_TurnierRundeId",
                table: "TabellenBereiche",
                column: "TurnierRundeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TabellenBereiche");
        }
    }
}
