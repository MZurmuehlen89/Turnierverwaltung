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
/// <c>null</c> = noch nicht eingegeben.</summary>
public readonly record struct TorEingabe(
    int? HeimRegulaer, int? AuswaertsRegulaer, int? HeimNachVerlaengerung, int? AuswaertsNachVerlaengerung);

/// <summary>Leitet während der Ergebniseingabe aus den bisher eingegebenen Toren ab, welche Abschnitte
/// (Verlängerung, Elfmeterschießen) fällig sind und wie das Spiel damit entschieden wird — ohne Datenbank.</summary>
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
        HatElfmeterschiessen(rahmen) && VerfahrenNoetig(rahmen, tore)
        && (VerlaengerungsVerfahren(rahmen) is null || VerlaengerungToreDifferenz(tore) == 0);

    /// <summary>Entscheidungsart, die zu den eingegebenen Toren gehört. Solange die Verlängerung noch nicht
    /// vollständig eingegeben ist, gilt sie als die entscheidende.</summary>
    public static EntscheidungArt Art(ErgebnisRahmen rahmen, TorEingabe tore)
    {
        if (!VerfahrenNoetig(rahmen, tore)) return EntscheidungArt.RegulaereSpielzeit;

        if (VerlaengerungsVerfahren(rahmen) is not { } verlaengerung)
            return HatElfmeterschiessen(rahmen) ? EntscheidungArt.Elfmeterschiessen : EntscheidungArt.RegulaereSpielzeit;

        if (VerlaengerungToreDifferenz(tore) == 0 && HatElfmeterschiessen(rahmen))
            return EntscheidungArt.Elfmeterschiessen;

        return verlaengerung switch
        {
            VerfahrenArt.GoldenGoal => EntscheidungArt.GoldenGoal,
            VerfahrenArt.SilverGoal => EntscheidungArt.SilverGoal,
            _ => EntscheidungArt.Verlaengerung,
        };
    }

    /// <summary>Tordifferenz (Heim minus Auswärts) allein der in der Verlängerung gefallenen Tore, oder
    /// <c>null</c>, solange der Stand nach Verlängerung nicht vollständig eingegeben ist. Entscheidend ist nur,
    /// was IN der Verlängerung fällt: beim Einzelspiel identisch mit dem Vergleich der Spielstände, im
    /// K.-o.-Rückspiel mit ausgeglichenem Gesamtergebnis nicht (reguläre Spielzeit z.B. 3:2, Issue #346).</summary>
    private static int? VerlaengerungToreDifferenz(TorEingabe tore)
    {
        if (tore.HeimNachVerlaengerung is not { } heimV || tore.AuswaertsNachVerlaengerung is not { } auswaertsV ||
            tore.HeimRegulaer is not { } heim || tore.AuswaertsRegulaer is not { } auswaerts) return null;

        return (heimV - heim) - (auswaertsV - auswaerts);
    }

    private static VerfahrenArt? VerlaengerungsVerfahren(ErgebnisRahmen rahmen)
    {
        foreach (var verfahren in rahmen.ErlaubteVerfahren)
            if (verfahren is VerfahrenArt.VerlaengerungKlassisch or VerfahrenArt.GoldenGoal or VerfahrenArt.SilverGoal)
                return verfahren;
        return null;
    }

    private static bool HatElfmeterschiessen(ErgebnisRahmen rahmen) =>
        rahmen.ErlaubteVerfahren.Contains(VerfahrenArt.Elfmeterschiessen);
}
