using Fussballturnierverwaltung.Application.Spiele.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spiele;

/// <summary>
/// Sieger und endgültige Entscheidungsart eines Spiels aus den eingegebenen Toren — ohne Datenbank.
/// Im K.-o.-Rückspiel (Issue #346) entscheidet das Gesamtergebnis beider Spiele; bei Gleichstand — und nur mit
/// aktiver Regel — die Auswärtstore (AD-001), danach das Entscheidungsverfahren, dessen Tore (Verlängerung/Elfmeter)
/// allein im Rückspiel fallen.
/// </summary>
public static class SiegerErmittlung
{
    /// <param name="hinspielErgebnis">Nur im K.-o.-Rückspiel gesetzt, sonst <c>null</c> (Einzelspiel).</param>
    public static (int? Sieger, EntscheidungArt Art) Bestimmen(
        int heimId, int auswaertsId,
        ErgebnisErfassenEingabe e,
        SpielErgebnis? hinspielErgebnis,
        bool auswaertstorRegelAktiv) =>
        hinspielErgebnis is null
            ? (Einzelspiel(heimId, auswaertsId, e), e.EntscheidungArt)
            : Rueckspiel(heimId, auswaertsId, e, hinspielErgebnis, auswaertstorRegelAktiv);

    // Rückspiel: Heim = Team B (war Auswärts im Hinspiel), Auswärts = Team A (war Heim im Hinspiel).
    private static (int? Sieger, EntscheidungArt Art) Rueckspiel(
        int heimId, int auswaertsId,
        ErgebnisErfassenEingabe e,
        SpielErgebnis hinspielErgebnis,
        bool auswaertstorRegelAktiv)
    {
        int teamAHinspiel = HinRueckspielWertung.HinspielTore(hinspielErgebnis, heim: true);
        int teamBHinspiel = HinRueckspielWertung.HinspielTore(hinspielErgebnis, heim: false);

        if (Vorne(teamBHinspiel + e.HeimToreRegulaer, teamAHinspiel + e.AuswaertsToreRegulaer, heimId, auswaertsId)
            is { } gesamtSieger)
            return (gesamtSieger, EntscheidungArt.RegulaereSpielzeit);

        // Auswärtstore: Team B traf auswärts im Hinspiel, Team A auswärts im Rückspiel.
        if (auswaertstorRegelAktiv
            && Vorne(teamBHinspiel, e.AuswaertsToreRegulaer, heimId, auswaertsId) is { } auswaertstorSieger)
            return (auswaertstorSieger, EntscheidungArt.Auswaertstorregel);

        return (VerfahrenImRueckspiel(heimId, auswaertsId, e), e.EntscheidungArt);
    }

    // Da der Gesamtstand nach 90 Minuten ausgeglichen ist, entscheiden allein die Tore der Verlängerung bzw. des
    // Elfmeterschießens — nicht (wie bei einem Einzelspiel) Regulär- plus Verlängerungstore.
    private static int? VerfahrenImRueckspiel(int heimId, int auswaertsId, ErgebnisErfassenEingabe e) =>
        e.EntscheidungArt switch
        {
            EntscheidungArt.Verlaengerung or EntscheidungArt.GoldenGoal or EntscheidungArt.SilverGoal
                when e.HeimToreVerlaengerung is { } heimV && e.AuswaertsToreVerlaengerung is { } auswaertsV
                => Vorne(heimV, auswaertsV, heimId, auswaertsId),
            _ => ElfmeterOderLos(heimId, auswaertsId, e)
        };

    private static int? Einzelspiel(int heimId, int auswaertsId, ErgebnisErfassenEingabe e) =>
        e.EntscheidungArt switch
        {
            EntscheidungArt.RegulaereSpielzeit
                => Vorne(e.HeimToreRegulaer, e.AuswaertsToreRegulaer, heimId, auswaertsId),
            EntscheidungArt.Verlaengerung or EntscheidungArt.GoldenGoal or EntscheidungArt.SilverGoal
                when e.HeimToreVerlaengerung is { } heimV && e.AuswaertsToreVerlaengerung is { } auswaertsV
                // #391: torgleich gibt es keinen Sieger – bis dahin kam stillschweigend das Auswärtsteam weiter.
                => Vorne(e.HeimToreRegulaer + heimV, e.AuswaertsToreRegulaer + auswaertsV, heimId, auswaertsId),
            _ => ElfmeterOderLos(heimId, auswaertsId, e)
        };

    private static int? ElfmeterOderLos(int heimId, int auswaertsId, ErgebnisErfassenEingabe e) =>
        e.EntscheidungArt switch
        {
            EntscheidungArt.Elfmeterschiessen
                when e.HeimElfmetertore is { } heimE && e.AuswaertsElfmetertore is { } auswaertsE
                => heimE > auswaertsE ? heimId : auswaertsId,
            EntscheidungArt.Losentscheid or EntscheidungArt.Muenzwurf
                => LosSieger(heimId, auswaertsId, e),
            _ => null
        };

    /// <summary>Wer mit seinem Wert vorne liegt; <c>null</c> bei Gleichstand.</summary>
    private static int? Vorne(int heimWert, int auswaertsWert, int heimId, int auswaertsId) =>
        heimWert.CompareTo(auswaertsWert) switch
        {
            > 0 => heimId,
            < 0 => auswaertsId,
            _ => null
        };

    // Issue #383: bei Los/Münze geben die Tore den Sieger nicht her — ohne LosGewinnerHeim blieb er null,
    // und die K.-o.-Folgepaarung wurde nie aufgelöst.
    private static int? LosSieger(int heimId, int auswaertsId, ErgebnisErfassenEingabe e) => e.LosGewinnerHeim switch
    {
        true  => heimId,
        false => auswaertsId,
        null  => null
    };
}
