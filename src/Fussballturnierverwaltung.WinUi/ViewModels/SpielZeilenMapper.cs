using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.WinUi.ViewModels;

internal static class SpielZeilenMapper
{
    /// <summary>Paarungsnummer (1-basiert) je Spiel-ID: Hinspiele bzw. Einzelspiele in Spielnummer-Reihenfolge,
    /// ein Rückspiel übernimmt die Nummer seines Hinspiels (Issue #346) — unabhängig von der Listenposition,
    /// die bei nach Spieltag sortierter Liste Hin- und Rückspiel nicht nebeneinander führt.</summary>
    internal static IReadOnlyDictionary<int, int> PaarungsNummern(IEnumerable<Spiel> spiele)
    {
        var liste = spiele.ToList();
        var nummern = liste.Where(s => !s.IstRueckspiel)
            .OrderBy(s => s.Spielnummer)
            .Select((s, i) => (s.Id, Nr: i + 1))
            .ToDictionary(x => x.Id, x => x.Nr);
        foreach (var rueck in liste.Where(s => s.IstRueckspiel).OrderBy(s => s.Spielnummer))
            nummern[rueck.Id] = rueck.HinspielId is int hinId && nummern.TryGetValue(hinId, out var nr)
                ? nr
                : nummern.Count + 1;
        return nummern;
    }

    internal static SpielZeile MapSpielZeile(
        Spiel s, TurnierRunde? runde, int paarungsNummer,
        IReadOnlyDictionary<int, int>? teamGruppeMap = null,
        IReadOnlyDictionary<int, (string? Heim, string? Auswaerts)>? vorschau = null,
        SpielErgebnis? hinspielErgebnis = null, bool istPaarungsStart = false)
    {
        string text = "-:-";
        string detail = "";
        string gesamt = "";

        if (s.Ergebnis is { } e)
        {
            text = $"{e.HeimToreGesamt}:{e.AuswaertsToreGesamt}";
            detail = SpielErgebnisFormatierer.DetailText(
                e.EntscheidungArt,
                e.HeimToreRegulaer, e.AuswaertsToreRegulaer,
                e.HeimToreVerlaengerung, e.AuswaertsToreVerlaengerung,
                e.HeimElfmetertore, e.AuswaertsElfmetertore);

            if (detail.Length == 0)
                detail = SpielErgebnisFormatierer.HalbzeitKlammer(
                    e.EntscheidungArt, e.HeimToreRegulaer, e.AuswaertsToreRegulaer,
                    e.HalbzeitHeimTore, e.HalbzeitAuswaertsTore);

            // Issue #340: unter dem Rückspiel eine eigene Zeile mit dem Gesamtergebnis der Paarung.
            if (hinspielErgebnis is not null)
                gesamt = SpielErgebnisFormatierer.RueckspielGesamtText(hinspielErgebnis, e);
        }

        // #397: gespeicherte Gruppe zuerst — die Team→Gruppe-Map ist nur noch Rückfall für Altdaten und kennt
        // Spiele mit Platzhalter-Heimteam nicht.
        int? gruppeNummer = s.GruppenNummer;
        if (gruppeNummer is null && teamGruppeMap is not null && s.HeimMannschaftId.HasValue
            && teamGruppeMap.TryGetValue(s.HeimMannschaftId.Value, out var gr))
            gruppeNummer = gr;

        (string? Heim, string? Auswaerts) vor = default;
        vorschau?.TryGetValue(s.Id, out vor);
        string? heimVorschau = s.HeimMannschaftId is null ? vor.Heim : null;
        string? auswaertsVorschau = s.AuswaertsMannschaftId is null ? vor.Auswaerts : null;

        return new SpielZeile
        {
            Id = s.Id,
            Spielnummer = s.Spielnummer,
            Spieltag = s.Spieltag,
            KoLabel = KoPositionsLabel(s, runde, paarungsNummer),
            HeimName = s.HeimMannschaft?.Name ?? (heimVorschau is not null ? $"({heimVorschau})" : null)
                       ?? s.HeimPlatzhalterName ?? "?",
            AuswaertsName = s.AuswaertsMannschaft?.Name ?? (auswaertsVorschau is not null ? $"({auswaertsVorschau})" : null)
                       ?? s.AuswaertsPlatzhalterName ?? "?",
            HeimWappen = TeilnehmerAnzeige.MannschaftsIcon(s.HeimMannschaft),
            AuswaertsWappen = TeilnehmerAnzeige.MannschaftsIcon(s.AuswaertsMannschaft),
            Status = s.Status,
            HatErgebnis = s.Ergebnis is not null,
            ErgebnisText = text,
            ErgebnisDetail = detail,
            GesamtText = gesamt,
            IstRueckspiel = s.IstRueckspiel,
            HinspielId = s.HinspielId,
            IstPlatzhalterSpiel = !s.HeimMannschaftId.HasValue || !s.AuswaertsMannschaftId.HasValue,
            HeimIstVorschau = heimVorschau is not null,
            AuswaertsIstVorschau = auswaertsVorschau is not null,
            GruppeNummer = gruppeNummer,
            IstPaarungsStart = istPaarungsStart
        };
    }

    internal static string? KoPositionsLabel(Spiel s, TurnierRunde? runde, int paarungsNummer)
    {
        if (runde is null) return null;
        bool mitRueck = runde.MitRueckspiel;
        return runde.Rundenart switch
        {
            Rundenart.Finale => FormatKoLabel("Finale", paarungsNummer, mitRueck, s.IstRueckspiel),
            Rundenart.SpielUmPlatzDrei => FormatKoLabel("3. Platz", paarungsNummer, mitRueck, s.IstRueckspiel),
            Rundenart.KoRunde => KoKuerzel(runde.EingangsMannschaftsanzahl) is { } k
                                          ? FormatKoLabel(k, paarungsNummer, mitRueck, s.IstRueckspiel) : null,
            _ => null
        };
    }

    private static string FormatKoLabel(string kuerzel, int paarungsNummer, bool mitRueckspiel, bool istRueckspiel) =>
        mitRueckspiel
            ? $"{kuerzel} {paarungsNummer} {(istRueckspiel ? "R" : "H")}"
            : $"{kuerzel} {paarungsNummer}";

    internal static string? KoKuerzel(int eingangTeams) => eingangTeams switch
    {
        4 => "HF",
        8 => "VF",
        16 => "AF",
        32 => "R16",
        64 => "R32",
        128 => "R64",
        _ => eingangTeams > 2 ? $"R{eingangTeams / 2}" : null
    };
}
