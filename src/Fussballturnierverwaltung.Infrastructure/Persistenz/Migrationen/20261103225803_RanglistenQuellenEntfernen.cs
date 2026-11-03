using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class RanglistenQuellenEntfernen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ranglisten_RanglistenQuellen_RanglistenQuelleId",
                table: "Ranglisten");

            migrationBuilder.DropTable(
                name: "RanglistenQuellen");

            migrationBuilder.DropIndex(
                name: "IX_Ranglisten_RanglistenQuelleId",
                table: "Ranglisten");

            migrationBuilder.DropColumn(
                name: "RanglistenQuelleId",
                table: "Ranglisten");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RanglistenQuelleId",
                table: "Ranglisten",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RanglistenQuellen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KopfzeileUeberspringen = table.Column<bool>(type: "bit", nullable: false),
                    MannschaftSpalte = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PunkteSpalte = table.Column<int>(type: "int", nullable: true),
                    TabellenIndex = table.Column<int>(type: "int", nullable: false),
                    TabellenSelektor = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RanglistenQuellen", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ranglisten_RanglistenQuelleId",
                table: "Ranglisten",
                column: "RanglistenQuelleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ranglisten_RanglistenQuellen_RanglistenQuelleId",
                table: "Ranglisten",
                column: "RanglistenQuelleId",
                principalTable: "RanglistenQuellen",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
