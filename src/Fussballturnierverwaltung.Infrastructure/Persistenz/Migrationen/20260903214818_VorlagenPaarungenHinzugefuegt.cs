using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class VorlagenPaarungenHinzugefuegt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PaarungFestVorgegeben",
                table: "VorlagenRunden",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "VorlagenPaarungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VorlagenRundeId = table.Column<int>(type: "int", nullable: false),
                    PaarungsNummer = table.Column<int>(type: "int", nullable: false),
                    SlotNr = table.Column<int>(type: "int", nullable: false),
                    PlatzhalterBezeichnung = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VorlagenPaarungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VorlagenPaarungen_VorlagenRunden_VorlagenRundeId",
                        column: x => x.VorlagenRundeId,
                        principalTable: "VorlagenRunden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VorlagenPaarungen_VorlagenRundeId_PaarungsNummer_SlotNr",
                table: "VorlagenPaarungen",
                columns: new[] { "VorlagenRundeId", "PaarungsNummer", "SlotNr" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VorlagenPaarungen");

            migrationBuilder.DropColumn(
                name: "PaarungFestVorgegeben",
                table: "VorlagenRunden");
        }
    }
}
