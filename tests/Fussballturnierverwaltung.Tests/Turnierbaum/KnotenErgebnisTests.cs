using FluentAssertions;
using Fussballturnierverwaltung.Application.Turnierbaum;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Turnierbaum;

/// <summary>Issue #438: Ergebnisanzeige eines Knotens im Turnierbaum, Einzelspiel und Hin-/Rückspiel-Paarung.</summary>
public class KnotenErgebnisTests
{
    private const int TEAM_A = 1;
    private const int TEAM_B = 2;

    private static Spiel Hinspiel(SpielErgebnis? ergebnis = null) =>
        new() { HeimMannschaftId = TEAM_A, AuswaertsMannschaftId = TEAM_B, Ergebnis = ergebnis };

    private static Spiel Rueckspiel(SpielErgebnis? ergebnis = null) =>
        new() { HeimMannschaftId = TEAM_B, AuswaertsMannschaftId = TEAM_A, Ergebnis = ergebnis };

    [Fact]
    public void OhneErgebnis_AllesLeerUndKeinSieger()
    {
        var e = KnotenErgebnis.Fuer(Hinspiel(), rueckspiel: null);

        e.Should().Be(new KnotenErgebnis(false, "", "", "", "", "", null));
        e.IstSieger(TEAM_A).Should().BeFalse();
        e.IstSieger(null).Should().BeFalse("ein Platz ohne Mannschaft ist nie Sieger");
    }

    [Fact]
    public void PaarungOhneHinspielErgebnis_WieOhneErgebnis()
    {
        KnotenErgebnis.Fuer(Hinspiel(), Rueckspiel()).HatErgebnis.Should().BeFalse();
    }

    [Fact]
    public void Einzelspiel_NachRegulaererSpielzeit()
    {
        var ergebnis = new SpielErgebnis
        {
            HeimToreRegulaer = 2, AuswaertsToreRegulaer = 1,
            EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = TEAM_A
        };

        var e = KnotenErgebnis.Fuer(Hinspiel(ergebnis), rueckspiel: null);

        e.Should().Be(new KnotenErgebnis(true, "2", "1", "", "", "", TEAM_A));
        e.IstSieger(TEAM_A).Should().BeTrue();
        e.IstSieger(TEAM_B).Should().BeFalse();
    }

    [Fact]
    public void Einzelspiel_NachVerlaengerungMitStandDerRegulaerenSpielzeit()
    {
        var ergebnis = new SpielErgebnis
        {
            HeimToreRegulaer = 1, AuswaertsToreRegulaer = 1, HeimToreVerlaengerung = 0, AuswaertsToreVerlaengerung = 1,
            EntscheidungArt = EntscheidungArt.Verlaengerung, SiegerMannschaftId = TEAM_B
        };

        var e = KnotenErgebnis.Fuer(Hinspiel(ergebnis), rueckspiel: null);

        e.Should().Be(new KnotenErgebnis(true, "1", "2", "(1:1) n.V.", "", "n.V.", TEAM_B));
    }

    [Fact]
    public void Paarung_OhneRueckspielErgebnisStandNachHinspielOhneSieger()
    {
        var hin = new SpielErgebnis
        {
            HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0,
            EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = TEAM_A
        };

        var e = KnotenErgebnis.Fuer(Hinspiel(hin), Rueckspiel());

        e.Should().Be(new KnotenErgebnis(true, "1", "0", "", "nach Hinspiel", "", null));
        e.IstSieger(TEAM_A).Should().BeFalse("der Sieger des Hinspiels hat die Paarung noch nicht gewonnen");
    }

    [Fact]
    public void Paarung_GesamtergebnisAusSichtDesHinspielsUndSiegerAusDemRueckspiel()
    {
        // Hin 1:0 für A, Rück 3:2 für B (daheim) → gesamt 3:3, B gewinnt im Elfmeterschießen.
        var hin = new SpielErgebnis
        {
            HeimToreRegulaer = 1, AuswaertsToreRegulaer = 0,
            EntscheidungArt = EntscheidungArt.RegulaereSpielzeit, SiegerMannschaftId = TEAM_A
        };
        var rueck = new SpielErgebnis
        {
            HeimToreRegulaer = 3, AuswaertsToreRegulaer = 2, HeimElfmetertore = 4, AuswaertsElfmetertore = 3,
            EntscheidungArt = EntscheidungArt.Elfmeterschiessen, SiegerMannschaftId = TEAM_B
        };

        var e = KnotenErgebnis.Fuer(Hinspiel(hin), Rueckspiel(rueck));

        e.Should().Be(new KnotenErgebnis(true, "3", "3", "i.E. 4:3", "Hin 1:0 · Rück 3:2", "i.E.", TEAM_B));
        e.IstSieger(TEAM_B).Should().BeTrue();
        e.IstSieger(TEAM_A).Should().BeFalse();
    }
}
