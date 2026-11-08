using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Tabelle;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Turniere;

/// <summary>Ermittelt den <see cref="Turnierausgang"/> (#527) ohne Datenbank. Entscheidend ist die Schlussrunde,
/// die letzte Runde ohne das Spiel um Platz 3: ein Finale kürt den Turniersieger, eine Liga, Liga mit Töpfen oder ein
/// Schweizer System den Meister. Endet ein Turnier anders (mehrere Gruppen, K.-o.-Runde mit mehreren Paarungen),
/// gibt es keinen Sieger.</summary>
public static class TurnierausgangBerechnung
{
    public static TurnierRunde? Schlussrunde(IEnumerable<TurnierRunde> runden)
        => runden.Where(r => r.Rundenart != Rundenart.SpielUmPlatzDrei).MaxBy(r => r.Reihenfolge);

    public static bool EndetMitTabelle(TurnierRunde schlussrunde)
        => schlussrunde.Rundenart is Rundenart.Liga or Rundenart.TopfLiga or Rundenart.SchweizerSystem;

    // Ältere Vorlagen führen das Finale als K.-o.-Runde mit zwei Teams.
    private static bool IstFinale(TurnierRunde runde)
        => runde.Rundenart == Rundenart.Finale
        || (runde.Rundenart == Rundenart.KoRunde && runde.EingangsMannschaftsanzahl == 2);

    // ── K.-o.: Finale, Spiel um Platz 3, Halbfinale ───────────────────────

    /// <summary>Platz 1 und 2 aus dem Finale; Platz 3 und 4 aus dem Spiel um Platz 3, ohne dieses teilen sich die
    /// Verlierer des Halbfinales Platz 3, sobald beide Halbfinals entschieden sind. Bis das Finale entschieden ist,
    /// stehen seine beiden Teams als <see cref="Turnierausgang.Finalisten"/> fest, sobald sie bekannt sind.</summary>
    public static Turnierausgang AusKoRunden(IReadOnlyList<TurnierRunde> runden, IReadOnlyList<Spiel> spiele)
    {
        if (Schlussrunde(runden) is not { } finale || !IstFinale(finale)) return Turnierausgang.Keiner;

        var finalspiel = Entscheidungsspiele(finale, spiele).FirstOrDefault();
        List<Platzierung> plaetze =
        [
            .. PlaetzeAusSpiel(finalspiel, 1, PlatzierungsGrund.FinaleGewonnen, PlatzierungsGrund.FinaleVerloren),
            .. PlaetzeDreiUndVier(runden, finale, spiele)
        ];
        var finalisten = plaetze.Exists(p => p.Platz == 1) ? [] : Finalisten(finalspiel);
        return new Turnierausgang(TurnierausgangArt.Finale, plaetze, finalisten);
    }

    private static IReadOnlyList<Platzierung> PlaetzeDreiUndVier(
        IReadOnlyList<TurnierRunde> runden, TurnierRunde finale, IReadOnlyList<Spiel> spiele)
    {
        if (runden.FirstOrDefault(r => r.Rundenart == Rundenart.SpielUmPlatzDrei) is { } platzDrei)
            return PlaetzeAusSpiel(Entscheidungsspiele(platzDrei, spiele).FirstOrDefault(), 3,
                PlatzierungsGrund.SpielUmPlatzDreiGewonnen, PlatzierungsGrund.SpielUmPlatzDreiVerloren);

        var halbfinale = Halbfinale(runden, finale, spiele);
        if (halbfinale is null || !HinRueckspielWertung.IstKoRunde(halbfinale)) return [];

        var paarungen = Entscheidungsspiele(halbfinale, spiele);
        if (paarungen.Count != 2) return [];

        var verlierer = paarungen.Select(s => Ausgang(s)?.Verlierer).ToList();
        return verlierer.TrueForAll(v => v is not null)
            ? [.. paarungen.Select((s, i) => new Platzierung(3, verlierer[i]!, PlatzierungsGrund.HalbfinaleVerloren, s.Ergebnis!.EntscheidungArt))]
            : [];
    }

    /// <summary>Die Runde, aus der das Finale seine Teams bekommt (Quellspiel des Finales); ohne Verknüpfung
    /// die Runde direkt davor. Eine weitere Runde zwischen Halbfinale und Finale (Relegation, Platzierungsspiele)
    /// würde sonst für das Halbfinale gehalten.</summary>
    private static TurnierRunde? Halbfinale(IReadOnlyList<TurnierRunde> runden, TurnierRunde finale, IReadOnlyList<Spiel> spiele)
    {
        var quellSpielId = spiele.Where(s => s.TurnierRundeId == finale.Id)
            .Select(s => s.HeimQuellSpielId ?? s.AuswaertsQuellSpielId)
            .FirstOrDefault(id => id is not null);
        if (quellSpielId is not null && spiele.FirstOrDefault(s => s.Id == quellSpielId) is { } quellSpiel)
            return runden.FirstOrDefault(r => r.Id == quellSpiel.TurnierRundeId);

        return runden
            .Where(r => r.Rundenart != Rundenart.SpielUmPlatzDrei && r.Reihenfolge < finale.Reihenfolge)
            .MaxBy(r => r.Reihenfolge);
    }

    private static IReadOnlyList<Platzierung> PlaetzeAusSpiel(
        Spiel? spiel, int platzDesSiegers, PlatzierungsGrund gewonnen, PlatzierungsGrund verloren)
    {
        if (spiel is null || Ausgang(spiel) is not { } ausgang) return [];
        var (sieger, verlierer) = ausgang;
        var art = spiel.Ergebnis!.EntscheidungArt;
        return [new Platzierung(platzDesSiegers, sieger, gewonnen, art), new Platzierung(platzDesSiegers + 1, verlierer, verloren, art)];
    }

    /// <summary>Sieger und Verlierer eines entschiedenen Spiels, sonst <c>null</c>.</summary>
    private static (Mannschaft Sieger, Mannschaft Verlierer)? Ausgang(Spiel spiel)
    {
        if (!TabellenRechner.HatErgebnis(spiel) || spiel.Ergebnis!.SiegerMannschaftId is not int siegerId
            || spiel.HeimMannschaft is not { } heim || spiel.AuswaertsMannschaft is not { } auswaerts)
            return null;
        return siegerId == heim.Id ? (heim, auswaerts) : (auswaerts, heim);
    }

    private static IReadOnlyList<Mannschaft> Finalisten(Spiel? finalspiel)
        => finalspiel is { HeimMannschaft: { } heim, AuswaertsMannschaft: { } auswaerts } ? [heim, auswaerts] : [];

    // Bei Hin- und Rückspiel trägt das Rückspiel den Gesamtsieger (#346).
    private static List<Spiel> Entscheidungsspiele(TurnierRunde runde, IReadOnlyList<Spiel> spiele)
        => KoPaarungen.EntscheidungsspieleJePaarung([.. spiele.Where(s => s.TurnierRundeId == runde.Id)]);

    // ── Tabelle: Meister ──────────────────────────────────────────────────

    /// <summary>Meister ist der Tabellenerste, sobald sein Platz sicher ist (<see cref="TabellenEintrag.SicherPositioniert"/>,
    /// auch vor dem letzten Spieltag). <paramref name="tabelle"/> ist die Gesamttabelle der Schlussrunde samt
    /// Sicherheit, <paramref name="rundenSpiele"/> sind deren Spiele.</summary>
    public static Turnierausgang AusTabelle(
        TurnierRunde schlussrunde, IReadOnlyList<TabellenEintrag> tabelle, IReadOnlyList<Spiel> rundenSpiele)
    {
        // Ohne ein einziges Ergebnis gälte jeder Rang als sicher, sobald kein Spiel mehr offen ist (kein Spielplan).
        if (!rundenSpiele.Any(TabellenRechner.HatErgebnis))
            return new Turnierausgang(TurnierausgangArt.Tabelle, [], []);

        var (offen, gesamt) = Spieltage(schlussrunde, rundenSpiele);
        var erster = tabelle.FirstOrDefault(e => e.Rang == 1);
        IReadOnlyList<Platzierung> plaetze = erster is { SicherPositioniert: true }
            ? [new Platzierung(1, erster.Mannschaft, PlatzierungsGrund.Meister)]
            : [];
        return new Turnierausgang(TurnierausgangArt.Tabelle, plaetze, [], erster?.Mannschaft, offen, gesamt);
    }

    /// <summary>Offene und alle Spieltage der Runde. Beim Schweizer System sind künftige Spieltage noch nicht
    /// angesetzt, sie zählen nach <see cref="RundenKonfiguration.AnzahlSpieltage"/> mit. Spiele ohne Spieltag zählen
    /// nicht (beides 0, wenn keines einen hat).</summary>
    private static (int Offen, int Gesamt) Spieltage(TurnierRunde runde, IReadOnlyList<Spiel> rundenSpiele)
    {
        rundenSpiele = [.. rundenSpiele.Where(s => s.Spieltag is not null)];
        int angesetzt = rundenSpiele.Select(s => s.Spieltag!.Value).DefaultIfEmpty().Max();
        int gesamt = runde.Rundenart == Rundenart.SchweizerSystem
            ? Math.Max(angesetzt, RundenKonfiguration.AusJson(runde.KonfigurationJson).AnzahlSpieltage ?? angesetzt)
            : angesetzt;
        int offenAngesetzt = rundenSpiele.Where(s => !TabellenRechner.HatErgebnis(s)).Select(s => s.Spieltag).Distinct().Count();
        return (offenAngesetzt + gesamt - angesetzt, gesamt);
    }
}
