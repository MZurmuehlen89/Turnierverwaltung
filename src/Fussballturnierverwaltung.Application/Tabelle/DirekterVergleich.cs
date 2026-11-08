using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Tabelle;

/// <summary>Die Kriterien des direkten Vergleichs: Sie werten gemeinsam die Mini-Tabelle der gleichstehenden Teams
/// (#628).</summary>
public static class DirekterVergleich
{
    public static bool IstKriterium(RankingRegelArt regelart) =>
        regelart is RankingRegelArt.DirekterVergleichPunkte
                 or RankingRegelArt.DirekterVergleichTordifferenz
                 or RankingRegelArt.DirekterVergleichErzielteTore
                 or RankingRegelArt.DirekterVergleichAuswaertstore;
}
