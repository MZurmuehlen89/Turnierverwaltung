using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
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

    /// <summary>Wie der direkte Vergleich des Regelsets rechnet, als Hinweis für den Regelwerk-Editor; <c>null</c>
    /// ohne Regelset oder wenn es keinen direkten Vergleich enthält.</summary>
    public static string? Hinweis(RankingRegelSet? set)
    {
        if (set is null || !set.Eintraege.Any(e => IstKriterium(e.RankingRegel.Regelart))) return null;

        const string MINI_TABELLE = "Direkter Vergleich: Mini-Tabelle aus den Spielen der gleichstehenden Teams untereinander.";
        return set.DirektenVergleichWiederholen
            ? $"{MINI_TABELLE} Trennt sie nur einen Teil, wird sie für die übrigen neu gerechnet (UEFA)."
            : $"{MINI_TABELLE} Trennt sie nur einen Teil, entscheiden für die übrigen die folgenden Kriterien (FIFA).";
    }
}
