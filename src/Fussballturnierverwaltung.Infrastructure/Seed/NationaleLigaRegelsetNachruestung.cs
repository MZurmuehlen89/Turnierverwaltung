using Fussballturnierverwaltung.Infrastructure.Seed.Daten;

namespace Fussballturnierverwaltung.Infrastructure.Seed;

/// <summary>#628: legt in bestehenden Datenbanken das Regelset „Nationale Liga (direkter Vergleich)“ an und stellt das
/// Regelwerk der La-Liga- und Serie-A-Vorlagen darauf um – einmalig über die Migration
/// <c>NationaleLigaRegelsetAnlegen</c>, und nur, wo das Regelwerk noch auf „UEFA Standard“ zeigt, damit eine eigene
/// Wahl erhalten bleibt. Neue Datenbanken haben bei der Migration noch keine Regelsets, dort legt der Seed alles an.</summary>
public static class NationaleLigaRegelsetNachruestung
{
    public static string Sql()
    {
        var (sets, eintraege) = RegelwerkeSeedDaten.RankingRegelSetsErstellen();
        var set = sets.Single(s => s.Name == RegelwerkeSeedDaten.NATIONALE_LIGA_SET);
        var kriterien = string.Join(", ", eintraege
            .Where(e => e.SetName == set.Name)
            .Select(e => $"({e.Reihenfolge}, {(int)e.Regelart})"));

        return $"""
            IF EXISTS (SELECT 1 FROM RankingRegelSets WHERE Name = N'UEFA Standard')
               AND NOT EXISTS (SELECT 1 FROM RankingRegelSets WHERE Name = {Text(set.Name)})
            BEGIN
                INSERT INTO RankingRegelSets (Name, Beschreibung, DirektenVergleichWiederholen)
                VALUES ({Text(set.Name)}, {Text(set.Beschreibung)}, {(set.DirektenVergleichWiederholen ? 1 : 0)});
                DECLARE @setId int = SCOPE_IDENTITY();

                INSERT INTO RankingRegelSetEintraege (RankingRegelSetId, RankingRegelId, Reihenfolge)
                SELECT @setId, r.Id, k.Reihenfolge
                FROM (VALUES {kriterien}) AS k(Reihenfolge, Regelart)
                JOIN (SELECT Regelart, MIN(Id) AS Id FROM RankingRegeln GROUP BY Regelart) AS r ON r.Regelart = k.Regelart;

                UPDATE Regelwerke SET RankingRegelSetId = @setId
                WHERE Name = {Text(TurnierVorlagenSeedDaten.LIGA_DIREKTER_VERGLEICH)}
                  AND RankingRegelSetId = (SELECT Id FROM RankingRegelSets WHERE Name = N'UEFA Standard');
            END
            """;
    }

    private static string Text(string wert) => $"N'{wert.Replace("'", "''")}'";
}
