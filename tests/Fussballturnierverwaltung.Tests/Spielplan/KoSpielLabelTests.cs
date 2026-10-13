using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class KoSpielLabelTests
{
    private static TurnierRunde Runde(Rundenart art, int eingang = 0, bool mitRueckspiel = false, string name = "") =>
        new() { Rundenart = art, Name = name, EingangsMannschaftsanzahl = eingang, MitRueckspiel = mitRueckspiel };

    // #455: dieselben Kürzel wie im Turnierbaum — die Teilnehmerzahl (16) spräche für "AF", der Name geht vor.
    [Theory]
    [InlineData("Playoff-Runde", "PO 1")]
    [InlineData("Achtelfinale", "AF 1")]
    [InlineData("Sechzehntelfinale", "L32 1")]
    [InlineData("Zwischenrunde", "Z 1")]
    [InlineData("Runde 2", "R2 1")]
    public void Fuer_KoRunde_KuerzelAusDemRundennamen(string name, string erwartet) =>
        KoSpielLabel.Fuer(new Spiel(), Runde(Rundenart.KoRunde, eingang: 16, name: name), paarungsNummer: 1)
            .Should().Be(erwartet);

    [Theory]
    [InlineData(4, "HF 1")]
    [InlineData(8, "VF 1")]
    [InlineData(16, "AF 1")]
    [InlineData(32, "L32 1")]
    [InlineData(64, "L64 1")]
    [InlineData(24, "R12 1")]
    public void Fuer_KoRundeOhneNamen_KuerzelAusDerTeilnehmerzahl(int eingang, string erwartet) =>
        KoSpielLabel.Fuer(new Spiel(), Runde(Rundenart.KoRunde, eingang), paarungsNummer: 1).Should().Be(erwartet);

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

    [Theory]
    [InlineData(2)]
    [InlineData(0)]
    public void Fuer_KoRundeMitHoechstensZweiTeilnehmern_IstNull(int eingang) =>
        KoSpielLabel.Fuer(new Spiel(), Runde(Rundenart.KoRunde, eingang, name: "Finale"), paarungsNummer: 1)
            .Should().BeNull();

    [Fact]
    public void Fuer_OhneRunde_IstNull() =>
        KoSpielLabel.Fuer(new Spiel(), runde: null, paarungsNummer: 1).Should().BeNull();
}
