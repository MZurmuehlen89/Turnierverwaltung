using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

/// <summary>Baut aus den Spielen einer Runde die Zeilen der Spiellisten (Spielplan und Spielauswahl der
/// Simulation).</summary>
internal static class SpielZeilenMapper
{
    /// <summary>Zeilen in der gelieferten Reihenfolge.</summary>
    public static List<SpielZeile> Zeilen(IReadOnlyList<Spiel> spiele, TurnierRunde? runde)
    {
        var paarungsNummern = KoPaarungen.PaarungsNummern(spiele);
        return spiele.Select(s => Zeile(s, runde, paarungsNummern[s.Id])).ToList();
    }

    /// <summary>Zeilen des Spielplans: mit Gruppe und Platzhalter-Vorschau, K.-o.-Runden mit Hin-/Rückspiel
    /// nach Paarung statt nach Spielnummer — Hin- und Rückspiel stehen direkt untereinander, unter dem
    /// Rückspiel das Gesamtergebnis (Issue #340, Vorbild kicker.de-Paarungsansicht).</summary>
    public static List<SpielZeile> SpielplanZeilen(
        IReadOnlyList<Spiel> spiele, TurnierRunde? runde,
        IReadOnlyDictionary<int, int> teamGruppeMap,
        IReadOnlyDictionary<int, (string? Heim, string? Auswaerts)> vorschau)
    {
        var paarungsNummern = KoPaarungen.PaarungsNummern(spiele);
        bool nachPaarung = runde is not null && runde.MitRueckspiel && HinRueckspielWertung.IstKoRunde(runde);
        var reihenfolge = nachPaarung
            ? spiele.OrderBy(s => paarungsNummern[s.Id]).ThenBy(s => s.IstRueckspiel).ToList()
            : spiele;
        var hinspielErgebnisse = spiele
            .Where(s => !s.IstRueckspiel && s.Ergebnis is not null)
            .ToDictionary(s => s.Id, s => s.Ergebnis!);

        var zeilen = new List<SpielZeile>(spiele.Count);
        int? letztePaarung = null;
        foreach (var s in reihenfolge)
        {
            int paarung = paarungsNummern[s.Id];
            bool paarungsStart = nachPaarung && letztePaarung is not null && paarung != letztePaarung;
            letztePaarung = paarung;

            var hinspielErgebnis = nachPaarung && s.IstRueckspiel && s.HinspielId is int hinId
                ? hinspielErgebnisse.GetValueOrDefault(hinId)
                : null;
            zeilen.Add(Zeile(s, runde, paarung, teamGruppeMap, vorschau, hinspielErgebnis, paarungsStart));
        }
        return zeilen;
    }

    /// <summary>Zeilen der Mannschaftsansicht (#479): Spiele mehrerer Runden in der gelieferten Reihenfolge, jedes
    /// mit seiner Runde (<see cref="Spiel.TurnierRunde"/> muss geladen sein). K.-o.-Spiele tragen statt des
    /// Paarungs-Kürzels nur „Hinspiel“/„Rückspiel“ — die Paarungsnummer ergibt sich erst aus allen Spielen der
    /// Runde, und die Runde steht ohnehin daneben. Unter dem Rückspiel das Gesamtergebnis wie im Spielplan.</summary>
    public static List<SpielZeile> MannschaftsZeilen(IReadOnlyList<Spiel> spiele)
    {
        var hinspielErgebnisse = spiele
            .Where(s => !s.IstRueckspiel && s.Ergebnis is not null)
            .ToDictionary(s => s.Id, s => s.Ergebnis!);

        return spiele.Select(s =>
        {
            bool istKo = HinRueckspielWertung.IstKoRunde(s.TurnierRunde);
            var hinspielErgebnis = istKo && s.IstRueckspiel && s.HinspielId is int hinId
                ? hinspielErgebnisse.GetValueOrDefault(hinId)
                : null;
            return Zeile(s, s.TurnierRunde, paarungsNummer: 0, hinspielErgebnis: hinspielErgebnis,
                mannschaftsansicht: true);
        }).ToList();
    }

    private static SpielZeile Zeile(
        Spiel s, TurnierRunde? runde, int paarungsNummer,
        IReadOnlyDictionary<int, int>? teamGruppeMap = null,
        IReadOnlyDictionary<int, (string? Heim, string? Auswaerts)>? vorschau = null,
        SpielErgebnis? hinspielErgebnis = null, bool istPaarungsStart = false, bool mannschaftsansicht = false)
    {
        (string? Heim, string? Auswaerts) vor = default;
        vorschau?.TryGetValue(s.Id, out vor);
        string? heimVorschau = s.HeimMannschaftId is null ? vor.Heim : null;
        string? auswaertsVorschau = s.AuswaertsMannschaftId is null ? vor.Auswaerts : null;

        return new SpielZeile
        {
            Id = s.Id,
            Spielnummer = s.Spielnummer,
            Spieltag = s.Spieltag,
            KoLabel = mannschaftsansicht ? HinRueckLabel(s, runde) : KoSpielLabel.Fuer(s, runde, paarungsNummer),
            HeimName = s.HeimMannschaft?.Name ?? (heimVorschau is not null ? $"({heimVorschau})" : null)
                       ?? s.HeimPlatzhalterName ?? "?",
            AuswaertsName = s.AuswaertsMannschaft?.Name ?? (auswaertsVorschau is not null ? $"({auswaertsVorschau})" : null)
                       ?? s.AuswaertsPlatzhalterName ?? "?",
            HeimWappen = MannschaftsIcon.Fuer(s.HeimMannschaft),
            AuswaertsWappen = MannschaftsIcon.Fuer(s.AuswaertsMannschaft),
            Status = s.Status,
            HatErgebnis = s.Ergebnis is not null,
            ErgebnisText = s.Ergebnis is { } e ? $"{e.HeimToreGesamt}:{e.AuswaertsToreGesamt}" : "-:-",
            ErgebnisDetail = ErgebnisDetail(s.Ergebnis),
            // Issue #340: unter dem Rückspiel eine eigene Zeile mit dem Gesamtergebnis der Paarung.
            GesamtText = s.Ergebnis is not null && hinspielErgebnis is not null
                ? SpielErgebnisFormatierer.RueckspielGesamtText(hinspielErgebnis, s.Ergebnis)
                : "",
            IstRueckspiel = s.IstRueckspiel,
            HinspielId = s.HinspielId,
            IstPlatzhalterSpiel = !s.HeimMannschaftId.HasValue || !s.AuswaertsMannschaftId.HasValue,
            HeimIstVorschau = heimVorschau is not null,
            AuswaertsIstVorschau = auswaertsVorschau is not null,
            GruppeNummer = GruppenNummer(s, teamGruppeMap),
            IstPaarungsStart = istPaarungsStart,
            RundeId = s.TurnierRundeId,
            HeimMannschaftId = s.HeimMannschaftId,
            AuswaertsMannschaftId = s.AuswaertsMannschaftId,
            RundeAnzeige = mannschaftsansicht ? RundeAnzeige(runde, s.GruppenNummer) : ""
        };
    }

    /// <summary>K.-o.-Spiel der Mannschaftsansicht: „Hinspiel“/„Rückspiel“, Einzelspiel ohne Zusatz; Runden mit
    /// Spieltagen <c>null</c> (zeigen „ST n“).</summary>
    private static string? HinRueckLabel(Spiel s, TurnierRunde? runde) =>
        runde is null || !HinRueckspielWertung.IstKoRunde(runde) ? null
        : !runde.MitRueckspiel ? ""
        : s.IstRueckspiel ? "Rückspiel" : "Hinspiel";

    private static string RundeAnzeige(TurnierRunde? runde, int? gruppenNummer) =>
        (runde?.Name ?? "") + (gruppenNummer is int nr ? $" · Gr.{nr}" : "");

    /// <summary>Zusatz zum Ergebnis: wie es entschieden wurde (n.V., i.E.), sonst der Halbzeitstand.</summary>
    private static string ErgebnisDetail(SpielErgebnis? ergebnis)
    {
        if (ergebnis is not { } e) return "";

        string detail = SpielErgebnisFormatierer.DetailText(e);

        return detail.Length > 0
            ? detail
            : SpielErgebnisFormatierer.HalbzeitKlammer(
                e.EntscheidungArt, e.HeimToreRegulaer, e.AuswaertsToreRegulaer,
                e.HalbzeitHeimTore, e.HalbzeitAuswaertsTore);
    }

    // #397: gespeicherte Gruppe zuerst — die Team→Gruppe-Map ist nur noch Rückfall für Altdaten und kennt
    // Spiele mit Platzhalter-Heimteam nicht.
    private static int? GruppenNummer(Spiel s, IReadOnlyDictionary<int, int>? teamGruppeMap) =>
        s.GruppenNummer
        ?? (teamGruppeMap is not null && s.HeimMannschaftId is int heimId
            && teamGruppeMap.TryGetValue(heimId, out var gruppe)
                ? (int?)gruppe
                : null);
}
