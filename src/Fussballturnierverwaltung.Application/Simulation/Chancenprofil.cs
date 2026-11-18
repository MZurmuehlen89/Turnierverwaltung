using Fussballturnierverwaltung.Domain.Entitaeten.Stammdaten;

namespace Fussballturnierverwaltung.Application.Simulation;

/// <summary>
/// Aufteilung der Torerwartung eines Teams in Chancenmenge und Verwertung (#431, E2). Das Produkt aus
/// <see cref="ChancenJe90"/> und <see cref="Chancenmodell.MittlereQuote"/> bleibt die Torerwartung, die Kalibrierung
/// (#404) bleibt damit gültig. Mittelfeld bestimmt die Menge, Offensiv die Verwertung, die gegnerische Defensive
/// die Güte der zugelassenen Chancen.
/// </summary>
public readonly record struct Chancenprofil(double ChancenJe90, double Verwertung)
{
    private const double MIN_VERWERTUNG = 0.75;
    private const double MAX_VERWERTUNG = 1.3;

    public static Chancenprofil Aus(double torerwartung, Mannschaft angreifer, Mannschaft verteidiger)
    {
        var verwertung = Verwertungsfaktor(angreifer, verteidiger);
        return new Chancenprofil(torerwartung / Chancenmodell.MittlereQuote(verwertung), verwertung);
    }

    // q = (Off_A / Mf_A) · (Mf_V / Def_V)^0,5, begrenzt; Rating ≤ 0 ergibt den Faktor 1
    private static double Verwertungsfaktor(Mannschaft angreifer, Mannschaft verteidiger)
    {
        if (angreifer.OffensivRating <= 0 || angreifer.MittelfeldRating <= 0
            || verteidiger.MittelfeldRating <= 0 || verteidiger.DefensivRating <= 0)
            return 1.0;

        var q = (double)angreifer.OffensivRating / angreifer.MittelfeldRating
                * Math.Sqrt((double)verteidiger.MittelfeldRating / verteidiger.DefensivRating);
        return Math.Clamp(q, MIN_VERWERTUNG, MAX_VERWERTUNG);
    }
}
