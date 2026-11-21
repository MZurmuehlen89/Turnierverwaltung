using FluentAssertions;
using Fussballturnierverwaltung.Application.Simulation;

namespace Fussballturnierverwaltung.Tests.Simulation;

/// <summary>Turnierstärke (#679): Wert je Team und Turnier, Faktor auf die Torerwartung.</summary>
public class TurnierstaerkeTests
{
    // ── K2 Reproduzierbar und gestreut ────────────────────────────────────

    [Theory]
    [InlineData(1, 1)]
    [InlineData(4711, 42)]
    [InlineData(int.MaxValue, 7)]
    [InlineData(-5, 300)]
    public void Wert_GleicherSeedUndGleicheMannschaft_GleicherWert(int seed, int id) =>
        Turnierstaerke.Wert(seed, id).Should().Be(Turnierstaerke.Wert(seed, id));

    [Fact]
    public void Wert_VerschiedeneMannschaftenUndSeeds_VerschiedeneWerte()
    {
        Turnierstaerke.Wert(100, 1).Should().NotBe(Turnierstaerke.Wert(100, 2));
        Turnierstaerke.Wert(100, 1).Should().NotBe(Turnierstaerke.Wert(101, 1));
    }

    [Fact]
    public void Wert_FestgeschriebenFuerFesteSeedUndId() =>
        // Die Mischung muss über Prozesse und Läufe gleich bleiben (kein HashCode.Combine) – ein Referenzwert hält sie fest.
        Turnierstaerke.Wert(4711, 3).Should().BeApproximately(0.06229426250368994, 1e-12);

    [Fact]
    public void Wert_StreutMitDerStreuungUmNull()
    {
        var werte = Enumerable.Range(1, 200).SelectMany(m => Enumerable.Range(1, 50).Select(s => Turnierstaerke.Wert(s, m)))
            .ToList();
        double mittel = werte.Average();
        double streuung = Math.Sqrt(werte.Sum(w => (w - mittel) * (w - mittel)) / (werte.Count - 1));

        mittel.Should().BeApproximately(0, 0.003);
        streuung.Should().BeApproximately(Turnierstaerke.STREUUNG, 0.003);
    }

    [Fact]
    public void Wert_BenachbarteSeedsHaengenNichtZusammen()
    {
        var a = Enumerable.Range(1, 500).Select(s => Turnierstaerke.Wert(s, 1)).ToList();
        var b = Enumerable.Range(1, 500).Select(s => Turnierstaerke.Wert(s + 1, 1)).ToList();
        Korrelation(a, b).Should().BeInRange(-0.12, 0.12);
    }

    private static double Korrelation(List<double> a, List<double> b)
    {
        double ma = a.Average(), mb = b.Average();
        double kov = a.Zip(b, (x, y) => (x - ma) * (y - mb)).Sum();
        return kov / Math.Sqrt(a.Sum(x => (x - ma) * (x - ma)) * b.Sum(y => (y - mb) * (y - mb)));
    }

    // ── Faktor ────────────────────────────────────────────────────────────

    [Fact]
    public void Faktor_GleicheFormen_ReduziertUmDenMittelwertAusgleich()
    {
        var form = new Turnierform(0.05, 0.05);
        double erwartet = Math.Exp(-Turnierstaerke.STREUUNG * Turnierstaerke.STREUUNG);
        form.HeimFaktor.Should().BeApproximately(erwartet, 1e-12);
        form.GastFaktor.Should().BeApproximately(erwartet, 1e-12);
    }

    [Fact]
    public void Faktor_StaerkerAlsDerGegner_ErhoehtDieEigeneUndSenktDieGegnerischeErwartung()
    {
        var form = new Turnierform(0.1, -0.1);
        double ausgleich = Turnierstaerke.STREUUNG * Turnierstaerke.STREUUNG;
        form.HeimFaktor.Should().BeApproximately(Math.Exp(0.2 - ausgleich), 1e-12);
        form.GastFaktor.Should().BeApproximately(Math.Exp(-0.2 - ausgleich), 1e-12);
    }

    [Fact]
    public void Faktor_MittelUeberViele_LiegtBeiEins()
    {
        double summe = 0;
        const int N = 20_000;
        for (int s = 1; s <= N; s++)
            summe += Turnierstaerke.Form(s, 1, 2).HeimFaktor;
        (summe / N).Should().BeApproximately(1.0, 0.003);
    }

    [Fact]
    public void Tauschen_VertauschtHeimUndGast()
    {
        var form = new Turnierform(0.1, -0.05).Tauschen();
        (form.Heim, form.Gast).Should().Be((-0.05, 0.1));
    }

    [Fact]
    public void Form_NimmtDenWertJeMannschaft()
    {
        var form = Turnierstaerke.Form(9, 3, 8);
        form.Heim.Should().Be(Turnierstaerke.Wert(9, 3));
        form.Gast.Should().Be(Turnierstaerke.Wert(9, 8));
    }
}
