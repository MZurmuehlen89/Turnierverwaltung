using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Tabelle;

public static class TabellenRechner
{
    /// <summary>Gruppen einer Gruppenphase mit ihren Spielen. Seit Issue #397 aus der gespeicherten
    /// <see cref="Spiel.GruppenNummer"/> — auch mit noch offenen Platzhaltern vollständig und mit der
    /// ausgelosten Nummer. Rückfall für vor #397 generierte Spiele: Rekonstruktion aus den Paarungen.</summary>
    public static List<(int Nummer, IReadOnlyList<Spiel> Spiele)> GruppenErmitteln(IReadOnlyList<Spiel> alleSpiele)
    {
        if (alleSpiele.All(s => s.GruppenNummer.HasValue))
            return [.. alleSpiele
                .GroupBy(s => s.GruppenNummer!.Value)
                .OrderBy(g => g.Key)
                .Select(g => (g.Key, (IReadOnlyList<Spiel>)[.. g]))];

        return [.. GruppenAusSpielenAbleiten(alleSpiele).Select((gruppenTeamIds, index) =>
        {
            var teamIds = gruppenTeamIds.ToHashSet();
            return (index + 1, (IReadOnlyList<Spiel>)[.. alleSpiele
                .Where(s => s.HeimMannschaftId.HasValue && teamIds.Contains(s.HeimMannschaftId.Value))]);
        })];
    }

    /// <summary>Alle bereits feststehenden Teams einer Gruppe — auch solche, die bisher nur gegen noch offene
    /// Platzhalter angesetzt sind und daher in keinem Spiel mit zwei bekannten Teams vorkommen.</summary>
    public static IReadOnlyList<Mannschaft> AufgeloesteTeilnehmer(IEnumerable<Spiel> spiele) =>
        [.. spiele
            .SelectMany(s => new[] { s.HeimMannschaft, s.AuswaertsMannschaft })
            .Where(m => m is not null)
            .Select(m => m!)
            .DistinctBy(m => m.Id)];

    public static IReadOnlyList<string> OffenePlaetze(IEnumerable<Spiel> spiele) =>
        [.. spiele
            .OrderBy(s => s.Spielnummer)
            .SelectMany(s => new[]
            {
                s.HeimMannschaftId is null ? s.HeimPlatzhalterName : null,
                s.AuswaertsMannschaftId is null ? s.AuswaertsPlatzhalterName : null
            })
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    public static IReadOnlyList<TabellenEintrag> TabelleAusSpielenBerechnen(
        IReadOnlyList<Spiel> spiele, PunkteRegel punkteRegel, List<RankingRegel> rankingRegeln,
        bool? nurHeimspiele = null, IReadOnlyList<Mannschaft>? teilnehmer = null)
    {
        var teamMap = spiele
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
            teamMap.TryAdd(m.Id, m);

        if (teamMap.Count == 0) return [];

        var abgeschlossene = spiele
            .Where(s => s.Status == SpielStatus.Abgeschlossen && s.Ergebnis is not null)
            .ToList();

        var stats = teamMap.Keys.ToDictionary(
            id => id,
            id => BerechneSpielerStats(id, abgeschlossene, punkteRegel, nurHeimspiele));
        GegnerSummenErgaenzen(stats, abgeschlossene);

        var eintraege = teamMap.Select(kv => new TabellenEintrag
        {
            Mannschaft    = kv.Value,
            Spiele        = stats[kv.Key].Spiele,
            Siege         = stats[kv.Key].Siege,
            Unentschieden = stats[kv.Key].Unentschieden,
            Niederlagen   = stats[kv.Key].Niederlagen,
            Tore          = stats[kv.Key].Tore,
            Gegentore     = stats[kv.Key].Gegentore,
            Punkte        = stats[kv.Key].Punkte,
            Buchholz      = stats[kv.Key].GegnerPunkte
        }).ToList();

        SortierenUndRangZuweisen(eintraege, stats, rankingRegeln, abgeschlossene, punkteRegel);
        return eintraege;
    }

    /// <summary>Ergänzt je Team die Summen über alle Gegner, gegen die es bisher gespielt hat: aktuelle Punkte
    /// (Buchholz-Zahl des Schweizer Systems, Issue #329), Tordifferenz und erzielte Tore — die Kriterien der
    /// UEFA-Ligaphase ab 2024/25 (Issue #353), in der nicht jeder gegen jeden spielt und ein direkter Vergleich
    /// daher ausscheidet. Ein Gegner zählt je Spiel einmal. Ein Freilos-Spieltag erzeugt kein Spiel für das
    /// aussetzende Team (siehe <see cref="Fussballturnierverwaltung.Application.Spielplan.SchweizerSystemGenerator"/>),
    /// zählt also korrekt nicht als Gegner mit.</summary>
    private static void GegnerSummenErgaenzen(Dictionary<int, RohStats> stats, List<Spiel> abgeschlossene)
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

    /// <summary>
    /// Rekonstruiert Gruppen aus Spielpaarungen via Union-Find.
    /// Reihenfolge der Gruppen entspricht der Generierungsreihenfolge (kleinste Spielnummer zuerst).
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<int>> GruppenAusSpielenAbleiten(IEnumerable<Spiel> spiele)
    {
        var spieleListe = spiele
            .Where(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue)
            .ToList();

        var parent = new Dictionary<int, int>();

        int Find(int x)
        {
            if (!parent.ContainsKey(x)) parent[x] = x;
            if (parent[x] != x) parent[x] = Find(parent[x]);
            return parent[x];
        }

        void Union(int x, int y)
        {
            var rx = Find(x); var ry = Find(y);
            if (rx != ry) parent[rx] = ry;
        }

        foreach (var s in spieleListe)
            Union(s.HeimMannschaftId!.Value, s.AuswaertsMannschaftId!.Value);

        // Gruppierungsreihenfolge anhand erster Spielnummer sicherstellen
        var teamErsteSpielnr = new Dictionary<int, int>();
        foreach (var s in spieleListe.OrderBy(s => s.Spielnummer))
        {
            teamErsteSpielnr.TryAdd(s.HeimMannschaftId!.Value, s.Spielnummer);
            teamErsteSpielnr.TryAdd(s.AuswaertsMannschaftId!.Value, s.Spielnummer);
        }

        return parent.Keys
            .GroupBy(id => Find(id))
            .OrderBy(g => g.Min(id => teamErsteSpielnr.GetValueOrDefault(id, int.MaxValue)))
            .Select(g => (IReadOnlyList<int>)g.OrderBy(id => id).ToList())
            .ToList();
    }

    // ── Statistikberechnung ───────────────────────────────────────────────

    private static RohStats BerechneSpielerStats(
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

            // Fairplay-Wertung: -(GelbeKarte * 1 + RoteKarte * 3) - der höhere Wert ist der bessere,
            // die Regel sortiert daher absteigend.
            foreach (var e in s.Ereignisse.Where(e => e.MannschaftId == teamId))
            {
                fairplay -= e.EreignisArt switch
                {
                    EreignisArt.GelbeKarte => 1,
                    EreignisArt.RoteKarte  => 3,
                    _                      => 0
                };
            }
        }

        int punkte = siege * punkteRegel.PunkteFuerSieg
                   + unentschieden * punkteRegel.PunkteFuerUnentschieden
                   + niederlagen * punkteRegel.PunkteFuerNiederlage;

        return new RohStats(gespielt, siege, unentschieden, niederlagen, tore, gegentore, auswaertsTore, punkte, fairplay)
        {
            AuswaertsSiege = auswaertsSiege
        };
    }

    // ── Sortierung ────────────────────────────────────────────────────────

    private static void SortierenUndRangZuweisen(
        List<TabellenEintrag> eintraege,
        Dictionary<int, RohStats> statsMap,
        List<RankingRegel> regeln,
        List<Spiel> alleAbgeschlossenen,
        PunkteRegel punkteRegel)
    {
        if (!regeln.Any())
        {
            // Fallback: Punkte → Tordifferenz → Erzielte Tore
            eintraege.Sort((x, y) =>
            {
                int cmp = y.Punkte.CompareTo(x.Punkte);
                if (cmp != 0) return cmp;
                cmp = y.Tordifferenz.CompareTo(x.Tordifferenz);
                if (cmp != 0) return cmp;
                return y.Tore.CompareTo(x.Tore);
            });
        }
        else
        {
            eintraege.Sort((x, y) => Vergleichen(
                x.Mannschaft.Id, y.Mannschaft.Id,
                statsMap, regeln, alleAbgeschlossenen, punkteRegel));
        }

        for (int i = 0; i < eintraege.Count; i++)
            eintraege[i].Rang = i + 1;
    }

    private static int Vergleichen(
        int xId, int yId,
        Dictionary<int, RohStats> statsMap,
        List<RankingRegel> regeln,
        List<Spiel> alleSpiele,
        PunkteRegel punkteRegel)
    {
        foreach (var regel in regeln)
        {
            int cmp;

            if (IstDirekterVergleich(regel.Regelart))
            {
                // Nur direkte Duelle zwischen x und y. Bei Heim-/Auswaertstabelle wird hier bewusst
                // nicht nach Seite gefiltert (beide Duelle zaehlen) - der direkte Vergleich bleibt
                // fuer diese informativen Zusatzansichten unveraendert.
                var dvSpiele = alleSpiele
                    .Where(s => (s.HeimMannschaftId == xId && s.AuswaertsMannschaftId == yId) ||
                                (s.HeimMannschaftId == yId && s.AuswaertsMannschaftId == xId))
                    .ToList();

                if (!dvSpiele.Any()) { cmp = 0; }
                else
                {
                    var xDv = BerechneSpielerStats(xId, dvSpiele, punkteRegel);
                    var yDv = BerechneSpielerStats(yId, dvSpiele, punkteRegel);
                    cmp = VergleichenNachRegelart(regel.Regelart, xDv, yDv);
                }
            }
            else if (regel.Regelart == RankingRegelArt.Losentscheid)
            {
                // Stabiler Tiebreaker: nach Team-ID (reproduzierbar)
                cmp = xId.CompareTo(yId);
            }
            else
            {
                cmp = VergleichenNachRegelart(regel.Regelart, statsMap[xId], statsMap[yId]);
            }

            // Sortierrichtung anwenden: Absteigend → negate (höherer Wert = besserer Rang)
            if (regel.Sortierrichtung == Sortierrichtung.Absteigend) cmp = -cmp;

            if (cmp != 0) return cmp;
        }

        return 0;
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

    private record RohStats(
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
}
