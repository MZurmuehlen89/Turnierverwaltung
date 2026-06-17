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
    /// <summary>Berechnet die aktuelle Tabelle einer Liga- oder Gruppenphase-Runde.</summary>
    public async Task<IReadOnlyList<TabellenEintrag>> TabelleBerechnenAsync(
        int turnierRundeId, CancellationToken ct = default)
    {
        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct)
            ?? throw new InvalidOperationException($"Runde {turnierRundeId} nicht gefunden.");

        var regelwerk = await regelwerkRepository.FindenNachIdMitAllenRegelnAsync(runde.RegelwerkId, ct)
            ?? throw new InvalidOperationException($"Regelwerk {runde.RegelwerkId} nicht gefunden.");

        var alleSpiele = await spielRepository.AlleNachRundeMitEreignissenAsync(turnierRundeId, ct);
        if (!alleSpiele.Any()) return [];

        var punkteRegel = regelwerk.PunkteRegeln.FirstOrDefault()
            ?? new PunkteRegel { PunkteFuerSieg = 3, PunkteFuerUnentschieden = 1, PunkteFuerNiederlage = 0 };

        // Alle Teams aus dem Spielplan ableiten (Platzhalter-Spiele ohne reale Teams überspringen)
        var teamMap = alleSpiele
            .Where(s => s.HeimMannschaftId.HasValue && s.AuswaertsMannschaftId.HasValue)
            .SelectMany(s => new[]
            {
                (s.HeimMannschaftId!.Value,      s.HeimMannschaft!),
                (s.AuswaertsMannschaftId!.Value, s.AuswaertsMannschaft!)
            })
            .DistinctBy(t => t.Item1)
            .ToDictionary(t => t.Item1, t => t.Item2);

        // Nur abgeschlossene Spiele für die Statistik
        var abgeschlossene = alleSpiele
            .Where(s => s.Status == SpielStatus.Abgeschlossen && s.Ergebnis is not null)
            .ToList();

        // Rohdaten pro Team berechnen
        var stats = teamMap.Keys.ToDictionary(
            id => id,
            id => BerechneSpielerStats(id, abgeschlossene, punkteRegel));

        // Ranking-Regeln aus dem Regelset (geordnet)
        var rankingRegeln = regelwerk.RankingRegelSet?.Eintraege
            .OrderBy(e => e.Reihenfolge)
            .Select(e => e.RankingRegel)
            .ToList() ?? [];

        // Einträge erstellen, sortieren, Rang vergeben
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

    // ── Statistikberechnung ───────────────────────────────────────────────

    private static RohStats BerechneSpielerStats(
        int teamId, IReadOnlyList<Spiel> spiele, PunkteRegel punkteRegel)
    {
        int gespielt = 0, siege = 0, unentschieden = 0, niederlagen = 0;
        int tore = 0, gegentore = 0, auswaertsTore = 0, fairplay = 0;

        foreach (var s in spiele)
        {
            if (s.Ergebnis is null) continue;
            bool istHeim = s.HeimMannschaftId == teamId;

            if (!istHeim && s.AuswaertsMannschaftId != teamId) continue;

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
                // Nur direkte Duelle zwischen x und y
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
}
