using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class KoSpielLabelTests
{
    private static TurnierRunde Runde(Rundenart art, int eingang = 0, bool mitRueckspiel = false) =>
        new() { Rundenart = art, EingangsMannschaftsanzahl = eingang, MitRueckspiel = mitRueckspiel };

    [Theory]
    [InlineData(4, "HF")]
    [InlineData(8, "VF")]
    [InlineData(16, "AF")]
    [InlineData(32, "R16")]
    [InlineData(64, "R32")]
    [InlineData(128, "R64")]
    [InlineData(24, "R12")]
    public void Kuerzel_RichtetSichNachDerTeilnehmerzahl(int eingang, string erwartet) =>
        KoSpielLabel.Kuerzel(eingang).Should().Be(erwartet);

    [Theory]
    [InlineData(2)]
    [InlineData(0)]
    public void Kuerzel_HoechstensZweiTeilnehmer_IstNull(int eingang) =>
        KoSpielLabel.Kuerzel(eingang).Should().BeNull();

    [Fact]
    public void Fuer_KoRunde_VerbindetKuerzelUndPaarungsnummer() =>
        KoSpielLabel.Fuer(new Spiel(), Runde(Rundenart.KoRunde, eingang: 8), paarungsNummer: 3).Should().Be("VF 3");

    [Theory]
    [InlineData(false, "AF 2 H")]
    [InlineData(true, "AF 2 R")]
    public void Fuer_MitRueckspiel_KennzeichnetHinUndRueckspiel(bool istRueckspiel, string erwartet) =>
        KoSpielLabel.Fuer(
                new Spiel { IstRueckspiel = istRueckspiel },
                Runde(Rundenart.KoRunde, eingang: 16, mitRueckspiel: true), paarungsNummer: 2)
            .Should().Be(erwartet);

    [Theory]
    [InlineData(Rundenart.Finale, "Finale 1")]
    [InlineData(Rundenart.SpielUmPlatzDrei, "3. Platz 1")]
    public void Fuer_FinaleUndSpielUmPlatzDrei_TragenIhrenNamen(Rundenart art, string erwartet) =>
        KoSpielLabel.Fuer(new Spiel(), Runde(art, eingang: 2), paarungsNummer: 1).Should().Be(erwartet);

    [Theory]
    [InlineData(Rundenart.Liga)]
    [InlineData(Rundenart.Gruppenphase)]
    [InlineData(Rundenart.SchweizerSystem)]
    [InlineData(Rundenart.TopfLiga)]
    public void Fuer_RundeOhneKoPaarungen_IstNull(Rundenart art) =>
        KoSpielLabel.Fuer(new Spiel(), Runde(art, eingang: 16), paarungsNummer: 1).Should().BeNull();

    [Fact]
    public void Fuer_KoRundeMitZweiTeilnehmern_IstNull() =>
        KoSpielLabel.Fuer(new Spiel(), Runde(Rundenart.KoRunde, eingang: 2), paarungsNummer: 1).Should().BeNull();

    [Fact]
    public void Fuer_OhneRunde_IstNull() =>
        KoSpielLabel.Fuer(new Spiel(), runde: null, paarungsNummer: 1).Should().BeNull();
}
