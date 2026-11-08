using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Application.Ranglisten;
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
    IRegelwerkRepository regelwerkRepository,
    IRanglisteRepository ranglisteRepository,
    IAuslosungRepository auslosungRepository)
{
    /// <summary>Zonen der Runde aus ihren ausgehenden Übergangsregeln (<c>ZielRunde</c> geladen) und ihren
    /// Tabellenbereichen (Issue #420).</summary>
    public async Task<IReadOnlyList<TabellenZone>> ZonenErmittelnAsync(
        int turnierRundeId, IEnumerable<Uebergangsregel> ausgehendeRegeln, CancellationToken ct = default)
    {
        var bereiche = await rundeRepository.TabellenBereicheAsync(turnierRundeId, ct);
        return TabellenZonen.Ermitteln(ausgehendeRegeln, bereiche);
    }

    /// <summary>Berechnet die Tabelle einer Liga- oder Gruppenphase-Runde. Mit <paramref name="bisSpieltag"/> wird der
    /// historische Stand nach diesem Spieltag berechnet statt des aktuellen Stands.</summary>
    public async Task<IReadOnlyList<TabellenEintrag>> TabelleBerechnenAsync(
        int turnierRundeId, TabellenAnsicht ansicht = TabellenAnsicht.Gesamt, int? bisSpieltag = null, CancellationToken ct = default)
    {
        var daten = await TabellenDatenLadenAsync(turnierRundeId, ct);
        var tabelle = TabellenRechner.TabelleDerAnsichtBerechnen(
            TabellenRechner.BisSpieltag(daten.Spiele, bisSpieltag), ansicht, daten.Regeln, daten.Teilnehmer);

        // Issue #358: mögliche Ränge nur für den aktuellen Gesamtstand — Teilansichten und historische
        // Spieltage sind keine Wertung, aus der sich ein Weiterkommen ableiten ließe.
        if (ansicht == TabellenAnsicht.Gesamt && bisSpieltag is null)
            RangPrognose.LigaRaengeSetzen(tabelle, daten.Runde, daten.Spiele, daten.Regeln.PunkteRegel);
        return tabelle;
    }

    /// <summary>Berechnet die Gesamttabelle einer Liga-/TopfLiga-/Schweizer-System-Runde inkl.
    /// <see cref="TabellenEintrag.SicherPositioniert"/> (Issue #343) - sicher ist ein Rang, wenn bester und
    /// schlechtester möglicher Rang ihm gleich sind (siehe <see cref="RangPrognose.LigaRaengeSetzen"/>).</summary>
    public async Task<IReadOnlyList<TabellenEintrag>> LigaTabelleMitSicherheitBerechnenAsync(
        int turnierRundeId, CancellationToken ct = default)
    {
        var tabelle = await TabelleBerechnenAsync(turnierRundeId, ct: ct);
        RangPrognose.SicherPositionierteMarkieren(tabelle);
        return tabelle;
    }

    /// <summary>Berechnet je eine Tabelle pro Gruppe inkl. möglicher Ränge und <see cref="TabellenEintrag.SicherPositioniert"/>
    /// (siehe <see cref="RangPrognose.GruppenRaengeSetzen"/>). Mit <paramref name="bisSpieltag"/> wird der historische
    /// Tabellenstand nach diesem Spieltag berechnet — ohne die möglichen Ränge, die nur zum aktuellen Stand passen
    /// (#480, wie bei <see cref="TabelleBerechnenAsync"/>).</summary>
    public async Task<IReadOnlyList<GruppenTabelle>> GruppenTabellenBerechnenAsync(
        int turnierRundeId, int? bisSpieltag = null, CancellationToken ct = default)
    {
        var daten = await TabellenDatenLadenAsync(turnierRundeId, ct);

        return [.. GruppenRechner.GruppenErmitteln(daten.Spiele).Select(g =>
        {
            var gruppe = GruppenRechner.GruppenTabelleBerechnen(g.Nummer, g.Spiele, bisSpieltag, daten.Regeln);
            if (bisSpieltag is null)
                RangPrognose.GruppenRaengeSetzen(gruppe, g.Spiele, daten.Regeln);
            return gruppe;
        })];
    }

    /// <summary>Berechnet die Rangfolge der Kandidaten an einer festen Platzierung (z.B. "Platz 3") über alle
    /// Gruppen der Quellrunde hinweg (siehe <see cref="GruppenRechner.UebergreifendeRangfolgeBerechnen"/>). Mit
    /// <paramref name="bisSpieltag"/> zum selben historischen Stand wie die Gruppentabellen.</summary>
    public async Task<IReadOnlyList<TabellenEintrag>> UebergreifendeRangfolgeBerechnenAsync(
        int turnierRundeId, int platzierung, int? bisSpieltag = null, CancellationToken ct = default)
    {
        var daten = await TabellenDatenLadenAsync(turnierRundeId, ct);
        return GruppenRechner.UebergreifendeRangfolgeBerechnen(daten.Spiele, platzierung, bisSpieltag, daten.Regeln);
    }

    /// <summary>Tabellenplatz-Verlauf über die Spieltage (#480): bei einer Gruppenphase je Gruppe einer, sonst einer
    /// für die ganze Runde (siehe <see cref="TabellenVerlaufRechner"/>).</summary>
    public async Task<IReadOnlyList<TabellenVerlauf>> VerlaeufeBerechnenAsync(int turnierRundeId, CancellationToken ct = default)
    {
        var daten = await TabellenDatenLadenAsync(turnierRundeId, ct);
        if (daten.Runde.Rundenart != Rundenart.Gruppenphase)
            return [TabellenVerlaufRechner.Berechnen(gruppenNummer: null, daten.Spiele, daten.Regeln, daten.Teilnehmer)];

        return [.. GruppenRechner.GruppenErmitteln(daten.Spiele)
            .Select(g => TabellenVerlaufRechner.Berechnen(g.Nummer, g.Spiele, daten.Regeln))];
    }

    // ── Laden ─────────────────────────────────────────────────────────────

    /// <summary>Alles, woraus die Tabellen einer Runde gerechnet werden - je Aufruf einmal geladen (#439).
    /// <see cref="Teilnehmer"/> sind Teams, die auch ohne Spiel in der Tabelle stehen (#608).</summary>
    private sealed record TabellenDaten(
        TurnierRunde Runde, TabellenRegeln Regeln, IReadOnlyList<Spiel> Spiele, IReadOnlyList<Mannschaft> Teilnehmer);

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

        var ranglistenPlaetze = await RanglistenPlaetzeLadenAsync(runde, rankingRegeln, ct);
        var teilnehmer = await TeilnehmerOhneSpielLadenAsync(runde, ct);
        var regeln = new TabellenRegeln(
            punkteRegel, rankingRegeln, ranglistenPlaetze, regelwerk.RankingRegelSet?.DirektenVergleichWiederholen ?? false);
        return new TabellenDaten(runde, regeln, alleSpiele, teilnehmer);
    }

    /// <summary>Schweizer System (#608): Ein Team mit Freilos hat an diesem Spieltag keinen Spiel-Datensatz. Bis es
    /// zum ersten Mal angesetzt ist, kennt nur die abgeschlossene Auslosung es. Andere Rundenarten legen alle
    /// Spiele auf einmal an, dort genügen die Spiele.</summary>
    private async Task<IReadOnlyList<Mannschaft>> TeilnehmerOhneSpielLadenAsync(TurnierRunde runde, CancellationToken ct)
    {
        if (runde.Rundenart != Rundenart.SchweizerSystem) return [];
        return await auslosungRepository.AusgeloesteMannschaftenAsync(runde.TurnierInstanzId, runde.Id, ct) ?? [];
    }

    /// <summary>Plätze der Standard-Rangliste der Runde für das Kriterium <see cref="RankingRegelArt.Koeffizient"/>
    /// (Issue #354) — dieselbe Rangliste, nach der die Lostöpfe gebildet werden. Nur geladen, wenn das Regelset das
    /// Kriterium nutzt und die Runde eine Standard-Rangliste hat; sonst <c>null</c>.</summary>
    private async Task<IReadOnlyDictionary<int, int>?> RanglistenPlaetzeLadenAsync(
        TurnierRunde runde, IReadOnlyList<RankingRegel> rankingRegeln, CancellationToken ct)
    {
        if (runde.StandardRanglisteId is not int ranglisteId) return null;
        if (!rankingRegeln.Any(r => r.Regelart == RankingRegelArt.Koeffizient)) return null;
        return await ranglisteRepository.PlaetzeAsync(ranglisteId, ct);
    }
}
