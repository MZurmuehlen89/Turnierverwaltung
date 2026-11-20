using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Spiele;

/// <summary>Was über ein Spiel feststeht, bevor sein Ergebnis eingegeben wird. Die Hinspiel-Tore (aus Sicht des
/// Hinspiels) gibt es nur im K.-o.-Rückspiel mit bereits eingetragenem Hinspiel (Issue #346).</summary>
public sealed record ErgebnisRahmen(
    bool IstKoRunde,
    bool IstKoHinspiel,
    (int Heim, int Auswaerts)? HinspielTore,
    bool AuswaertstorRegelAktiv,
    IReadOnlyList<VerfahrenArt> ErlaubteVerfahren)
{
    public static ErgebnisRahmen Leer { get; } = new(false, false, null, false, []);
}

/// <summary>Eingegebene Spielstände; der Stand nach Verlängerung enthält die Tore der regulären Spielzeit.
/// <c>null</c> = noch nicht eingegeben. Die Wiederholungsspiel-Felder (#547) folgen derselben Regel: der Stand nach
/// Verlängerung des Wiederholungsspiels enthält dessen Tore nach 90 Minuten; alle aus Sicht des Heimteams des Spiels.</summary>
public readonly record struct TorEingabe(
    int? HeimRegulaer, int? AuswaertsRegulaer, int? HeimNachVerlaengerung, int? AuswaertsNachVerlaengerung,
    int? HeimWiederholung = null, int? AuswaertsWiederholung = null,
    int? HeimWiederholungNachVerlaengerung = null, int? AuswaertsWiederholungNachVerlaengerung = null);

/// <summary>Leitet während der Ergebniseingabe aus den bisher eingegebenen Toren ab, welche Abschnitte
/// (Verlängerung, Elfmeterschießen, Losentscheid) fällig sind, ob die Eingabe in sich stimmig ist und wie das Spiel
/// damit entschieden wird — ohne Datenbank.</summary>
public static class ErgebnisEntscheidung
{
    /// <summary>Ob nach der regulären Spielzeit ein Entscheidungsverfahren fällig ist: im Einzelspiel bei
    /// Remis, im K.-o.-Hinspiel nie, im K.-o.-Rückspiel bei ausgeglichenem Gesamtergebnis (Issue #346).</summary>
    public static bool VerfahrenNoetig(ErgebnisRahmen rahmen, TorEingabe tore)
    {
        if (!rahmen.IstKoRunde || rahmen.IstKoHinspiel) return false;
        if (tore.HeimRegulaer is not { } heim || tore.AuswaertsRegulaer is not { } auswaerts) return false;

        return rahmen.HinspielTore is { } hin
            ? HinRueckspielWertung.EntscheidungsverfahrenNoetig(
                hin.Heim, hin.Auswaerts, heim, auswaerts, rahmen.AuswaertstorRegelAktiv)
            : heim == auswaerts;
    }

    public static bool VerlaengerungAktiv(ErgebnisRahmen rahmen, TorEingabe tore) =>
        VerlaengerungsVerfahren(rahmen) is not null && VerfahrenNoetig(rahmen, tore);

    /// <summary>Elfmeterschießen ist fällig, wenn es ohne Verlängerung direkt folgt oder die Verlängerung
    /// vollständig eingegeben ist und nichts entschieden hat.</summary>
    public static bool ElfmeterAktiv(ErgebnisRahmen rahmen, TorEingabe tore) =>
        FolgeArtFaellig(rahmen, tore) == EntscheidungArt.Elfmeterschiessen;

    /// <summary>Losentscheid bzw. Münzwurf, wenn er an derselben Stelle fällig ist wie sonst das Elfmeterschießen
    /// (#391), sonst <c>null</c>. Steht ein Wiederholungsspiel davor, ist das Los erst fällig, wenn auch dieses
    /// torgleich endete (#547).</summary>
    public static EntscheidungArt? LosArt(ErgebnisRahmen rahmen, TorEingabe tore) =>
        FolgeArtFaellig(rahmen, tore) is { } art && art is EntscheidungArt.Losentscheid or EntscheidungArt.Muenzwurf
            ? art
            : null;

    /// <summary>#547: Ob die Tore eines Wiederholungsspiels einzugeben sind – es steht im Regelwerk an der Stelle, an der
    /// sonst das Entscheidungsverfahren fällig wäre (nach der Verlängerung, wenn sie torgleich endete).</summary>
    public static bool WiederholungAktiv(ErgebnisRahmen rahmen, TorEingabe tore) =>
        FolgeArtFaellig(rahmen, tore with { HeimWiederholung = null, AuswaertsWiederholung = null })
            == EntscheidungArt.Wiederholungsspiel;

    /// <summary>#547: Ob das Wiederholungsspiel nach 90 Minuten torgleich stand und – steht eine Verlängerung vor ihm im
    /// Regelwerk – eine eigene Verlängerung einzugeben ist.</summary>
    public static bool WiederholungVerlaengerungAktiv(ErgebnisRahmen rahmen, TorEingabe tore) =>
        WiederholungAktiv(rahmen, tore) && VerlaengerungVorWiederholung(rahmen) is not null
        && tore.HeimWiederholung is { } heim && tore.AuswaertsWiederholung == heim;

    /// <summary>#547: Das eingegebene Wiederholungsspiel als Ergebnis (Verlängerungstore = Stand nach Verlängerung minus
    /// Stand nach 90 Minuten), oder <c>null</c>, solange es nicht fällig oder nicht vollständig eingegeben ist.</summary>
    public static Wiederholungsergebnis? Wiederholung(ErgebnisRahmen rahmen, TorEingabe tore)
    {
        if (!WiederholungAktiv(rahmen, tore)
            || tore.HeimWiederholung is not { } heim || tore.AuswaertsWiederholung is not { } auswaerts)
            return null;

        if (!WiederholungVerlaengerungAktiv(rahmen, tore))
            return new Wiederholungsergebnis(heim, auswaerts, null, null);

        return tore.HeimWiederholungNachVerlaengerung is { } heimNach
            && tore.AuswaertsWiederholungNachVerlaengerung is { } auswaertsNach
            ? new Wiederholungsergebnis(heim, auswaerts, heimNach - heim, auswaertsNach - auswaerts)
            : null;
    }

    /// <summary>#547: Endete das Wiederholungsspiel torgleich und sieht das Regelwerk danach nichts mehr vor, bliebe das
    /// Spiel ohne Sieger – das lehnt der Validator beim Speichern ab, hier schon beim Tippen.</summary>
    public static string? WiederholungFehler(ErgebnisRahmen rahmen, TorEingabe tore) =>
        Wiederholung(rahmen, tore) is { } wdh && wdh.HeimGesamt == wdh.AuswaertsGesamt && FolgeArt(rahmen, tore) is null
            ? "Das Wiederholungsspiel endete torgleich, das Regelwerk sieht danach keine Entscheidung vor."
            : null;

    /// <summary>Silver Goal: ob gefragt wird, ob die Verlängerung schon nach ihrer ersten Hälfte entschieden war
    /// (#391) — nur bei einem stimmigen Sieg in der Verlängerung.</summary>
    public static bool SilverGoalWaehlbar(ErgebnisRahmen rahmen, TorEingabe tore) =>
        VerlaengerungAktiv(rahmen, tore)
        && VerlaengerungsVerfahren(rahmen) == VerfahrenArt.SilverGoal
        && VerlaengerungsTore(tore) is { } verlaengerung && verlaengerung.Heim != verlaengerung.Auswaerts
        && VerlaengerungFehler(rahmen, tore) is null;

    /// <summary>Was an der eingegebenen Verlängerung nicht stimmen kann, sonst <c>null</c> — zum Anzeigen beim
    /// Tippen (#391): Der Stand fällt nicht unter den nach 90 Minuten, und beim Golden Goal endet die Verlängerung
    /// mit dem ersten Tor.</summary>
    public static string? VerlaengerungFehler(ErgebnisRahmen rahmen, TorEingabe tore)
    {
        if (!VerlaengerungAktiv(rahmen, tore) || VerlaengerungsTore(tore) is not { } verlaengerung) return null;

        if (verlaengerung.Heim < 0 || verlaengerung.Auswaerts < 0)
            return "Der Stand nach Verlängerung kann nicht unter dem Stand nach 90 Minuten liegen.";
        if (VerlaengerungsVerfahren(rahmen) == VerfahrenArt.GoldenGoal && verlaengerung.Heim + verlaengerung.Auswaerts > 1)
            return "Golden Goal: Die Verlängerung endet mit dem ersten Tor, in ihr fällt höchstens ein Tor.";
        // Ohne Folgeverfahren im Regelwerk bliebe das Spiel ohne Sieger – das lehnt der Validator beim Speichern ab.
        if (verlaengerung.Heim == verlaengerung.Auswaerts && FolgeArt(rahmen, default) is null)
            return "Die Verlängerung endete torgleich, das Regelwerk sieht danach keine Entscheidung vor.";
        return null;
    }

    /// <summary>Entscheidungsart, die zu den eingegebenen Toren gehört. Solange die Verlängerung noch nicht
    /// vollständig eingegeben ist, gilt sie als die entscheidende. Unter Silver Goal sagt der Haken, ob die
    /// Verlängerung schon nach ihrer ersten Hälfte endete (#391).</summary>
    public static EntscheidungArt Art(ErgebnisRahmen rahmen, TorEingabe tore, bool silverGoalNachErsterHaelfte = false)
    {
        if (!VerfahrenNoetig(rahmen, tore)) return EntscheidungArt.RegulaereSpielzeit;
        if (FolgeArtFaellig(rahmen, tore) is { } folge) return folge;

        return VerlaengerungsVerfahren(rahmen) switch
        {
            null                                                     => EntscheidungArt.RegulaereSpielzeit,
            VerfahrenArt.GoldenGoal                                  => EntscheidungArt.GoldenGoal,
            VerfahrenArt.SilverGoal when silverGoalNachErsterHaelfte => EntscheidungArt.SilverGoal,
            _                                                        => EntscheidungArt.Verlaengerung,
        };
    }

    /// <summary>Halbzeitstand für ein erneut gespeichertes Ergebnis (#391): Die Eingabe hat dafür kein Feld mehr.
    /// Der gespeicherte bleibt, solange der Stand nach 90 Minuten derselbe ist — sonst passt er nicht mehr sicher
    /// zum Spiel und wird verworfen.</summary>
    public static (int? Heim, int? Auswaerts) HalbzeitstandBehalten(
        SpielErgebnis? gespeichert, int heimRegulaer, int auswaertsRegulaer) =>
        gespeichert is not null && gespeichert.HeimToreRegulaer == heimRegulaer
                                && gespeichert.AuswaertsToreRegulaer == auswaertsRegulaer
            ? (gespeichert.HalbzeitHeimTore, gespeichert.HalbzeitAuswaertsTore)
            : (null, null);

    /// <summary>Das Verfahren nach der Verlängerung (ohne sie direkt nach 90 Minuten), sobald es fällig ist: die
    /// Verlängerung ist vollständig eingegeben und hat nichts entschieden.</summary>
    private static EntscheidungArt? FolgeArtFaellig(ErgebnisRahmen rahmen, TorEingabe tore)
    {
        if (!VerfahrenNoetig(rahmen, tore)) return null;
        // Eine Verlängerung unter dem Stand nach 90 Minuten ist ein Eingabefehler, kein Gleichstand.
        if (VerlaengerungsVerfahren(rahmen) is not null
            && (VerlaengerungsTore(tore) is not { } verlaengerung || verlaengerung.Heim != verlaengerung.Auswaerts
                || verlaengerung.Heim < 0))
            return null;
        return FolgeArt(rahmen, tore);
    }

    /// <summary>Allein die in der Verlängerung gefallenen Tore, oder <c>null</c>, solange der Stand nach
    /// Verlängerung nicht vollständig eingegeben ist. Entscheidend ist nur, was IN der Verlängerung fällt: beim
    /// Einzelspiel identisch mit dem Vergleich der Spielstände, im K.-o.-Rückspiel mit ausgeglichenem
    /// Gesamtergebnis nicht (reguläre Spielzeit z.B. 3:2, Issue #346).</summary>
    private static (int Heim, int Auswaerts)? VerlaengerungsTore(TorEingabe tore)
    {
        if (tore.HeimNachVerlaengerung is not { } heimV || tore.AuswaertsNachVerlaengerung is not { } auswaertsV ||
            tore.HeimRegulaer is not { } heim || tore.AuswaertsRegulaer is not { } auswaerts) return null;

        return (heimV - heim, auswaertsV - auswaerts);
    }

    private static VerfahrenArt? VerlaengerungsVerfahren(ErgebnisRahmen rahmen)
    {
        foreach (var verfahren in rahmen.ErlaubteVerfahren)
            if (verfahren is VerfahrenArt.VerlaengerungKlassisch or VerfahrenArt.GoldenGoal or VerfahrenArt.SilverGoal)
                return verfahren;
        return null;
    }

    /// <summary>Die Verlängerungsart, die im Regelwerk vor dem Wiederholungsspiel steht – sie läuft im Wiederholungsspiel
    /// noch einmal (#547).</summary>
    private static VerfahrenArt? VerlaengerungVorWiederholung(ErgebnisRahmen rahmen)
    {
        foreach (var verfahren in rahmen.ErlaubteVerfahren)
        {
            if (verfahren == VerfahrenArt.Wiederholungsspiel) return null;
            if (verfahren is VerfahrenArt.VerlaengerungKlassisch or VerfahrenArt.GoldenGoal or VerfahrenArt.SilverGoal)
                return verfahren;
        }
        return null;
    }

    /// <summary>Endete das Wiederholungsspiel (vollständig eingegeben) nach 90 Minuten bzw. nach seiner Verlängerung
    /// torgleich? Dann greift das Verfahren dahinter (#547).</summary>
    private static bool WiederholungTorgleich(ErgebnisRahmen rahmen, TorEingabe tore)
    {
        if (tore.HeimWiederholung is not { } heim || tore.AuswaertsWiederholung is not { } auswaerts || heim != auswaerts)
            return false;
        if (VerlaengerungVorWiederholung(rahmen) is null)
            return true;
        return tore.HeimWiederholungNachVerlaengerung is { } heimNach
            && tore.AuswaertsWiederholungNachVerlaengerung == heimNach;
    }

    /// <summary>Das erste Verfahren im Regelwerk, das entscheidet: ohne weiteres Spiel – oder das Wiederholungsspiel,
    /// solange es nicht torgleich endete (#547).</summary>
    private static EntscheidungArt? FolgeArt(ErgebnisRahmen rahmen, TorEingabe tore)
    {
        foreach (var verfahren in rahmen.ErlaubteVerfahren)
        {
            switch (verfahren)
            {
                case VerfahrenArt.Wiederholungsspiel:
                    if (WiederholungTorgleich(rahmen, tore)) break;
                    return EntscheidungArt.Wiederholungsspiel;
                case VerfahrenArt.Elfmeterschiessen: return EntscheidungArt.Elfmeterschiessen;
                case VerfahrenArt.Losentscheid:      return EntscheidungArt.Losentscheid;
                case VerfahrenArt.Muenzwurf:         return EntscheidungArt.Muenzwurf;
            }
        }
        return null;
    }
}
