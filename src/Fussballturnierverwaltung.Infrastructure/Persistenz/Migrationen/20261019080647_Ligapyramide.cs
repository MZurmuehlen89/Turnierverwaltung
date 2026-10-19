using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class Ligapyramide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProfigrenzeSpielklasse",
                table: "Ranglisten",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RanglistenAbschnitte",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RanglisteId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Spielklasse = table.Column<int>(type: "int", nullable: false),
                    BisPlatz = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RanglistenAbschnitte", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RanglistenAbschnitte_Ranglisten_RanglisteId",
                        column: x => x.RanglisteId,
                        principalTable: "Ranglisten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RanglistenAbschnitte_RanglisteId_BisPlatz",
                table: "RanglistenAbschnitte",
                columns: new[] { "RanglisteId", "BisPlatz" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RanglistenAbschnitte");

            migrationBuilder.DropColumn(
                name: "ProfigrenzeSpielklasse",
                table: "Ranglisten");
        }
    }
}
