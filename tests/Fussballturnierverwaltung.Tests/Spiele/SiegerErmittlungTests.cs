using FluentAssertions;
using Fussballturnierverwaltung.Application.Spiele;
using Fussballturnierverwaltung.Application.Spiele.Eingaben;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Spiele;

public class SiegerErmittlungTests
{
    private const int HEIM = 1;
    private const int AUSWAERTS = 2;

    private static (int? Sieger, EntscheidungArt Art) Einzelspiel(ErgebnisErfassenEingabe eingabe) =>
        SiegerErmittlung.Bestimmen(HEIM, AUSWAERTS, eingabe, hinspielErgebnis: null, auswaertstorRegelAktiv: false);

    // Rückspiel: HEIM ist das Hinspiel-Auswärtsteam (Team B), AUSWAERTS das Hinspiel-Heimteam (Team A).
    private static (int? Sieger, EntscheidungArt Art) Rueckspiel(
        int hinHeim, int hinAuswaerts, ErgebnisErfassenEingabe eingabe, bool auswaertstorRegel = false) =>
        SiegerErmittlung.Bestimmen(HEIM, AUSWAERTS, eingabe,
            new SpielErgebnis { HeimToreRegulaer = hinHeim, AuswaertsToreRegulaer = hinAuswaerts }, auswaertstorRegel);

    private static (int? Sieger, EntscheidungArt Art) Erwartet(int? sieger, EntscheidungArt art) => (sieger, art);

    // ── Einzelspiel ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(2, 1, HEIM)]
    [InlineData(0, 3, AUSWAERTS)]
    [InlineData(1, 1, null)]
    public void Einzelspiel_RegulaereSpielzeit_MehrToreGewinnen(int heim, int auswaerts, int? erwartet) =>
        Einzelspiel(new(heim, auswaerts, EntscheidungArt.RegulaereSpielzeit))
            .Should().Be(Erwartet(erwartet, EntscheidungArt.RegulaereSpielzeit));

    [Theory]
    [InlineData(EntscheidungArt.Verlaengerung)]
    [InlineData(EntscheidungArt.GoldenGoal)]
    [InlineData(EntscheidungArt.SilverGoal)]
    public void Einzelspiel_Verlaengerung_ZaehltRegulaerePlusVerlaengerungstore(EntscheidungArt art) =>
        Einzelspiel(new(1, 1, art, HeimToreVerlaengerung: 0, AuswaertsToreVerlaengerung: 1))
            .Should().Be(Erwartet(AUSWAERTS, art));

    [Fact]
    public void Einzelspiel_VerlaengerungOhneTore_KeinSieger() =>
        Einzelspiel(new(1, 1, EntscheidungArt.Verlaengerung)).Sieger.Should().BeNull();

    [Fact]
    public void Einzelspiel_Elfmeterschiessen_MehrElfmetertoreGewinnen() =>
        Einzelspiel(new(1, 1, EntscheidungArt.Elfmeterschiessen, 0, 0, HeimElfmetertore: 4, AuswaertsElfmetertore: 3))
            .Should().Be(Erwartet(HEIM, EntscheidungArt.Elfmeterschiessen));

    [Theory]
    [InlineData(EntscheidungArt.Losentscheid, true, HEIM)]
    [InlineData(EntscheidungArt.Muenzwurf, false, AUSWAERTS)]
    [InlineData(EntscheidungArt.Losentscheid, null, null)]
    public void Einzelspiel_LosOderMuenze_LosGewinnerWirdSieger(EntscheidungArt art, bool? losGewinnerHeim, int? erwartet) =>
        Einzelspiel(new(0, 0, art, LosGewinnerHeim: losGewinnerHeim)).Should().Be(Erwartet(erwartet, art));

    [Fact]
    public void Einzelspiel_Auswaertstorregel_KeinSieger() =>
        Einzelspiel(new(1, 1, EntscheidungArt.Auswaertstorregel)).Sieger.Should().BeNull();

    // ── K.-o.-Rückspiel (Issue #346) ─────────────────────────────────────

    [Theory]
    [InlineData(1, 0, 0, 0, AUSWAERTS)] // Gesamt A 1:0 B
    [InlineData(1, 0, 2, 0, HEIM)]      // Gesamt B 2:1 A trotz Hinspielniederlage
    public void Rueckspiel_GesamtergebnisUngleich_GesamtsiegerNachRegulaererSpielzeit(
        int hinHeim, int hinAuswaerts, int rueckHeim, int rueckAuswaerts, int? erwartet) =>
        Rueckspiel(hinHeim, hinAuswaerts, new(rueckHeim, rueckAuswaerts, EntscheidungArt.Verlaengerung))
            .Should().Be(Erwartet(erwartet, EntscheidungArt.RegulaereSpielzeit));

    [Fact]
    public void Rueckspiel_GesamtGleichMitRegel_MehrAuswaertstoreGewinnen() =>
        // Hinspiel A–B 2:1, Rückspiel B–A 1:0 → 2:2; B traf auswärts 1-mal, A 0-mal.
        Rueckspiel(2, 1, new(1, 0, EntscheidungArt.RegulaereSpielzeit), auswaertstorRegel: true)
            .Should().Be(Erwartet(HEIM, EntscheidungArt.Auswaertstorregel));

    [Fact]
    public void Rueckspiel_AuswaertstoreGleich_EntscheidungsverfahrenEntscheidet() =>
        // Hinspiel A–B 1:1, Rückspiel B–A 1:1 → Auswärtstore gleich, Elfmeterschießen.
        Rueckspiel(1, 1, new(1, 1, EntscheidungArt.Elfmeterschiessen, 0, 0, HeimElfmetertore: 2, AuswaertsElfmetertore: 4),
                auswaertstorRegel: true)
            .Should().Be(Erwartet(AUSWAERTS, EntscheidungArt.Elfmeterschiessen));

    [Fact]
    public void Rueckspiel_GesamtGleichOhneRegel_EntscheidenNurDieVerlaengerungstore() =>
        // Hinspiel A–B 2:3, Rückspiel B–A 2:3 → 5:5; Verlängerung 1:0 für B, obwohl A regulär mehr Tore hat.
        Rueckspiel(2, 3, new(2, 3, EntscheidungArt.Verlaengerung, HeimToreVerlaengerung: 1, AuswaertsToreVerlaengerung: 0))
            .Should().Be(Erwartet(HEIM, EntscheidungArt.Verlaengerung));

    [Theory]
    [InlineData(EntscheidungArt.Losentscheid, true, HEIM)]
    [InlineData(EntscheidungArt.Muenzwurf, false, AUSWAERTS)]
    public void Rueckspiel_GesamtGleich_LosGewinnerWirdSieger(EntscheidungArt art, bool losGewinnerHeim, int? erwartet) =>
        Rueckspiel(1, 0, new(1, 0, art, LosGewinnerHeim: losGewinnerHeim)).Should().Be(Erwartet(erwartet, art));

    [Theory]
    [InlineData(EntscheidungArt.GoldenGoal)]
    [InlineData(EntscheidungArt.SilverGoal)]
    public void Rueckspiel_GoldenOderSilverGoal_EntscheidenNurDieVerlaengerungstore(EntscheidungArt art) =>
        Rueckspiel(1, 0, new(1, 0, art, HeimToreVerlaengerung: 0, AuswaertsToreVerlaengerung: 1))
            .Should().Be(Erwartet(AUSWAERTS, art));

    [Fact]
    public void Rueckspiel_VerlaengerungUnentschieden_KeinSieger() =>
        Rueckspiel(1, 0, new(1, 0, EntscheidungArt.Verlaengerung, HeimToreVerlaengerung: 1, AuswaertsToreVerlaengerung: 1))
            .Sieger.Should().BeNull();

    [Fact]
    public void Rueckspiel_GesamtGleichNachRegulaererSpielzeit_KeinSieger() =>
        Rueckspiel(1, 0, new(1, 0, EntscheidungArt.RegulaereSpielzeit))
            .Should().Be(Erwartet(null, EntscheidungArt.RegulaereSpielzeit));

    [Fact]
    public void Rueckspiel_HinspielMitVerlaengerung_ZaehltDieseToreMit() =>
        // Altbestand vor #346: Hinspiel A–B 1:1 n.V. 2:1 → zählt als 2:1; Rückspiel B–A 1:0 → 2:2, Auswärtstor B.
        SiegerErmittlung.Bestimmen(HEIM, AUSWAERTS, new(1, 0, EntscheidungArt.RegulaereSpielzeit),
                new SpielErgebnis
                {
                    HeimToreRegulaer = 1, AuswaertsToreRegulaer = 1,
                    HeimToreVerlaengerung = 1, AuswaertsToreVerlaengerung = 0
                },
                auswaertstorRegelAktiv: true)
            .Should().Be(Erwartet(HEIM, EntscheidungArt.Auswaertstorregel));

    // ── Torgleiche Verlängerung (#391) ────────────────────────────────────

    [Theory]
    [InlineData(EntscheidungArt.Verlaengerung)]
    [InlineData(EntscheidungArt.GoldenGoal)]
    [InlineData(EntscheidungArt.SilverGoal)]
    public void Einzelspiel_TorgleicheVerlaengerung_KeinSieger(EntscheidungArt art) =>
        Einzelspiel(new(1, 1, art, HeimToreVerlaengerung: 1, AuswaertsToreVerlaengerung: 1))
            .Should().Be(Erwartet(null, art), "bis #391 kam bei Gleichstand stillschweigend das Auswärtsteam weiter");

    [Fact]
    public void Einzelspiel_LosNachTorgleicherVerlaengerung_LosGewinnerWirdSieger() =>
        Einzelspiel(new(0, 0, EntscheidungArt.Losentscheid, HeimToreVerlaengerung: 0, AuswaertsToreVerlaengerung: 0,
                LosGewinnerHeim: true))
            .Should().Be(Erwartet(HEIM, EntscheidungArt.Losentscheid));
}
