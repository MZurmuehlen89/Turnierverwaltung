using FluentAssertions;
using Fussballturnierverwaltung.Application.Stammdaten;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Stammdaten;

/// <summary>Vorschlag der Grundausrichtung aus den Ratings (#667, K10): gemessen am Normalabstand Offensiv − Defensiv
/// = +2; Abweichungen um ±1 gelten als Rundungsrauschen.</summary>
public class SpielweisenVorschlagTests
{
    [Theory]
    [InlineData(80, 78, Grundausrichtung.Ausgeglichen)]   // Normalabstand
    [InlineData(80, 79, Grundausrichtung.Ausgeglichen)]   // d = −1
    [InlineData(80, 77, Grundausrichtung.Ausgeglichen)]   // d = +1
    [InlineData(80, 80, Grundausrichtung.Defensiv)]       // d = −2
    [InlineData(80, 81, Grundausrichtung.Defensiv)]       // d = −3
    [InlineData(80, 82, Grundausrichtung.SehrDefensiv)]   // d = −4
    [InlineData(70, 90, Grundausrichtung.SehrDefensiv)]
    [InlineData(80, 76, Grundausrichtung.Offensiv)]       // d = +2
    [InlineData(80, 75, Grundausrichtung.Offensiv)]       // d = +3
    [InlineData(80, 74, Grundausrichtung.SehrOffensiv)]   // d = +4
    [InlineData(95, 60, Grundausrichtung.SehrOffensiv)]
    public void Grundausrichtung_AusOffensivUndDefensivRating(int offensiv, int defensiv, Grundausrichtung erwartet)
    {
        SpielweisenVorschlag.AusRatings(offensiv, defensiv).Should().Be(erwartet);
    }
}
