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
        var (runde, punkteRegel, rankingRegeln, alleSpiele) = await TabellenDatenLadenAsync(turnierRundeId, ct);
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

        var tabelle = TabellenRechner.TabelleBerechnen(spiele, punkteRegel, rankingRegeln, nurHeimspiele);

        // Issue #358: mögliche Ränge nur für den aktuellen Gesamtstand — Teilansichten und historische
        // Spieltage sind keine Wertung, aus der sich ein Weiterkommen ableiten ließe.
        if (ansicht == TabellenAnsicht.Gesamt && bisSpieltag is null)
            RangPrognose.LigaRaengeSetzen(tabelle, runde, alleSpiele, punkteRegel);
        return tabelle;
    }

    /// <summary>Berechnet die Gesamttabelle einer Liga-/TopfLiga-/Schweizer-System-Runde inkl.
    /// <see cref="TabellenEintrag.SicherPositioniert"/> (Issue #343) - sicher ist ein Rang, wenn bester und
    /// schlechtester möglicher Rang ihm gleich sind (siehe <see cref="RangPrognose.LigaRaengeSetzen"/>).</summary>
    public async Task<IReadOnlyList<TabellenEintrag>> LigaTabelleMitSicherheitBerechnenAsync(
        int turnierRundeId, CancellationToken ct = default)
    {
        var (runde, punkteRegel, rankingRegeln, alleSpiele) = await TabellenDatenLadenAsync(turnierRundeId, ct);
        if (!alleSpiele.Any()) return [];

        var tabelle = TabellenRechner.TabelleBerechnen(alleSpiele, punkteRegel, rankingRegeln);
        RangPrognose.LigaRaengeSetzen(tabelle, runde, alleSpiele, punkteRegel);
        RangPrognose.SicherPositionierteMarkieren(tabelle);
        return tabelle;
    }

    /// <summary>Berechnet je eine Tabelle pro Gruppe inkl. Qualifikationsstatus und <see cref="TabellenEintrag.SicherPositioniert"/>
    /// (Issue #202 - ob der exakte Rang durch kein Ergebnis der verbleibenden Spiele mehr verändert werden kann). Gruppen werden
    /// aus den Spielpaarungen rekonstruiert. Mit <paramref name="bisSpieltag"/> wird der historische Tabellenstand nach diesem
    /// Spieltag berechnet; die Qualifikationsstatus-Berechnung bezieht sich dabei weiterhin auf den vollständigen (aktuellen) Datenstand.</summary>
    public async Task<IReadOnlyList<GruppenTabelle>> GruppenTabellenBerechnenAsync(
        int turnierRundeId, int? bisSpieltag = null, CancellationToken ct = default)
    {
        var (runde, punkteRegel, rankingRegeln, alleSpiele) = await TabellenDatenLadenAsync(turnierRundeId, ct);
        if (!alleSpiele.Any()) return [];

        int qualPlaetze = runde.GruppenAnzahl is > 0
            ? Math.Max(1, runde.AusgangsMannschaftsanzahl / runde.GruppenAnzahl.Value)
            : 2;

        var result = TabellenRechner.GruppenErmitteln(alleSpiele).Select(g =>
        {
            var gruppenSpieleGefiltert = SpieleBisSpieltagFiltern(g.Spiele, bisSpieltag);
            var tabelle = TabellenRechner.TabelleBerechnen(
                gruppenSpieleGefiltert, punkteRegel, rankingRegeln, teilnehmer: TabellenRechner.AufgeloesteTeilnehmer(g.Spiele));
            return (Gruppe: new GruppenTabelle(g.Nummer, tabelle) { OffenePlaetze = TabellenRechner.OffenePlaetze(g.Spiele) },
                    g.Spiele);
        }).ToList();

        foreach (var (gruppe, spiele) in result)
        {
            // Issue #397: Solange ein Platz der Gruppe noch nicht aufgelöst ist, fehlen dessen Spiele in der
            // Status-Berechnung (Gegner unbekannt) — kein Rang darf dann als sicher gelten.
            if (gruppe.OffenePlaetze.Count > 0) continue;
            RangPrognose.GruppenRaengeSetzen(gruppe, spiele, qualPlaetze, punkteRegel, rankingRegeln);
        }

        return result.Select(r => r.Gruppe).ToList();
    }

    /// <summary>Berechnet die Rangfolge der Kandidaten an einer festen Platzierung (z.B. "Platz 3") über alle
    /// Gruppen der Quellrunde hinweg, sortiert nach denselben Ranking-Regeln wie innerhalb einer Gruppe (Issue #184).
    /// "Direkter Vergleich"-Kriterien liefern zwischen Teams aus verschiedenen Gruppen naturgemäß keinen Treffer
    /// (kein gemeinsames Spiel) und fallen automatisch auf das nächste Kriterium durch - kein Sonderfall nötig.</summary>
    public async Task<IReadOnlyList<TabellenEintrag>> UebergreifendeRangfolgeBerechnenAsync(
        int turnierRundeId, int platzierung, CancellationToken ct = default)
    {
        var (_, punkteRegel, rankingRegeln, alleSpiele) = await TabellenDatenLadenAsync(turnierRundeId, ct);
        if (!alleSpiele.Any()) return [];

        // Gesamttabelle über ALLE Teams der Runde - liefert korrekte Team-Stats (die Statistik filtert
        // ohnehin je Team auf dessen eigene Spiele) und die richtige Sortierreihenfolge in einem Aufwasch.
        var gesamtTabelle = TabellenRechner.TabelleBerechnen(alleSpiele, punkteRegel, rankingRegeln);

        var kandidatenIds = TabellenRechner.GruppenErmitteln(alleSpiele)
            .Select(g => TabellenRechner.TabelleBerechnen(g.Spiele, punkteRegel, rankingRegeln)
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

    // ── Laden ─────────────────────────────────────────────────────────────

    /// <summary>Alles, woraus die Tabellen einer Runde gerechnet werden - je Aufruf einmal geladen (#439).</summary>
    private sealed record TabellenDaten(
        TurnierRunde Runde, PunkteRegel PunkteRegel, IReadOnlyList<RankingRegel> RankingRegeln, IReadOnlyList<Spiel> Spiele);

    private async Task<TabellenDaten> TabellenDatenLadenAsync(int turnierRundeId, CancellationToken ct)
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

        return new TabellenDaten(runde, punkteRegel, rankingRegeln, alleSpiele);
    }
}
