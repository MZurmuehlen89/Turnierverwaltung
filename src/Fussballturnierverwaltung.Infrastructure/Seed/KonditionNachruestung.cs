using Fussballturnierverwaltung.Infrastructure.Seed.Daten;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

/// <summary>#677: setzt die Kondition der Mannschaften bestehender Datenbanken – einmalig über die Migration
/// <c>MannschaftKonditionVorbelegen</c>, damit eine spätere Änderung in der Mannschaftspflege nicht bei jedem Start
/// zurückkommt. Neue Seed-Mannschaften bekommen sie beim Anlegen (<see cref="KonditionSeedDaten"/>). Alle anderen
/// bleiben auf dem Standard „normal“ der Spalte.</summary>
public static class KonditionNachruestung
{
    public static string Sql() => string.Join(Environment.NewLine, KonditionSeedListe.Handliste.Select(Update));

    private static string Update(KonditionSeedDaten.Eintrag eintrag) => $"""
        UPDATE "Mannschaften" SET "Kondition" = {(int)eintrag.Kondition}
        WHERE "Mannschaftsart" = {(int)eintrag.Art} AND "Kurzname" = {Text(eintrag.Kurzname)}
          AND "LandId" = (SELECT "Id" FROM "Laender" WHERE "FifaCode" = {Text(eintrag.Land)});
        """;

    private static string Text(string wert) => $"'{wert.Replace("'", "''")}'";
}
