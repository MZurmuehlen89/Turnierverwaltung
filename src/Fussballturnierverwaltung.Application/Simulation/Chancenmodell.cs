using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>
/// Chancenarten und ihre Verwertungsquoten in der ausführlichen Simulation (#431, rein, ohne Zufall). Zielwerte je
/// Team und Spiel (Kommentar „Schritt 1“ zu #431): 0,16 Elfmeter, 1,85 Großchancen, 10,9 normale Chancen.
/// Standardsituationen sind keine Art, nur die Herkunft im Tortext (<see cref="Torbeschreibung"/>).
/// </summary>
public static class Chancenmodell
{
    private const double CHANCEN_JE_TEAM_UND_SPIEL = 12.91;

    public const double ANTEIL_ELFMETER     = 0.16 / CHANCEN_JE_TEAM_UND_SPIEL;
    public const double ANTEIL_GROSSCHANCE  = 1.85 / CHANCEN_JE_TEAM_UND_SPIEL;
    public const double ANTEIL_NORMAL       = 1 - ANTEIL_ELFMETER - ANTEIL_GROSSCHANCE;
    public const double QUOTE_ELFMETER      = 0.785;
    public const double QUOTE_GROSSCHANCE   = 0.37;
    public const double QUOTE_NORMAL        = 0.07;
    private const double MAX_QUOTE          = 0.95;

    /// <summary>Art der Chance zu einem gleichverteilten Wurf in [0, 1).</summary>
    public static Chancenart Art(double wurf)
    {
        if (wurf < ANTEIL_ELFMETER) return Chancenart.Elfmeter;
        return wurf < ANTEIL_ELFMETER + ANTEIL_GROSSCHANCE ? Chancenart.Grosschance : Chancenart.Normal;
    }

    /// <summary>Wahrscheinlichkeit, dass die Chance ein Tor wird. Der Elfmeter hängt nicht von der Verwertung ab.</summary>
    public static double Quote(Chancenart art, double verwertung) => art switch
    {
        Chancenart.Elfmeter => QUOTE_ELFMETER,
        Chancenart.Grosschance => Math.Min(QUOTE_GROSSCHANCE * verwertung, MAX_QUOTE),
        _ => Math.Min(QUOTE_NORMAL * verwertung, MAX_QUOTE)
    };

    /// <summary>Mittlere Torquote über alle Arten: Torerwartung = Chancenzahl × dieser Wert.</summary>
    public static double MittlereQuote(double verwertung)
        => ANTEIL_ELFMETER * QUOTE_ELFMETER
           + ANTEIL_GROSSCHANCE * Quote(Chancenart.Grosschance, verwertung)
           + ANTEIL_NORMAL * Quote(Chancenart.Normal, verwertung);
}
