using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Tabelle;

/// <summary>Rechnet Tabellen aus Spielen, ohne Datenbankzugriff: Statistik je Team, Sortierung nach den
/// Ranking-Regeln. Die Gruppen einer Gruppenphase bildet <see cref="GruppenRechner"/>, das Laden von Runde,
/// Regelwerk und Spielen übernimmt <see cref="TabellenService"/> (#439).</summary>
public static class TabellenRechner
{
    // ── Tabelle ───────────────────────────────────────────────────────────

    /// <summary>Tabelle aus den Spielen mit Ergebnis. Mit <paramref name="nurHeimspiele"/> zählt je Team nur die
    /// Heim- (<c>true</c>) bzw. nur die Auswärtsseite (<c>false</c>). <paramref name="teilnehmer"/> nennt Teams,
    /// die auch ohne Spiel gegen einen bereits bekannten Gegner in der Tabelle stehen sollen.</summary>
    public static IReadOnlyList<TabellenEintrag> TabelleBerechnen(
        IReadOnlyList<Spiel> spiele, TabellenRegeln regeln,
        bool? nurHeimspiele = null, IReadOnlyList<Mannschaft>? teilnehmer = null)
    {
        var teams = TeamsSammeln(spiele, teilnehmer);
        if (teams.Count == 0) return [];

        var abgeschlossene = MitErgebnis(spiele);
        var stats = teams.Keys.ToDictionary(
            id => id,
            id => TeamStatistik(id, abgeschlossene, regeln.PunkteRegel, nurHeimspiele));
        GegnerSummenErgaenzen(stats, abgeschlossene);

        var eintraege = teams.Select(kv => EintragAus(kv.Value, stats[kv.Key])).ToList();
        SortierenUndRangZuweisen(eintraege, new Wertung(stats, abgeschlossene, regeln));
        return eintraege;
    }

    /// <summary>Tabelle einer <see cref="TabellenAnsicht"/>: Hin- und Rückrunde werten nur die Spiele ihrer
    /// Hälfte, Heim- und Auswärtstabelle je Team nur die eine Seite.</summary>
    public static IReadOnlyList<TabellenEintrag> TabelleDerAnsichtBerechnen(
        IReadOnlyList<Spiel> spiele, TabellenAnsicht ansicht, TabellenRegeln regeln)
    {
        spiele = ansicht switch
        {
            TabellenAnsicht.Hinrunde   => spiele.Where(s => !s.IstRueckspiel).ToList(),
            TabellenAnsicht.Rueckrunde => spiele.Where(s => s.IstRueckspiel).ToList(),
            _                          => spiele
        };

        bool? nurHeimspiele = ansicht switch
        {
            TabellenAnsicht.Heimtabelle      => true,
            TabellenAnsicht.Auswaertstabelle => false,
            _                                => null
        };

        return TabelleBerechnen(spiele, regeln, nurHeimspiele);
    }

    /// <summary>Die Spiele bis einschließlich <paramref name="bisSpieltag"/> - der Stand nach diesem Spieltag;
    /// ohne Angabe alle Spiele.</summary>
    public static IReadOnlyList<Spiel> BisSpieltag(IReadOnlyList<Spiel> spiele, int? bisSpieltag) =>
        bisSpieltag is null ? spiele : spiele.Where(s => s.Spieltag is { } sp && sp <= bisSpieltag).ToList();

    /// <summary>Die Spieltage, für die ein historischer Tabellenstand sinnvoll ist: 1 bis zum
    /// letzten Spieltag mit mindestens einem eingetragenen Ergebnis (auch bei nur teilweise
    /// simuliertem Spieltag). Leer, wenn noch kein Ergebnis vorliegt — verhindert, dass der
    /// Tabellenstand-Snapshot auf komplett unsimulierte, zukünftige Spieltage zeigen kann.</summary>
    public static IReadOnlyList<int> SpieltageMitErgebnis(IEnumerable<Spiel> spiele)
    {
        int letzterMitErgebnis = spiele
            .Where(s => s.Status == SpielStatus.Abgeschlossen && s.Spieltag.HasValue)
            .Select(s => s.Spieltag!.Value)
            .DefaultIfEmpty()
            .Max();

        return letzterMitErgebnis > 0
            ? Enumerable.Range(1, letzterMitErgebnis).ToList()
            : [];
    }

    /// <summary>Die Spiele, die in eine Tabelle eingehen: abgeschlossen und mit Ergebnis.</summary>
    public static IReadOnlyList<Spiel> MitErgebnis(IEnumerable<Spiel> spiele) => [.. spiele.Where(HatErgebnis)];

    public static bool HatErgebnis(Spiel spiel) => spiel.Status == SpielStatus.Abgeschlossen && spiel.Ergebnis is not null;

    private static Dictionary<int, Mannschaft> TeamsSammeln(IReadOnlyList<Spiel> spiele, IReadOnlyList<Mannschaft>? teilnehmer)
    {
        var teams = spiele
            .Where(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue)
            .SelectMany(s => new[]
            {
                (s.HeimMannschaftId!.Value,      s.HeimMannschaft!),
                (s.AuswaertsMannschaftId!.Value, s.AuswaertsMannschaft!)
            })
            .DistinctBy(t => t.Item1)
            .ToDictionary(t => t.Item1, t => t.Item2);

        // Issue #397: Teams ohne Spiel gegen einen bereits bekannten Gegner trotzdem mit 0 Punkten führen.
        foreach (var m in teilnehmer ?? [])
            teams.TryAdd(m.Id, m);

        return teams;
    }

    private static TabellenEintrag EintragAus(Mannschaft mannschaft, RohStats stats) => new()
    {
        Mannschaft    = mannschaft,
        Spiele        = stats.Spiele,
        Siege         = stats.Siege,
        Unentschieden = stats.Unentschieden,
        Niederlagen   = stats.Niederlagen,
        Tore          = stats.Tore,
        Gegentore     = stats.Gegentore,
        Punkte        = stats.Punkte,
        Buchholz      = stats.GegnerPunkte
    };

    // ── Statistik je Team ─────────────────────────────────────────────────

    private static RohStats TeamStatistik(
        int teamId, IReadOnlyList<Spiel> spiele, PunkteRegel punkteRegel, bool? nurHeimspiele = null)
    {
        int gespielt = 0, siege = 0, unentschieden = 0, niederlagen = 0;
        int tore = 0, gegentore = 0, auswaertsTore = 0, auswaertsSiege = 0, fairplay = 0;

        foreach (var s in spiele)
        {
            if (s.Ergebnis is null) continue;
            bool istHeim = s.HeimMannschaftId == teamId;

            if (!istHeim && s.AuswaertsMannschaftId != teamId) continue;
            if (nurHeimspiele is { } nur && istHeim != nur) continue;

            gespielt++;
            int eigTore = istHeim ? s.Ergebnis.HeimToreGesamt     : s.Ergebnis.AuswaertsToreGesamt;
            int gegTore = istHeim ? s.Ergebnis.AuswaertsToreGesamt : s.Ergebnis.HeimToreGesamt;

            tore      += eigTore;
            gegentore += gegTore;

            if (!istHeim) auswaertsTore += eigTore;

            if (s.Ergebnis.EntscheidungArt == EntscheidungArt.RegulaereSpielzeit && eigTore == gegTore)
                unentschieden++;
            else if (eigTore > gegTore || s.Ergebnis.SiegerMannschaftId == teamId)
            {
                siege++;
                if (!istHeim) auswaertsSiege++;
            }
            else
                niederlagen++;

            fairplay += FairplayPunkteImSpiel(s, teamId);
        }

        int punkte = siege * punkteRegel.PunkteFuerSieg
                   + unentschieden * punkteRegel.PunkteFuerUnentschieden
                   + niederlagen * punkteRegel.PunkteFuerNiederlage;

        return new RohStats(gespielt, siege, unentschieden, niederlagen, tore, gegentore, auswaertsTore, punkte, fairplay)
        {
            AuswaertsSiege = auswaertsSiege
        };
    }

    /// <summary>Fairplay-Wertung eines Teams in einem Spiel: -(GelbeKarte * 1 + RoteKarte * 3) - der höhere Wert
    /// ist der bessere, die Regel sortiert daher absteigend.</summary>
    private static int FairplayPunkteImSpiel(Spiel spiel, int teamId) =>
        -spiel.Ereignisse
            .Where(e => e.MannschaftId == teamId)
            .Sum(e => e.EreignisArt switch
            {
                EreignisArt.GelbeKarte => 1,
                EreignisArt.RoteKarte  => 3,
                _                      => 0
            });

    /// <summary>Ergänzt je Team die Summen über alle Gegner, gegen die es bisher gespielt hat: aktuelle Punkte
    /// (Buchholz-Zahl des Schweizer Systems, Issue #329), Tordifferenz und erzielte Tore — die Kriterien der
    /// UEFA-Ligaphase ab 2024/25 (Issue #353), in der nicht jeder gegen jeden spielt und ein direkter Vergleich
    /// daher ausscheidet. Ein Gegner zählt je Spiel einmal. Ein Freilos-Spieltag erzeugt kein Spiel für das
    /// aussetzende Team (siehe <see cref="Fussballturnierverwaltung.Application.Spielplan.SchweizerSystemGenerator"/>),
    /// zählt also korrekt nicht als Gegner mit.</summary>
    private static void GegnerSummenErgaenzen(Dictionary<int, RohStats> stats, IReadOnlyList<Spiel> abgeschlossene)
    {
        var summen = stats.Keys.ToDictionary(id => id, _ => (Punkte: 0, Tordifferenz: 0, Tore: 0));

        foreach (var s in abgeschlossene)
        {
            if (s.HeimMannschaftId is not { } heimId || s.AuswaertsMannschaftId is not { } auswaertsId) continue;
            if (!stats.TryGetValue(heimId, out var heim) || !stats.TryGetValue(auswaertsId, out var auswaerts)) continue;

            summen[heimId]      = Addieren(summen[heimId], auswaerts);
            summen[auswaertsId] = Addieren(summen[auswaertsId], heim);
        }

        foreach (var (id, summe) in summen)
        {
            stats[id] = stats[id] with
            {
                GegnerPunkte       = summe.Punkte,
                GegnerTordifferenz = summe.Tordifferenz,
                GegnerTore         = summe.Tore
            };
        }

        static (int Punkte, int Tordifferenz, int Tore) Addieren((int Punkte, int Tordifferenz, int Tore) summe, RohStats gegner) =>
            (summe.Punkte + gegner.Punkte, summe.Tordifferenz + gegner.Tore - gegner.Gegentore, summe.Tore + gegner.Tore);
    }

    private sealed record RohStats(
        int Spiele,
        int Siege,
        int Unentschieden,
        int Niederlagen,
        int Tore,
        int Gegentore,
        int AuswaertsTore,
        int Punkte,
        int FairplayWertung = 0,
        int GegnerPunkte = 0,
        int GegnerTordifferenz = 0,
        int GegnerTore = 0,
        int AuswaertsSiege = 0);

    // ── Sortierung ────────────────────────────────────────────────────────

    /// <summary>Woran zwei Teams gemessen werden: ihre Statistik, für den direkten Vergleich die Spiele mit
    /// Ergebnis, dazu die Regeln – die Kriterien, die Punkteregel, nach der die Duelle gewertet werden, und für den
    /// Koeffizienten die Plätze in der Standard-Rangliste der Runde.</summary>
    private sealed record Wertung(
        IReadOnlyDictionary<int, RohStats> Stats, IReadOnlyList<Spiel> Abgeschlossene, TabellenRegeln Regeln);

    private static void SortierenUndRangZuweisen(List<TabellenEintrag> eintraege, Wertung wertung)
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
        return y.Tore.CompareTo(x.Tore);
    }

    private static int Vergleichen(int xId, int yId, Wertung wertung)
    {
        foreach (var regel in wertung.Regeln.RankingRegeln)
        {
            int cmp = NachRegelVergleichen(regel.Regelart, xId, yId, wertung);

            // Sortierrichtung anwenden: Absteigend → negate (höherer Wert = besserer Rang)
            if (regel.Sortierrichtung == Sortierrichtung.Absteigend) cmp = -cmp;

            if (cmp != 0) return cmp;
        }

        return 0;
    }

    private static int NachRegelVergleichen(RankingRegelArt regelart, int xId, int yId, Wertung wertung)
    {
        if (IstDirekterVergleich(regelart)) return DirektVergleichen(regelart, xId, yId, wertung);

        // Stabiler Tiebreaker: nach Team-ID (reproduzierbar)
        if (regelart == RankingRegelArt.Losentscheid) return xId.CompareTo(yId);

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
            TeamStatistik(xId, duelle, wertung.Regeln.PunkteRegel),
            TeamStatistik(yId, duelle, wertung.Regeln.PunkteRegel));
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
