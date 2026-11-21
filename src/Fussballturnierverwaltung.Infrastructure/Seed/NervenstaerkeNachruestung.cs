using Fussballturnierverwaltung.Infrastructure.Seed.Daten;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

/// <summary>#669: setzt die Nervenstärke der Mannschaften bestehender Datenbanken – einmalig über die Migration
/// <c>MannschaftNervenstaerkeVorbelegen</c>, damit eine spätere Änderung in der Mannschaftspflege nicht bei jedem Start
/// zurückkommt. Neue Seed-Mannschaften bekommen sie beim Anlegen (<see cref="NervenstaerkeSeedDaten"/>). Alle anderen
/// bleiben auf dem Standard „normal“ der Spalte.</summary>
public static class NervenstaerkeNachruestung
{
    public static string Sql() => string.Join(Environment.NewLine, NervenstaerkeSeedListe.Handliste.Select(Update));

    private static string Update(NervenstaerkeSeedDaten.Eintrag eintrag) => $"""
        UPDATE "Mannschaften" SET "Nervenstaerke" = {(int)eintrag.Nervenstaerke}
        WHERE "Mannschaftsart" = {(int)eintrag.Art} AND "Kurzname" = {Text(eintrag.Kurzname)}
          AND "LandId" = (SELECT "Id" FROM "Laender" WHERE "FifaCode" = {Text(eintrag.Land)});
        """;

    private static string Text(string wert) => $"'{wert.Replace("'", "''")}'";
}
