using FluentAssertions;
using Fussballturnierverwaltung.Application.Spielplan;
using Fussballturnierverwaltung.Domain.Entitaeten.Spiele;

namespace Fussballturnierverwaltung.Tests.Spielplan;

public class KoPaarungenTests
{
    private static Spiel Hinspiel(int id, int spielnummer) =>
        new() { Id = id, Spielnummer = spielnummer, HeimMannschaftId = id * 10, AuswaertsMannschaftId = id * 10 + 1 };

    private static Spiel Rueckspiel(int id, int spielnummer, int? hinspielId) =>
        new() { Id = id, Spielnummer = spielnummer, IstRueckspiel = true, HinspielId = hinspielId };

    private static Spiel Entschieden(int heimId, int auswaertsId, int? siegerId) =>
        new()
        {
            HeimMannschaftId = heimId,
            AuswaertsMannschaftId = auswaertsId,
            Ergebnis = new SpielErgebnis { SiegerMannschaftId = siegerId },
        };

    [Fact]
    public void RueckspielJeHinspiel_OrdnetUeberDieHinspielIdZu()
    {
        var rueck1 = Rueckspiel(3, 3, hinspielId: 1);
        var rueck2 = Rueckspiel(4, 4, hinspielId: 2);

        var ergebnis = KoPaarungen.RueckspielJeHinspiel([Hinspiel(1, 1), Hinspiel(2, 2), rueck1, rueck2]);

        ergebnis.Should().HaveCount(2);
        ergebnis[1].Should().BeSameAs(rueck1);
        ergebnis[2].Should().BeSameAs(rueck2);
    }

    [Fact]
    public void RueckspielJeHinspiel_RueckspielOhneHinspielId_BleibtAussenVor() =>
        KoPaarungen.RueckspielJeHinspiel([Hinspiel(1, 1), Rueckspiel(2, 2, hinspielId: null)]).Should().BeEmpty();

    [Fact]
    public void EntscheidungsspieleJePaarung_OhneRueckspiele_LiefertDieSpieleNachSpielnummer()
    {
        var ergebnis = KoPaarungen.EntscheidungsspieleJePaarung([Hinspiel(2, 2), Hinspiel(1, 1)]);

        ergebnis.Select(s => s.Id).Should().Equal(1, 2);
    }

    [Fact]
    public void EntscheidungsspieleJePaarung_MitRueckspielen_LiefertJePaarungDasRueckspiel()
    {
        var ergebnis = KoPaarungen.EntscheidungsspieleJePaarung(
            [Hinspiel(1, 1), Hinspiel(2, 2), Rueckspiel(3, 3, hinspielId: 1), Rueckspiel(4, 4, hinspielId: 2)]);

        ergebnis.Select(s => s.Id).Should().Equal(3, 4);
    }

    [Fact]
    public void EntscheidungsspieleJePaarung_HinspielOhneRueckspiel_StehtFuerSichSelbst()
    {
        var ergebnis = KoPaarungen.EntscheidungsspieleJePaarung(
            [Hinspiel(1, 1), Hinspiel(2, 2), Rueckspiel(3, 3, hinspielId: 2)]);

        ergebnis.Select(s => s.Id).Should().Equal(1, 3);
    }

    [Fact]
    public void SiegerOderVerliererId_OhneErgebnis_IstNull() =>
        KoPaarungen.SiegerOderVerliererId(Hinspiel(1, 1), platzierung: 1).Should().BeNull();

    [Fact]
    public void SiegerOderVerliererId_ErgebnisOhneSieger_IstNull() =>
        KoPaarungen.SiegerOderVerliererId(Entschieden(10, 11, siegerId: null), platzierung: 2).Should().BeNull();

    [Theory]
    [InlineData(10, 1, 10)]
    [InlineData(10, 2, 11)]
    [InlineData(11, 1, 11)]
    [InlineData(11, 2, 10)]
    public void SiegerOderVerliererId_Platz1IstDerSieger_Platz2DerGegner(int siegerId, int platzierung, int erwartet) =>
        KoPaarungen.SiegerOderVerliererId(Entschieden(10, 11, siegerId), platzierung).Should().Be(erwartet);

    [Fact]
    public void SiegerOderVerliererId_AnderePlatzierung_IstNull() =>
        KoPaarungen.SiegerOderVerliererId(Entschieden(10, 11, siegerId: 10), platzierung: 3).Should().BeNull();
}
