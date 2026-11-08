using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Tabelle;

/// <summary>Ordnet die Einträge einer Tabelle nach den Ranking-Regeln und vergibt die Ränge (#628, aus
/// <see cref="TabellenRechner"/> herausgelöst).</summary>
internal static class TabellenSortierung
{
    /// <summary>Woran zwei Teams gemessen werden: ihre Statistik, für den direkten Vergleich die Spiele mit
    /// Ergebnis, dazu die Regeln – die Kriterien, die Punkteregel, nach der die Duelle gewertet werden, und für den
    /// Koeffizienten die Plätze in der Standard-Rangliste der Runde.</summary>
    internal sealed record Wertung(
        IReadOnlyDictionary<int, RohStats> Stats, IReadOnlyList<Spiel> Abgeschlossene, TabellenRegeln Regeln);

    internal static void SortierenUndRangZuweisen(List<TabellenEintrag> eintraege, Wertung wertung)
    {
        if (wertung.Regeln.RankingRegeln.Count == 0)
            eintraege.Sort(OhneRegelnVergleichen);
        else
            eintraege.Sort((x, y) => Vergleichen(x.Mannschaft.Id, y.Mannschaft.Id, wertung));

        for (int i = 0; i < eintraege.Count; i++)
            eintraege[i].Rang = i + 1;
    }

    /// <summary>Rangfolge ohne Ranking-Regeln im Regelwerk: Punkte → Tordifferenz → erzielte Tore.</summary>
    private static int OhneRegelnVergleichen(TabellenEintrag x, TabellenEintrag y)
    {
        int cmp = y.Punkte.CompareTo(x.Punkte);
        if (cmp != 0) return cmp;
        cmp = y.Tordifferenz.CompareTo(x.Tordifferenz);
        if (cmp != 0) return cmp;
        cmp = y.Tore.CompareTo(x.Tore);
        return cmp != 0 ? cmp : GleichstandAufloesen(x.Mannschaft.Id, y.Mannschaft.Id);
    }

    /// <summary>Letzter Vergleich, wenn kein Kriterium entscheidet (#622): nach Team-Id wie der Losentscheid.
    /// <see cref="List{T}.Sort(Comparison{T})"/> ist nicht stabil – ohne ihn hinge die Reihenfolge punkt- und
    /// torgleicher Teams an der Reihenfolge der Spiele und könnte zwischen zwei Berechnungen wechseln.</summary>
    private static int GleichstandAufloesen(int xId, int yId) => xId.CompareTo(yId);

    private static int Vergleichen(int xId, int yId, Wertung wertung)
    {
        foreach (var regel in wertung.Regeln.RankingRegeln)
        {
            int cmp = NachRegelVergleichen(regel.Regelart, xId, yId, wertung);

            // Sortierrichtung anwenden: Absteigend → negate (höherer Wert = besserer Rang)
            if (regel.Sortierrichtung == Sortierrichtung.Absteigend) cmp = -cmp;

            if (cmp != 0) return cmp;
        }

        return GleichstandAufloesen(xId, yId);
    }

    private static int NachRegelVergleichen(RankingRegelArt regelart, int xId, int yId, Wertung wertung)
    {
        if (IstDirekterVergleich(regelart)) return DirektVergleichen(regelart, xId, yId, wertung);

        // Stabiler Tiebreaker: nach Team-ID (reproduzierbar)
        if (regelart == RankingRegelArt.Losentscheid) return GleichstandAufloesen(xId, yId);

        if (regelart == RankingRegelArt.Koeffizient) return NachRanglistenplatzVergleichen(xId, yId, wertung.Regeln.RanglistenPlaetze);

        return VergleichenNachRegelart(regelart, wertung.Stats[xId], wertung.Stats[yId]);
    }

    /// <summary>Vergleicht den Platz in der Standard-Rangliste der Runde (Issue #354, z. B. UEFA-Klub-Koeffizient als
    /// letztes Kriterium der Ligaphase). Ein Team ohne Eintrag steht hinter jedem mit Eintrag; ohne Rangliste
    /// entscheidet das Kriterium nichts, wie der direkte Vergleich ohne Duell.</summary>
    private static int NachRanglistenplatzVergleichen(int xId, int yId, IReadOnlyDictionary<int, int>? plaetze)
    {
        if (plaetze is null || plaetze.Count == 0) return 0;
        return Platz(xId).CompareTo(Platz(yId));

        int Platz(int id) => plaetze.TryGetValue(id, out int platz) ? platz : int.MaxValue;
    }

    /// <summary>Vergleicht nur über die direkten Duelle zwischen x und y. Bei Heim-/Auswaertstabelle wird hier
    /// bewusst nicht nach Seite gefiltert (beide Duelle zaehlen) - der direkte Vergleich bleibt fuer diese
    /// informativen Zusatzansichten unveraendert.</summary>
    private static int DirektVergleichen(RankingRegelArt regelart, int xId, int yId, Wertung wertung)
    {
        var duelle = wertung.Abgeschlossene
            .Where(s => (s.HeimMannschaftId == xId && s.AuswaertsMannschaftId == yId) ||
                        (s.HeimMannschaftId == yId && s.AuswaertsMannschaftId == xId))
            .ToList();
        if (duelle.Count == 0) return 0;

        return VergleichenNachRegelart(
            regelart,
            TeamStatistik.Berechnen(xId, duelle, wertung.Regeln.PunkteRegel),
            TeamStatistik.Berechnen(yId, duelle, wertung.Regeln.PunkteRegel));
    }

    private static int VergleichenNachRegelart(RankingRegelArt regelart, RohStats x, RohStats y) =>
        regelart switch
        {
            RankingRegelArt.Punkte or
            RankingRegelArt.DirekterVergleichPunkte             => x.Punkte.CompareTo(y.Punkte),
            RankingRegelArt.Tordifferenz or
            RankingRegelArt.DirekterVergleichTordifferenz       => (x.Tore - x.Gegentore).CompareTo(y.Tore - y.Gegentore),
            RankingRegelArt.ErzielteTore or
            RankingRegelArt.DirekterVergleichErzielteTore       => x.Tore.CompareTo(y.Tore),
            RankingRegelArt.WenigerGegentore                   => x.Gegentore.CompareTo(y.Gegentore),
            RankingRegelArt.Siege                              => x.Siege.CompareTo(y.Siege),
            RankingRegelArt.DirekterVergleichAuswaertstore or
            RankingRegelArt.AuswaertsToreGesamt                => x.AuswaertsTore.CompareTo(y.AuswaertsTore),
            RankingRegelArt.AuswaertsSiege                     => x.AuswaertsSiege.CompareTo(y.AuswaertsSiege),
            RankingRegelArt.FairplayWertung                    => x.FairplayWertung.CompareTo(y.FairplayWertung),
            RankingRegelArt.GegnerPunkte                       => x.GegnerPunkte.CompareTo(y.GegnerPunkte),
            RankingRegelArt.GegnerTordifferenz                 => x.GegnerTordifferenz.CompareTo(y.GegnerTordifferenz),
            RankingRegelArt.GegnerErzielteTore                 => x.GegnerTore.CompareTo(y.GegnerTore),
            _                                                => 0
        };

    private static bool IstDirekterVergleich(RankingRegelArt regelart) =>
        regelart is RankingRegelArt.DirekterVergleichPunkte
                 or RankingRegelArt.DirekterVergleichTordifferenz
                 or RankingRegelArt.DirekterVergleichErzielteTore
                 or RankingRegelArt.DirekterVergleichAuswaertstore;
}
