using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Issue #671: maßgeblicher Vorsprung im K.-o.-Rückspiel nach dem Gesamtstand, mit Auswärtstorregel nach
/// AD-001. Die Hinspiel-Tore stehen hier aus Sicht des Hinspiels (Heim = späterer Rückspiel-Gast).</summary>
public class HinspielstandTests
{
    // ── K1: Gesamtstand statt Stand des Rückspiels ────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HinspielNullZuDreiVerloren_RueckspielEinsZuNull_ZweiToreZurueck(bool auswaertstorRegel)
    {
        var stand = Hinspielstand.AusHinspiel(hinspielHeimTore: 3, hinspielAuswaertsTore: 0, auswaertstorRegel);

        stand.VorsprungDesHeimteams(heimTore: 1, gastTore: 0).Should().Be(-2,
            "das Rückspiel-Heimteam liegt nach Gesamtstand 1:3 zurück, obwohl es das Rückspiel anführt");
    }

    // ── K2: Auswärtstorregel ──────────────────────────────────────────────

    [Theory]
    // Hinspiel 1:1 – das Rückspiel-Heimteam hat ein Auswärtstor
    [InlineData(1, 1, 0, 0, true, 1)]
    [InlineData(1, 1, 0, 0, false, 0)]
    [InlineData(1, 1, 1, 1, true, 0)]
    [InlineData(1, 1, 0, 1, true, -1)]
    // Hinspiel 1:0 für den Rückspiel-Gast, Rückspiel 1:1: ein weiteres Heimtor reicht wegen des Auswärtstors nicht
    [InlineData(1, 0, 1, 1, true, -2)]
    [InlineData(1, 0, 1, 1, false, -1)]
    // Hinspiel 3:1 für den Rückspiel-Gast, Rückspiel 1:0: ein Tor genügt – das Heimteam hat das bessere Auswärtstor
    [InlineData(3, 1, 1, 0, true, -1)]
    [InlineData(2, 0, 0, 0, true, -2)]
    [InlineData(0, 2, 0, 0, true, 2)]
    public void Auswaertstorregel_VorsprungInToren(
        int hinspielHeim, int hinspielAuswaerts, int rueckspielHeim, int rueckspielGast, bool regel, int erwartet)
    {
        var stand = Hinspielstand.AusHinspiel(hinspielHeim, hinspielAuswaerts, regel);

        stand.VorsprungDesHeimteams(rueckspielHeim, rueckspielGast).Should().Be(erwartet);
    }

    // ── K5: ohne Hinspiel zählt nur der Spielstand ────────────────────────

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(3, 1, 2)]
    [InlineData(0, 2, -2)]
    public void OhneHinspiel_VorsprungIstSpielstand(int heimTore, int gastTore, int erwartet)
    {
        default(Hinspielstand).VorsprungDesHeimteams(heimTore, gastTore).Should().Be(erwartet);
    }
}
