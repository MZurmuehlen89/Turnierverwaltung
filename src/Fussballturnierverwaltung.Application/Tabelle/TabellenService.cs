using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Tabelle;

public class TabellenService(
    ITurnierRundeRepository rundeRepository,
    ISpielRepository spielRepository,
    IRegelwerkRepository regelwerkRepository)
{
    /// <summary>Berechnet die Tabelle einer Liga- oder Gruppenphase-Runde. Mit <paramref name="bisSpieltag"/> wird der
    /// historische Stand nach diesem Spieltag berechnet statt des aktuellen Stands.</summary>
    public async Task<IReadOnlyList<TabellenEintrag>> TabelleBerechnenAsync(
        int turnierRundeId, TabellenAnsicht ansicht = TabellenAnsicht.Gesamt, int? bisSpieltag = null, CancellationToken ct = default)
    {
        var (punkteRegel, rankingRegeln, alleSpiele) = await LadeTabellenDatenAsync(turnierRundeId, ct);
        if (!alleSpiele.Any()) return [];

        var spiele = SpieleBisSpieltagFiltern(alleSpiele, bisSpieltag);

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

        return TabelleAusSpielenBerechnen(spiele, punkteRegel, rankingRegeln, nurHeimspiele);
    }

    /// <summary>Berechnet je eine Tabelle pro Gruppe inkl. Qualifikationsstatus und <see cref="TabellenEintrag.SicherPositioniert"/>
    /// (Issue #202 - ob der exakte Rang durch kein Ergebnis der verbleibenden Spiele mehr verändert werden kann). Gruppen werden
    /// aus den Spielpaarungen rekonstruiert. Mit <paramref name="bisSpieltag"/> wird der historische Tabellenstand nach diesem
    /// Spieltag berechnet; die Qualifikationsstatus-Berechnung bezieht sich dabei weiterhin auf den vollständigen (aktuellen) Datenstand.</summary>
    public async Task<IReadOnlyList<GruppenTabelle>> GruppenTabellenBerechnenAsync(
        int turnierRundeId, int? bisSpieltag = null, CancellationToken ct = default)
    {
        var (punkteRegel, rankingRegeln, alleSpiele) = await LadeTabellenDatenAsync(turnierRundeId, ct);
        if (!alleSpiele.Any()) return [];

        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct);
        int qualPlaetze = runde is { GruppenAnzahl: > 0 }
            ? Math.Max(1, runde.AusgangsMannschaftsanzahl / runde.GruppenAnzahl.Value)
            : 2;

        var gruppen = GruppenAusSpielenAbleiten(alleSpiele);
        var result = gruppen.Select((gruppenTeamIds, index) =>
        {
            var teamIds = gruppenTeamIds.ToHashSet();
            var gruppenSpiele = (IReadOnlyList<Spiel>)alleSpiele
                .Where(s => s.HeimMannschaftId.HasValue && teamIds.Contains(s.HeimMannschaftId.Value))
                .ToList();
            var gruppenSpieleGefiltert = SpieleBisSpieltagFiltern(gruppenSpiele, bisSpieltag);
            return (Gruppe: new GruppenTabelle(index + 1, TabelleAusSpielenBerechnen(gruppenSpieleGefiltert, punkteRegel, rankingRegeln)),
                    Spiele: gruppenSpiele);
        }).ToList();

        foreach (var (gruppe, spiele) in result)
            QualifikationsstatusSetzen(gruppe, spiele, qualPlaetze, punkteRegel, rankingRegeln);

        return result.Select(r => r.Gruppe).ToList();
    }

    private static IReadOnlyList<Spiel> SpieleBisSpieltagFiltern(IReadOnlyList<Spiel> spiele, int? bisSpieltag) =>
        bisSpieltag is null ? spiele : spiele.Where(s => s.Spieltag is { } sp && sp <= bisSpieltag).ToList();

    /// <summary>Liefert die Spieltage, für die ein historischer Tabellenstand sinnvoll ist: 1 bis zum
    /// letzten Spieltag mit mindestens einem eingetragenen Ergebnis (auch bei nur teilweise
    /// simuliertem Spieltag). Leer, wenn noch kein Ergebnis vorliegt — verhindert, dass der
    /// Tabellenstand-Snapshot auf komplett unsimulierte, zukünftige Spieltage zeigen kann.</summary>
    public async Task<IReadOnlyList<int>> VerfuegbareSpieltageAsync(int turnierRundeId, CancellationToken ct = default)
    {
        var spiele = await spielRepository.AlleNachRundeAsync(turnierRundeId, ct);
        int letzterMitErgebnis = spiele
            .Where(s => s.Status == SpielStatus.Abgeschlossen && s.Spieltag.HasValue)
            .Select(s => s.Spieltag!.Value)
            .DefaultIfEmpty()
            .Max();

        return letzterMitErgebnis > 0
            ? Enumerable.Range(1, letzterMitErgebnis).ToList()
            : [];
    }

    // ── Interne Lade- und Berechnungsmethoden ────────────────────────────

    private async Task<(PunkteRegel, List<RankingRegel>, IReadOnlyList<Spiel>)> LadeTabellenDatenAsync(
        int turnierRundeId, CancellationToken ct)
    {
        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde {turnierRundeId} nicht gefunden.");

        var regelwerk = await regelwerkRepository.FindenNachIdMitAllenRegelnAsync(runde.RegelwerkId, ct)
            ?? throw new InvalidOperationException($"Regelwerk {runde.RegelwerkId} nicht gefunden.");

        var alleSpiele = await spielRepository.AlleNachRundeMitEreignissenAsync(turnierRundeId, ct);

        var punkteRegel = regelwerk.PunkteRegeln.FirstOrDefault()
            ?? new PunkteRegel { PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 };

        var rankingRegeln = regelwerk.RankingRegelSet?.Eintraege
            .OrderBy(e => e.Reihenfolge)
            .Select(e => e.RankingRegel)
            .ToList() ?? [];

        return (punkteRegel, rankingRegeln, alleSpiele);
    }

    private static IReadOnlyList<TabellenEintrag> TabelleAusSpielenBerechnen(
        IReadOnlyList<Spiel> spiele, PunkteRegel punkteRegel, List<RankingRegel> rankingRegeln,
        bool? nurHeimspiele = null)
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

        if (teamMap.Count == 0) return [];

        var abgeschlossene = spiele
            .Where(s => s.Status == SpielStatus.Abgeschlossen && s.Ergebnis is not null)
            .ToList();

        var stats = teamMap.Keys.ToDictionary(
            id => id,
            id => BerechneSpielerStats(id, abgeschlossene, punkteRegel, nurHeimspiele));

        var eintraege = teamMap.Select(kv => new TabellenEintrag
        {
            Mannschaft    = kv.Value,
            Spiele        = stats[kv.Key].Spiele,
            Siege         = stats[kv.Key].Siege,
            Unentschieden = stats[kv.Key].Unentschieden,
            Niederlagen   = stats[kv.Key].Niederlagen,
            Tore          = stats[kv.Key].Tore,
            Gegentore     = stats[kv.Key].Gegentore,
            Punkte        = stats[kv.Key].Punkte
        }).ToList();

        SortierenUndRangZuweisen(eintraege, stats, rankingRegeln, abgeschlossene, punkteRegel);
        return eintraege;
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
        int tore = 0, gegentore = 0, auswaertsTore = 0, fairplay = 0;

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
                siege++;
            else
                niederlagen++;

            // Fairplay-Wertung: -(GelbeKarte * 1 + RoteKarte * 3)
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

        return new RohStats(gespielt, siege, unentschieden, niederlagen, tore, gegentore, auswaertsTore, punkte, fairplay);
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
            RankingRegelArt.DirekterVergleichAuswaertstore      => x.AuswaertsTore.CompareTo(y.AuswaertsTore),
            RankingRegelArt.FairplayWertung                    => x.FairplayWertung.CompareTo(y.FairplayWertung),
            _                                                  => 0
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
        int FairplayWertung = 0);

    // ── Qualifikationsstatus ──────────────────────────────────────────────

    private static void QualifikationsstatusSetzen(
        GruppenTabelle gruppe,
        IReadOnlyList<Spiel> gruppenSpiele,
        int qualifiziertePlaetze,
        PunkteRegel punkteRegel,
        List<RankingRegel> rankingRegeln)
    {
        var abgeschlossen = gruppenSpiele
            .Where(s => s.Status == SpielStatus.Abgeschlossen && s.Ergebnis is not null)
            .ToList();
        var offen = gruppenSpiele
            .Where(s => s.Status != SpielStatus.Abgeschlossen
                     && s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue)
            .ToList();

        if (!offen.Any())
        {
            foreach (var e in gruppe.Tabelle)
            {
                e.Qualifikationsstatus = e.Rang <= qualifiziertePlaetze
                    ? Qualifikationsstatus.SicherQualifiziert
                    : Qualifikationsstatus.SicherAusgeschieden;
                e.SicherPositioniert = true; // keine offenen Spiele mehr - jeder Rang ist final
            }
            return;
        }

        foreach (var eintrag in gruppe.Tabelle)
        {
            int teamId = eintrag.Mannschaft.Id;

            int bestRang = ExtremRangErmitteln(
                abgeschlossen, offen, teamId, punkteRegel, rankingRegeln, teamGewinntAlles: true, bestenfalls: true);
            int worstRang = ExtremRangErmitteln(
                abgeschlossen, offen, teamId, punkteRegel, rankingRegeln, teamGewinntAlles: false, bestenfalls: false);

            eintrag.Qualifikationsstatus = bestRang > qualifiziertePlaetze
                ? Qualifikationsstatus.SicherAusgeschieden
                : worstRang <= qualifiziertePlaetze
                    ? Qualifikationsstatus.SicherQualifiziert
                    : Qualifikationsstatus.Offen;
            eintrag.SicherPositioniert = bestRang == worstRang;
        }
    }

    /// <summary>Ermittelt den best- bzw. schlechtestmöglichen Rang von <paramref name="teamId"/>: die eigenen offenen
    /// Spiele sind gemäß <paramref name="teamGewinntAlles"/> fixiert und werden mit einem extremen statt einem
    /// knappen Ergebnis simuliert - für das eigene Spiel gibt es nur zwei Beteiligte (das Team und den Gegner),
    /// ein extremeres Ergebnis wirkt daher immer eindeutig in dieselbe Richtung (nie schwächer).
    ///
    /// Für alle fremden offenen Spiele (die <paramref name="teamId"/> nicht betreffen) wird per Brute-Force jede
    /// Kombination aus Sieger UND Tordifferenz (knapp/extrem) durchgerechnet - macht pro fremdem Spiel 4 statt 2
    /// Varianten, insgesamt 4^n statt 2^n Kombinationen. Notwendig, weil ein fremdes Spiel einen "Umweg" über einen
    /// dritten Verein nimmt: Team X gewinnt gegen Team Y - ein knapper Sieg schadet Y kaum, ein extremer Sieg
    /// zertrümmert Ys Tordifferenz. Ob das für <paramref name="teamId"/> gut oder schlecht ist, hängt davon ab, ob
    /// teamId gerade mit X oder mit Y um einen Platz konkurriert - das kann bei jedem Aufruf anders liegen, ein
    /// pauschal "immer knapp" oder "immer extrem" für fremde Spiele ist daher in keine Richtung sicher korrekt
    /// (beide wurden ausprobiert und haben je einen der beiden unten stehenden Tests zum Scheitern gebracht).
    /// (AD: Issue #201/#202/#205, Funde aus manuellem #202-Test).</summary>
    private static int ExtremRangErmitteln(
        List<Spiel> abgeschlossen, List<Spiel> offen, int teamId,
        PunkteRegel punkteRegel, List<RankingRegel> rankingRegeln,
        bool teamGewinntAlles, bool bestenfalls)
    {
        var eigene = offen.Where(s => s.HeimMannschaftId == teamId || s.AuswaertsMannschaftId == teamId).ToList();
        var fremde = offen.Where(s => s.HeimMannschaftId != teamId && s.AuswaertsMannschaftId != teamId).ToList();

        var eigeneErgebnisse = eigene
            .Select(s => SpielMitSimuliertemErgebnis(
                s, heimSiegt: s.HeimMannschaftId == teamId ? teamGewinntAlles : !teamGewinntAlles, ExtremesTorergebnis))
            .ToList();

        int extremRang = bestenfalls ? int.MaxValue : int.MinValue;
        int kombinationen = 1 << (2 * fremde.Count); // 4 Varianten pro fremdem Spiel = 2 Bit pro Spiel im Maskenwort

        for (int mask = 0; mask < kombinationen; mask++)
        {
            var fremdeErgebnisse = fremde.Select((s, i) =>
            {
                int variante = (mask >> (2 * i)) & 0b11;
                bool heimSiegt = variante is 0 or 1;
                int torergebnis = variante is 0 or 2 ? KnappesTorergebnis : ExtremesTorergebnis;
                return SpielMitSimuliertemErgebnis(s, heimSiegt, torergebnis);
            }).ToList();

            var tabelle = TabelleAusSpielenBerechnen(
                [..abgeschlossen, ..eigeneErgebnisse, ..fremdeErgebnisse], punkteRegel, rankingRegeln);
            int rang = tabelle.FirstOrDefault(e => e.Mannschaft.Id == teamId)?.Rang ?? int.MaxValue;

            extremRang = bestenfalls ? Math.Min(extremRang, rang) : Math.Max(extremRang, rang);
        }

        return extremRang;
    }

    private const int KnappesTorergebnis  = 1;
    private const int ExtremesTorergebnis = 1000;

    private static Spiel SpielMitSimuliertemErgebnis(Spiel original, bool heimSiegt, int torergebnis) => new()
    {
        Spielnummer           = original.Spielnummer,
        HeimMannschaftId      = original.HeimMannschaftId,
        HeimMannschaft        = original.HeimMannschaft,
        AuswaertsMannschaftId = original.AuswaertsMannschaftId,
        AuswaertsMannschaft   = original.AuswaertsMannschaft,
        Status                = SpielStatus.Abgeschlossen,
        Ergebnis = new SpielErgebnis
        {
            HeimToreRegulaer      = heimSiegt ? torergebnis : 0,
            AuswaertsToreRegulaer = heimSiegt ? 0 : torergebnis,
            EntscheidungArt       = EntscheidungArt.RegulaereSpielzeit
        }
    };
}
