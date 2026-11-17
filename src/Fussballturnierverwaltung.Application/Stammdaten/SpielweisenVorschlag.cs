using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Application.Stammdaten;

/// <summary>Vorschlag der Grundausrichtung einer Mannschaft aus ihren Ratings (#667) – für den Seed und als Hinweis
/// in der Mannschaftspflege. Maßstab ist der Abstand Offensiv − Defensiv: Im Bestand liegt er bei gut 70 % der
/// Mannschaften bei genau +2, Abweichungen um einen Punkt gelten als Rundungsrauschen.</summary>
public static class SpielweisenVorschlag
{
    public const int NORMALABSTAND = 2;
    public const int EINE_STUFE    = 2; // Abweichung vom Normalabstand für „offensiv“/„defensiv“
    public const int ZWEI_STUFEN   = 4; // … für „sehr offensiv“/„sehr defensiv“

    public static Grundausrichtung AusRatings(int offensivRating, int defensivRating)
    {
        int abweichung = offensivRating - defensivRating - NORMALABSTAND;
        int stufen = Math.Abs(abweichung) >= ZWEI_STUFEN ? 2 : Math.Abs(abweichung) >= EINE_STUFE ? 1 : 0;
        return (Grundausrichtung)(Math.Sign(abweichung) * stufen);
    }
}
