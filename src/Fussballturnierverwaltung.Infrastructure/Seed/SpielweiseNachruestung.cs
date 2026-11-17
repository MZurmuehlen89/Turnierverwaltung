using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Infrastructure.Seed.Daten;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

/// <summary>#667: setzt die Spielweise der Mannschaften bestehender Datenbanken – einmalig über die Migration
/// <c>MannschaftSpielweiseVorbelegen</c>, damit eine spätere Änderung in der Mannschaftspflege nicht bei jedem Start
/// zurückkommt. Neue Seed-Mannschaften bekommen sie beim Anlegen (<see cref="SpielweiseSeedDaten"/>). Erst der
/// Rating-Vorschlag für alle Mannschaften (in SQL nachgerechnet, ein Test vergleicht mit
/// <see cref="SpielweisenVorschlag"/>), dann die Handliste.</summary>
public static class SpielweiseNachruestung
{
    public static string Sql() => string.Join(Environment.NewLine,
        [VorschlagAusRatings(), .. SpielweiseSeedDaten.Handliste.Select(Update)]);

    private static string VorschlagAusRatings()
    {
        string abweichung = $"(\"OffensivRating\" - \"DefensivRating\" - {SpielweisenVorschlag.NORMALABSTAND})";
        return $"""
            UPDATE "Mannschaften" SET "Flexibilitaet" = 0, "Grundausrichtung" = CASE
                WHEN {abweichung} >= {SpielweisenVorschlag.ZWEI_STUFEN} THEN 2
                WHEN {abweichung} >= {SpielweisenVorschlag.EINE_STUFE} THEN 1
                WHEN {abweichung} <= -{SpielweisenVorschlag.ZWEI_STUFEN} THEN -2
                WHEN {abweichung} <= -{SpielweisenVorschlag.EINE_STUFE} THEN -1
                ELSE 0 END;
            """;
    }

    private static string Update(SpielweiseSeedDaten.Eintrag eintrag)
    {
        var grundausrichtung = eintrag.Grundausrichtung is { } wert ? $"\"Grundausrichtung\" = {(int)wert}, " : "";
        return $"""
            UPDATE "Mannschaften" SET {grundausrichtung}"Flexibilitaet" = {(int)eintrag.Flexibilitaet}
            WHERE "Mannschaftsart" = {(int)eintrag.Art} AND "Kurzname" = '{eintrag.Kurzname.Replace("'", "''")}';
            """;
    }
}
