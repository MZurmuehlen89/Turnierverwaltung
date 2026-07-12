using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class RanglisteHinzugefuegt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ranglisten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Datum = table.Column<DateOnly>(type: "date", nullable: false),
                    Quelle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ranglisten", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RanglistenEintraege",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RanglisteId = table.Column<int>(type: "int", nullable: false),
                    MannschaftId = table.Column<int>(type: "int", nullable: false),
                    Rang = table.Column<int>(type: "int", nullable: false),
                    Punkte = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RanglistenEintraege", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RanglistenEintraege_Mannschaften_MannschaftId",
                        column: x => x.MannschaftId,
                        principalTable: "Mannschaften",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RanglistenEintraege_Ranglisten_RanglisteId",
                        column: x => x.RanglisteId,
                        principalTable: "Ranglisten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RanglistenEintraege_MannschaftId",
                table: "RanglistenEintraege",
                column: "MannschaftId");

            migrationBuilder.CreateIndex(
                name: "IX_RanglistenEintraege_RanglisteId_MannschaftId",
                table: "RanglistenEintraege",
                columns: new[] { "RanglisteId", "MannschaftId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RanglistenEintraege");

            migrationBuilder.DropTable(
                name: "Ranglisten");
        }
    }
}
