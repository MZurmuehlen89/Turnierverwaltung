using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Tabelle;

/// <summary>Ordnet die Einträge einer Tabelle nach den Ranking-Regeln und vergibt die Ränge (#628, aus
/// <see cref="TabellenRechner"/> herausgelöst).</summary>
internal static class TabellenSortierung
{
    /// <summary>Woran die Teams gemessen werden: ihre Statistik, für die Mini-Tabelle des direkten Vergleichs die Spiele
    /// mit Ergebnis, dazu die Regeln – die Kriterien, die Punkteregel, nach der die Mini-Tabelle gewertet wird, für den
    /// Koeffizienten die Plätze in der Standard-Rangliste der Runde und ob die Mini-Tabelle wiederholt wird.</summary>
    internal sealed record Wertung(
        IReadOnlyDictionary<int, RohStats> Stats, IReadOnlyList<Spiel> Abgeschlossene, TabellenRegeln Regeln);

    internal static void SortierenUndRangZuweisen(List<TabellenEintrag> eintraege, Wertung wertung)
    {
        if (wertung.Regeln.RankingRegeln.Count == 0)
            eintraege.Sort(OhneRegelnVergleichen);
        else
            NachReihenfolgeSortieren(eintraege, Ordnen([.. eintraege.Select(e => e.Mannschaft.Id)], 0, wertung));

        for (int i = 0; i < eintraege.Count; i++)
            eintraege[i].Rang = i + 1;
    }

    private static void NachReihenfolgeSortieren(List<TabellenEintrag> eintraege, IReadOnlyList<int> reihenfolge)
    {
        var position = reihenfolge.Select((id, i) => (id, i)).ToDictionary(t => t.id, t => t.i);
        eintraege.Sort((x, y) => position[x.Mannschaft.Id].CompareTo(position[y.Mannschaft.Id]));
    }

    // ── Gestufte Rangfolge ────────────────────────────────────────────────

    /// <summary>Ordnet gleichstehende Teams ab dem Kriterium Nummer <paramref name="ab"/>: das Kriterium teilt sie in
    /// Stufen, jede Stufe mit mehr als einem Team geht ans nächste Kriterium. Ein Kriterium vergleicht also immer nur
    /// Teams, die bei allen vorigen gleich sind – Voraussetzung für die Mini-Tabelle des direkten Vergleichs (#628),
    /// die je Gleichstandsgruppe aus deren Spielen untereinander gerechnet wird statt paarweise.</summary>
    private static List<int> Ordnen(IReadOnlyList<int> teams, int ab, Wertung wertung)
    {
        var regeln = wertung.Regeln.RankingRegeln;
        if (teams.Count < 2 || ab >= regeln.Count)
            return [.. teams.Order()];

        if (!DirekterVergleich.IstKriterium(regeln[ab].Regelart))
            return [.. Abstufen(teams, NachRegel(regeln[ab], wertung)).SelectMany(stufe => Ordnen(stufe, ab + 1, wertung))];

        int danach = EndeDesDirektenVergleichs(regeln, ab);
        return [.. Abstufen(teams, NachMiniTabelle(teams, regeln, ab, danach, wertung))
            .SelectMany(stufe => Ordnen(stufe, WeiterNachMiniTabelle(stufe.Count, teams.Count, ab, danach, wertung), wertung))];
    }

    /// <summary>Wo es nach der Mini-Tabelle weitergeht: hat sie nichts getrennt, beim Kriterium danach. Hat sie einen
    /// Teil getrennt, rechnet ein UEFA-Regelset sie für die verbliebenen Teams neu (EM-Reglement Art. 20.01d), sonst
    /// entscheiden auch für sie die folgenden Kriterien (FIFA).</summary>
    private static int WeiterNachMiniTabelle(int stufenGroesse, int gruppenGroesse, int ab, int danach, Wertung wertung) =>
        stufenGroesse < gruppenGroesse && wertung.Regeln.DirektenVergleichWiederholen ? ab : danach;

    /// <summary>Die Kriterien des direkten Vergleichs ab <paramref name="ab"/> bilden einen Block – sie werten alle
    /// dieselbe Mini-Tabelle. Liefert den Index des ersten Kriteriums danach.</summary>
    private static int EndeDesDirektenVergleichs(IReadOnlyList<RankingRegel> regeln, int ab)
    {
        int ende = ab;
        while (ende < regeln.Count && DirekterVergleich.IstKriterium(regeln[ende].Regelart)) ende++;
        return ende;
    }

    /// <summary>Sortiert nach <paramref name="vergleich"/> und fasst gleich bewertete Teams zu Stufen zusammen.</summary>
    private static List<List<int>> Abstufen(IReadOnlyList<int> teams, Comparison<int> vergleich)
    {
        var stufen = new List<List<int>>();
        foreach (int id in teams.OrderBy(id => id, Comparer<int>.Create(vergleich)))
        {
            if (stufen.Count > 0 && vergleich(stufen[^1][0], id) == 0)
                stufen[^1].Add(id);
            else
                stufen.Add([id]);
        }
        return stufen;
    }

    private static Comparison<int> NachRegel(RankingRegel regel, Wertung wertung) =>
        (x, y) => Gerichtet(regel.Sortierrichtung, NachRegelVergleichen(regel.Regelart, x, y, wertung));

    /// <summary>Vergleich über die Mini-Tabelle: nur die Spiele der <paramref name="teams"/> untereinander, gewertet
    /// nach den Kriterien des direkten Vergleichs von <paramref name="ab"/> bis vor <paramref name="danach"/>. Ohne
    /// Spiel untereinander (Teams verschiedener Gruppen) entscheidet sie nichts. Bei Heim-/Auswärtstabelle zählen
    /// bewusst beide Seiten – der direkte Vergleich bleibt für diese Zusatzansichten unverändert.</summary>
    private static Comparison<int> NachMiniTabelle(
        IReadOnlyList<int> teams, IReadOnlyList<RankingRegel> regeln, int ab, int danach,
        Wertung wertung)
    {
        var ids = teams.ToHashSet();
        List<Spiel> untereinander = [.. wertung.Abgeschlossene.Where(s =>
            s.HeimMannschaftId is { } heim && ids.Contains(heim) &&
            s.AuswaertsMannschaftId is { } auswaerts && ids.Contains(auswaerts))];
        var mini = teams.ToDictionary(id => id, id => TeamStatistik.Berechnen(id, untereinander, wertung.Regeln.PunkteRegel));

        return (x, y) =>
        {
            for (int i = ab; i < danach; i++)
            {
                int cmp = Gerichtet(regeln[i].Sortierrichtung, VergleichenNachRegelart(regeln[i].Regelart, mini[x], mini[y]));
                if (cmp != 0) return cmp;
            }
            return 0;
        };
    }

    /// <summary>Absteigend: der höhere Wert steht vorn.</summary>
    private static int Gerichtet(Sortierrichtung richtung, int cmp) => richtung == Sortierrichtung.Absteigend ? -cmp : cmp;

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

    /// <summary>Vergleich nach einem Kriterium, das nur die Werte der beiden Teams braucht (alle außer dem direkten
    /// Vergleich).</summary>
    private static int NachRegelVergleichen(RankingRegelArt regelart, int xId, int yId, Wertung wertung)
    {
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
}
