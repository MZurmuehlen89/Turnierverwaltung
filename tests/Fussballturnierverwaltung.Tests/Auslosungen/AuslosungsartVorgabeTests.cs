using FluentAssertions;
using Fussballturnierverwaltung.Application.Auslosungen;
using Fussballturnierverwaltung.Domain.Entitaeten.Turniere;
using Fussballturnierverwaltung.Domain.Enums;

namespace Fussballturnierverwaltung.Tests.Auslosungen;

public class AuslosungsartVorgabeTests
{
    private static TurnierRunde Runde(Rundenart art, Auslosungsart? vorgabe = null) =>
        new() { Rundenart = art, VorgegebeneAuslosungsart = vorgabe };

    [Fact]
    public void KompatibleArten_OhneRundenart_AlleArten() =>
        AuslosungsartVorgabe.KompatibleArten(null).Should().Equal(Enum.GetValues<Auslosungsart>());

    [Fact]
    public void KompatibleArten_TopfLiga_NurTopfZuordnung() =>
        AuslosungsartVorgabe.KompatibleArten(Rundenart.TopfLiga).Should().Equal(Auslosungsart.TopfZuordnung);

    [Fact]
    public void StartFuer_OhneRunde_Bisher() =>
        AuslosungsartVorgabe.StartFuer(null, Auslosungsart.NachSetzliste).Should().Be(Auslosungsart.NachSetzliste);

    // #347: die Vorlage gibt der Runde ihre Art vor.
    [Fact]
    public void StartFuer_KompatibleVorgabe_GehtVor() =>
        AuslosungsartVorgabe.StartFuer(Runde(Rundenart.KoRunde, Auslosungsart.GruppensiegerGegenGruppenzweite), Auslosungsart.Zufall)
            .Should().Be(Auslosungsart.GruppensiegerGegenGruppenzweite);

    [Fact]
    public void StartFuer_InkompatibleVorgabe_BleibtBeiBisher() =>
        AuslosungsartVorgabe.StartFuer(Runde(Rundenart.KoRunde, Auslosungsart.TopfZuordnung), Auslosungsart.NachToepfen)
            .Should().Be(Auslosungsart.NachToepfen);

    [Fact]
    public void StartFuer_BisherInkompatibel_ErsteKompatibleArt() =>
        AuslosungsartVorgabe.StartFuer(Runde(Rundenart.TopfLiga), Auslosungsart.Zufall)
            .Should().Be(Auslosungsart.TopfZuordnung);

    [Theory]
    [InlineData(Auslosungsart.Zufall, false)]
    [InlineData(Auslosungsart.Benutzerdefiniert, false)]
    [InlineData(Auslosungsart.NachToepfen, true)]
    [InlineData(Auslosungsart.TopfZuordnung, true)]
    public void ErlaubtMehrereToepfe_NurMitTopfStruktur(Auslosungsart art, bool erwartet) =>
        AuslosungsartVorgabe.ErlaubtMehrereToepfe(art).Should().Be(erwartet);

    [Theory]
    [InlineData(Auslosungsart.Zufall, 4, 1)]
    [InlineData(Auslosungsart.TopfZuordnung, 1, 4)]
    [InlineData(Auslosungsart.TopfZuordnung, 3, 3)]
    [InlineData(Auslosungsart.NachToepfen, 1, 1)]
    [InlineData(Auslosungsart.NachToepfen, 5, 5)]
    public void LostoepfeNachWechsel(Auslosungsart art, int bisher, int erwartet) =>
        AuslosungsartVorgabe.LostoepfeNachWechsel(art, bisher, runde: null).Should().Be(erwartet);

    [Theory]
    [InlineData(2, 4)]
    [InlineData(1, 6)]
    public void LostoepfeNachWechsel_TopfZuordnung_RichtetSichNachGegnernJeTopf(int gegnerJeTopf, int erwartet)
    {
        var runde = Runde(Rundenart.TopfLiga);
        runde.GegnerJeTopf = gegnerJeTopf;

        AuslosungsartVorgabe.LostoepfeNachWechsel(Auslosungsart.TopfZuordnung, bisher: 1, runde).Should().Be(erwartet);
    }

    [Theory]
    [InlineData(16, 4, 4)]
    [InlineData(48, 12, 4)]
    [InlineData(8, 2, 4)]
    public void LostoepfeNachWechsel_NachToepfenInDerGruppenphase_EinTopfJeGruppenplatz(int teams, int gruppen, int erwartet)
    {
        var runde = Runde(Rundenart.Gruppenphase);
        runde.EingangsMannschaftsanzahl = teams;
        runde.GruppenAnzahl = gruppen;

        AuslosungsartVorgabe.LostoepfeNachWechsel(Auslosungsart.NachToepfen, bisher: 1, runde).Should().Be(erwartet);
    }

    [Fact]
    public void LostoepfeNachWechsel_NachToepfenInDerGruppenphaseOhneGruppen_BleibtBeimBisherigen()
    {
        var runde = Runde(Rundenart.Gruppenphase);
        runde.EingangsMannschaftsanzahl = 16;

        AuslosungsartVorgabe.LostoepfeNachWechsel(Auslosungsart.NachToepfen, bisher: 1, runde).Should().Be(1);
    }

    [Theory]
    [InlineData(Auslosungsart.NachToepfen)]
    [InlineData(Auslosungsart.GruppensiegerGegenGruppenzweite)]
    public void LostoepfeNachWechsel_TopfPaareInDerKoRunde_ZweiToepfe(Auslosungsart art) =>
        AuslosungsartVorgabe.LostoepfeNachWechsel(art, bisher: 1, Runde(Rundenart.KoRunde)).Should().Be(2);

    [Fact]
    public void LostoepfeNachWechsel_MehrereGewaehlteToepfe_BleibenStehen() =>
        AuslosungsartVorgabe.LostoepfeNachWechsel(Auslosungsart.GruppensiegerGegenGruppenzweite, bisher: 4, Runde(Rundenart.KoRunde))
            .Should().Be(4);
}
