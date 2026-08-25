using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class RanglistenQuelleHinzugefuegt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RanglistenQuellen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TabellenIndex = table.Column<int>(type: "int", nullable: false),
                    TabellenSelektor = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MannschaftSpalte = table.Column<int>(type: "int", nullable: false),
                    PunkteSpalte = table.Column<int>(type: "int", nullable: true),
                    KopfzeileUeberspringen = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RanglistenQuellen", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RanglistenQuellen");
        }
    }
}
