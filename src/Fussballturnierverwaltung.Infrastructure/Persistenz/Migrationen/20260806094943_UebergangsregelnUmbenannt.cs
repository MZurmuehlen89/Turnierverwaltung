using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fussballturnierverwaltung.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class UebergangsregelnUmbenannt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "QualifikationsRegeln",
                newName: "Uebergangsregeln");

            migrationBuilder.RenameColumn(
                name: "QualifikationsRegelId",
                table: "LostopfEintraege",
                newName: "UebergangsregelId");

            migrationBuilder.RenameIndex(
                name: "IX_LostopfEintraege_QualifikationsRegelId",
                table: "LostopfEintraege",
                newName: "IX_LostopfEintraege_UebergangsregelId");

            migrationBuilder.RenameIndex(
                name: "IX_QualifikationsRegeln_QuellRundeId",
                table: "Uebergangsregeln",
                newName: "IX_Uebergangsregeln_QuellRundeId");

            migrationBuilder.RenameIndex(
                name: "IX_QualifikationsRegeln_ZielRundeId",
                table: "Uebergangsregeln",
                newName: "IX_Uebergangsregeln_ZielRundeId");

            migrationBuilder.Sql("EXEC sp_rename N'PK_QualifikationsRegeln', N'PK_Uebergangsregeln', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'FK_QualifikationsRegeln_TurnierRunden_QuellRundeId', N'FK_Uebergangsregeln_TurnierRunden_QuellRundeId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'FK_QualifikationsRegeln_TurnierRunden_ZielRundeId', N'FK_Uebergangsregeln_TurnierRunden_ZielRundeId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'FK_LostopfEintraege_QualifikationsRegeln_QualifikationsRegelId', N'FK_LostopfEintraege_Uebergangsregeln_UebergangsregelId', N'OBJECT';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("EXEC sp_rename N'FK_LostopfEintraege_Uebergangsregeln_UebergangsregelId', N'FK_LostopfEintraege_QualifikationsRegeln_QualifikationsRegelId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'FK_Uebergangsregeln_TurnierRunden_ZielRundeId', N'FK_QualifikationsRegeln_TurnierRunden_ZielRundeId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'FK_Uebergangsregeln_TurnierRunden_QuellRundeId', N'FK_QualifikationsRegeln_TurnierRunden_QuellRundeId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'PK_Uebergangsregeln', N'PK_QualifikationsRegeln', N'OBJECT';");

            migrationBuilder.RenameIndex(
                name: "IX_Uebergangsregeln_ZielRundeId",
                table: "Uebergangsregeln",
                newName: "IX_QualifikationsRegeln_ZielRundeId");

            migrationBuilder.RenameIndex(
                name: "IX_Uebergangsregeln_QuellRundeId",
                table: "Uebergangsregeln",
                newName: "IX_QualifikationsRegeln_QuellRundeId");

            migrationBuilder.RenameIndex(
                name: "IX_LostopfEintraege_UebergangsregelId",
                table: "LostopfEintraege",
                newName: "IX_LostopfEintraege_QualifikationsRegelId");

            migrationBuilder.RenameColumn(
                name: "UebergangsregelId",
                table: "LostopfEintraege",
                newName: "QualifikationsRegelId");

            migrationBuilder.RenameTable(
                name: "Uebergangsregeln",
                newName: "QualifikationsRegeln");
        }
    }
}
