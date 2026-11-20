using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Gesamtwertung der freien Hin-/Rückspiel-Simulation (#509 Schnitt C, K3): Summenstand, Sieger und Zusatztext
/// je Zweig, aus Sicht des Hinspiel-Heimteams. Das Rückspiel hat vertauschte Rollen – sein „Heim“ ist das
/// Hinspiel-Auswärtsteam. Erwartungen vom unverändert verschobenen Code.</summary>
public class FreiePaarungGesamtTests
{
    private static SimulationsErgebnis Hin(int heim, int auswaerts) =>
        new(heim, auswaerts, null, null, null, null, EntscheidungArt.RegulaereSpielzeit, []);

    private static SimulationsErgebnis Rueck(
        int heim, int auswaerts, EntscheidungArt art = EntscheidungArt.RegulaereSpielzeit,
        (int, int)? verlaengerung = null, (int, int)? elfmeter = null, bool? los = null, Wiederholungsergebnis? wiederholung = null) =>
        new(heim, auswaerts, verlaengerung?.Item1, verlaengerung?.Item2, elfmeter?.Item1, elfmeter?.Item2,
            art, [], LosGewinnerHeim: los, Wiederholung: wiederholung);

    [Fact]
    public void GesamtstandNach90Minuten_HeimVorn()
    {
        FreiePaarungGesamt.Bewerten(Hin(2, 0), Rueck(0, 1))
            .Should().Be(new FreiePaarungGesamt(3, 0, PaarungSieger.Heim, "nach 180 Minuten"));
    }

    [Fact]
    public void GesamtstandNach90Minuten_AuswaertsVorn()
    {
        FreiePaarungGesamt.Bewerten(Hin(0, 1), Rueck(2, 0))
            .Should().Be(new FreiePaarungGesamt(0, 3, PaarungSieger.Auswaerts, "nach 180 Minuten"));
    }

    [Fact]
    public void Gleichstand_OhneVerfahren_KeinSieger()
    {
        FreiePaarungGesamt.Bewerten(Hin(1, 1), Rueck(0, 0))
            .Should().Be(new FreiePaarungGesamt(1, 1, PaarungSieger.Keiner, "nach 180 Minuten"));
    }

    [Fact]
    public void Verlaengerung_ToreZaehlenImGesamtstand_RueckspielHeimIstHinspielAuswaerts()
    {
        // 90 Min.: 1:1 und 1:1 → 2:2; Verlängerung: Rückspiel-Heim (= Hinspiel-Auswärts) trifft einmal.
        FreiePaarungGesamt.Bewerten(Hin(1, 1), Rueck(1, 1, EntscheidungArt.Verlaengerung, verlaengerung: (1, 0)))
            .Should().Be(new FreiePaarungGesamt(2, 3, PaarungSieger.Auswaerts, "nach Verlängerung"));
        // … und das Rückspiel-Auswärtsteam (= Hinspiel-Heim)
        FreiePaarungGesamt.Bewerten(Hin(1, 1), Rueck(1, 1, EntscheidungArt.Verlaengerung, verlaengerung: (0, 1)))
            .Should().Be(new FreiePaarungGesamt(3, 2, PaarungSieger.Heim, "nach Verlängerung"));
    }

    [Theory]
    [InlineData(EntscheidungArt.GoldenGoal)]
    [InlineData(EntscheidungArt.SilverGoal)]
    public void GoldenUndSilverGoal_ZusatzNachVerlaengerung(EntscheidungArt art)
    {
        FreiePaarungGesamt.Bewerten(Hin(0, 0), Rueck(0, 0, art, verlaengerung: (0, 1)))
            .Should().Be(new FreiePaarungGesamt(1, 0, PaarungSieger.Heim, "nach Verlängerung"));
    }

    [Theory]
    [InlineData(5, 4, PaarungSieger.Auswaerts, "nach Elfmeterschießen (4:5)")]
    [InlineData(3, 4, PaarungSieger.Heim, "nach Elfmeterschießen (4:3)")]
    public void Elfmeterschiessen_StandGespiegelt_RueckspielHeimIstHinspielAuswaerts(
        int heimElfmeter, int auswaertsElfmeter, PaarungSieger sieger, string zusatz)
    {
        FreiePaarungGesamt.Bewerten(Hin(1, 1), Rueck(0, 0, EntscheidungArt.Elfmeterschiessen,
                verlaengerung: (0, 0), elfmeter: (heimElfmeter, auswaertsElfmeter)))
            .Should().Be(new FreiePaarungGesamt(1, 1, sieger, zusatz));
    }

    [Theory]
    [InlineData(2, 1, PaarungSieger.Auswaerts)]
    [InlineData(1, 2, PaarungSieger.Heim)]
    [InlineData(1, 1, PaarungSieger.Heim)] // Gleichstand im Wiederholungsspiel fällt auf „Heim“ (Bestand)
    public void Wiederholungsspiel_RueckspielHeimIstHinspielAuswaerts(int heimTore, int auswaertsTore, PaarungSieger sieger)
    {
        FreiePaarungGesamt.Bewerten(Hin(0, 0), Rueck(0, 0, EntscheidungArt.Wiederholungsspiel,
                wiederholung: new Wiederholungsergebnis(heimTore, auswaertsTore, null, null)))
            .Should().Be(new FreiePaarungGesamt(0, 0, sieger, "nach Wiederholungsspiel"));
    }

    [Fact]
    public void Wiederholungsspiel_OhneErgebnis_KeinSieger()
    {
        FreiePaarungGesamt.Bewerten(Hin(0, 0), Rueck(0, 0, EntscheidungArt.Wiederholungsspiel))
            .Should().Be(new FreiePaarungGesamt(0, 0, PaarungSieger.Keiner, "nach Wiederholungsspiel"));
    }

    [Theory]
    [InlineData(EntscheidungArt.Losentscheid, true, PaarungSieger.Auswaerts, "nach Losentscheid")]
    [InlineData(EntscheidungArt.Losentscheid, false, PaarungSieger.Heim, "nach Losentscheid")]
    [InlineData(EntscheidungArt.Muenzwurf, true, PaarungSieger.Auswaerts, "nach Münzwurf")]
    [InlineData(EntscheidungArt.Muenzwurf, false, PaarungSieger.Heim, "nach Münzwurf")]
    public void LosUndMuenzwurf_RueckspielHeimIstHinspielAuswaerts(
        EntscheidungArt art, bool losFuerRueckspielHeim, PaarungSieger sieger, string zusatz)
    {
        FreiePaarungGesamt.Bewerten(Hin(1, 1), Rueck(0, 0, art, los: losFuerRueckspielHeim))
            .Should().Be(new FreiePaarungGesamt(1, 1, sieger, zusatz));
    }

    [Fact]
    public void Auswaertstorregel_Zusatztext()
    {
        FreiePaarungGesamt.Bewerten(Hin(1, 1), Rueck(1, 1, EntscheidungArt.Auswaertstorregel))
            .Zusatz.Should().Be("nach Auswärtstorregel");
    }

    // K4 (#539): Hinspiel 1:1 → Rückspiel-Heim (= Hinspiel-Auswärts) traf dort 1 auswärts. Bei Rückspiel 0:0 hat
    // es 1, das Rückspiel-Auswärts (= Hinspiel-Heim) 0 Auswärtstore → Hinspiel-Auswärts gewinnt.
    [Theory]
    [InlineData(0, 0, PaarungSieger.Auswaerts)]
    [InlineData(2, 2, PaarungSieger.Heim)] // Gesamt 3:3; Rückspiel-Auswärts traf 2 auswärts, Rückspiel-Heim hat 1
    public void Auswaertstorregel_MehrAuswaertstoreGewinnen(int rueckHeim, int rueckAuswaerts, PaarungSieger sieger)
    {
        FreiePaarungGesamt.Bewerten(Hin(1, 1), Rueck(rueckHeim, rueckAuswaerts, EntscheidungArt.Auswaertstorregel))
            .Sieger.Should().Be(sieger);
    }

    [Fact]
    public void GesamtstandHatVorrangVorDemVerfahren()
    {
        FreiePaarungGesamt.Bewerten(Hin(2, 0), Rueck(0, 0, EntscheidungArt.Losentscheid, los: true))
            .Should().Be(new FreiePaarungGesamt(2, 0, PaarungSieger.Heim, "nach Losentscheid"));
    }
}
