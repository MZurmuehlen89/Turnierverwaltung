using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Turniere;
using Fussballturnierverwaltung.Domain.Entitaeten.Regelwerke;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
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

        var tabelle = TabelleAusSpielenBerechnen(spiele, punkteRegel, rankingRegeln, nurHeimspiele);

        // Issue #358: mögliche Ränge nur für den aktuellen Gesamtstand — Teilansichten und historische
        // Spieltage sind keine Wertung, aus der sich ein Weiterkommen ableiten ließe.
        if (ansicht == TabellenAnsicht.Gesamt && bisSpieltag is null)
        {
            var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct);
            RangSchrankenSetzen(tabelle, runde, alleSpiele, punkteRegel);
        }
        return tabelle;
    }

    /// <summary>Berechnet die Gesamttabelle einer Liga-/TopfLiga-/Schweizer-System-Runde inkl.
    /// <see cref="TabellenEintrag.SicherPositioniert"/> (Issue #343, Liga-Gegenstück zu #202). Anders als bei
    /// <see cref="GruppenTabellenBerechnenAsync"/> wird nicht jede Ergebniskombination der offenen Spiele
    /// durchgerechnet (6^n ist bei z. B. 18 offenen Spielen je Ligaphasen-Spieltag nicht berechenbar), sondern
    /// konservativ über Punkte-Schranken: bester Rang = 1 + Anzahl Teams, die selbst im schlechtesten Fall mehr
    /// Punkte haben als das Team im besten Fall; schlechtester Rang = 1 + Anzahl Teams, die im besten Fall
    /// mindestens so viele Punkte erreichen können wie das Team im schlechtesten Fall (Punktgleichheit zählt
    /// als "kann vorbeiziehen", da Tiebreaker bei offenen Spielen nicht vorhersagbar sind). Sicher ⇔ beide
    /// gleich. Kann einen tatsächlich schon feststehenden Rang übersehen, meldet aber nie fälschlich "sicher".</summary>
    public async Task<IReadOnlyList<TabellenEintrag>> LigaTabelleMitSicherheitBerechnenAsync(
        int turnierRundeId, CancellationToken ct = default)
    {
        var (punkteRegel, rankingRegeln, alleSpiele) = await LadeTabellenDatenAsync(turnierRundeId, ct);
        if (!alleSpiele.Any()) return [];

        var tabelle = TabelleAusSpielenBerechnen(alleSpiele, punkteRegel, rankingRegeln);
        var runde = await rundeRepository.FindenNachIdMitRegelwerkAsync(turnierRundeId, ct);
        RangSchrankenSetzen(tabelle, runde, alleSpiele, punkteRegel);
        foreach (var eintrag in tabelle)
            eintrag.SicherPositioniert = eintrag.BesterRang == eintrag.Rang && eintrag.SchlechtesterRang == eintrag.Rang;
        return tabelle;
    }

    /// <summary>Setzt <see cref="TabellenEintrag.BesterRang"/>/<see cref="TabellenEintrag.SchlechtesterRang"/>
    /// einer Liga-Tabelle über die Punkte-Schranken (siehe <see cref="LigaTabelleMitSicherheitBerechnenAsync"/>).
    /// Bleibt aus, solange der Rest der Runde unbekannt ist (noch unaufgelöste Platzhalter-Spiele, Schweizer
    /// System ohne Spieltag-Anzahl).</summary>
    private static void RangSchrankenSetzen(
        IReadOnlyList<TabellenEintrag> tabelle, TurnierRunde? runde, IReadOnlyList<Spiel> alleSpiele, PunkteRegel punkteRegel)
    {
        var verbleibend = VerbleibendeSpieleProTeam(runde, alleSpiele, tabelle);
        if (verbleibend is null) return;

        if (verbleibend.Values.All(n => n == 0))
        {
            // alles gespielt - jeder Rang ist final
            foreach (var e in tabelle) e.BesterRang = e.SchlechtesterRang = e.Rang;
            return;
        }

        int maxProSpiel = Math.Max(punkteRegel.PunkteFuerSieg, Math.Max(punkteRegel.PunkteFuerUnentschieden, punkteRegel.PunkteFuerNiederlage));
        int minProSpiel = Math.Min(punkteRegel.PunkteFuerSieg, Math.Min(punkteRegel.PunkteFuerUnentschieden, punkteRegel.PunkteFuerNiederlage));
        var schranken = tabelle.ToDictionary(
            e => e.Mannschaft.Id,
            e => (Min: e.Punkte + verbleibend[e.Mannschaft.Id] * minProSpiel,
                  Max: e.Punkte + verbleibend[e.Mannschaft.Id] * maxProSpiel));

        foreach (var eintrag in tabelle)
        {
            var (eigenMin, eigenMax) = schranken[eintrag.Mannschaft.Id];
            var andere = schranken.Where(kv => kv.Key != eintrag.Mannschaft.Id).Select(kv => kv.Value).ToList();
            eintrag.BesterRang        = 1 + andere.Count(s => s.Min > eigenMax);
            eintrag.SchlechtesterRang = 1 + andere.Count(s => s.Max >= eigenMin);
        }
    }

    /// <summary>Anzahl noch ausstehender Spiele je Team, oder <c>null</c> wenn diese nicht zuverlässig bestimmbar
    /// ist. Schweizer System: künftige Spieltage sind noch nicht generiert, daher kommen zu den angesetzten offenen
    /// Spielen (<see cref="RundenKonfiguration.AnzahlSpieltage"/> − höchster generierter Spieltag) weitere hinzu
    /// (je Spieltag höchstens ein Spiel pro Team; ein Freilos wird dabei konservativ wie ein Spiel gezählt).</summary>
    private static Dictionary<int, int>? VerbleibendeSpieleProTeam(
        TurnierRunde? runde, IReadOnlyList<Spiel> alleSpiele, IReadOnlyList<TabellenEintrag> tabelle)
    {
        if (alleSpiele.Any(s => s.HeimMannschaftId is null || s.AuswaertsMannschaftId is null)) return null;

        var offen = alleSpiele.Where(s => s.Status != SpielStatus.Abgeschlossen || s.Ergebnis is null).ToList();
        var ergebnis = tabelle.ToDictionary(
            e => e.Mannschaft.Id,
            e => offen.Count(s => s.HeimMannschaftId == e.Mannschaft.Id || s.AuswaertsMannschaftId == e.Mannschaft.Id));

        if (runde?.Rundenart == Rundenart.SchweizerSystem)
        {
            if (RundenKonfiguration.AusJson(runde.KonfigurationJson).AnzahlSpieltage is not { } anzahlSpieltage) return null;
            int generiert = alleSpiele.Select(s => s.Spieltag ?? 0).DefaultIfEmpty().Max();
            int ausstehend = Math.Max(0, anzahlSpieltage - generiert);
            foreach (var id in ergebnis.Keys) ergebnis[id] += ausstehend;
        }
        return ergebnis;
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

        var result = GruppenErmitteln(alleSpiele).Select(g =>
        {
            var gruppenSpieleGefiltert = SpieleBisSpieltagFiltern(g.Spiele, bisSpieltag);
            var tabelle = TabelleAusSpielenBerechnen(
                gruppenSpieleGefiltert, punkteRegel, rankingRegeln, teilnehmer: AufgeloesteTeilnehmer(g.Spiele));
            return (Gruppe: new GruppenTabelle(g.Nummer, tabelle) { OffenePlaetze = OffenePlaetze(g.Spiele) },
                    g.Spiele);
        }).ToList();

        foreach (var (gruppe, spiele) in result)
        {
            // Issue #397: Solange ein Platz der Gruppe noch nicht aufgelöst ist, fehlen dessen Spiele in der
            // Status-Berechnung (Gegner unbekannt) — kein Rang darf dann als sicher gelten.
            if (gruppe.OffenePlaetze.Count > 0) continue;
            QualifikationsstatusSetzen(gruppe, spiele, qualPlaetze, punkteRegel, rankingRegeln);
        }

        return result.Select(r => r.Gruppe).ToList();
    }

    /// <summary>Gruppen einer Gruppenphase mit ihren Spielen. Seit Issue #397 aus der gespeicherten
    /// <see cref="Spiel.GruppenNummer"/> — auch mit noch offenen Platzhaltern vollständig und mit der
    /// ausgelosten Nummer. Rückfall für vor #397 generierte Spiele: Rekonstruktion aus den Paarungen.</summary>
    private static List<(int Nummer, IReadOnlyList<Spiel> Spiele)> GruppenErmitteln(IReadOnlyList<Spiel> alleSpiele)
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
    private static IReadOnlyList<Mannschaft> AufgeloesteTeilnehmer(IEnumerable<Spiel> spiele) =>
        [.. spiele
            .SelectMany(s => new[] { s.HeimMannschaft, s.AuswaertsMannschaft })
            .Where(m => m is not null)
            .Select(m => m!)
            .DistinctBy(m => m.Id)];

    private static IReadOnlyList<string> OffenePlaetze(IEnumerable<Spiel> spiele) =>
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

    /// <summary>Berechnet die Rangfolge der Kandidaten an einer festen Platzierung (z.B. "Platz 3") über alle
    /// Gruppen der Quellrunde hinweg, sortiert nach denselben Ranking-Regeln wie innerhalb einer Gruppe (Issue #184).
    /// "Direkter Vergleich"-Kriterien liefern zwischen Teams aus verschiedenen Gruppen naturgemäß keinen Treffer
    /// (kein gemeinsames Spiel) und fallen automatisch auf das nächste Kriterium durch - kein Sonderfall nötig.</summary>
    public async Task<IReadOnlyList<TabellenEintrag>> UebergreifendeRangfolgeBerechnenAsync(
        int turnierRundeId, int platzierung, CancellationToken ct = default)
    {
        var (punkteRegel, rankingRegeln, alleSpiele) = await LadeTabellenDatenAsync(turnierRundeId, ct);
        if (!alleSpiele.Any()) return [];

        // Gesamttabelle über ALLE Teams der Runde - liefert korrekte Team-Stats (BerechneSpielerStats filtert
        // ohnehin je Team auf dessen eigene Spiele) und die richtige Sortierreihenfolge in einem Aufwasch.
        var gesamtTabelle = TabelleAusSpielenBerechnen(alleSpiele, punkteRegel, rankingRegeln);

        var kandidatenIds = GruppenErmitteln(alleSpiele)
            .Select(g => TabelleAusSpielenBerechnen(g.Spiele, punkteRegel, rankingRegeln)
                .FirstOrDefault(e => e.Rang == platzierung)?.Mannschaft.Id)
            .Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();

        var ergebnis = gesamtTabelle.Where(e => kandidatenIds.Contains(e.Mannschaft.Id)).ToList();
        for (int i = 0; i < ergebnis.Count; i++) ergebnis[i].Rang = i + 1;
        return ergebnis;
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

        // AD-026: Rundenregelwerk überschreibt das Standard-Regelwerk der Instanz, fehlt es, gilt dieses.
        var regelwerkId = runde.RegelwerkId
            ?? await rundeRepository.StandardRegelwerkIdDerInstanzAsync(runde.TurnierInstanzId, ct)
            ?? throw new InvalidOperationException(
                $"Runde '{runde.Name}' hat kein Regelwerk zugewiesen, und das Turnier hat kein Standard-Regelwerk.");
        var regelwerk = await regelwerkRepository.FindenNachIdMitAllenRegelnAsync(regelwerkId, ct)
            ?? throw new InvalidOperationException($"Regelwerk {regelwerkId} nicht gefunden.");

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
                e.BesterRang = e.SchlechtesterRang = e.Rang;
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
            eintrag.BesterRang = bestRang;
            eintrag.SchlechtesterRang = worstRang;
        }
    }

    /// <summary>Ermittelt den best- bzw. schlechtestmöglichen Rang von <paramref name="teamId"/>: die eigenen offenen
    /// Spiele sind gemäß <paramref name="teamGewinntAlles"/> fixiert und werden mit einem extremen statt einem
    /// knappen Ergebnis simuliert - für das eigene Spiel gibt es nur zwei Beteiligte (das Team und den Gegner),
    /// ein extremeres Ergebnis wirkt daher immer eindeutig in dieselbe Richtung (nie schwächer).
    ///
    /// Für alle fremden offenen Spiele (die <paramref name="teamId"/> nicht betreffen) wird per Brute-Force jede
    /// Kombination aus Ausgang (Heim-/Auswärtssieg/Unentschieden) UND Tordifferenz (knapp/extrem) durchgerechnet -
    /// macht pro fremdem Spiel 6 statt 2 Varianten, insgesamt 6^n statt 2^n Kombinationen. Notwendig, weil ein
    /// fremdes Spiel einen "Umweg" über einen dritten Verein nimmt: Team X gewinnt gegen Team Y - ein knapper Sieg
    /// schadet Y kaum, ein extremer Sieg zertrümmert Ys Tordifferenz. Ob das für <paramref name="teamId"/> gut oder
    /// schlecht ist, hängt davon ab, ob teamId gerade mit X oder mit Y um einen Platz konkurriert - das kann bei
    /// jedem Aufruf anders liegen, ein pauschal "immer knapp" oder "immer extrem" für fremde Spiele ist daher in
    /// keine Richtung sicher korrekt (beide wurden ausprobiert und haben je einen der beiden unten stehenden Tests
    /// zum Scheitern gebracht). Unentschieden ist ein eigenständiger, nicht auf einen "Sieger" reduzierbarer
    /// Ausgang und fehlte ursprünglich komplett in der Brute-Force (nur 4 Sieg-Varianten) - dadurch konnte ein Team
    /// fälschlich als SicherPositioniert gelten, obwohl erst ein Unentschieden im fremden Spiel diesen Rang absichert
    /// oder gefährdet hätte (Fund aus manuellem #206-Test: Rang 2 einer Gruppe wurde vorschnell aufgelöst, obwohl
    /// nur ein Sieg in beide Richtungen, nicht aber ein Unentschieden im offenen Fremdspiel geprüft wurde).
    /// (AD: Issue #201/#202/#205/#206, Funde aus manuellem Test).</summary>
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
        int kombinationen = (int)Math.Pow(_fremdSpielVariantenAnzahl, fremde.Count);

        for (int kombi = 0; kombi < kombinationen; kombi++)
        {
            var fremdeErgebnisse = new List<Spiel>(fremde.Count);
            int rest = kombi;
            foreach (var s in fremde)
            {
                int variante = rest % _fremdSpielVariantenAnzahl;
                rest /= _fremdSpielVariantenAnzahl;
                fremdeErgebnisse.Add(variante switch
                {
                    0 => SpielMitSimuliertemErgebnis(s, heimSiegt: true,  KnappesTorergebnis),
                    1 => SpielMitSimuliertemErgebnis(s, heimSiegt: true,  ExtremesTorergebnis),
                    2 => SpielMitSimuliertemErgebnis(s, heimSiegt: false, KnappesTorergebnis),
                    3 => SpielMitSimuliertemErgebnis(s, heimSiegt: false, ExtremesTorergebnis),
                    4 => SpielMitUnentschieden(s, UnentschiedenKnapp),
                    _ => SpielMitUnentschieden(s, UnentschiedenExtrem)
                });
            }

            var tabelle = TabelleAusSpielenBerechnen(
                [..abgeschlossen, ..eigeneErgebnisse, ..fremdeErgebnisse], punkteRegel, rankingRegeln);
            int rang = tabelle.FirstOrDefault(e => e.Mannschaft.Id == teamId)?.Rang ?? int.MaxValue;

            extremRang = bestenfalls ? Math.Min(extremRang, rang) : Math.Max(extremRang, rang);
        }

        return extremRang;
    }

    // 6 Varianten pro fremdem Spiel: Heim-/Auswärtssieg × knapp/extrem, plus Unentschieden × knapp/extrem.
    private const int _fremdSpielVariantenAnzahl = 6;

    private const int KnappesTorergebnis    = 1;
    private const int ExtremesTorergebnis   = 1000;
    private const int UnentschiedenKnapp    = 0;
    private const int UnentschiedenExtrem   = 500;

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

    private static Spiel SpielMitUnentschieden(Spiel original, int tore) => new()
    {
        Spielnummer           = original.Spielnummer,
        HeimMannschaftId      = original.HeimMannschaftId,
        HeimMannschaft        = original.HeimMannschaft,
        AuswaertsMannschaftId = original.AuswaertsMannschaftId,
        AuswaertsMannschaft   = original.AuswaertsMannschaft,
        Status                = SpielStatus.Abgeschlossen,
        Ergebnis = new SpielErgebnis
        {
            HeimToreRegulaer      = tore,
            AuswaertsToreRegulaer = tore,
            EntscheidungArt       = EntscheidungArt.RegulaereSpielzeit
        }
    };
}
