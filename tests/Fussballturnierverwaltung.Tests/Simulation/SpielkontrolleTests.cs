using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>#674, K4: Spielkontrolle – abklingende Schwankung ohne Torbezug, im Mittel ohne Wirkung auf die Chancenrate.</summary>
public class SpielkontrolleTests
{
    private const int MINUTEN = 200_000;

    [Fact]
    public void Anpfiff_StandNull_BeideTeamsGleich()
    {
        var kontrolle = new Spielkontrolle(new Random(674));

        kontrolle.Stand.Should().Be(0);
        kontrolle.HeimFaktor.Should().Be(kontrolle.GastFaktor);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(-0.2)]
    public void Naechster_OhneRauschen_KlingtJeMinuteUmDenErhaltAb(double stand)
    {
        Spielkontrolle.Naechster(stand, 0).Should().BeApproximately(Spielkontrolle.ERHALT_JE_MINUTE * stand, 1e-12);
    }

    [Fact]
    public void Faktor_SymmetrischZwischenHeimUndGast_NormiertMitDerStreuung()
    {
        Spielkontrolle.Faktor(0).Should().BeApproximately(Math.Exp(-Spielkontrolle.STREUUNG * Spielkontrolle.STREUUNG / 2), 1e-12);
        (Spielkontrolle.Faktor(0.3) / Spielkontrolle.Faktor(-0.3)).Should().BeApproximately(Math.Exp(0.6), 1e-12);
    }

    [Fact]
    public void LangerVerlauf_StreuungUndErhaltWieVorgegeben_FaktorImMittelEins()
    {
        var kontrolle = new Spielkontrolle(new Random(674));
        var staende = new double[MINUTEN];
        double heimFaktoren = 0, gastFaktoren = 0;
        for (int i = 0; i < MINUTEN; i++)
        {
            kontrolle.Fortschreiben();
            staende[i] = kontrolle.Stand;
            heimFaktoren += kontrolle.HeimFaktor;
            gastFaktoren += kontrolle.GastFaktor;
        }

        double mittel = staende.Average();
        double streuung = Math.Sqrt(staende.Average(z => (z - mittel) * (z - mittel)));
        double kovarianz = Enumerable.Range(1, MINUTEN - 1).Average(i => (staende[i] - mittel) * (staende[i - 1] - mittel));
        streuung.Should().BeApproximately(Spielkontrolle.STREUUNG, 0.02);
        (kovarianz / (streuung * streuung)).Should().BeApproximately(Spielkontrolle.ERHALT_JE_MINUTE, 0.005);
        (heimFaktoren / MINUTEN).Should().BeApproximately(1, 0.02, "die Spielkontrolle verteilt Chancen um, sie erzeugt keine");
        (gastFaktoren / MINUTEN).Should().BeApproximately(1, 0.02);
    }

    [Fact]
    public void GleicherStartwert_GleicherVerlauf()
    {
        var a = new Spielkontrolle(new Random(7));
        var b = new Spielkontrolle(new Random(7));
        for (int i = 0; i < 90; i++)
        {
            a.Fortschreiben();
            b.Fortschreiben();
        }

        a.Stand.Should().Be(b.Stand);
    }
}
